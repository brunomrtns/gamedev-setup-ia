#!/usr/bin/env python3
"""import_quaternius_pack.py — Extrai os packs oficiais Quaternius (CC0) para o projeto.

Fontes (Downloads):
  - Universal Base Characters[Standard].zip      -> Models/ (FBX Unity + texturas)
  - Universal Animation Library[Standard].zip    -> Animations/ (UAL1_Standard.fbx in-place)
  - Universal Animation Library 2[Standard].zip  -> Animations/ (UAL2_Standard.fbx + Mannequin_F)

Destino canônico:
  unity/Assets/Game/Art/Characters/Quaternius/{Models,Animations,Textures}/

Uso:
  python tools/art/import_quaternius_pack.py
"""
import shutil
import sys
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
DL = Path.home() / "Downloads"
OUT = ROOT / "unity" / "Assets" / "Game" / "Art" / "Characters" / "Quaternius"

BASE_ZIP = DL / "Universal Base Characters[Standard].zip"
UAL1_ZIP = DL / "Universal Animation Library[Standard].zip"
UAL2_ZIP = DL / "Universal Animation Library 2[Standard].zip"


def extract_matching(zip_path: Path, dest: Path, pred, rename=None) -> list:
    """Extrai entradas do zip que passam no filtro `pred(nome)`."""
    out = []
    with zipfile.ZipFile(zip_path) as z:
        for n in z.namelist():
            if n.endswith("/") or not pred(n):
                continue
            name = rename(n) if rename else Path(n).name
            target = dest / name
            target.parent.mkdir(parents=True, exist_ok=True)
            with z.open(n) as src, open(target, "wb") as dst:
                shutil.copyfileobj(src, dst)
            out.append(target)
    return out


def main() -> int:
    for z in (BASE_ZIP, UAL1_ZIP, UAL2_ZIP):
        if not z.exists():
            print(f"[ERRO] zip ausente: {z}", file=sys.stderr)
            return 1

    models = OUT / "Models"
    anims = OUT / "Animations"
    texs = OUT / "Textures"
    n = 0

    # --- Modelos base: apenas variantes Unity (FBX de Base Characters + Hairstyles) ---
    n += len(extract_matching(
        BASE_ZIP, models,
        lambda n: n.lower().endswith(".fbx")
        and ("/unity/" in n.lower() or "(unity)" in n.lower())
        and "unreal engine" not in n.lower()
        and "godot" not in n.lower()))

    # --- Texturas: todos os PNGs do pack (albedo/normal/eye/hair) ---
    n += len(extract_matching(
        BASE_ZIP, texs,
        lambda n: n.lower().endswith(".png")))

    # --- Bibliotecas de animação (in-place; _RM é root-motion, não usado) ---
    n += len(extract_matching(
        UAL1_ZIP, anims,
        lambda n: n.lower().endswith(".fbx") and "/unity/" in n.lower()
        and not n.lower().endswith("_rm.fbx")))
    n += len(extract_matching(
        UAL2_ZIP, anims,
        lambda n: n.lower().endswith(".fbx") and "/unity/" in n.lower()
        and not n.lower().endswith("_rm.fbx")))

    print(f"[Quaternius] {n} arquivos extraídos para {OUT}")
    for d in (models, anims, texs):
        if d.exists():
            for f in sorted(d.iterdir()):
                print(f"  {d.name}/{f.name} ({f.stat().st_size // 1024} KB)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
