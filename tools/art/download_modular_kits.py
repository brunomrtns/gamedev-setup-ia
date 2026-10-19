#!/usr/bin/env python3
"""
tools/art/download_modular_kits.py
Ferramenta determinística V2 de aquisição de assets open source (CC0 / livres de royalties):

1. Kits modulares de ambiente (Kenney, licença CC0):
   - Space Kit (sci-fi modular), City Kit Commercial, Fantasy Town Kit
   - Extração -> seleção de formatos (FBX > OBJ > GLB) -> copiados para
     unity/Assets/Game/Art/Environment/Modular/<Kit>/Models
   - Normalizacao headless via Blender (tools/art/batch_normalize.py, preset "kit":
     triangulacao + UV garantidas, pivo original PRESERVADO para kitbashing)
2. Texturas PBR de superficie (Poly Haven, CC0):
   - Albedo (Diffuse), Normal Map Tangent Space OpenGL (nor_gl = convencao Unity),
     Roughness e Metallic em 1024x1024 + variante 512x512 (orcamento mobile)
   - Mapa combinado MetallicSmoothness (URP Lit _MetallicGlossMap: R=metallic, A=1-roughness)
   - Salvas em unity/Assets/Game/Art/Common/Textures/
3. Manifesto com hash SHA-256 e licenca em docs/art/modular-kits-manifest.json
   (catalogados como approved no docs/art/asset-catalog.md)

Ambiente: requer requests + PIL + numpy (venv WSL game-ai3d). Se executado no Python
Windows sem deps, delega automaticamente para o WSL (padrao AGENTS.md secao 3).

Uso:
  python tools/art/download_modular_kits.py [--skip-existing] [--no-blender]
"""

import sys
import os
import io
import json
import re
import zipfile
import hashlib
import argparse
import subprocess
import shutil
import tempfile
from pathlib import Path

ROOT_DIR = Path(__file__).resolve().parent.parent.parent
MODULAR_BASE = ROOT_DIR / "unity" / "Assets" / "Game" / "Art" / "Environment" / "Modular"
TEXTURES_DIR = ROOT_DIR / "unity" / "Assets" / "Game" / "Art" / "Common" / "Textures"
MANIFEST_PATH = ROOT_DIR / "docs" / "art" / "modular-kits-manifest.json"
CATALOG_PATH = ROOT_DIR / "docs" / "art" / "asset-catalog.md"

KENNEY_KITS = [
    {"slug": "space-kit", "name": "SpaceKit",
     "desc": "Kitbashing sci-fi modular (paredes, painéis, estruturas de estação espacial)"},
    {"slug": "city-kit-commercial", "name": "CityKitCommercial",
     "desc": "Edifícios urbanos modulares comerciais (skyline Neon Rush)"},
    {"slug": "fantasy-town-kit", "name": "FantasyTownKit",
     "desc": "Construções medievais modulares (pedra/madeira)"},
    {"slug": "racing-kit", "name": "RacingKit",
     "desc": "Pistas modulares, cones, sinalização e props de corrida"},
    {"slug": "car-kit", "name": "CarKit",
     "desc": "Veículos/carros low-poly estilizados para jogos de corrida"},
    {"slug": "nature-kit", "name": "NatureKit",
     "desc": "Árvores, rochas e vegetação para cenários de aventura"},
]

POLYHAVEN_TEXTURES = [
    {"id": "metal_plate", "prefix": "MetalPlate", "metallic": True, "use": "Metal escovado"},
    {"id": "box_profile_metal_sheet", "prefix": "MetalProfile", "metallic": True, "use": "Chapa metálica perfilada"},
    {"id": "green_metal_rust", "prefix": "MetalRust", "metallic": True, "use": "Metal enferrujado"},
    {"id": "concrete_panels", "prefix": "ConcretePanel", "metallic": False, "use": "Painéis de concreto industrial"},
    {"id": "chipped_concrete", "prefix": "ConcreteWorn", "metallic": False, "use": "Concreto desgastado"},
    {"id": "asphalt_01", "prefix": "Asphalt", "metallic": False, "use": "Asfalto de pista"},
    {"id": "aerial_rocks_01", "prefix": "RockAbyssal", "metallic": False, "use": "Rocha natural (fenda oceânica)"},
]

