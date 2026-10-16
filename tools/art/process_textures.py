#!/usr/bin/env python3
"""
tools/art/process_textures.py
Processa texturas de modelos gerados por IA para renderização PBR de alta fidelidade no Unity URP.

Cura o Problema D:
- Separa a textura difusa (BaseMap) limpa.
- Detecta e isola pixels saturados e luminosos (visor, LEDs, linhas de energia) para gerar um _EmissionMap dedicado.
- Evita emissão global sólida que lava os detalhes do modelo em azul monocromático.
"""

import os
import sys
import argparse
import numpy as np
from PIL import Image

def process_textures(input_path: str, output_dir: str):
    os.makedirs(output_dir, exist_ok=True)
    
    if not os.path.exists(input_path):
        print(f"[Error] Textura de entrada não encontrada: {input_path}")
        return False
        
    print(f"[process_textures] Carregando: {input_path}")
    src_img = Image.open(input_path).convert("RGBA")
    arr = np.array(src_img, dtype=np.float32) / 255.0
    
    rgb = arr[..., :3]
    alpha = arr[..., 3] if arr.shape[-1] == 4 else np.ones(rgb.shape[:2], dtype=np.float32)
    
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    val = np.maximum(np.maximum(r, g), b)
    min_c = np.minimum(np.minimum(r, g), b)
    delta = val - min_c
    
    sat = np.zeros_like(val)
    mask_nz = val > 0.05
    sat[mask_nz] = delta[mask_nz] / val[mask_nz]
    
    # Heurística de Emissão Inteligente:
    # Detecta pixels de visores neon (ciano/azul brilhante, magenta, amarelo/laranja vivo)
    # ou áreas ultra luminosas com valor > 0.85
    is_cyan_visor = (sat > 0.35) & (g > 0.4) & (b > 0.45) & (r < 0.6) & (val > 0.5)
    is_magenta_accent = (sat > 0.4) & (r > 0.45) & (b > 0.4) & (g < 0.5) & (val > 0.5)
    is_high_glow = (val > 0.88) & (sat > 0.25)
    
    emission_bool = is_cyan_visor | is_magenta_accent | is_high_glow
    
    # Criar mapa de emissão preto com os pixels coloridos preservados
    emission_rgb = np.zeros_like(rgb)
    emission_rgb[emission_bool] = rgb[emission_bool] * 1.5  # Boost de intensidade para bloom URP
    emission_rgb = np.clip(emission_rgb, 0.0, 1.0)
    
    # Mapa de Suavidade / Metálico PBR
    # Superfícies não emissivas: armadura metálica (metallic 0.6, smoothness 0.65)
    # Visor e lâmpadas: vidro altamente reflexivo (smoothness 0.95, metallic 0.1)
    smoothness = np.full_like(val, 0.65)
    smoothness[emission_bool] = 0.95
    
    # Salvar saídas
    diffuse_path = os.path.join(output_dir, "robot_diffuse.png")
    emission_path = os.path.join(output_dir, "robot_emission_mask.png")
    smoothness_path = os.path.join(output_dir, "robot_smoothness.png")
    
    # 1. BaseMap Difuso Limpo
    src_img.save(diffuse_path)
    print(f"[process_textures] Salvo BaseMap: {diffuse_path}")
    
    # 2. EmissionMap
    em_img = Image.fromarray((emission_rgb * 255.0).astype(np.uint8))
    em_img.save(emission_path)
    glow_count = np.sum(emission_bool)
    print(f"[process_textures] Salvo EmissionMap ({glow_count} pixels incandescentes): {emission_path}")
    
    # 3. SmoothnessMap
    sm_img = Image.fromarray((smoothness * 255.0).astype(np.uint8))
    sm_img.save(smoothness_path)
    print(f"[process_textures] Salvo SmoothnessMap: {smoothness_path}")
    
    return True

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Processa texturas IA para URP PBR")
    parser.add_argument("--input", required=True, help="Caminho da textura de entrada (PNG)")
    parser.add_argument("--output-dir", required=True, help="Diretório de saída")
    args = parser.parse_args()
    
    ok = process_textures(args.input, args.output_dir)
    sys.exit(0 if ok else 1)
