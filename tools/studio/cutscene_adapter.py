#!/usr/bin/env python3
"""
tools/studio/cutscene_adapter.py
Adapter G2 (Studio V2): converte um contrato rico `cutscene_*.yaml` (ex.: CONCERTO
`cutscene_01_abertura.yaml`) no JSON flat consumido pelo `CutsceneBuilder.cs`
(Tier 1 — Vertical Slice), convertendo `lens_mm -> fov`, tipos de camera
(`dolly`, `orbit`, `static`, `closeup`) em trilhos `camera_from/camera_to/look_at`
e preservando referencias de VO (`vo -> vo_line_id`).
"""

import argparse
import json
import math
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent.parent
sys.path.insert(0, str(ROOT / "tools" / "studio"))
from yaml_loader import load_yaml  # noqa: E402


def lens_mm_to_fov(lens_mm: float, sensor_height_mm: float = 24.0) -> float:
    """Converte distancia focal (mm, sensor 35mm full-frame) em vertical FOV (graus)."""
    if not lens_mm or lens_mm <= 0:
        return 45.0
    fov = 2.0 * math.degrees(math.atan(sensor_height_mm / (2.0 * float(lens_mm))))
    return round(max(15.0, min(95.0, fov)), 1)


def to_vec3(val, default):
    if isinstance(val, (list, tuple)) and len(val) >= 3:
        return [float(val[0]), float(val[1]), float(val[2])]
    return list(default)


def adapt_cutscene(yaml_path: Path, out_json: Path, scene_path: str = "",
                   vo_manifest_path: str = "") -> dict:
    data = load_yaml(yaml_path)
    meta = data.get("meta", {})
    cutscene_id = meta.get("id") or yaml_path.stem
    raw_shots = data.get("shots", [])

    shots = []
    warnings = []
    for idx, s in enumerate(raw_shots, start=1):
        sid = s.get("id") or f"shot_{idx:02d}"
        dur = float(s.get("dur_s") or s.get("duration_sec") or 3.0)
        cam = s.get("camera", {}) if isinstance(s.get("camera"), dict) else {}
        ctype = str(cam.get("type", "static")).lower()
        fov = lens_mm_to_fov(cam.get("lens_mm", 0)) if cam.get("lens_mm") else float(cam.get("fov", 45.0))

        look_at = to_vec3(cam.get("look_at"), [0.0, 1.5, 0.0])
        if ctype == "dolly":
            cam_from = to_vec3(cam.get("from"), [0.0, 2.0, 6.0])
            cam_to = to_vec3(cam.get("to"), [0.0, 2.0, 3.5])
        elif ctype == "orbit":
            r = float(cam.get("radius_m", 3.0))
            h = float(cam.get("height_m", 1.5))
            cam_from = [look_at[0] - r, look_at[1] + h, look_at[2] + r]
            cam_to = [look_at[0] + r, look_at[1] + h, look_at[2] + r]
        elif ctype == "closeup":
            cam_from = to_vec3(cam.get("pos"), [look_at[0] + 0.4, look_at[1] + 0.2, look_at[2] + 1.8])
            cam_to = [cam_from[0], cam_from[1], cam_from[2] - 0.2]
        else:
            cam_from = to_vec3(cam.get("pos") or cam.get("from"), [0.0, 2.0, 5.0])
            cam_to = list(cam_from)

        if isinstance(cam.get("look_at"), str) or isinstance(cam.get("target"), str):
            anchor = cam.get("look_at") or cam.get("target")
            warnings.append(f"{sid}: ancora nominal '{anchor}' mapeada para origem [0,1.5,0] no Tier 1")

        shots.append({
            "id": sid,
            "duration_sec": dur,
            "pad_after_sec": 0.2,
            "vo_line_id": s.get("vo") or "",
            "camera_from": cam_from,
            "camera_to": cam_to,
            "look_at": look_at,
            "fov": fov,
            "subtitle_speaker": "",
            "subtitle_text": "",
        })

    spec = {
        "schema": "cutscene-spec-v1",
        "cutscene_id": cutscene_id,
        "scene_path": scene_path,
        "vo_manifest_path": vo_manifest_path,
        "shots": shots,
    }
    out_json.parent.mkdir(parents=True, exist_ok=True)
    out_json.write_text(json.dumps(spec, indent=2, ensure_ascii=False), encoding="utf-8")

    return {
        "source_yaml": str(yaml_path),
        "output_json": str(out_json),
        "cutscene_id": cutscene_id,
        "shots_count": len(shots),
        "vo_shots_count": sum(1 for sh in shots if sh["vo_line_id"]),
        "total_duration_sec": round(sum(sh["duration_sec"] for sh in shots), 2),
        "warnings": warnings,
    }


def main():
    ap = argparse.ArgumentParser(description="Adapter G2: cutscene_*.yaml -> CutsceneBuilder JSON")
    ap.add_argument("--input", required=True, help="Caminho para cutscene_*.yaml")
    ap.add_argument("--out", required=True, help="Caminho do JSON de saida")
    ap.add_argument("--scene-path", default="", help="Cena Unity opcional para captura de keyframes")
    ap.add_argument("--vo-manifest", default="", help="Caminho do vo_manifest.json opcional")
    args = ap.parse_args()

    report = adapt_cutscene(Path(args.input).resolve(), Path(args.out).resolve(),
                            args.scene_path, args.vo_manifest)
    print(json.dumps(report, indent=2, ensure_ascii=False))


if __name__ == "__main__":
    main()