MODEL_EXTS_ORDER = [".fbx", ".obj", ".glb"]

# Modelos conhecidamente degenerados (sem UV0 útil) — excluídos na instalação
MODEL_DENYLIST = {"fountain-center.fbx"}


def to_wsl_path(win_path: str) -> str:
    p = Path(win_path)
    if not p.drive:  # ja e caminho POSIX/WSL (ex: /mnt/c/...) — idempotente
        return p.as_posix()
    p = p.resolve()
    drive = p.drive.replace(":", "").lower()
    body = p.as_posix().split(":", 1)[-1]
    return f"/mnt/{drive}{body}"


def self_delegate_to_wsl(extra_args):
    """Delega para o venv do WSL (padrao canônico AGENTS.md secao 3)."""
    wsl_script = to_wsl_path(str(Path(__file__).resolve()))
    cmd = ["wsl", "/home/bruno/.venvs/game-ai3d/bin/python", wsl_script, "--wsl-worker"] + extra_args
    res = subprocess.run(cmd)
    sys.exit(res.returncode)


def sha256_file(path: Path) -> str:
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def download_file(url: str, dest: Path) -> Path:
    import requests
    dest.parent.mkdir(parents=True, exist_ok=True)
    if dest.exists() and dest.stat().st_size > 0:
        print(f"[SKIP] Já existe: {dest.name}")
        return dest
    print(f"[GET] {url}")
    r = requests.get(url, timeout=120, headers={"User-Agent": "gamedev-setup-ia/2.0"})
    r.raise_for_status()
    dest.write_bytes(r.content)
    print(f"[OK]  {dest.name} ({len(r.content) // 1024} KB)")
    return dest


# ----------------------------------------------------------------------------
# Kenney (scraping determinístico do link CC0 na pagina do asset)
# ----------------------------------------------------------------------------

def kenney_zip_url(slug: str) -> str:
    import requests
    html = requests.get(f"https://kenney.nl/assets/{slug}", timeout=60,
                        headers={"User-Agent": "gamedev-setup-ia/2.0"}).text
    m = re.search(r"href='(https://kenney\.nl/media/pages/assets/[^']+\.zip)'", html)
    if not m:
        raise RuntimeError(f"Link de download CC0 nao encontrado para o kit: {slug}")
    return m.group(1)


def pick_model_files(extract_dir: Path):
    """Seleciona o formato preferido (FBX > OBJ > GLB) e as texturas do kit."""
    all_models = list(extract_dir.rglob("*"))
    for ext in MODEL_EXTS_ORDER:
        models = sorted([p for p in all_models if p.suffix.lower() == ext and p.is_file()])
        if models:
            # Evita pasta de outro formato (ex: .obj dentro de "glTF" com extensao .obj)
            if ext == ".obj":
                models = [p for p in models if "gltf" not in str(p.parent).lower()]
            if models:
                textures = sorted([p for p in all_models
                                   if p.suffix.lower() in (".png", ".jpg", ".jpeg")
                                   and p.is_file() and "preview" not in p.name.lower()
                                   and "sample" not in p.name.lower()])
                return ext, models, textures
    return None, [], []


def process_kenney_kit(kit: dict, skip_existing: bool) -> dict:
    import requests
    out_dir = MODULAR_BASE / kit["name"]
    models_dir = out_dir / "Models"
    textures_dir = out_dir / "Textures"
    print(f"\n=== KIT Kenney: {kit['slug']} -> {out_dir.name} ===")

    zip_url = kenney_zip_url(kit["slug"])
    zip_sha = None
    with tempfile.TemporaryDirectory(prefix="kenney_") as td:
        zip_path = Path(td) / f"{kit['slug']}.zip"
        download_file(zip_url, zip_path)
        zip_sha = sha256_file(zip_path)
        extract_dir = Path(td) / "x"
        with zipfile.ZipFile(zip_path) as zf:
            zf.extractall(extract_dir)

        ext, models, textures = pick_model_files(extract_dir)
        if not models:
            raise RuntimeError(f"Nenhum modelo 3D encontrado no kit {kit['slug']}")

        models_dir.mkdir(parents=True, exist_ok=True)
        copied = 0
        for m in models:
            if m.name.lower() in MODEL_DENYLIST:
                continue
            dst = models_dir / m.name
            if skip_existing and dst.exists():
                continue
            shutil.copy2(m, dst)
            copied += 1

        textures_dir.mkdir(parents=True, exist_ok=True)
        tex_copied = 0
        for t in textures:
            dst = textures_dir / t.name
            if not dst.exists():
                shutil.copy2(t, dst)
                tex_copied += 1

    return {
        "kit": kit["name"], "slug": kit["slug"], "description": kit["desc"],
        "license": "CC0", "source_url": f"https://kenney.nl/assets/{kit['slug']}",
        "zip_url": zip_url, "zip_sha256": zip_sha,
        "model_format": ext, "model_count": len(models), "models_copied": copied,
        "texture_count": len(textures), "textures_copied": tex_copied,
        "dest": str(models_dir.relative_to(ROOT_DIR)).replace("\\", "/"),
    }


