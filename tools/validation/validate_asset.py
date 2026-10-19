#!/usr/bin/env python3
"""
Validador de Assets 3D e Texturas — V2 (Endurecido)

Novidades V2 (Quality Gate V2 / AGENTS.md):
- Inspeciona .fbx: UV0 válido, transform neutro na raiz (sem offsets -90 não compensados),
  materiais associados; FBX binário via Blender headless (WSL), FBX ASCII via parser nativo.
- Inspeciona .prefab: materiais com shader Universal Render Pipeline/Lit;
  FALHA (exit 1) se qualquer prefab de renderização 3D tiver _BaseMap (Albedo) nulo
  ou cor chapada monocromática sem textura; detecta primitivas nativas.
- Inspeciona texturas: orçamento de 10MB e resolução <= 2048 (quando PIL disponível).
- Modo --all: varre Assets/Game/Art, Assets/Game/Prefabs e Assets/Game/Materials e
  grava relatório JSON de evidência em evidence/test-reports/asset-validation-v2.json.

Uso:
  python tools/validation/validate_asset.py --asset <caminho>
  python tools/validation/validate_asset.py --all
"""

import sys
import os
import re
import json
import argparse
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import studio_validators as sv  # noqa: E402

ROOT = sv.ROOT
REPORT_PATH = ROOT / "evidence" / "test-reports" / "asset-validation-v2.json"

VALID_3D = [".fbx", ".obj", ".blend", ".gltf", ".glb"]
VALID_TEXTURES = [".png", ".tga", ".jpg", ".jpeg"]
VALID_AUDIO = [".wav", ".ogg", ".mp3"]

SCAN_DIRS = [
    "unity/Assets/Game/Art",
    "unity/Assets/Game/Prefabs",
    "unity/Assets/Game/Materials",
]


def validate_texture(p: Path):
    errors, warnings = [], []
    size_mb = p.stat().st_size / (1024 * 1024)
    if size_mb > 10:
        errors.append(f"Textura excede 10MB ({size_mb:.2f}MB) — reduza para orcamento mobile")
    elif size_mb > 4:
        warnings.append(f"Textura pesada ({size_mb:.2f}MB)")
    try:
        from PIL import Image
        with Image.open(p) as im:
            if max(im.size) > 2048:
                errors.append(f"Textura {im.size[0]}x{im.size[1]} excede 2048px")
    except ImportError:
        warnings.append("PIL ausente: dimensoes nao verificadas (use venv WSL p/ verificacao completa)")
    except Exception:
        errors.append("Textura ilegivel/corrompida")
    return errors, warnings


def validate_audio(p: Path):
    size_mb = p.stat().st_size / (1024 * 1024)
    if size_mb > 20:
        return [f"Audio excede 20MB ({size_mb:.2f}MB)"], []
    return [], []


def validate_model_size(p: Path):
    errors, warnings = [], []
    size_mb = p.stat().st_size / (1024 * 1024)
    if size_mb > 50:
        errors.append(f"Modelo 3D excede 50MB ({size_mb:.2f}MB)")
    elif size_mb > 15:
        warnings.append(f"Modelo 3D pesado ({size_mb:.2f}MB)")
    return errors, warnings


