#!/usr/bin/env python3
"""
tools/validation/validate_visual.py
Gate de Sanidade Visual Determinístico V2 (DOC 3 §3-A).

Entrada:
  - FBX ou GLB (--asset)
  - Ficha do asset (--manifest fab3d_manifest.json, auto-detectado na pasta do asset se omitido)
  - Classe (--class player_character|enemy_character|small_prop_item|large_prop_building|prop|biped|drone)

Checagens obrigatórias:
  1. Orientação (orientation): bbox em Y-up, pivô próximo à base (y_min ~ 0) e, para bípede,
     altura > max(largura_x, profundidade_z) e altura >= 1.6 * min(largura_x, profundidade_z)
     (detecta personagem deitado, tombado ou cabeça-para-baixo).
  2. Budget (budget): tri-count <= max_tris da classe/manifesto e 0 N-gons.
  3. UV / Texel Density (uv_texel_density): UV0 presente, razão área-UV/área-malha (uv_world_ratio)
     dentro da faixa [0.005, 50.0] (detecta UV 1x1 esticado ou UV colapsado).
  4. Material (material): slots de material > 0, textura BaseColor presente/não-vazia e material
     URP Lit sem _BaseMap nulo (quando .mat já gerado).
  5. Escala em metros (scale_meters): dimensões plausíveis em metros para a classe.

Saída:
  - Relatório JSON em evidence/validation/validate_visual_<nome>.json (ou --out)
  - Exit 0 se aprovado; Exit 1 com motivo objetivo em qualquer falha.
"""

import argparse
import json
import sys
from datetime import datetime, timezone
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import studio_validators as sv  # noqa: E402

ROOT = sv.ROOT
VALIDATION_DIR = ROOT / "evidence" / "validation"

CLASS_DEFAULTS = {
    "player_character":    {"max_tris": 4500, "is_biped": True,  "min_h": 0.40, "max_h": 3.50},
    "enemy_character":     {"max_tris": 2500, "is_biped": True,  "min_h": 0.35, "max_h": 3.50},
    "biped":               {"max_tris": 8000, "is_biped": True,  "min_h": 0.40, "max_h": 3.50},
    "drone":               {"max_tris": 2500, "is_biped": False, "min_h": 0.15, "max_h": 2.50},
    "small_prop_item":     {"max_tris": 600,  "is_biped": False, "min_h": 0.05, "max_h": 5.00},
    "large_prop_building": {"max_tris": 3000, "is_biped": False, "min_h": 0.20, "max_h": 50.0},
    "prop":                {"max_tris": 3000, "is_biped": False, "min_h": 0.05, "max_h": 50.0},
}

MIN_UV_WORLD_RATIO = 0.005
MAX_UV_WORLD_RATIO = 50.0
BIPED_ASPECT_MIN = 1.6