# ----------------------------------------------------------------------------
# Poly Haven (API pública CC0)
# ----------------------------------------------------------------------------

def polyhaven_file_url(asset_id: str, map_name: str, res: str = "1k") -> str:
    """API Poly Haven: files/<id> -> { <mapa>: { <res>: { jpg: { url, file } } } }"""
    import requests
    meta = requests.get(f"https://api.polyhaven.com/files/{asset_id}", timeout=60,
                        headers={"User-Agent": "gamedev-setup-ia/2.0"}).json()
    node = meta.get(map_name, {}).get(res, {}).get("jpg")
    if not node:
        return None
    return node.get("url") or f"https://dl.polyhaven.org/file/ph-assets/Textures/jpg/{res}/{asset_id}/{node.get('file', '')}"


def build_metallic_smoothness(diff_img, rough_img, metal_img, metallic: bool):
    """URP Lit _MetallicGlossMap: R = metallic, A = smoothness (1 - roughness)."""
    import numpy as np
    from PIL import Image
    size = diff_img.size
    rough = np.asarray(rough_img.convert("L").resize(size, Image.Resampling.LANCZOS), dtype=np.float32) / 255.0
    if metal_img is not None:
        metal = np.asarray(metal_img.convert("L").resize(size, Image.Resampling.LANCZOS), dtype=np.float32) / 255.0
    else:
        metal = np.full(size, 1.0 if metallic else 0.0, dtype=np.float32)
    ms = np.zeros((size[1], size[0], 4), dtype=np.uint8)
    ms[..., 0] = np.clip(metal * 255.0, 0, 255).astype(np.uint8)
    ms[..., 3] = np.clip((1.0 - rough) * 255.0, 0, 255).astype(np.uint8)
    return Image.fromarray(ms, "RGBA")


