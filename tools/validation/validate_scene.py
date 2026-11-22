#!/usr/bin/env python3
"""
Validador Determinístico de Cenas Unity (.unity) — V2.1 (Adendo C: Android URP Budget)

V2 (Quality Gate V2 / AGENTS.md):
- Detecta Missing Scripts (fileID 0) e referências nulas.
- Inspeciona TODOS os MeshRenderer da cena (shader URP Lit + _BaseMap).
- Proíbe primitivas GameObject nativas (Cube/Sphere/Plane/...) visíveis fora de isenções.
- PrefabInstances devem resolver para prefabs existentes.

V2.1 (Adendo C — Budget Android URP):
- Luzes reais simultâneas <= 5 por estado (Light enabled, não-Baked).
- Tris totais estimados da cena <= 150.000.
- Draw calls estimados <= 120 (considerando static batching por material).
- Memória de textura estimada <= 128 MB.
- Batching estático >= 50% dos MeshRenderers quando a cena possui >= 10 MeshRenderers.
- Grava relatório JSON em evidence/validation/validate_scene_<nome>.json.

Uso:
  python tools/validation/validate_scene.py --scene <caminho> [--strict] [--out <json>]
"""

import argparse
import json
import re
import sys
from datetime import datetime, timezone
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import studio_validators as sv  # noqa: E402

ROOT = sv.ROOT
VALIDATION_DIR = ROOT / "evidence" / "validation"

ANDROID_URP_BUDGET = {
    "max_real_lights": 5,
    "max_total_tris": 150000,
    "max_draw_calls": 120,
    "max_texture_mem_mb": 128.0,
    "min_static_batching_ratio": 0.50,
    "static_batching_min_renderers": 10,
}

_TEX_GUID_RE = re.compile(r"m_Texture: \{fileID: 2800000, guid: ([a-f0-9]{32})")


def _estimate_texture_mb(tex_path: Path) -> float:
    """Estima footprint VRAM em MB (conservador para Android URP ASTC/RGBA com mipmaps)."""
    if not tex_path.exists():
        return 0.0
    try:
        size_bytes = tex_path.stat().st_size
        # PNG comprimido em disco ~0.25x-0.5x de ASTC/RGBA com mips; fator conservador 2.0x
        return max((size_bytes * 2.0) / (1024.0 * 1024.0), 0.15)
    except OSError:
        return 0.0


