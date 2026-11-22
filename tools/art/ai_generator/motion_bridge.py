#!/usr/bin/env python3
"""
tools/art/ai_generator/motion_bridge.py
Ponte Text-to-Motion 100% Local (MoMask WSL -> BVH -> Blender FBX Mecanim) — Studio V2 Fase 5.1-B.

Gera animacoes humanas sob medida a partir de texto (ex.: para cutscenes e acoes unicas
do CONCERTO) usando MoMask no WSL (`~/tools/momask-codes` + `~/.venvs/momask`), converte
o `.bvh` em `.fbx` via Blender 5.0 e grava evidencia em `evidence/art/motion_<name>.json`.

Uso:
  python tools/art/ai_generator/motion_bridge.py ^
      --name dez_repair_kneel ^
      --prompt "a person kneels down and tightens something with both hands" ^
      --length 100
"""

import argparse
import json
import os
import shutil
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent.parent.parent
sys.path.insert(0, str(ROOT / "tools" / "studio"))
from yaml_loader import to_wsl_path  # noqa: E402

BVH_TO_FBX = Path(__file__).resolve().parent / "bvh_to_fbx_blender.py"
DEFAULT_MOMASK_DIR = os.getenv("STUDIO_WSL_MOMASK_DIR", "/home/bruno/tools/momask-codes")
DEFAULT_MOMASK_PY = os.getenv("STUDIO_WSL_MOMASK_PYTHON", "/home/bruno/.venvs/momask/bin/python")


def parse_report(stdout: str) -> dict:
    for line in reversed(stdout.splitlines()):
        if line.startswith("REPORT_JSON:"):
            return json.loads(line[len("REPORT_JSON:"):])
    return {}


def main():
    ap = argparse.ArgumentParser(description="Ponte Text-to-Motion Local (MoMask -> BVH -> FBX)")
    ap.add_argument("--name", required=True, help="Slug da animacao (ex.: dez_repair_kneel)")
    ap.add_argument("--prompt", required=True, help="Descricao da acao em ingles")
    ap.add_argument("--length", type=int, default=80, help="Duracao em frames a 20 fps (ex.: 80 = 4.0s)")
    ap.add_argument("--gpu-id", type=int, default=0, help="GPU ID (-1 para CPU)")
    ap.add_argument("--out-dir", default=None,
                    help="Pasta de saida (default: unity/Assets/Game/Art/Animations/Generated/<name>)")
    args = ap.parse_args()

    out_dir = Path(args.out_dir).resolve() if args.out_dir else \
        ROOT / "unity" / "Assets" / "Game" / "Art" / "Animations" / "Generated" / args.name
    out_dir.mkdir(parents=True, exist_ok=True)

    ext_name = f"studio_{args.name}"
    momask_cmd = (
        f"cd {DEFAULT_MOMASK_DIR} && "
        f"MOMASK_SKIP_PLOT=1 {DEFAULT_MOMASK_PY} gen_t2m.py "
        f"--gpu_id {args.gpu_id} --ext {ext_name} "
        f"--text_prompt \"{args.prompt.replace('\"', '')}\" "
        f"--motion_length {args.length} --repeat_times 1"
    )
    print(f"[motion_bridge] Gerando movimento '{args.name}' via MoMask WSL...")
    res = subprocess.run(["wsl", "bash", "-c", momask_cmd], capture_output=True, text=True)
    if res.stdout:
        print(res.stdout[-3000:])
    if res.returncode != 0:
        print(f"[motion_bridge] FALHA no MoMask (exit {res.returncode})")
        if res.stderr:
            print(res.stderr[-3000:])
        sys.exit(res.returncode)

    # Localiza o BVH gerado (preferindo _ik.bvh quando existir)
    find_cmd = (
        f"find {DEFAULT_MOMASK_DIR}/generation/{ext_name} -name '*.bvh' | sort"
    )
    fres = subprocess.run(["wsl", "bash", "-c", find_cmd], capture_output=True, text=True)
    bvhs = [ln.strip() for ln in fres.stdout.splitlines() if ln.strip()]
    if not bvhs:
        print(f"[motion_bridge] Nenhum .bvh encontrado em {DEFAULT_MOMASK_DIR}/generation/{ext_name}")
        sys.exit(1)

    chosen_bvh_wsl = next((b for b in bvhs if "_ik.bvh" in b), bvhs[0])
    dst_bvh = out_dir / f"{args.name}.bvh"
    cp = subprocess.run(["wsl", "cp", chosen_bvh_wsl, to_wsl_path(dst_bvh)], capture_output=True, text=True)
    if cp.returncode != 0:
        print(f"[motion_bridge] Falha ao copiar BVH: {cp.stderr}")
        sys.exit(1)

    # Converte BVH -> FBX via Blender 5.0 WSL
    dst_fbx = out_dir / f"{args.name}.fbx"
    bres = subprocess.run(
        ["wsl", "blender", "-b", "-P", to_wsl_path(BVH_TO_FBX), "--",
         to_wsl_path(dst_bvh), to_wsl_path(dst_fbx)],
        capture_output=True, text=True,
    )
    if bres.stdout:
        print(bres.stdout[-2000:])
    if bres.returncode != 0:
        print(f"[motion_bridge] FALHA na conversao Blender BVH->FBX (exit {bres.returncode})")
        if bres.stderr:
            print(bres.stderr[-2000:])
        sys.exit(bres.returncode)

    blend_rep = parse_report(bres.stdout)
    report = {
        "schema": "motion-bridge-v1",
        "name": args.name,
        "prompt": args.prompt,
        "requested_frames_20fps": args.length,
        "bvh_file": str(dst_bvh),
        "fbx_file": str(dst_fbx),
        "blender_report": blend_rep,
        "errors": [],
    }
    ev_dir = ROOT / "evidence" / "art"
    ev_dir.mkdir(parents=True, exist_ok=True)
    ev_path = ev_dir / f"motion_{args.name}.json"
    ev_path.write_text(json.dumps(report, indent=2, ensure_ascii=False), encoding="utf-8")

    print(f"[motion_bridge] CONCLUIDO: {dst_fbx} | Evidencia: {ev_path}")


if __name__ == "__main__":
    main()
