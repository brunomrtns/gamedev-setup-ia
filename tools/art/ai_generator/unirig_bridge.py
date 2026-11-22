#!/usr/bin/env python3
"""
tools/art/ai_generator/unirig_bridge.py
Ponte UniRig 100% Local (GLB/FBX -> Esqueleto Neural + Skinning Neural -> FBX rigado)
— Studio V2 Fase 5.1-B.

Executa o pipeline oficial do UniRig (`~/tools/UniRig` na venv `~/.venvs/unirig`):
  1. `launch/inference/generate_skeleton.sh` (Articulation-XL 256)
  2. `launch/inference/generate_skin.sh` (Bone-Point Cross Attention)
  3. `launch/inference/merge.sh` (mescla malha + esqueleto + pesos no FBX final)
  4. Grava evidencia em `evidence/art/unirig_<name>.json`.

Ideal para personagens fora do padrao (ex.: Dez com 4 bracos, criaturas, capas/caudas)
onde o `auto_rig.py` geometrico de 18 ossos nao basta.
"""

import argparse
import json
import os
import subprocess
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent.parent.parent
sys.path.insert(0, str(ROOT / "tools" / "studio"))
from yaml_loader import to_wsl_path  # noqa: E402

DEFAULT_UNIRIG_DIR = os.getenv("STUDIO_WSL_UNIRIG_DIR", "/home/bruno/tools/UniRig")
DEFAULT_UNIRIG_VENV = os.getenv("STUDIO_WSL_UNIRIG_VENV", "/home/bruno/.venvs/unirig")


def run_wsl_step(cmd_str: str, label: str):
    print(f"[unirig_bridge] {label}...")
    res = subprocess.run(["wsl", "bash", "-c", cmd_str], capture_output=True, text=True)
    if res.stdout:
        print(res.stdout[-3000:])
    if res.returncode != 0:
        print(f"[unirig_bridge] FALHA em '{label}' (exit {res.returncode})")
        if res.stderr:
            print(res.stderr[-3000:])
        sys.exit(res.returncode)


def main():
    ap = argparse.ArgumentParser(description="Ponte UniRig Neural Auto-Rigging (WSL CUDA)")
    ap.add_argument("--input", required=True, help="Caminho da malha (.glb, .fbx ou .obj)")
    ap.add_argument("--name", required=True, help="Slug do personagem/criatura")
    ap.add_argument("--out-dir", default=None,
                    help="Pasta de saida (default: unity/Assets/Game/Art/Generated/<name>)")
    ap.add_argument("--seed", type=int, default=12345)
    args = ap.parse_args()

    in_path = Path(args.input).resolve()
    if not in_path.exists():
        print(f"[unirig_bridge] Arquivo de entrada nao encontrado: {in_path}")
        sys.exit(2)

    out_dir = Path(args.out_dir).resolve() if args.out_dir else \
        ROOT / "unity" / "Assets" / "Game" / "Art" / "Generated" / args.name
    out_dir.mkdir(parents=True, exist_ok=True)

    # Copia entrada para out_dir sem espacos no nome (evita quebra nos scripts bash do UniRig)
    staged_in = out_dir / f"{args.name}_input{in_path.suffix.lower()}"
    import shutil
    shutil.copy2(in_path, staged_in)

    skel_fbx = out_dir / f"{args.name}_skeleton.fbx"
    skin_fbx = out_dir / f"{args.name}_skin.fbx"
    final_fbx = out_dir / f"{args.name}_rigged.fbx"

    env_prefix = f"cd {DEFAULT_UNIRIG_DIR} && export PATH=\"{DEFAULT_UNIRIG_VENV}/bin:$PATH\""
    t0 = time.time()

    run_wsl_step(
        f"{env_prefix} && bash ./launch/inference/generate_skeleton.sh "
        f"--input '{to_wsl_path(staged_in)}' --output '{to_wsl_path(skel_fbx)}' --seed {args.seed}",
        "1/3 UniRig Skeleton Prediction",
    )
    run_wsl_step(
        f"{env_prefix} && bash ./launch/inference/generate_skin.sh "
        f"--input '{to_wsl_path(skel_fbx)}' --output '{to_wsl_path(skin_fbx)}' --seed {args.seed}",
        "2/3 UniRig Skinning Prediction",
    )
    run_wsl_step(
        f"{env_prefix} && bash ./launch/inference/merge.sh "
        f"--source '{to_wsl_path(skin_fbx)}' --target '{to_wsl_path(staged_in)}' --output '{to_wsl_path(final_fbx)}'",
        "3/3 UniRig Merge Mesh+Rig",
    )

    elapsed = round(time.time() - t0, 2)
    report = {
        "schema": "unirig-bridge-v1",
        "name": args.name,
        "input_mesh": str(in_path),
        "skeleton_fbx": str(skel_fbx),
        "skin_fbx": str(skin_fbx),
        "rigged_fbx": str(final_fbx),
        "size_bytes": final_fbx.stat().st_size if final_fbx.exists() else 0,
        "elapsed_sec": elapsed,
        "errors": [] if final_fbx.exists() else ["rigged_fbx ausente"],
    }
    ev_dir = ROOT / "evidence" / "art"
    ev_dir.mkdir(parents=True, exist_ok=True)
    ev_path = ev_dir / f"unirig_{args.name}.json"
    ev_path.write_text(json.dumps(report, indent=2, ensure_ascii=False), encoding="utf-8")

    print(f"[unirig_bridge] CONCLUIDO ({elapsed}s): {final_fbx} | Evidencia: {ev_path}")
    if report["errors"]:
        sys.exit(1)


if __name__ == "__main__":
    main()