def process_polyhaven_texture(fam: dict, skip_existing: bool) -> dict:
    from PIL import Image
    print(f"\n=== TEXTURA Poly Haven: {fam['id']} -> {fam['prefix']} ===")
    fam_dir = TEXTURES_DIR / fam["prefix"]
    fam_dir.mkdir(parents=True, exist_ok=True)

    maps = {
        "D": polyhaven_file_url(fam["id"], "Diffuse"),
        "N": polyhaven_file_url(fam["id"], "nor_gl"),   # OpenGL Y+ = tangent space Unity
        "R": polyhaven_file_url(fam["id"], "Rough"),
        "M": polyhaven_file_url(fam["id"], "Metal"),
    }
    files = {}
    images = {}
    for suffix, url in maps.items():
        if not url:
            continue
        dst = fam_dir / f"{fam['prefix']}_{suffix}.jpg"
        if not (skip_existing and dst.exists()):
            download_file(url, dst)
        files[suffix] = str(dst.relative_to(ROOT_DIR)).replace("\\", "/")
        images[suffix] = Image.open(dst)

    if "D" not in images or "R" not in images:
        raise RuntimeError(f"Familia {fam['id']} sem Albedo/Roughness — inviavel para PBR")

    # Mapa combinado MetallicSmoothness (URP Lit) 1024
    ms_img = build_metallic_smoothness(images["D"], images["R"], images.get("M"), fam["metallic"])
    ms_path = fam_dir / f"{fam['prefix']}_MS.png"
    ms_img.save(ms_path, format="PNG", optimize=True)
    files["MS"] = str(ms_path.relative_to(ROOT_DIR)).replace("\\", "/")

    # Variante mobile 512x512 (orcamento docs/standards/mobile-performance.md)
    dir512 = TEXTURES_DIR / (fam["prefix"] + "_512")
    dir512.mkdir(parents=True, exist_ok=True)
    for suffix, img in images.items():
        small = img.convert("RGB").resize((512, 512), Image.Resampling.LANCZOS)
        p512 = dir512 / f"{fam['prefix']}_{suffix}_512.jpg"
        small.save(p512, format="JPEG", quality=88, optimize=True)
        files[f"{suffix}512"] = str(p512.relative_to(ROOT_DIR)).replace("\\", "/")
    ms512 = ms_img.resize((512, 512), Image.Resampling.LANCZOS)
    p512 = dir512 / f"{fam['prefix']}_MS_512.png"
    ms512.save(p512, format="PNG", optimize=True)
    files["MS512"] = str(p512.relative_to(ROOT_DIR)).replace("\\", "/")

    return {
        "family": fam["prefix"], "asset_id": fam["id"], "license": "CC0",
        "source": f"https://polyhaven.com/a/{fam['id']}", "use": fam["use"],
        "maps": {k: v for k, v in files.items()},
        "resolutions": ["1024", "512"], "sha256_albedo": sha256_file(fam_dir / f"{fam['prefix']}_D.jpg"),
    }


# ----------------------------------------------------------------------------
# Normalizacao Blender headless (uma unica sessao para todos os kits)
# ----------------------------------------------------------------------------

def is_binary_fbx(path: Path) -> bool:
    try:
        with open(path, "rb") as f:
            return f.read(21).startswith(b"Kaydara FBX Binary")
    except OSError:
        return False


def run_blender_batch_normalize(skip_existing: bool) -> dict:
    """Gera manifesto de normalizacao e roda batch_normalize.py em UMA sessao Blender WSL.

    Nota: FBX ASCII (Kenney) nao e importavel pelo Blender 5.x — permanece vendor-canonical
    (Y-up, pivots de kitbashing) e e auditado deterministicamente pelo validate_asset.py V2
    (parser ASCII de transformacoes/UV/triangulacao).
    """
    entries = []
    skipped_ascii = 0
    for kit_dir in sorted(MODULAR_BASE.glob("*/Models")):
        for model in sorted(kit_dir.glob("*.fbx")) + sorted(kit_dir.glob("*.obj")):
            if model.suffix.lower() == ".fbx" and not is_binary_fbx(model):
                skipped_ascii += 1
                continue
            # Normalizacao in-place e idempotente (FBX canonico permanece canonico)
            entries.append({
                "input": to_wsl_path(str(model)),
                "output": to_wsl_path(str(model)),
                "preset": "kit",
            })
    if not entries:
        return {"normalized": 0, "skipped_ascii_fbx": skipped_ascii}

    manifest = Path(tempfile.gettempdir()) / "studio_kit_normalize.json"
    manifest.write_text(json.dumps(entries), encoding="utf-8")

    batch_script = to_wsl_path(str(ROOT_DIR / "tools" / "art" / "batch_normalize.py"))
    # Dentro do WSL o blender e invocado diretamente; do Windows, via prefixo wsl
    prefix = [] if os.name != "nt" else ["wsl"]
    cmd = prefix + ["blender", "-b", "-P", batch_script, "--", to_wsl_path(str(manifest))]
    print(f"\n=== NORMALIZACAO BLENDER HEADLESS: {len(entries)} modelos (1 sessao) ===")
    res = subprocess.run(cmd)
    if res.returncode != 0:
        raise RuntimeError("Falha na normalizacao Blender dos kits (ver log acima)")
    return {"normalized": len(entries)}


# ----------------------------------------------------------------------------
# Catalogo e manifesto
# ----------------------------------------------------------------------------