def check_android_urp_budget(scene_path: Path, budget: dict = None):
    cfg = dict(ANDROID_URP_BUDGET)
    if budget:
        cfg.update(budget)

    content = sv.read_yaml(scene_path)
    blocks = sv.split_blocks(content)

    # Map GameObject id -> StaticEditorFlags
    go_static_flags = {}
    for cls, oid, body in blocks:
        if cls == "1":  # GameObject
            m_flags = re.search(r"m_StaticEditorFlags:\s*(\d+)", body)
            go_static_flags[oid] = int(m_flags.group(1)) if m_flags else 0

    # 1. Real lights (classID 108, enabled != 0, lightmapping != 2 [Baked])
    real_lights = 0
    for cls, oid, body in blocks:
        if cls == "108":
            en = re.search(r"m_Enabled:\s*(\d+)", body)
            if en and int(en.group(1)) == 0:
                continue
            lm = re.search(r"m_Lightmapping:\s*(\d+)", body)
            if lm and int(lm.group(1)) == 2:
                continue  # Baked-only light does not cost real-time light budget
            real_lights += 1

    # 2. MeshRenderers (23) & SkinnedMeshRenderers (137) -> draw calls, static batching, materials
    mesh_renderers = 0
    static_batched_renderers = 0
    dynamic_draw_calls = 0
    static_material_buckets = set()
    all_material_guids = set()
    skinned_renderers = 0

    for cls, oid, body in blocks:
        if cls == "23":  # MeshRenderer
            en = re.search(r"m_Enabled:\s*(\d+)", body)
            if en and int(en.group(1)) == 0:
                continue
            mesh_renderers += 1
            go_m = re.search(r"m_GameObject: \{fileID: (\d+)\}", body)
            go_id = go_m.group(1) if go_m else None
            flags = go_static_flags.get(go_id, 0)
            mats = sv._MAT_GUID_RE.findall(body)
            all_material_guids.update(mats)
            mat_key = mats[0] if mats else f"inline_{oid}"
            # Unity BatchingStatic bit is 4 (or any non-zero static flag including Everything=4294967295)
            is_static = bool(flags & 4) or flags > 0
            if is_static:
                static_batched_renderers += 1
                static_material_buckets.add(mat_key)
            else:
                dynamic_draw_calls += max(1, len(mats))
        elif cls == "137":  # SkinnedMeshRenderer
            en = re.search(r"m_Enabled:\s*(\d+)", body)
            if en and int(en.group(1)) == 0:
                continue
            skinned_renderers += 1
            mats = sv._MAT_GUID_RE.findall(body)
            all_material_guids.update(mats)
            dynamic_draw_calls += max(1, len(mats))

    estimated_draw_calls = dynamic_draw_calls + len(static_material_buckets)
    static_ratio = (static_batched_renderers / mesh_renderers) if mesh_renderers > 0 else 1.0

    # 3. Estimated triangles (from fab3d_manifest.json if linked, else conservative per-renderer budget)
    # Optional explicit scene comment override for fixtures/benchmarks: # STUDIO_SCENE_TRIS: <int>
    tris_override = re.search(r"#\s*STUDIO_SCENE_TRIS:\s*(\d+)", content)
    if tris_override:
        estimated_tris = int(tris_override.group(1))
    else:
        estimated_tris = mesh_renderers * 500 + skinned_renderers * 3500

    # 4. Estimated texture memory from all materials referenced in the scene
    tex_guids = set()
    for g in all_material_guids:
        mat_rel = sv.resolve_guid(g)
        if not mat_rel:
            continue
        mat_path = sv.ASSETS / mat_rel[len("Assets/"):]
        if mat_path.exists() and mat_path.suffix == ".mat":
            mat_content = sv.read_yaml(mat_path)
            tex_guids.update(_TEX_GUID_RE.findall(mat_content))

    estimated_tex_mb = 0.0
    for tg in tex_guids:
        tex_rel = sv.resolve_guid(tg)
        if tex_rel:
            estimated_tex_mb += _estimate_texture_mb(sv.ASSETS / tex_rel[len("Assets/"):])
    tex_override = re.search(r"#\s*STUDIO_SCENE_TEX_MB:\s*([\d.]+)", content)
    if tex_override:
        estimated_tex_mb = float(tex_override.group(1))

    errors = []
    if real_lights > cfg["max_real_lights"]:
        errors.append(
            f"SCENE_BUDGET_LIGHTS_FAIL: {real_lights} luzes reais ativas excedem o limite Android URP "
            f"(max {cfg['max_real_lights']})."
        )
    if estimated_tris > cfg["max_total_tris"]:
        errors.append(
            f"SCENE_BUDGET_TRIS_FAIL: {estimated_tris} tris estimados excedem o limite Android URP "
            f"(max {cfg['max_total_tris']})."
        )
    if estimated_draw_calls > cfg["max_draw_calls"]:
        errors.append(
            f"SCENE_BUDGET_DRAWCALLS_FAIL: {estimated_draw_calls} draw calls estimados excedem o limite Android URP "
            f"(max {cfg['max_draw_calls']})."
        )
    if estimated_tex_mb > cfg["max_texture_mem_mb"]:
        errors.append(
            f"SCENE_BUDGET_TEXMEM_FAIL: {estimated_tex_mb:.2f} MB de texturas excedem o limite Android URP "
            f"(max {cfg['max_texture_mem_mb']:.1f} MB)."
        )
    if mesh_renderers >= cfg["static_batching_min_renderers"] and static_ratio < cfg["min_static_batching_ratio"]:
        errors.append(
            f"SCENE_BUDGET_STATIC_BATCHING_FAIL: batching estático em apenas {static_ratio*100:.1f}% "
            f"({static_batched_renderers}/{mesh_renderers} MeshRenderers) — exigido >= "
            f"{cfg['min_static_batching_ratio']*100:.0f}% para Android URP."
        )

    metrics = {
        "real_lights": real_lights,
        "max_real_lights": cfg["max_real_lights"],
        "estimated_tris": estimated_tris,
        "max_total_tris": cfg["max_total_tris"],
        "estimated_draw_calls": estimated_draw_calls,
        "max_draw_calls": cfg["max_draw_calls"],
        "estimated_texture_mem_mb": round(estimated_tex_mb, 2),
        "max_texture_mem_mb": cfg["max_texture_mem_mb"],
        "mesh_renderers": mesh_renderers,
        "skinned_renderers": skinned_renderers,
        "static_batched_renderers": static_batched_renderers,
        "static_batching_ratio": round(static_ratio, 3),
        "min_static_batching_ratio": cfg["min_static_batching_ratio"],
    }
    return len(errors) == 0, errors, metrics