def validate_visual(
    asset_path: Path,
    manifest_path: Path = None,
    asset_class: str = None,
    max_tris_override: int = None,
    out_json: Path = None,
):
    errors = []
    warnings = []
    checks = {
        "orientation": {"passed": False, "details": {}},
        "budget": {"passed": False, "details": {}},
        "uv_texel_density": {"passed": False, "details": {}},
        "material": {"passed": False, "details": {}},
        "scale_meters": {"passed": False, "details": {}},
    }

    asset_path = asset_path.resolve()
    if not asset_path.exists():
        errors.append(f"FILE_NOT_FOUND: Asset não encontrado: {asset_path}")
        return False, errors, warnings, checks, None

    asset_dir = asset_path.parent
    asset_name = asset_path.stem

    # Auto-detect manifest if not provided
    manifest = {}
    if manifest_path is None:
        candidate = asset_dir / "fab3d_manifest.json"
        if candidate.exists():
            manifest_path = candidate
    if manifest_path and manifest_path.exists():
        try:
            manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
            asset_name = manifest.get("asset_name", asset_name)
        except Exception as exc:
            warnings.append(f"Falha ao ler manifesto {manifest_path}: {exc}")

    # Determine class and budget
    resolved_class = asset_class or manifest.get("category")
    if not resolved_class:
        preset = manifest.get("preset", "")
        resolved_class = "player_character" if preset == "character" else "prop"
    if resolved_class not in CLASS_DEFAULTS:
        warnings.append(f"Classe '{resolved_class}' desconhecida; usando 'prop'")
        resolved_class = "prop"

    cls_cfg = CLASS_DEFAULTS[resolved_class]
    max_tris = max_tris_override or manifest.get("max_tris") or cls_cfg["max_tris"]
    is_biped = cls_cfg["is_biped"] or (manifest.get("preset") == "character" and resolved_class != "drone")

    # Inspect 3D file via Blender headless (WSL)
    reports = sv.inspect_binary_fbxs([asset_path])
    rep = None
    if reports:
        rep = reports.get(str(asset_path))
        if rep is None and len(reports) == 1:
            rep = next(iter(reports.values()))

    if not rep or rep.get("status") != "ok":
        err_msg = rep.get("error", "Blender inspect retornou vazio") if rep else "Falha ao executar Blender headless"
        errors.append(f"INSPECT_FAIL: Não foi possível inspecionar a malha 3D ({err_msg})")
        return False, errors, warnings, checks, None

    bounds = rep.get("bounds_unity_y_up", {})
    torso = rep.get("torso_bounds", {})
    w_x = float(bounds.get("width_x", 0.0))
    h_y = float(bounds.get("height_y", 0.0))
    d_z = float(bounds.get("depth_z", 0.0))
    y_min = float(bounds.get("y_min", 0.0))
    y_max = float(bounds.get("y_max", 0.0))
    torso_w = float(torso.get("width_x", w_x))
    torso_d = float(torso.get("depth_z", d_z))

    # 1. ORIENTATION CHECK
    identity_ok = bool(rep.get("identity_transform", False))
    y_center = (y_min + y_max) * 0.5
    grounded_ok = (abs(y_min) <= 0.15) or (abs(y_center) <= 0.15)
    orientation_errors = []
    if not identity_ok:
        orientation_errors.append("Transform residual na raiz (identity_transform=false; rotação/escala não aplicadas)")
    if not grounded_ok:
        orientation_errors.append(
            f"Pivô desalinhado em Y-up: y_min={y_min:.4f}m, y_center={y_center:.4f}m "
            f"(esperado base y_min~0 ou centro y_center~0 ± 0.15m)"
        )

    min_horiz = max(min(w_x, d_z), 1e-5)
    max_horiz = max(w_x, d_z, 1e-5)
    aspect_vs_min_horiz = h_y / min_horiz
    aspect_vs_torso_depth = h_y / max(torso_d, 1e-5)

    if is_biped:
        if h_y <= w_x or h_y <= d_z:
            orientation_errors.append(
                f"ORIENTATION_FAIL: Bípede deitado/tombado — altura Y ({h_y:.3f}m) <= extensão horizontal "
                f"(width_x={w_x:.3f}m, depth_z={d_z:.3f}m). O eixo maior do bípede deve ser Y-up."
            )
        if aspect_vs_min_horiz < BIPED_ASPECT_MIN:
            orientation_errors.append(
                f"ORIENTATION_FAIL: Bípede fora da razão vertical canônica — height_y ({h_y:.3f}m) / "
                f"min_horizontal ({min_horiz:.3f}m) = {aspect_vs_min_horiz:.2f} < {BIPED_ASPECT_MIN:.2f} "
                f"(modelo deitado, achatado ou rotacionado no eixo errado)."
            )

    checks["orientation"] = {
        "passed": len(orientation_errors) == 0,
        "details": {
            "is_biped": is_biped,
            "identity_transform": identity_ok,
            "grounded_y_min": y_min,
            "bounds_unity_y_up": bounds,
            "torso_bounds": torso,
            "aspect_height_over_min_horiz": round(aspect_vs_min_horiz, 3),
            "aspect_height_over_torso_depth": round(aspect_vs_torso_depth, 3),
            "required_biped_aspect": BIPED_ASPECT_MIN if is_biped else None,
            "errors": orientation_errors,
        },
    }
    errors.extend(orientation_errors)

    # 2. BUDGET CHECK
    tris = int(rep.get("tris", 0))
    ngons = int(rep.get("ngons", 0))
    budget_errors = []
    if tris <= 0:
        budget_errors.append("BUDGET_FAIL: Malha com 0 triângulos.")
    elif tris > int(max_tris * 1.05):
        budget_errors.append(
            f"BUDGET_FAIL: Tri-count ({tris}) excede o budget da classe '{resolved_class}' ({max_tris} tris, tol 5%)."
        )
    if ngons > 0:
        budget_errors.append(f"BUDGET_FAIL: Malha contém {ngons} N-gons (>4 lados); triangulação obrigatória.")

    checks["budget"] = {
        "passed": len(budget_errors) == 0,
        "details": {
            "class": resolved_class,
            "tris": tris,
            "max_tris": max_tris,
            "ngons": ngons,
            "errors": budget_errors,
        },
    }
    errors.extend(budget_errors)

    # 3. UV / TEXEL DENSITY CHECK
    uv_valid = bool(rep.get("uv_valid", False))
    uv_total_area = float(rep.get("uv_total_area", 0.0))
    uv_inside01 = float(rep.get("uv_inside01", 0.0))
    mesh_area_m2 = float(rep.get("mesh_area_m2", 0.0))
    uv_world_ratio = float(rep.get("uv_world_ratio", 0.0))
    uv_errors = []
    if not uv_valid or uv_total_area <= 1e-4:
        uv_errors.append(f"UV_FAIL: UV0 ausente ou degenerado (uv_valid={uv_valid}, uv_total_area={uv_total_area:.5f}).")
    if uv_world_ratio < MIN_UV_WORLD_RATIO or uv_world_ratio > MAX_UV_WORLD_RATIO:
        uv_errors.append(
            f"TEXEL_DENSITY_FAIL: Razão área-UV/área-mundo ({uv_world_ratio:.5f}) fora da faixa "
            f"[{MIN_UV_WORLD_RATIO}, {MAX_UV_WORLD_RATIO}] (uv_area={uv_total_area:.4f}, mesh_area={mesh_area_m2:.4f}m²) "
            f"— indica UV 1x1 esticado ou colapsado."
        )

    checks["uv_texel_density"] = {
        "passed": len(uv_errors) == 0,
        "details": {
            "uv_valid": uv_valid,
            "uv_total_area": uv_total_area,
            "uv_inside01": uv_inside01,
            "mesh_area_m2": mesh_area_m2,
            "uv_world_ratio": uv_world_ratio,
            "allowed_ratio_range": [MIN_UV_WORLD_RATIO, MAX_UV_WORLD_RATIO],
            "errors": uv_errors,
        },
    }
    errors.extend(uv_errors)

    # 4. MATERIAL CHECK
    mat_slots = int(rep.get("material_slots", 0))
    mat_errors = []
    if mat_slots <= 0:
        mat_errors.append("MATERIAL_FAIL: Malha 3D sem nenhum slot de material associado (material_slots=0).")

    basecolor_candidates = [
        asset_dir / f"{asset_name}_basecolor.png",
        asset_dir / f"{asset_path.stem}_basecolor.png",
        asset_dir / "robot_diffuse.png",
    ]
    basecolor_found = next((c for c in basecolor_candidates if c.exists()), None)
    if basecolor_found is None:
        pngs = [p for p in asset_dir.glob("*.png") if not p.name.endswith("_mr.png")]
        basecolor_found = pngs[0] if pngs else None

    if asset_path.suffix.lower() == ".fbx":
        if basecolor_found is None or basecolor_found.stat().st_size < 512:
            mat_errors.append(
                f"MATERIAL_FAIL: Textura BaseColor (_BaseMap) ausente ou vazia na pasta do asset ({asset_dir})."
            )

    unity_mat_path = ROOT / "unity" / "Assets" / "Game" / "Materials" / "Fab3D" / f"Mat_Fab3D_{asset_name}.mat"
    if unity_mat_path.exists():
        mat_ok, sub_issues, sub_warns = sv.check_material(unity_mat_path)
        if not mat_ok:
            mat_errors.extend([f"MATERIAL_FAIL: {i}" for i in sub_issues])
        warnings.extend(sub_warns)

    checks["material"] = {
        "passed": len(mat_errors) == 0,
        "details": {
            "material_slots": mat_slots,
            "basecolor_texture": str(basecolor_found.relative_to(ROOT).as_posix())
            if (basecolor_found and basecolor_found.is_relative_to(ROOT))
            else (str(basecolor_found) if basecolor_found else None),
            "unity_material_checked": str(unity_mat_path.relative_to(ROOT).as_posix()) if unity_mat_path.exists() else None,
            "errors": mat_errors,
        },
    }
    errors.extend(mat_errors)

    # 5. SCALE IN METERS CHECK
    scale_errors = []
    min_h, max_h = cls_cfg["min_h"], cls_cfg["max_h"]
    ref_dim = h_y if is_biped else max(w_x, h_y, d_z)
    if ref_dim < min_h or ref_dim > max_h:
        scale_errors.append(
            f"SCALE_FAIL: Dimensão principal ({ref_dim:.3f}m) fora da faixa plausível "
            f"[{min_h:.2f}m, {max_h:.2f}m] para a classe '{resolved_class}'."
        )

    checks["scale_meters"] = {
        "passed": len(scale_errors) == 0,
        "details": {
            "reference_dimension_m": round(ref_dim, 4),
            "allowed_range_m": [min_h, max_h],
            "errors": scale_errors,
        },
    }
    errors.extend(scale_errors)

    passed = len(errors) == 0
    VALIDATION_DIR.mkdir(parents=True, exist_ok=True)
    report_file = Path(out_json) if out_json else (VALIDATION_DIR / f"validate_visual_{asset_name}.json")
    report_file.parent.mkdir(parents=True, exist_ok=True)

    payload = {
        "validator": "validate_visual.py",
        "version": "1.0.0",
        "timestamp": datetime.now(timezone.utc).isoformat(),
        "asset_name": asset_name,
        "asset_path": asset_path.relative_to(ROOT).as_posix() if asset_path.is_relative_to(ROOT) else str(asset_path),
        "class": resolved_class,
        "passed": passed,
        "checks": checks,
        "errors": errors,
        "warnings": warnings,
    }
    report_file.write_text(json.dumps(payload, indent=2, ensure_ascii=False), encoding="utf-8")
    return passed, errors, warnings, checks, report_file