def update_manifest(kits, textures, blender_info):
    data = {
        "version": "2.0",
        "generated_at": __import__("datetime").datetime.now().isoformat(),
        "licenses": "CC0 (Kenney + Poly Haven) — livres de royalties",
        "kenney_kits": kits,
        "polyhaven_textures": textures,
        "blender_normalization": blender_info,
    }
    MANIFEST_PATH.parent.mkdir(parents=True, exist_ok=True)
    MANIFEST_PATH.write_text(json.dumps(data, indent=2, ensure_ascii=False), encoding="utf-8")
    print(f"\n[OK] Manifesto salvo: {MANIFEST_PATH.relative_to(ROOT_DIR)}")


def update_catalog(kits, textures):
    section = ["\n---\n",
               "## 3. Assets Open Source CC0 Aprovados (V2 — Modular Kits & PBR)\n",
               "Fonte determinística: [`modular-kits-manifest.json`](modular-kits-manifest.json) "
               "(hashes SHA-256, URLs e licenças). Status: **approved**.\n",
               "### 3.1 Kits Modulares de Ambiente (`unity/Assets/Game/Art/Environment/Modular/`)\n"]
    for k in kits:
        section.append(f"- **{k['kit']}** ({k['model_count']} modelos `{k['model_format']}`, "
                       f"{k['texture_count']} texturas) — {k['description']}. CC0. `{k['dest']}`\n")
    section.append("\n### 3.2 Texturas PBR Poly Haven (`unity/Assets/Game/Art/Common/Textures/`)\n")
    for t in textures:
        maps = ", ".join(sorted(t["maps"].keys()))
        section.append(f"- **{t['family']}** — {t['use']}. Mapas: {maps}. CC0. `{t['source']}`\n")
    section.append("\n### 3.3 Quality Gate V2\n"
                   "Toda geração de ambiente deve usar estritamente estes kits/materiais ou modelos "
                   "processados pelo pipeline headless `tools/art/normalize_fbx.py` "
                   "(`fbx_canonical.py`). Vignettes primitivas sem textura são proibidas "
                   "(ver `docs/standards/asset-quality.md`).\n")
    existing = CATALOG_PATH.read_text(encoding="utf-8")
    marker = "\n---\n\n## 3. Assets Open Source CC0 Aprovados"
    if marker in existing:
        existing = existing.split(marker)[0].rstrip() + "\n"
        CATALOG_PATH.write_text(existing, encoding="utf-8")
    with open(CATALOG_PATH, "a", encoding="utf-8") as f:
        f.writelines(section)
    print(f"[OK] Catálogo atualizado: {CATALOG_PATH.relative_to(ROOT_DIR)}")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--skip-existing", action="store_true")
    ap.add_argument("--no-blender", action="store_true", help="Pula normalização headless")
    ap.add_argument("--wsl-worker", action="store_true", help=argparse.SUPPRESS)
    args, extra = ap.parse_known_args()

    if not args.wsl_worker and os.name == "nt":
        try:
            import requests  # noqa: F401
            import PIL  # noqa: F401
        except ImportError:
            print("[INFO] deps ausentes no Python Windows -> delegando ao venv WSL game-ai3d")
            passthrough = [a for a in sys.argv[1:] if a != "--wsl-worker"]
            self_delegate_to_wsl(passthrough)

    kits, textures = [], []
    for kit in KENNEY_KITS:
        try:
            kits.append(process_kenney_kit(kit, args.skip_existing))
        except Exception as e:
            print(f"[WARN] Kit {kit['slug']} falhou: {e}")
    for fam in POLYHAVEN_TEXTURES:
        try:
            textures.append(process_polyhaven_texture(fam, args.skip_existing))
        except Exception as e:
            print(f"[WARN] Textura {fam['id']} falhou: {e}")

    if not kits and not textures:
        print("[ERROR] Nenhum asset obtido — verificando rede/licenças")
        sys.exit(1)

    blender_info = {"normalized": 0}
    if not args.no_blender and kits:
        try:
            blender_info = run_blender_batch_normalize(args.skip_existing)
        except Exception as e:
            print(f"[WARN] Normalização Blender pulada/falhou: {e}")

    update_manifest(kits, textures, blender_info)
    update_catalog(kits, textures)
    print("\n[SUCCESS] Ecossistema open source CC0 V2 instalado e catalogado.")


if __name__ == "__main__":
    main()