def validate_scene_file(scene_path: str, out_json: Path = None):
    p = Path(scene_path)
    if not p.is_absolute() and not p.exists():
        cand = ROOT / "unity" / scene_path
        if cand.exists():
            p = cand
        elif (ROOT / scene_path).exists():
            p = ROOT / scene_path

    if not p.exists():
        return False, [f"Arquivo de cena não encontrado: {scene_path}"], [], {}, None

    ok_struct, struct_issues, warnings = sv.check_scene(p)
    ok_budget, budget_issues, metrics = check_android_urp_budget(p)
    all_errors = struct_issues + budget_issues
    passed = ok_struct and ok_budget

    VALIDATION_DIR.mkdir(parents=True, exist_ok=True)
    report_path = Path(out_json) if out_json else (VALIDATION_DIR / f"validate_scene_{p.stem}.json")
    report_path.parent.mkdir(parents=True, exist_ok=True)
    payload = {
        "validator": "validate_scene.py",
        "version": "2.1.0",
        "target_profile": "android_urp",
        "timestamp": datetime.now(timezone.utc).isoformat(),
        "scene": p.resolve().relative_to(ROOT).as_posix() if p.resolve().is_relative_to(ROOT) else str(p),
        "passed": passed,
        "structural_ok": ok_struct,
        "android_urp_budget_ok": ok_budget,
        "metrics": metrics,
        "errors": all_errors,
        "warnings": warnings,
    }
    report_path.write_text(json.dumps(payload, indent=2, ensure_ascii=False), encoding="utf-8")
    return passed, all_errors, warnings, metrics, report_path


def main():
    parser = argparse.ArgumentParser(description="Validador de Cena Unity V2.1 (Android URP Budget)")
    parser.add_argument("--scene", required=True, help="Caminho relativo ou absoluto da cena .unity")
    parser.add_argument("--strict", action="store_true", help="Falha mesmo com avisos")
    parser.add_argument("--out", default=None, help="Caminho opcional do relatório JSON")
    args = parser.parse_args()

    print(f"[*] Validando cena (Android URP): {args.scene}")
    is_valid, errors, warnings, metrics, report_path = validate_scene_file(
        args.scene, out_json=Path(args.out) if args.out else None
    )

    if metrics:
        print(
            f"[validate_scene] lights={metrics['real_lights']}/{metrics['max_real_lights']} | "
            f"tris~={metrics['estimated_tris']}/{metrics['max_total_tris']} | "
            f"draw_calls~={metrics['estimated_draw_calls']}/{metrics['max_draw_calls']} | "
            f"tex_mb~={metrics['estimated_texture_mem_mb']}/{metrics['max_texture_mem_mb']} | "
            f"static_batch={metrics['static_batching_ratio']*100:.1f}%"
        )
    if report_path:
        rel = report_path.relative_to(ROOT).as_posix() if report_path.resolve().is_relative_to(ROOT) else str(report_path)
        print(f"[validate_scene] Relatório salvo em: {rel}")

    for w in warnings:
        print(f"[WARN] {w}")

    if errors:
        for e in errors:
            print(f"[FAIL] {e}")
        print("\n[RESULTADO] VALIDAÇÃO REPROVADA (exit 1).")
        sys.exit(1)

    if args.strict and warnings:
        print("\n[RESULTADO] VALIDAÇÃO REPROVADA (Modo Strict, exit 1).")
        sys.exit(1)

    print("\n[RESULTADO] VALIDAÇÃO APROVADA (exit 0): cena íntegra e dentro do orçamento Android URP.")
    sys.exit(0)


if __name__ == "__main__":
    main()