def validate_single_asset(asset_path: str, blender_reports=None):
    """Retorna (ok, errors, warnings) para um único arquivo."""
    p = Path(asset_path)
    if not p.exists():
        return False, [f"Arquivo de asset nao encontrado: {asset_path}"], []

    ext = p.suffix.lower()
    errors, warnings = [], []

    if ext in VALID_3D:
        errors, warnings = validate_model_size(p)
        if ext == ".fbx":
            if sv.is_binary_fbx(p):
                rep = (blender_reports or {}).get(str(p))
                if rep is None:
                    warnings.append("FBX binario nao inspecionado nesta execucao (Blender)")
                elif rep.get("status") != "ok":
                    errors.append(f"FBX binario ilegivel: {rep.get('error')}")
                else:
                    if not rep.get("uv_valid", False):
                        errors.append(f"UV0 invalido/ausente (area={rep.get('uv_min_area')})")
                    if not rep.get("identity_transform", False):
                        errors.append("transform residual na raiz (rotacao/escala nao aplicadas)")
                    if rep.get("ngons", 0) > 0:
                        errors.append(f"{rep['ngons']} N-gons (triangule a malha)")
                    if rep.get("material_slots", 0) == 0:
                        warnings.append("sem slots de material associados")
            else:
                ok, sub_errors, sub_warns = sv.check_ascii_fbx(p)
                errors += sub_errors
                warnings += sub_warns

    elif ext in VALID_TEXTURES:
        errors, warnings = validate_texture(p)
    elif ext in VALID_AUDIO:
        errors, warnings = validate_audio(p)
    elif ext == ".prefab":
        ok, sub_errors, sub_warns = sv.check_prefab(p)
        errors += sub_errors
        warnings += sub_warns
    elif ext == ".mat":
        ok, sub_errors, sub_warns = sv.check_material(p)
        errors += sub_errors
        warnings += sub_warns
    else:
        warnings.append(f"Extensao nao padrao para pipeline de arte: {ext}")

    return len(errors) == 0, errors, warnings


def run_all():
    """Varredura completa com inspeção Blender em lote para FBX binários."""
    all_errors, all_warnings, results = [], [], {}
    targets = []
    for scan_dir in SCAN_DIRS:
        base = ROOT / scan_dir
        if not base.exists():
            continue
        for f in base.rglob("*"):
            if f.is_file() and f.suffix.lower() in (VALID_3D + VALID_TEXTURES + VALID_AUDIO + [".prefab", ".mat"]):
                targets.append(f)

    # Delega FBX binarios ao Blender (uma sessao)
    binary_fbxs = [f for f in targets if f.suffix.lower() == ".fbx" and sv.is_binary_fbx(f)]
    print(f"[*] Alvos: {len(targets)} arquivos | FBX binarios para inspecao Blender: {len(binary_fbxs)}")
    blender_reports = sv.inspect_binary_fbxs(binary_fbxs)

    for f in sorted(targets):
        rel = f.relative_to(ROOT).as_posix()
        ok, errors, warnings = validate_single_asset(str(f), blender_reports)
        results[rel] = {"ok": ok, "errors": errors, "warnings": warnings}
        for e in errors:
            all_errors.append(f"{rel}: {e}")
        for w in warnings:
            all_warnings.append(f"{rel}: {w}")

    REPORT_PATH.parent.mkdir(parents=True, exist_ok=True)
    REPORT_PATH.write_text(json.dumps({
        "version": "2.0",
        "passed": len([r for r in results.values() if r["ok"]]),
        "failed": len([r for r in results.values() if not r["ok"]]),
        "results": results,
    }, indent=2, ensure_ascii=False), encoding="utf-8")

    print(f"[*] Aprovados: {len(results) - len([r for r in results.values() if not r['ok']])}/{len(results)}")
    print(f"[*] Relatorio: {REPORT_PATH.relative_to(ROOT).as_posix()}")
    return len(all_errors) == 0, all_errors, all_warnings


def main():
    parser = argparse.ArgumentParser(description="Validador de Asset V2")
    group = parser.add_mutually_exclusive_group(required=True)
    group.add_argument("--asset", help="Caminho do arquivo de asset")
    group.add_argument("--all", action="store_true", help="Varre todos os assets do projeto")
    args = parser.parse_args()

    if args.all:
        ok, errors, warnings = run_all()
    else:
        print(f"[*] Validando asset: {args.asset}")
        ok, errors, warnings = validate_single_asset(args.asset)

    for w in warnings:
        print(f"[WARN] {w}")
    if errors:
        for e in errors:
            print(f"[FAIL] {e}")
        print("\n[RESULTADO] ASSET(S) REPROVADO(S).")
        sys.exit(1)

    print("\n[RESULTADO] ASSET APROVADO." if not args.all else "\n[RESULTADO] TODOS OS ASSETS APROVADOS.")
    sys.exit(0)


if __name__ == "__main__":
    main()
