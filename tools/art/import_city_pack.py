#!/usr/bin/env python3
"""import_city_pack.py — Extrai whitelist do Complete City Pack (.unitypackage).

Fonte (Downloads): Complete City Pack v1.0.unitypackage (tar.gz; cada asset =
<guid>/asset + <guid>/pathname + <guid>/asset.meta).
Destino: unity/Assets/Game/Art/Environment/Modular/CityPack/{Models,Textures}
Manifesto SHA-256 em .studio/citypack_manifest.json. Idempotente.

WHITELIST (missão ART PASS):
  classical_a–z + _uu/_vv/_ww/_xx/_yy/_zz · p_tree_a–f, tree_a–d, tree_bed_b
  container_* (4) · construction_barrier/building · industrial_gate · fence_a
  wall_industrial · tractor · statue stand_a/b · road_direction · bannar
  floor_park · canopi · tower_cn
"""
import hashlib, json, re, sys, tarfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
PKG = Path.home() / "Downloads" / "Complete City Pack v1.0.unitypackage"
OUT = ROOT / "unity" / "Assets" / "Game" / "Art" / "Environment" / "Modular" / "CityPack"
MANIFEST = ROOT / ".studio" / "citypack_manifest.json"

WHITELIST_FBX = re.compile(
    r"^(classical_[a-z](_uu|_vv|_ww|_xx|_yy|_zz)?|"
    r"p_tree_[a-f]|tree_[a-d]|tree_bed_b|"
    r"container_(blue|green|red|white)|construction_barrier|construction_building|"
    r"industrial_gate|fence_a|wall_industrial|tractor|"
    r"statue stand_[ab]|statue_stand_[ab]|road_direction|bannar|floor_park|canopi|"
    r"tower_cn)\.fbx$", re.IGNORECASE)
WHITELIST_TEX = re.compile(r"\.(png|tif|tiff|jpg|jpeg)$", re.IGNORECASE)


def sha256(p: Path) -> str:
    h = hashlib.sha256()
    with open(p, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def main() -> int:
    if not PKG.exists():
        print(f"[ERRO] unitypackage ausente: {PKG}", file=sys.stderr)
        return 1
    if MANIFEST.exists() and "--force" not in sys.argv:
        m = json.loads(MANIFEST.read_text())
        if m.get("pkg_sha256") == sha256(PKG) and OUT.exists():
            print(f"[citypack] já extraído ({m['file_count']} arquivos)")
            return 0

    # 1. mapa guid -> pathname
    guid2path = {}
    with tarfile.open(PKG) as z:
        members = {m.name: m for m in z.getmembers()}
    for name in members:
        if name.endswith("/pathname"):
            g = name.split("/")[0]
            raw = members[name]
            with tarfile.open(PKG) as z:
                p = z.extractfile(raw).read().decode("utf8", "replace").split("\n")[0]
            guid2path[g] = p

    # 2. extrai whitelisted — UMA passada sequencial no tar (evita re-decompress)
    wanted = {}
    for g, path in guid2path.items():
        base = Path(path).name
        if path.lower().endswith(".fbx") and WHITELIST_FBX.match(base):
            wanted[g] = ("Models", base)
        elif WHITELIST_TEX.search(base) and (
                "/textures" in path.lower() or "texture" in base.lower()):
            wanted[g] = ("Textures", base)

    z.close()
    z = tarfile.open(PKG)  # 2ª passada: iterate do início
    extracted = []
    for m in z:  # iteração sequencial — offsets crescentes, gzip não rewound
        if not m.name.endswith("/asset"):
            continue
        g = m.name.split("/")[0]
        w = wanted.get(g)
        if w is None:
            continue
        sub, base = w
        dest = OUT / sub / base
        dest.parent.mkdir(parents=True, exist_ok=True)
        with open(dest, "wb") as dst:
            dst.write(z.extractfile(m).read())
        extracted.append(dest)
    z.close()

    MANIFEST.parent.mkdir(exist_ok=True)
    MANIFEST.write_text(json.dumps({
        "pkg": PKG.name, "pkg_sha256": sha256(PKG),
        "file_count": len(extracted),
        "files": {str(p.relative_to(ROOT)): sha256(p) for p in extracted},
    }, indent=1))
    print(f"[citypack] {len(extracted)} arquivos -> {OUT.relative_to(ROOT)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
