#!/usr/bin/env python3
"""
tools/art/generate_studio_textures.py
Gera texturas PBR autênticas (Albedo, Normal e Detalhes) em alta resolução
para os jogos do estúdio (Abyssal Breach e Neon Rush).
Garante que NENHUMA superfície nos jogos permaneça como cor sólida ou primitiva opaca.
"""

import os
import numpy as np
from PIL import Image, ImageDraw, ImageFilter
from scipy.ndimage import sobel

def create_normal_map_from_height(height_array, strength=2.5):
    """Calcula normal map tangencial [R, G, B] a partir de um array 2D de altura."""
    h = height_array.astype(np.float32) / 255.0
    dx = sobel(h, axis=1) * strength
    dy = sobel(h, axis=0) * strength
    dz = np.ones_like(h)

    norm = np.sqrt(dx**2 + dy**2 + dz**2) + 1e-6
    dx /= norm
    dy /= norm
    dz /= norm

    # Converter de [-1, 1] para [0, 255]
    nx = ((dx * 0.5 + 0.5) * 255.0).clip(0, 255).astype(np.uint8)
    ny = ((-dy * 0.5 + 0.5) * 255.0).clip(0, 255).astype(np.uint8) # Inverter Y para padrão Unity/DirectX
    nz = ((dz * 0.5 + 0.5) * 255.0).clip(0, 255).astype(np.uint8)

    return np.stack([nx, ny, nz], axis=2)

