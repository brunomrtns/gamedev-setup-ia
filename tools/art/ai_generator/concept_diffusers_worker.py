#!/usr/bin/env python3
"""
tools/art/ai_generator/concept_diffusers_worker.py
Worker WSL (CUDA, diffusers 0.33.1 na venv ~/.venvs/hunyuan) da Fase 4 — Conceito 2D,
Texturas Tileaveis e UI 100% locais (Studio V2).

Modos suportados:
  - concept  : imagem de referencia isolada (entrada direta para a Fabrica 3D)
  - tileable : textura 100% sem emenda (circular Conv2d padding) para chao/paredes
  - ui       : icones, retratos de dialogo e arte de interface

Modelo padrao: stabilityai/sdxl-turbo (fp16, 4 steps, ~1.5s na RTX 3060 12GB,
nao exige token HF).
"""

import argparse
import json
import sys
import time
from pathlib import Path


def enable_circular_padding(model):
    """Ativa padding circular em todas as Conv2d para gerar texturas 100% tileaveis."""
    import torch.nn as nn

    count = 0
    for module in model.modules():
        if isinstance(module, nn.Conv2d):
            module.padding_mode = "circular"
            count += 1
    return count


def main():
    ap = argparse.ArgumentParser(description="Worker WSL Diffusers 2D (Studio V2 Fase 4)")
    ap.add_argument("--prompt", required=True)
    ap.add_argument("--negative-prompt", default="")
    ap.add_argument("--mode", default="concept", choices=["concept", "tileable", "ui"])
    ap.add_argument("--out", required=True, help="Caminho PNG de saida")
    ap.add_argument("--model", default="stabilityai/sdxl-turbo")
    ap.add_argument("--size", type=int, default=512, help="Resolucao quadrada (512 ou 1024)")
    ap.add_argument("--steps", type=int, default=4)
    ap.add_argument("--guidance", type=float, default=0.0,
                    help="Guidance scale (0.0 recomendado para sdxl-turbo; 5.0-7.5 para SDXL base)")
    ap.add_argument("--seed", type=int, default=42)
    args = ap.parse_args()

    t0 = time.time()
    import torch
    from diffusers import AutoPipelineForText2Image

    device = "cuda" if torch.cuda.is_available() else "cpu"
    dtype = torch.float16 if device == "cuda" else torch.float32
    print(f"[concept_worker] Carregando {args.model} ({device}, {dtype})...")

    pipe = AutoPipelineForText2Image.from_pretrained(
        args.model,
        torch_dtype=dtype,
        variant="fp16" if device == "cuda" else None,
    )
    if device == "cuda":
        pipe.enable_model_cpu_offload()
    else:
        pipe = pipe.to(device)

    circular_layers = 0
    if args.mode == "tileable":
        if hasattr(pipe, "unet") and pipe.unet is not None:
            circular_layers += enable_circular_padding(pipe.unet)
        if hasattr(pipe, "vae") and pipe.vae is not None:
            circular_layers += enable_circular_padding(pipe.vae)
        print(f"[concept_worker] Modo tileable ativado ({circular_layers} camadas Conv2d circulares)")

    generator = torch.Generator(device="cpu").manual_seed(args.seed)
    gen_kwargs = {
        "prompt": args.prompt,
        "num_inference_steps": args.steps,
        "guidance_scale": args.guidance,
        "width": args.size,
        "height": args.size,
        "generator": generator,
    }
    if args.negative_prompt and args.guidance > 0.0:
        gen_kwargs["negative_prompt"] = args.negative_prompt

    img = pipe(**gen_kwargs).images[0]
    out_path = Path(args.out)
    out_path.parent.mkdir(parents=True, exist_ok=True)
    img.save(out_path)

    elapsed = round(time.time() - t0, 2)
    report = {
        "mode": args.mode,
        "model": args.model,
        "device": device,
        "size": [img.width, img.height],
        "steps": args.steps,
        "seed": args.seed,
        "circular_layers": circular_layers,
        "elapsed_sec": elapsed,
        "output_png": str(out_path),
        "prompt": args.prompt,
    }
    print("REPORT_JSON:" + json.dumps(report, ensure_ascii=False))


if __name__ == "__main__":
    main()
