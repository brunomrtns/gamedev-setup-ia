#!/usr/bin/env python3
"""
tools/art/ai_generator/concept_bridge.py
Ponte 2D 100% Local (Windows -> WSL Diffusers CUDA) — Studio V2 Fase 4 + Adapter G2.

Gera conceitos 2D (para alimentar a Fabrica 3D), texturas 100% tileaveis (chao/parede)
e icones/UI respeitando a Biblia Visual (`art-bible.yaml`).

Suporta os dois schemas de Biblia Visual do Studio V2:
  - Template padrao: `local_generation_prompts.{style_prefix, style_suffix, negative_prompt}`
  - Schema CONCERTO: `prompt_base.{text, negative}` + `characters.<id>.prompt_mod` + `palettes.<fuso>`

Reutiliza a venv `~/.venvs/hunyuan` (ou `STUDIO_WSL_2D_PYTHON`).
Regra permanente: 100% local, somente C:\\ (D:\\ proibido).
"""

import argparse
import json
import os
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent.parent.parent
sys.path.insert(0, str(ROOT / "tools" / "studio"))
from yaml_loader import load_yaml, to_wsl_path  # noqa: E402

WORKER_SCRIPT = Path(__file__).resolve().parent / "concept_diffusers_worker.py"
DEFAULT_WSL_PYTHON = os.getenv("STUDIO_WSL_2D_PYTHON", "/home/bruno/.venvs/hunyuan/bin/python")


def resolve_art_bible(bible_path: Path, character: str = None, fuso: str = None) -> dict:
    data = load_yaml(bible_path)
    prefix, suffix, negative = "", "", ""

    if isinstance(data.get("local_generation_prompts"), dict):
        lgp = data["local_generation_prompts"]
        prefix = str(lgp.get("style_prefix") or "").strip()
        suffix = str(lgp.get("style_suffix") or "").strip()
        negative = str(lgp.get("negative_prompt") or "").strip()
    elif isinstance(data.get("prompt_base"), dict):
        pb = data["prompt_base"]
        prefix = str(pb.get("text") or "").strip()
        negative = str(pb.get("negative") or "").strip()

    if not prefix and not suffix:
        raise ValueError(
            f"art-bible '{bible_path}' nao possui 'local_generation_prompts' nem 'prompt_base.text' preenchido"
        )

    char_mod = ""
    if character:
        chars = data.get("characters", {})
        c = chars.get(character)
        if not isinstance(c, dict) or not c.get("prompt_mod"):
            raise ValueError(f"Personagem '{character}' nao encontrado em characters.* de '{bible_path}'")
        char_mod = str(c["prompt_mod"]).strip()

    fuso_mod = ""
    if fuso:
        palettes = data.get("palettes", {})
        pal = palettes.get(fuso)
        if not isinstance(pal, dict):
            raise ValueError(f"Fuso '{fuso}' nao encontrado em palettes.* de '{bible_path}'")
        colors = []
        for k in ("sync", "desync"):
            v = pal.get(k)
            if isinstance(v, list):
                colors.extend(str(x) for x in v)
        if pal.get("accent_energy"):
            colors.append(str(pal["accent_energy"]))
        fuso_mod = f"{fuso} palette ({', '.join(colors[:5])})"

    return {
        "style_prefix": prefix,
        "style_suffix": suffix,
        "negative_prompt": negative,
        "character_mod": char_mod,
        "fuso_mod": fuso_mod,
    }


def parse_report(stdout: str) -> dict:
    for line in reversed(stdout.splitlines()):
        if line.startswith("REPORT_JSON:"):
            return json.loads(line[len("REPORT_JSON:"):])
    print("[concept_bridge] REPORT_JSON nao encontrado na saida do worker")
    sys.exit(1)


def main():
    ap = argparse.ArgumentParser(description="Ponte 2D Local (Conceito / Tileable / UI)")
    ap.add_argument("--name", required=True, help="Slug do asset (ex.: porca_drone, piso_caldeiral)")
    ap.add_argument("--prompt", default="", help="Descricao da peca/textura (opcional se --character for usado)")
    ap.add_argument("--mode", default="concept", choices=["concept", "tileable", "ui"])
    ap.add_argument("--art-bible", default=None, help="Caminho para docs/art/art-bible.yaml")
    ap.add_argument("--character", default=None, help="ID de personagem em art-bible.yaml (ex.: dez, porca)")
    ap.add_argument("--fuso", default=None, help="ID de paleta/fuso em art-bible.yaml (ex.: caldeiral)")
    ap.add_argument("--model", default="stabilityai/sdxl-turbo")
    ap.add_argument("--size", type=int, default=512)
    ap.add_argument("--steps", type=int, default=4)
    ap.add_argument("--guidance", type=float, default=0.0)
    ap.add_argument("--seed", type=int, default=42)
    ap.add_argument("--out", default=None, help="Caminho PNG de saida")
    ap.add_argument("--wsl-python", default=DEFAULT_WSL_PYTHON)
    ap.add_argument("--resolve-only", action="store_true",
                    help="Apenas resolve e imprime o prompt final da art-bible (teste G2 sem GPU)")
    args = ap.parse_args()

    bible = resolve_art_bible(Path(args.art_bible).resolve(), args.character, args.fuso) \
        if args.art_bible else {
            "style_prefix": "", "style_suffix": "", "negative_prompt": "",
            "character_mod": "", "fuso_mod": "",
        }

    if args.mode == "tileable":
        parts = [
            "seamless tileable top-down PBR albedo texture, flat even lighting, no shadows",
            args.prompt,
            bible["fuso_mod"],
        ]
    elif args.mode == "ui":
        parts = [
            "clean stylized game UI icon, centered composition, crisp readability",
            args.prompt,
            bible["fuso_mod"],
        ]
    else:
        parts = [
            bible["style_prefix"],
            bible["character_mod"],
            args.prompt,
            bible["fuso_mod"],
            bible["style_suffix"],
        ]
    full_prompt = ", ".join(p for p in parts if p)
    if not full_prompt:
        print("[concept_bridge] Prompt final vazio — passe --prompt ou --art-bible + --character")
        sys.exit(2)

    if args.resolve_only:
        print(json.dumps({
            "name": args.name,
            "mode": args.mode,
            "full_prompt": full_prompt,
            "negative_prompt": bible["negative_prompt"],
        }, indent=2, ensure_ascii=False))
        return

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