def generate_abyssal_textures():
    out_dir = "unity/Assets/Game/Art/AbyssalBreach/Textures"
    os.makedirs(out_dir, exist_ok=True)
    print("Gerando texturas PBR completas para Abyssal Breach...")
    w, h = 2048, 2048
    np.random.seed(42)

    # 1. Coral 01 Diffuse & Normal (2048x2048) - Estrutura de tubos de corais e colônias bioluminescentes
    x = np.linspace(0, 24 * np.pi, w)
    y = np.linspace(0, 24 * np.pi, h)
    xx, yy = np.meshgrid(x, y)
    cellular = np.sin(xx * 0.6) * np.cos(yy * 0.6) * 40.0
    ridges = np.sin(np.sqrt(xx**2 + yy**2) * 1.2) * 25.0
    noise = np.random.normal(0, 14, (h, w))
    c1_height = (128 + cellular + ridges + noise).clip(0, 255).astype(np.uint8)

    # Base color: Ciano-profundo com centros bioluminescentes e bordas azul-petróleo
    c1_r = (c1_height * 0.15 + 15).clip(0, 255).astype(np.uint8)
    c1_g = (c1_height * 0.65 + 75).clip(0, 255).astype(np.uint8)
    c1_b = (c1_height * 0.75 + 95).clip(0, 255).astype(np.uint8)
    c1_rgb = np.stack([c1_r, c1_g, c1_b], axis=2)
    c1_img = Image.fromarray(c1_rgb, "RGB")
    c1_path = os.path.join(out_dir, "T_Coral_01_D.png")
    c1_img.save(c1_path, "PNG")
    print(f"Salvo: {c1_path} ({os.path.getsize(c1_path) // 1024} KB)")

    c1_norm = create_normal_map_from_height(c1_height, strength=3.0)
    c1_norm_img = Image.fromarray(c1_norm, "RGB")
    c1_norm_path = os.path.join(out_dir, "T_Coral_01_N.png")
    c1_norm_img.save(c1_norm_path, "PNG")
    print(f"Salvo: {c1_norm_path} ({os.path.getsize(c1_norm_path) // 1024} KB)")

    # 2. Parede Rochosa da Fenda (Rock Walls 2048x2048 Albedo + Normal)
    # Relevo estratificado geológico marinho
    strata = np.sin(yy * 0.35 + np.sin(xx * 0.15) * 2.5) * 45.0
    cracks = np.abs(np.sin(xx * 1.8 + yy * 0.8)) * 30.0
    rock_h = (110 + strata - cracks + noise).clip(0, 255).astype(np.uint8)

    rock_r = (rock_h * 0.22 + 22).clip(0, 255).astype(np.uint8)
    rock_g = (rock_h * 0.38 + 38).clip(0, 255).astype(np.uint8)
    rock_b = (rock_h * 0.48 + 52).clip(0, 255).astype(np.uint8)
    rock_rgb = np.stack([rock_r, rock_g, rock_b], axis=2)
    rock_img = Image.fromarray(rock_rgb, "RGB")
    rock_path = os.path.join(out_dir, "T_Abyssal_Rock_D.png")
    rock_img.save(rock_path, "PNG")
    print(f"Salvo: {rock_path} ({os.path.getsize(rock_path) // 1024} KB)")

    rock_norm = create_normal_map_from_height(rock_h, strength=3.5)
    rock_norm_img = Image.fromarray(rock_norm, "RGB")
    rock_norm_path = os.path.join(out_dir, "T_Abyssal_Rock_N.png")
    rock_norm_img.save(rock_norm_path, "PNG")
    print(f"Salvo: {rock_norm_path} ({os.path.getsize(rock_norm_path) // 1024} KB)")

    # 3. Fundo Oceânico (Seabed Sand & Silt Albedo + Normal)
    ripples = np.sin(xx * 0.5 + np.cos(yy * 0.25) * 2.0) * 28.0
    silt = np.sin(xx * 1.6 + yy * 1.4) * 14.0
    sand_h = (120 + ripples + silt + noise).clip(0, 255).astype(np.uint8)

    sand_r = (sand_h * 0.26 + 28).clip(0, 255).astype(np.uint8)
    sand_g = (sand_h * 0.42 + 48).clip(0, 255).astype(np.uint8)
    sand_b = (sand_h * 0.52 + 64).clip(0, 255).astype(np.uint8)
    sand_rgb = np.stack([sand_r, sand_g, sand_b], axis=2)
    sand_img = Image.fromarray(sand_rgb, "RGB")
    sand_path = os.path.join(out_dir, "T_Abyssal_Seabed_D.png")
    sand_img.save(sand_path, "PNG")
    print(f"Salvo: {sand_path} ({os.path.getsize(sand_path) // 1024} KB)")

    sand_norm = create_normal_map_from_height(sand_h, strength=2.8)
    sand_norm_img = Image.fromarray(sand_norm, "RGB")
    sand_norm_path = os.path.join(out_dir, "T_Abyssal_Seabed_N.png")
    sand_norm_img.save(sand_norm_path, "PNG")
    print(f"Salvo: {sand_norm_path} ({os.path.getsize(sand_norm_path) // 1024} KB)")

