#!/usr/bin/env python3
"""
tools/art/extract_vault_textures.py
Extrator robusto de texturas PBR originais embutidas nos pacotes .uasset do assets_vault:
- Medieval Knives: T_KNIFE_01_4K_D, T_KNIFE_01_4K_N, T_KNIFE_01_4K_M
- Underwater Coral: T_Coral_01_D, T_Coral_01_N, T_Coral_01_SS, T_Coral_18_D, T_Coral_22_D
- Sci-Fi Skies: T_scifiskies_bg_01, T_OceanSurface_Caustics
- FPS Rifle System: T_Assault_Rifle_B_Diff, T_Assault_Rifle_B_Norm, T_Assault_Rifle_B_Masks
"""

import os
import io
import sys
from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parent.parent.parent
VAULT = ROOT / "assets_vault"
OUT_TEX = ROOT / "unity" / "Assets" / "Game" / "Art" / "AbyssalBreach" / "Textures"

def find_file(pattern):
    for r, d, files in os.walk(VAULT):
        for f in files:
            if f.lower() == pattern.lower():
                return Path(r) / f
    return None

def extract_largest_png(uasset_path):
    if not uasset_path or not uasset_path.exists():
        return None
    data = uasset_path.read_bytes()
    png_sig = b"\x89PNG\r\n\x1a\n"
    iend_sig = b"IEND\xae\x42\x60\x82"
    
    candidates = []
    pos = 0
    while True:
        idx = data.find(png_sig, pos)
        if idx == -1:
            break
        iend = data.find(iend_sig, idx)
        if iend != -1 and (iend - idx) < 30_000_000:
            chunk = data[idx:iend + 8]
            try:
                im = Image.open(io.BytesIO(chunk))
                im.load()
                candidates.append((im.width * im.height, im))
            except Exception:
                pass
        pos = idx + 8
    
    if not candidates:
        return None
    candidates.sort(key=lambda x: x[0], reverse=True)
    return candidates[0][1]

def main():
    OUT_TEX.mkdir(parents=True, exist_ok=True)
    print("=== EXTRAINDO TEXTURAS REAIS DO ASSETS_VAULT ===")

    targets = [
        # Medieval Knives (4K PBR)
        ("T_KNIFE_01_4K_D.uasset", "T_Knife_01_D.png", (2048, 2048)),
        ("T_KNIFE_01_4K_N.uasset", "T_Knife_01_N.png", (2048, 2048)),
        ("T_KNIFE_01_4K_M.uasset", "T_Knife_01_M.png", (2048, 2048)),
        # Underwater Coral (High-Res PBR)
        ("T_Coral_01_D.uasset", "T_Coral_01_D.png", (1024, 1024)),
        ("T_Coral_01_N.uasset", "T_Coral_01_N.png", (1024, 1024)),
        ("T_Coral_01_SS.uasset", "T_Coral_01_SS.png", (1024, 1024)),
        ("T_Coral_18_D.uasset", "T_Coral_02_D.png", (1024, 1024)),
        ("T_Coral_22_D.uasset", "T_Coral_03_D.png", (1024, 1024)),
        # Sci-Fi Sky / Caustics
        ("T_ScifiSkies_flowmap_01.uasset", "T_OceanSurface_Caustics.png", (1024, 1024)),
        ("T_scifiskies_bg_01.uasset", "T_Skybox_Panorama.png", (2048, 1024)),
        # Rifle Textures
        ("T_Assault_Rifle_B_Diff.uasset", "T_Assault_Rifle_B_Diff.png", (1024, 1024)),
        ("T_Assault_Rifle_B_Norm.uasset", "T_Assault_Rifle_B_Norm.png", (1024, 1024)),
        ("T_Assault_Rifle_B_Masks.uasset", "T_Assault_Rifle_B_Masks.png", (1024, 1024)),
    ]

    for src_name, dst_name, max_size in targets:
        uasset = find_file(src_name)
        if not uasset:
            print(f"[AVISO] Arquivo não encontrado no vault: {src_name}")
            continue
        
        img = extract_largest_png(uasset)
        out_path = OUT_TEX / dst_name
        if img is not None:
            # Redimensionar se exceder orçamento para manter draw time e memória sob controle
            if img.width > max_size[0] or img.height > max_size[1]:
                img = img.resize(max_size, Image.Resampling.LANCZOS)
            img.save(out_path, format="PNG", optimize=True)
            print(f"[OK] Extraído do Vault: {src_name} -> {dst_name} ({img.size}, {out_path.stat().st_size // 1024} KB)")
        else:
            print(f"[AVISO] Nenhum bloco PNG extraível em {src_name}")

if __name__ == "__main__":
    main()
