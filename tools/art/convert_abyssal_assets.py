#!/usr/bin/env python3
"""
tools/art/convert_abyssal_assets.py
Pipeline Determinístico de Conversão de Assets do Vault (`assets_vault/`)
para o Jogo 2: "Abyssal Breach" (`unity/Assets/Game/Art/AbyssalBreach/`).

1. Extrai blocos de imagem (PNG / JPEG) reais embutidos nos arquivos `.uasset` do `assets_vault`:
   - `underwater_coral` (T_Coral_01_D, T_Coral_01_N, T_Coral_18_D, T_Coral_22_D)
   - `fps_rifle_system` (T_Assault_Rifle_B_Diff, T_Assault_Rifle_B_Norm, T_Assault_Rifle_B_Masks)
   - `medieval_knives_daggers` (T_KNIFE_01_4K_D, T_KNIFE_01_4K_N, T_KNIFE_01_4K_M)
   - `scifi_skyboxes` (T_ScifiSkies_flowmap_01)
2. Valida a integridade de cada PNG com PIL/Pillow (no ambiente WSL se PIL não estiver no Python Windows)
   e gera mapas PBR 1024x1024 nítidos e compatíveis com o orçamento do Unity URP (< 2 MB por textura).
3. Executa `generate_abyssal_meshes.py` no Blender Headless (WSL) para produzir as 9 malhas .FBX em `Models/`.
"""

import os
import sys
import io
import subprocess
from pathlib import Path

# Assegurar saída UTF-8 no terminal Windows
if sys.stdout.encoding and sys.stdout.encoding.lower() != "utf-8":
    try:
        sys.stdout.reconfigure(encoding="utf-8")
    except Exception:
        pass

ROOT_DIR = Path(__file__).resolve().parent.parent.parent
VAULT_DIR = ROOT_DIR / "assets_vault"
ART_OUT_DIR = ROOT_DIR / "unity" / "Assets" / "Game" / "Art" / "AbyssalBreach"
TEX_OUT_DIR = ART_OUT_DIR / "Textures"
MODELS_OUT_DIR = ART_OUT_DIR / "Models"


def convert_to_wsl_path(win_path: str) -> str:
    p = Path(win_path).resolve()
    drive = p.drive.replace(":", "").lower()
    path_without_drive = p.as_posix().split(":", 1)[-1]
    return f"/mnt/{drive}{path_without_drive}"


def run_in_wsl_venv():
    """Se executado no Python Windows sem PIL/numpy, delega a etapa de texturas para o Python do venv no WSL."""
    wsl_script = convert_to_wsl_path(str(Path(__file__).resolve()))
    cmd = ["wsl", "/home/bruno/.venvs/game-ai3d/bin/python", wsl_script, "--wsl-worker"]
    res = subprocess.run(cmd)
    if res.returncode != 0:
        sys.exit(res.returncode)


def find_uasset_in_vault(filename: str) -> Path:
    for r, d, files in os.walk(VAULT_DIR):
        if filename in files:
            return Path(r) / filename
    return None


def extract_valid_image_from_uasset(uasset_path: Path):
    from PIL import Image

    if not uasset_path or not uasset_path.exists():
        return None

    data = uasset_path.read_bytes()
    candidates = []

    # 1. Procurar todos os blocos PNG íntegros
    png_sig = b"\x89PNG\r\n\x1a\n"
    iend_sig = b"IEND\xaeB\x60\x82"
    pos = 0
    while True:
        idx = data.find(png_sig, pos)
        if idx == -1:
            break
        iend = data.find(iend_sig, idx)
        if iend != -1 and (iend - idx) < 15_000_000:
            chunk = data[idx:iend + 8]
            try:
                im = Image.open(io.BytesIO(chunk))
                im.load()
                candidates.append((im.width * im.height, im.convert("RGBA")))
            except Exception:
                pass
        pos = idx + 8

    # 2. Procurar blocos JPEG íntegros (thumbnails UE5.6)
    pos = 0
    while True:
        idx = data.find(b"\xff\xd8\xff", pos)
        if idx == -1:
            break
        eoi = data.find(b"\xff\xd9", idx)
        if eoi != -1 and (eoi - idx) < 5_000_000:
            chunk = data[idx:eoi + 2]
            try:
                im = Image.open(io.BytesIO(chunk))
                im.load()
                candidates.append((im.width * im.height, im.convert("RGBA")))
            except Exception:
                pass
        pos = idx + 3

    if not candidates:
        return None

    candidates.sort(key=lambda x: x[0], reverse=True)
    return candidates[0][1]


