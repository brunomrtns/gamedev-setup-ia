#!/usr/bin/env python3
"""import_downtown_megakit.py — Extrai o Downtown City MegaKit (CC0) p/ o projeto.

Fonte (Downloads): Downtown City MegaKit[Standard].zip
Destino canônico: unity/Assets/Game/Art/Environment/Modular/DowntownMegaKit/Source/
  Models/  <- Exports/FBX (Unity)/*.fbx (153 peças + prédios pré-montados)
  Textures/<textures soltas>

Idempotente: pula extração se o manifesto .studio/megakit_manifest.json já
confere com o zip (SHA-256). Uso:
  python tools/art/import_downtown_megakit.py [--force]
"""
import hashlib, json, sys, zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
DL = Path.home() / "Downloads"
ZIP = DL / "Downtown City MegaKit[Standard].zip"
OUT = ROOT / "unity" / "Assets" / "Game" / "Art" / "Environment" / "Modular" / "DowntownMegaKit" / "Source"
MANIFEST = ROOT / ".studio" / "megakit_manifest.json"

FBX_PREFIX = "Exports/FBX (Unity)/"
TEX_PREFIX = "Textures/"


def sha256(p: Path) -> str:
    h = hashlib.sha256()
    with open(p, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def main() -> int:
    if not ZIP.exists():
        print(f"[ERRO] zip ausente: {ZIP}", file=sys.stderr)
        return 1
    if MANIFEST.exists() and "--force" not in sys.argv:
        m = json.loads(MANIFEST.read_text())
        if m.get("zip_sha256") == sha256(ZIP) and OUT.exists():
            print(f"[megakit] já extraído ({m['file_count']} arquivos) — idempotente")
            return 0

    extracted = []
    with zipfile.ZipFile(ZIP) as z:
        for n in z.namelist():
            if n.endswith("/"):
                continue
            if n.startswith(FBX_PREFIX) and n.lower().endswith(".fbx"):
                dest = OUT / "Models" / Path(n).name
            elif n.startswith(TEX_PREFIX) and not n.startswith(TEX_PREFIX + "Unreal-"):
                dest = OUT / "Textures" / Path(n).name
            else:
                continue
            dest.parent.mkdir(parents=True, exist_ok=True)
            with z.open(n) as src, open(dest, "wb") as dst:
                dst.write(src.read())
            extracted.append(dest)

    MANIFEST.parent.mkdir(exist_ok=True)
    MANIFEST.write_text(json.dumps({
        "zip": ZIP.name, "zip_sha256": sha256(ZIP),
        "file_count": len(extracted),
        "files": {str(p.relative_to(ROOT)): sha256(p) for p in extracted},
    }, indent=1))
    print(f"[megakit] {len(extracted)} arquivos -> {OUT.relative_to(ROOT)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
