#!/usr/bin/env python3
"""
tools/art/ai_generator/concept_bridge.py
Ponte 2D 100% Local (Windows -> WSL Diffusers CUDA) — Studio V2 Fase 4.

Gera conceitos 2D (para alimentar a Fabrica 3D), texturas 100% tileaveis (chao/parede)
e icones/UI respeitando a Biblia Visual (docs/art/art-bible.yaml).

Reutiliza a venv ~/.venvs/hunyuan ja existente no WSL (diffusers 0.33.1 + CUDA).
Regra permanente: 100% local, somente C:\\ (D:\\ proibido).

Uso:
  python tools/art/ai_generator/concept_bridge.py ^
      --name altar_antigo --mode concept ^
      --prompt "ancient stone shrine with glowing golden rune" ^
      --art-bible docs/art/art-bible.template.yaml
"""

import argparse
import json
import re
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent.parent.parent
WORKER_SCRIPT = Path(__file__).resolve().parent / "concept_diffusers_worker.py"
DEFAULT_WSL_PYTHON = "/home/bruno/.venvs/hunyuan/bin/python"


def to_wsl_path(win_path) -> str:
    s = str(win_path).strip()
    if s.startswith("/"):
        return s
    p = Path(s).resolve()
    drive = p.drive.replace(":", "").lower()
    return f"/mnt/{drive}{p.as_posix().split(':', 1)[-1]}"


def extract_bible_prompts(bible_path: Path) -> dict:
    """Extrai style_prefix, style_suffix e negative_prompt de art-bible.yaml sem PyYAML."""
    if not bible_path or not bible_path.exists():
        return {"style_prefix": "", "style_suffix": "", "negative_prompt": ""}
    text = bible_path.read_text(encoding="utf-8")
    result = {"style_prefix": "", "style_suffix": "", "negative_prompt": ""}
    for key in list(result.keys()):
        m = re.search(rf"^\s*{key}:\s*>\s*\n((?:\s{{4,}}.*\n)+)", text, re.MULTILINE)
        if m:
            lines = [ln.strip() for ln in m.group(1).splitlines() if ln.strip()]
            result[key] = " ".join(lines)
        else:
            m2 = re.search(rf'^\s*{key}:\s*"([^"]*)"', text, re.MULTILINE)
            if m2:
                result[key] = m2.group(1).strip()
    return result


def parse_report(stdout: str) -> dict:
    for line in reversed(stdout.splitlines()):
        if line.startswith("REPORT_JSON:"):
            return json.loads(line[len("REPORT_JSON:"):])
    print("[concept_bridge] REPORT_JSON nao encontrado na saida do worker")
    sys.exit(1)


def main():
    ap = argparse.ArgumentParser(description="Ponte 2D Local (Conceito / Tileable / UI)")
    ap.add_argument("--name", required=True, help="Slug do asset (ex.: altar_antigo, chao_pedra)")
    ap.add_argument("--prompt", required=True, help="Descricao da peca/textura")
    ap.add_argument("--mode", default="concept", choices=["concept", "tileable", "ui"])
    ap.add_argument("--art-bible", default=None, help="Caminho para docs/art/art-bible.yaml")
    ap.add_argument("--model", default="stabilityai/sdxl-turbo")
    ap.add_argument("--size", type=int, default=512)
    ap.add_argument("--steps", type=int, default=4)
    ap.add_argument("--guidance", type=float, default=0.0)
    ap.add_argument("--seed", type=int, default=42)
    ap.add_argument("--out", default=None, help="Caminho PNG de saida")
    ap.add_argument("--wsl-python", default=DEFAULT_WSL_PYTHON)
    args = ap.parse_args()

    bible = extract_bible_prompts(Path(args.art_bible).resolve()) if args.art_bible else {
        "style_prefix": "", "style_suffix": "", "negative_prompt": ""
    }

    if args.mode == "tileable":
        full_prompt = f"seamless tileable top-down PBR albedo texture, flat even lighting, no shadows, {args.prompt}"
    elif args.mode == "ui":
        full_prompt = f"clean stylized game UI icon, centered composition, crisp readability, {args.prompt}"
    else:
        parts = [bible["style_prefix"], args.prompt, bible["style_suffix"]]
        full_prompt = ", ".join(p for p in parts if p)

    if args.out:
        out_png = Path(args.out).resolve()
    elif args.mode == "tileable":
        out_png = ROOT / "unity" / "Assets" / "Game" / "Art" / "Textures" / "Generated" / f"{args.name}_albedo.png"
    elif args.mode == "ui":
        out_png = ROOT / "unity" / "Assets" / "Game" / "Art" / "UI" / "Generated" / f"{args.name}.png"
    else:
        out_png = ROOT / "evidence" / "concepts" / f"{args.name}_concept.png"

    out_png.parent.mkdir(parents=True, exist_ok=True)

    cmd = [
        "wsl", args.wsl_python, to_wsl_path(WORKER_SCRIPT),
        "--prompt", full_prompt,
        "--negative-prompt", bible["negative_prompt"],
        "--mode", args.mode,
        "--out", to_wsl_path(out_png),
        "--model", args.model,
        "--size", str(args.size),
        "--steps", str(args.steps),
        "--guidance", str(args.guidance),
        "--seed", str(args.seed),
    ]
    print(f"[concept_bridge] Gerando ({args.mode}) '{args.name}' via WSL Diffusers...")
    res = subprocess.run(cmd, capture_output=True, text=True)
    if res.stdout:
        print(res.stdout[-4000:])
    if res.returncode != 0:
        print(f"[concept_bridge] FALHA (exit {res.returncode})")
        if res.stderr:
            print(res.stderr[-4000:])
        sys.exit(res.returncode)

    report = parse_report(res.stdout)
    report["name"] = args.name
    report["art_bible"] = args.art_bible or ""
    report["output_win"] = str(out_png)

    ev_dir = ROOT / "evidence" / "art"
    ev_dir.mkdir(parents=True, exist_ok=True)
    ev_path = ev_dir / f"concept_{args.name}.json"
    ev_path.write_text(json.dumps(report, indent=2, ensure_ascii=False), encoding="utf-8")

    print(f"""
[concept_bridge] IMAGEM GERADA ({args.mode}): {out_png}
[concept_bridge] Tempo: {report.get('elapsed_sec')}s | Resolucao: {report.get('size')} | Evidencia: {ev_path}
""")


if __name__ == "__main__":
    main()
