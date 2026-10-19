#!/usr/bin/env python3
"""
tools/art/ai_generator/generate_model.py
Gerador local de modelos 3D via IA (TripoSR) — Quality Gate V2.

Entrada: imagem de referência (--image) ou prompt de texto (--prompt, via
Stable Diffusion local se `diffusers` estiver no venv WSL).
Saída: modelo canônico .obj (+ texture.png) ou .glb, com textura bakeada
(Albedo). Opcionalmente normaliza eixos/pivô/escala (--normalize) e exporta
o relatório para inspeção posterior por normalize_fbx.py / auto_rig.py.

Ambiente: roda no Windows como orquestrador; a inferência pesada e as libs
científicas delegam automaticamente ao WSL2 (venv game-ai3d + Blender/CUDA).

Uso:
  python tools/art/ai_generator/generate_model.py --image concept.png \
      --output unity/Assets/Game/Art/Generated/mech.obj --normalize
  python tools/art/ai_generator/generate_model.py --image concept.png \
      --output out.glb --texture-resolution 1024
"""

import argparse
import shutil
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent.parent.parent
TRIPOSR_DIR = ROOT / "tools" / "art" / "TripoSR"
WSL_PYTHON = "/home/bruno/.venvs/game-ai3d/bin/python"


def wsl_path(win_path) -> str:
    p = Path(win_path).resolve()
    drive = p.drive.replace(":", "").lower()
    return f"/mnt/{drive}{p.as_posix().split(':', 1)[-1]}"


def run(cmd, desc):
    print(f"[generate_model] {desc}: {' '.join(str(c) for c in cmd)}")
    res = subprocess.run([str(c) for c in cmd])
    if res.returncode != 0:
        print(f"[generate_model] FALHA em '{desc}' (exit {res.returncode})")
        sys.exit(res.returncode)


def prompt_to_image(prompt: str, out_png: Path):
    """Gera imagem de referência via SD local (diffusers) no venv WSL."""
    worker = (
        "import sys; "
        "from diffusers import StableDiffusionPipeline; "
        "import torch; "
        "p=StableDiffusionPipeline.from_pretrained("
        "'stabilityai/stable-diffusion-2-1-base',torch_dtype=torch.float16).to('cuda'); "
        "p(sys.argv[1],num_inference_steps=30).images[0].save(sys.argv[2])"
    )
    run(["wsl", WSL_PYTHON, "-c", worker, prompt, wsl_path(out_png)],
        "Texto->Imagem (diffusers/CUDA)")


def main():
    ap = argparse.ArgumentParser(description="Gerador IA 3D local (TripoSR)")
    src = ap.add_mutually_exclusive_group(required=True)
    src.add_argument("--image", help="Imagem de referência (png/jpg)")
    src.add_argument("--prompt", help="Prompt de texto (gera imagem via SD local)")
    ap.add_argument("--output", required=True, help=".obj ou .glb de saída")
    ap.add_argument("--texture-resolution", type=int, default=1024,
                    choices=[512, 1024, 2048])
    ap.add_argument("--normalize", action="store_true",
                    help="Normalizar eixos/pivô/escala via normalize_mesh.py")
    ap.add_argument("--target-height", type=float, default=0.0,
                    help="Altura alvo em metros ao normalizar (0 = manter)")
    args = ap.parse_args()

    out = Path(args.output).resolve()
    out.parent.mkdir(parents=True, exist_ok=True)
    if out.suffix.lower() not in (".obj", ".glb"):
        print("[generate_model] Saída deve ser .obj ou .glb")
        sys.exit(2)

    # 1. Imagem de entrada
    work = out.parent / ".gen_work"
    work.mkdir(exist_ok=True)
    if args.prompt:
        img = work / "prompt_image.png"
        prompt_to_image(args.prompt, img)
    else:
        img = Path(args.image).resolve()
        if not img.exists():
            print(f"[generate_model] Imagem não encontrada: {img}")
            sys.exit(2)

    # 2. Inferência TripoSR no WSL (CUDA)
    if not TRIPOSR_DIR.exists():
        print(f"[generate_model] TripoSR não encontrado em {TRIPOSR_DIR}")
        sys.exit(2)
    run(["wsl", WSL_PYTHON, wsl_path(TRIPOSR_DIR / "run.py"), wsl_path(img),
         "--output-dir", wsl_path(work), "--bake-texture",
         "--texture-resolution", str(args.texture_resolution)],
        "Inferência TripoSR (WSL/CUDA)")

    # TripoSR grava em <out>/0/mesh.obj + texture.png
    gen_dir = work / "0"
    mesh_src = gen_dir / "mesh.obj"
    tex_src = gen_dir / "texture.png"
    if not mesh_src.exists():
        cands = list(work.rglob("*.obj"))
        if not cands:
            print("[generate_model] TripoSR não produziu mesh.obj")
            sys.exit(1)
        mesh_src = cands[0]
        tex_src = mesh_src.with_name("texture.png")

    # 3. Normalização opcional (eixos Z-Up->Y-Up, pivô nos pés, escala)
    final_mesh = out.with_suffix(".obj")
    if args.normalize or args.target_height > 0:
        norm = ROOT / "tools" / "art" / "normalize_mesh.py"
        cmd = ["wsl", WSL_PYTHON, wsl_path(norm),
               "--input", wsl_path(mesh_src), "--output", wsl_path(final_mesh)]
        if args.target_height > 0:
            cmd += ["--target-height", str(args.target_height)]
        run(cmd, "Normalização de eixos/pivô")
    else:
        shutil.copy2(mesh_src, final_mesh)

    if tex_src.exists():
        shutil.copy2(tex_src, out.parent / f"{out.stem}_albedo.png")

    # 4. Conversão .glb opcional via trimesh (venv WSL)
    if out.suffix.lower() == ".glb":
        worker = (
            "import sys,trimesh; "
            "m=trimesh.load(sys.argv[1]); "
            "m.export(sys.argv[2])"
        )
        run(["wsl", WSL_PYTHON, "-c", worker,
             wsl_path(final_mesh), wsl_path(out)], "Conversão obj->glb (trimesh)")
        final_mesh.unlink(missing_ok=True)
        final_mesh = out

    size_kb = final_mesh.stat().st_size / 1024
    print(f"[generate_model] OK: {final_mesh} ({size_kb:.0f} KB)")
    print("[generate_model] Próximo passo para personagens: "
          "wsl blender -b -P tools/art/auto_rig.py -- <mesh.obj> <out.fbx> [altura]")


if __name__ == "__main__":
    main()