def main():
    ap = argparse.ArgumentParser(description="Validador de Sanidade Visual V2 (DOC 3 §3-A)")
    ap.add_argument("--asset", required=True, help="Caminho do arquivo .fbx ou .glb")
    ap.add_argument("--manifest", default=None, help="Caminho opcional do fab3d_manifest.json")
    ap.add_argument("--class", dest="asset_class", default=None, choices=sorted(CLASS_DEFAULTS),
                    help="Classe do asset (auto-detectada do manifesto se omitida)")
    ap.add_argument("--max-tris", type=int, default=None, help="Override opcional do budget de triângulos")
    ap.add_argument("--out", default=None, help="Caminho do JSON de saída em evidence/validation/")
    args = ap.parse_args()

    passed, errors, warnings, checks, report_file = validate_visual(
        asset_path=Path(args.asset),
        manifest_path=Path(args.manifest) if args.manifest else None,
        asset_class=args.asset_class,
        max_tris_override=args.max_tris,
        out_json=Path(args.out) if args.out else None,
    )

    for w in warnings:
        print(f"[WARN] {w}")
    for check_name, info in checks.items():
        status = "PASS" if info["passed"] else "FAIL"
        print(f"[validate_visual] {check_name}: {status}")
    if report_file:
        rel = report_file.relative_to(ROOT).as_posix() if report_file.is_relative_to(ROOT) else str(report_file)
        print(f"[validate_visual] Relatório salvo em: {rel}")

    if not passed:
        for e in errors:
            print(f"[FAIL] {e}")
        print("\n[RESULTADO] VALIDATE_VISUAL REPROVADO (exit 1).")
        sys.exit(1)

    print("\n[RESULTADO] VALIDATE_VISUAL APROVADO (exit 0).")
    sys.exit(0)


if __name__ == "__main__":
    main()