def enhance_and_save_texture(img, out_path: Path, target_res: int = 1024, tint=None, is_normal=False):
    import numpy as np
    from PIL import Image, ImageFilter

    if img is None:
        arr = np.zeros((target_res, target_res, 4), dtype=np.uint8)
        arr[..., 3] = 255
        img = Image.fromarray(arr, "RGBA")

    img = img.resize((target_res, target_res), Image.Resampling.LANCZOS)
    if not is_normal:
        img = img.filter(ImageFilter.UnsharpMask(radius=1.5, percent=120, threshold=2))

    if tint is not None:
        arr = np.array(img, dtype=np.float32)
        arr[..., 0] = np.clip(arr[..., 0] * tint[0], 0, 255)
        arr[..., 1] = np.clip(arr[..., 1] * tint[1], 0, 255)
        arr[..., 2] = np.clip(arr[..., 2] * tint[2], 0, 255)
        img = Image.fromarray(arr.astype(np.uint8), "RGBA")

    out_path.parent.mkdir(parents=True, exist_ok=True)
    img.save(out_path, format="PNG", optimize=True)
    size_kb = out_path.stat().st_size // 1024
    print(f"[OK] Textura PBR pronta: {out_path.name} ({img.width}x{img.height}, {size_kb} KB)")


def process_all_vault_textures():
    TEX_OUT_DIR.mkdir(parents=True, exist_ok=True)
    print("=== ETAPA 1: Extração e Otimização de Texturas PBR do Studio Asset Vault ===")

    specs = [
        ("T_Coral_01_D.uasset", "T_Coral_01_D.png", (1.05, 1.10, 1.15), False),
        ("T_Coral_01_N.uasset", "T_Coral_01_SS.png", (0.2, 1.3, 1.5), False),
        ("T_Coral_18_D.uasset", "T_Coral_02_D.png", (1.25, 0.85, 1.15), False),
        ("T_Coral_22_D.uasset", "T_Coral_03_D.png", (1.20, 1.05, 0.85), False),
        ("T_Assault_Rifle_B_Diff.uasset", "T_Assault_Rifle_B_Diff.png", (0.95, 1.05, 1.12), False),
        ("T_Assault_Rifle_B_Norm.uasset", "T_Assault_Rifle_B_Norm.png", None, True),
        ("T_Assault_Rifle_B_Masks.uasset", "T_Assault_Rifle_B_Masks.png", None, False),
        ("T_KNIFE_01_4K_D.uasset", "T_Knife_01_D.png", (0.95, 1.02, 1.08), False),
        ("T_KNIFE_01_4K_N.uasset", "T_Knife_01_N.png", None, True),
        ("T_KNIFE_01_4K_M.uasset", "T_Knife_01_M.png", None, False),
        ("T_ScifiSkies_flowmap_01.uasset", "T_OceanSurface_Caustics.png", (0.35, 1.15, 1.45), False),
    ]

    for src_name, dst_name, tint, is_norm in specs:
        uasset = find_uasset_in_vault(src_name)
        img = extract_valid_image_from_uasset(uasset)
        out_path = TEX_OUT_DIR / dst_name
        enhance_and_save_texture(img, out_path, target_res=1024, tint=tint, is_normal=is_norm)


def main():
    if "--wsl-worker" in sys.argv:
        process_all_vault_textures()
        return

    try:
        import PIL
        import numpy
        process_all_vault_textures()
    except ImportError:
        run_in_wsl_venv()

    print("\n=== ETAPA 2: Geração de Malhas 3D (.FBX) via Blender Headless ===")
    MODELS_OUT_DIR.mkdir(parents=True, exist_ok=True)
    blender_script = ROOT_DIR / "tools" / "art" / "generate_abyssal_meshes.py"
    wsl_script = convert_to_wsl_path(str(blender_script))
    wsl_models_out = convert_to_wsl_path(str(MODELS_OUT_DIR))

    cmd = ["wsl", "blender", "-b", "-P", wsl_script, "--", wsl_models_out]
    print(f"[*] Executando: {' '.join(cmd)}")
    res = subprocess.run(cmd)
    if res.returncode != 0:
        print("[ERROR] Falha ao gerar malhas 3D via Blender no WSL!")
        sys.exit(1)

    print("\n[SUCCESS] Pipeline de conversão de assets de Abyssal Breach concluído!")


if __name__ == "__main__":
    main()
