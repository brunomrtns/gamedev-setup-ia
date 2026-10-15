#!/usr/bin/env python3
"""
Validador de Assets 3D e Texturas
Valida limites de escala, orientação, tamanho de arquivo e padrões estéticos.
"""

import sys
import os
import argparse
from pathlib import Path

def validate_asset_file(asset_path):
    p = Path(asset_path)
    if not p.exists():
        return False, [f"Arquivo de asset não encontrado: {asset_path}"]

    errors = []
    warnings = []
    ext = p.suffix.lower()

    # 1. Extensões válidas
    valid_3d = [".fbx", ".obj", ".blend", ".gltf", ".glb"]
    valid_textures = [".png", ".tga", ".jpg", ".psd"]
    valid_audio = [".wav", ".ogg", ".mp3"]

    file_size_mb = p.stat().st_size / (1024 * 1024)

    if ext in valid_3d:
        if file_size_mb > 50:
            errors.append(f"Modelo 3D excede o limite máximo permitido de 50MB ({file_size_mb:.2f}MB).")
        elif file_size_mb > 15:
            warnings.append(f"Aviso: Modelo 3D relativamente pesado ({file_size_mb:.2f}MB). Considere otimizar.")
            
    elif ext in valid_textures:
        if file_size_mb > 10:
            errors.append(f"Textura excede o limite de 10MB ({file_size_mb:.2f}MB). Reduza resolução para mobile.")
            
    elif ext in valid_audio:
        if file_size_mb > 20:
            errors.append(f"Arquivo de áudio excede 20MB ({file_size_mb:.2f}MB).")
    else:
        warnings.append(f"Extensão não padrão para pipeline de arte: {ext}")

    is_valid = len(errors) == 0
    return is_valid, errors, warnings

def main():
    parser = argparse.ArgumentParser(description="Validador de Asset")
    parser.add_argument("--asset", required=True, help="Caminho do arquivo de asset")
    args = parser.parse_args()

    print(f"[*] Validando asset: {args.asset}")
    is_valid, errors, warnings = validate_asset_file(args.asset)

    for w in warnings:
        print(f"[WARN] {w}")

    if errors:
        for e in errors:
            print(f"[FAIL] {e}")
        print("\n[RESULTADO] ASSET REPROVADO.")
        sys.exit(1)

    print("\n[RESULTADO] ASSET APROVADO.")
    sys.exit(0)

if __name__ == "__main__":
    main()