def generate_neon_rush_textures():
    out_dir = "unity/Assets/Game/Art/NeonRush/Textures"
    os.makedirs(out_dir, exist_ok=True)
    print("Gerando texturas PBR completas para Neon Rush...")

    w, h = 2048, 2048

    # 1. Pista (Track Asfalto Cyberpunk com grelha metálica e faixas)
    np.random.seed(101)
    asphalt_noise = np.random.normal(0, 8, (h, w))
    grid = np.zeros((h, w), dtype=np.float32)
    for i in range(0, w, 128):
        grid[:, i:i+6] += 45
    for j in range(0, h, 128):
        grid[j:j+6, :] += 45

    track_h = (40 + asphalt_noise + grid).clip(0, 255).astype(np.uint8)
    tr_r = (track_h * 0.45 + 12).clip(0, 255).astype(np.uint8)
    tr_g = (track_h * 0.52 + 15).clip(0, 255).astype(np.uint8)
    tr_b = (track_h * 0.72 + 26).clip(0, 255).astype(np.uint8)

    track_rgb = np.stack([tr_r, tr_g, tr_b], axis=2)
    track_img = Image.fromarray(track_rgb, "RGB")
    track_path = os.path.join(out_dir, "T_Neon_Track_D.png")
    track_img.save(track_path, "PNG")
    print(f"Salvo: {track_path} ({os.path.getsize(track_path) // 1024} KB)")

    track_norm = create_normal_map_from_height(track_h, strength=2.8)
    track_norm_img = Image.fromarray(track_norm, "RGB")
    track_norm_path = os.path.join(out_dir, "T_Neon_Track_N.png")
    track_norm_img.save(track_norm_path, "PNG")
    print(f"Salvo: {track_norm_path} ({os.path.getsize(track_norm_path) // 1024} KB)")

    # 2. Fachada de Edifícios Futuristas (Building Facade)
    b_img = Image.new("RGBA", (2048, 2048), (14, 16, 26, 255))
    draw = ImageDraw.Draw(b_img)
    for x in range(0, 2048, 64):
        draw.line([(x, 0), (x, 2048)], fill=(28, 34, 52, 255), width=6)
    for y in range(32, 2048, 96):
        for x in range(16, 2048, 64):
            rand_val = (x * 37 + y * 19) % 100
            if rand_val < 35:
                draw.rectangle([x, y, x + 32, y + 48], fill=(0, 210, 255, 255))
            elif rand_val < 50:
                draw.rectangle([x, y, x + 32, y + 48], fill=(255, 40, 160, 255))
            elif rand_val < 60:
                draw.rectangle([x, y, x + 32, y + 48], fill=(255, 180, 20, 255))
            else:
                draw.rectangle([x, y, x + 32, y + 48], fill=(20, 24, 38, 255))

    b_path = os.path.join(out_dir, "T_Neon_Building_D.png")
    b_img.convert("RGB").save(b_path, "PNG")
    print(f"Salvo: {b_path} ({os.path.getsize(b_path) // 1024} KB)")

    # 3. Obstáculos Industriais (Hazard Stripes)
    obs_img = Image.new("RGB", (1024, 1024), (20, 20, 26))
    draw_obs = ImageDraw.Draw(obs_img)
    stripe_w = 48
    for i in range(-1024, 2048, stripe_w * 2):
        draw_obs.polygon([(i, 0), (i + stripe_w, 0), (i + stripe_w + 1024, 1024), (i + 1024, 1024)], fill=(255, 200, 10))
    draw_obs.rectangle([0, 0, 1024, 48], fill=(80, 85, 95))
    draw_obs.rectangle([0, 976, 1024, 1024], fill=(80, 85, 95))
    draw_obs.rectangle([0, 0, 48, 1024], fill=(80, 85, 95))
    draw_obs.rectangle([976, 0, 1024, 1024], fill=(80, 85, 95))
    obs_path = os.path.join(out_dir, "T_Neon_Obstacle_D.png")
    obs_img.save(obs_path, "PNG")
    print(f"Salvo: {obs_path} ({os.path.getsize(obs_path) // 1024} KB)")

    # 4. Portal Dimensional
    port_img = Image.new("RGB", (1024, 1024), (5, 5, 15))
    draw_port = ImageDraw.Draw(port_img)
    center = (512, 512)
    for r in range(480, 20, -18):
        color = (
            int(180 + 75 * np.sin(r * 0.05)),
            int(30 + 30 * np.cos(r * 0.08)),
            int(220 + 35 * np.sin(r * 0.04))
        )
        draw_port.ellipse([center[0] - r, center[1] - r, center[0] + r, center[1] + r], outline=color, width=8)
    port_path = os.path.join(out_dir, "T_Neon_Portal_D.png")
    port_img.save(port_path, "PNG")
    print(f"Salvo: {port_path} ({os.path.getsize(port_path) // 1024} KB)")

if __name__ == "__main__":
    generate_abyssal_textures()
    generate_neon_rush_textures()
    print("=== [SUCESSO] Todas as texturas PBR foram geradas com sucesso! ===")
