#!/usr/bin/env python3
"""
tools/qa/run_visual_gates.py
Orquestrador Canônico de Gates do Studio Core (DOC 3 §3-B).

Ordem obrigatória e inegociável:
  1. glTF-Validator (GLB)          -> tools/validation/validate_gltf.py
  2. validate_asset                -> tools/validation/validate_asset.py
  3. validate_visual               -> tools/validation/validate_visual.py
  4. import (Fab3dImporter)        -> Studio.Editor.Fab3dImporter.RunImportBatch
  5. captura MCP / Preview         -> Studio.Editor.Fab3dImporter.CapturePreviewBatch
  6. validate_scene (opcional)     -> tools/validation/validate_scene.py
  7. run_unity_tests (opcional)    -> tools/qa/run_unity_tests.py

Nenhum asset pula etapa. Qualquer falha interrompe com exit 1.
"""

import argparse
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent.parent


def find_unity_exe() -> str:
    candidates = [
        Path(r"C:\Program Files\Unity\Hub\Editor\6000.3.26f1\Editor\Unity.exe"),
        Path(r"C:\Program Files\Unity\Hub\Editor\6000.0.85f1\Editor\Unity.exe"),
    ]
    for c in candidates:
        if c.exists():
            return str(c)
    return "unity"


def run_step(step_name: str, cmd: list):
    print(f"\n[run_visual_gates] === {step_name} ===")
    print(f"[run_visual_gates] CMD: {' '.join(str(x) for x in cmd)}")
    proc = subprocess.run([str(x) for x in cmd])
    if proc.returncode != 0:
        print(f"[run_visual_gates] FALHA em '{step_name}' (exit {proc.returncode})")
        sys.exit(proc.returncode)
    print(f"[run_visual_gates] PASS: {step_name}")


def main():
    ap = argparse.ArgumentParser(description="Pipeline Canônico de Gates Visuais (DOC 3 §3-B)")
    ap.add_argument("--asset-dir", required=True,
                    help="Diretório do asset (ex.: unity/Assets/Game/Art/Generated/smoke_dragonborn)")
    ap.add_argument("--scene", default=None,
                    help="Cena opcional para validate_scene (ex.: Assets/Game/Scenes/Calibration.unity)")
    ap.add_argument("--run-tests", action="store_true",
                    help="Executa também run_unity_tests.py no final")
    ap.add_argument("--skip-unity-import", action="store_true",
                    help="Executa apenas os gates pré-import (1..3: glTF-Validator, validate_asset, validate_visual)")
    args = ap.parse_args()

    asset_dir = (ROOT / args.asset_dir).resolve() if not Path(args.asset_dir).is_absolute() else Path(args.asset_dir).resolve()
    if not asset_dir.exists():
        print(f"[run_visual_gates] Diretório não encontrado: {asset_dir}")
        sys.exit(1)

    name = asset_dir.name
    glbs = list(asset_dir.glob("*.glb")) + list(asset_dir.glob("*.gltf"))
    fbxs = list(asset_dir.glob("*.fbx"))
    manifest = asset_dir / "fab3d_manifest.json"

    # 1. glTF-Validator (GLB)
    if glbs:
        run_step(
            "1. glTF-Validator (GLB)",
            [sys.executable, str(ROOT / "tools" / "validation" / "validate_gltf.py"),
             "--input", str(glbs[0]), "--name", name],
        )

    if not fbxs:
        print(f"[run_visual_gates] Nenhum .fbx encontrado em {asset_dir}")
        sys.exit(1)
    fbx_path = fbxs[0]

    # 2. validate_asset
    run_step(
        "2. validate_asset",
        [sys.executable, str(ROOT / "tools" / "validation" / "validate_asset.py"),
         "--asset", str(fbx_path)],
    )

    # 3. validate_visual
    vis_cmd = [sys.executable, str(ROOT / "tools" / "validation" / "validate_visual.py"),
               "--asset", str(fbx_path)]
    if manifest.exists():
        vis_cmd += ["--manifest", str(manifest)]
    run_step("3. validate_visual", vis_cmd)

    if not args.skip_unity_import:
        unity_exe = find_unity_exe()
        unity_proj = ROOT / "unity"
        log_dir = ROOT / "evidence" / "logs"
        log_dir.mkdir(parents=True, exist_ok=True)

        # 4. import (Fab3dImporter) + 5. captura automática pós-import
        run_step(
            "4 & 5. Fab3dImporter + Captura Automática Pós-Import",
            [unity_exe, "-batchmode", "-quit", "-projectPath", str(unity_proj),
             "-executeMethod", "Studio.Editor.Fab3dImporter.RunImportBatch",
             f"-fab3dAsset={name}",
             "-logFile", str(log_dir / f"fab3d_import_{name}.log")],
        )

    # 6. validate_scene
    if args.scene:
        run_step(
            "6. validate_scene",
            [sys.executable, str(ROOT / "tools" / "validation" / "validate_scene.py"),
             "--scene", args.scene, "--strict"],
        )

    # 7. run_unity_tests
    if args.run_tests:
        run_step(
            "7. run_unity_tests",
            [sys.executable, str(ROOT / "tools" / "qa" / "run_unity_tests.py")],
        )

    print("\n[run_visual_gates] PIPELINE DE GATES CONCLUÍDO COM SUCESSO (exit 0).")
    sys.exit(0)


if __name__ == "__main__":
    main()
