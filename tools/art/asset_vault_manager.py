#!/usr/bin/env python3
"""
tools/art/asset_vault_manager.py
Gerenciador e Catálogo do Banco de Assets do Estúdio Autônomo (assets_vault).

Permite listar, buscar por gênero/tags, inspecionar inventários e indexar novos pacotes
de assets para uso pelos agentes de IA e pipelines de geração de jogos.
"""

import os
import sys
import json
import argparse
from pathlib import Path

# Assegurar saída UTF-8 no terminal Windows
if sys.stdout.encoding and sys.stdout.encoding.lower() != "utf-8":
    try:
        sys.stdout.reconfigure(encoding="utf-8")
    except Exception:
        pass

ROOT_DIR = Path(__file__).resolve().parent.parent.parent
VAULT_DIR = ROOT_DIR / "assets_vault"
CATALOG_FILE = VAULT_DIR / "catalog.json"

DEFAULT_METADATA = {
    "fps_rifle_system": {
        "id": "fps_rifle_system",
        "name": "First-Person Shooter Rifle System",
        "category": "weapons/firearms",
        "path": "weapons/firearms/fps_rifle_system",
        "engine_source": "Unreal Engine 5.5 - 5.6",
        "genres": ["ActionShooter", "RpgSurvival", "AdventureExploration"],
        "tags": ["rifle", "assault_rifle", "fps", "animations", "mannequin", "aiming", "shooting", "weapons", "ue5"],
        "description": "Sistema completo de rifle em primeira pessoa com mesh de rifle de assalto, esqueleto, animações em primeira pessoa (Manny/Quinn), Blueprints de mira, disparo e lógica de combate."
    },
    "medieval_swords": {
        "id": "medieval_swords",
        "name": "Medieval Swords Pack",
        "category": "weapons/melee",
        "path": "weapons/melee/swords",
        "engine_source": "Unreal Engine 5.6",
        "genres": ["Fighting", "ActionShooter", "RpgSurvival", "AdventureExploration"],
        "tags": ["sword", "blade", "melee", "combat", "medieval", "weapons", "slashing", "ue5"],
        "description": "Coleção de 13 espadas medievais detalhadas, com StaticMeshes, SkeletalMeshes, Física, Materiais PBR e texturas 2K/4K."
    },
    "medieval_knives_daggers": {
        "id": "medieval_knives_daggers",
        "name": "Medieval Knives and Daggers Pack",
        "category": "weapons/melee",
        "path": "weapons/melee/knives_daggers",
        "engine_source": "Unreal Engine 5.6",
        "genres": ["Fighting", "RpgSurvival", "AdventureExploration", "ActionShooter"],
        "tags": ["knife", "dagger", "melee", "stealth", "medieval", "weapons", "survival", "ue5"],
        "description": "Conjunto de facas e adagas medievais de combate e sobrevivência com variações de malhas estáticas/esqueléticas, materiais e texturas PBR."
    },
    "scifi_skyboxes": {
        "id": "scifi_skyboxes",
        "name": "Matte Painting Skybox Pack - Sci-Fi Skies",
        "category": "environments/skies",
        "path": "environments/sci_fi_skyboxes",
        "engine_source": "Unreal Engine 5.6",
        "genres": ["RunnerPlatformer", "VehicleRacing", "ActionShooter", "AdventureExploration", "RpgSurvival"],
        "tags": ["skybox", "hdri", "sci-fi", "space", "planets", "skydome", "matte_painting", "environment", "ue5"],
        "description": "Pacote de skyboxes e panoramas cósmicos e futuristas (Sci-Fi, planetas, nebulosas) de alta resolução para cenários espaciais e cyberpunk."
    },
    "underwater_coral": {
        "id": "underwater_coral",
        "name": "Underwater Coral Pack 01",
        "category": "environments/underwater",
        "path": "environments/underwater_coral",
        "engine_source": "Unreal Engine 5.6",
        "genres": ["AdventureExploration", "RpgSurvival"],
        "tags": ["underwater", "coral", "ocean", "sea", "reef", "flora", "environment", "props", "nature", "ue5"],
        "description": "Pacote massivo de ambiente submarino contendo recifes de corais, flora aquática, fundos oceânicos e texturas subaquáticas."
    }
}


def build_catalog():
    VAULT_DIR.mkdir(parents=True, exist_ok=True)
    catalog = {
        "vault_name": "Studio Asset Vault (AI Game Studio)",
        "total_packages": len(DEFAULT_METADATA),
        "packages": {}
    }

    for pkg_id, meta in DEFAULT_METADATA.items():
        pkg_path = VAULT_DIR / meta["path"]
        extracted_path = pkg_path / "extracted"

        archive_file = None
        archive_size_mb = 0.0
        if pkg_path.exists():
            for f in pkg_path.iterdir():
                if f.is_file() and f.suffix.lower() in (".zip", ".rar", ".7z", ".tar"):
                    archive_file = f.name
                    archive_size_mb = round(f.stat().st_size / (1024 * 1024), 2)
                    break

        stats = {
            "total_files": 0,
            "uassets": 0,
            "maps": 0,
            "configs": 0,
            "textures": 0,
            "meshes": 0,
            "materials": 0,
            "blueprints": 0
        }

        if extracted_path.exists():
            for root, dirs, files in os.walk(extracted_path):
                for file in files:
                    stats["total_files"] += 1
                    name_lower = file.lower()
                    ext = os.path.splitext(name_lower)[1]
                    if ext == ".uasset":
                        stats["uassets"] += 1
                        if any(k in name_lower for k in ["t_", "tex", "texture", "_d", "_n", "_m"]):
                            stats["textures"] += 1
                        elif any(k in name_lower for k in ["sm_", "skm_", "mesh"]):
                            stats["meshes"] += 1
                        elif any(k in name_lower for k in ["m_", "mi_", "mat", "material"]):
                            stats["materials"] += 1
                        elif any(k in name_lower for k in ["bp_", "ia_", "imc_"]):
                            stats["blueprints"] += 1
                    elif ext == ".umap":
                        stats["maps"] += 1
                    elif ext == ".ini":
                        stats["configs"] += 1

        catalog["packages"][pkg_id] = {
            **meta,
            "archive_file": archive_file,
            "archive_size_mb": archive_size_mb,
            "stats": stats
        }

    with open(CATALOG_FILE, "w", encoding="utf-8") as f:
        json.dump(catalog, f, indent=2, ensure_ascii=False)

    print(f"[OK] Catálogo do vault gerado com sucesso em: {CATALOG_FILE}")
    return catalog


def load_catalog():
    if not CATALOG_FILE.exists():
        return build_catalog()
    with open(CATALOG_FILE, "r", encoding="utf-8") as f:
        return json.load(f)


def list_packages(catalog):
    print("\n" + "=" * 80)
    print("📦 STUDIO ASSET VAULT — CATÁLOGO DE ASSETS DISPONÍVEIS")
    print("=" * 80)
    for pkg_id, item in sorted(catalog["packages"].items()):
        stats = item.get("stats", {})
        print(f"\nID: {pkg_id}")
        print(f"  Nome: {item['name']}")
        print(f"  Categoria: {item['category']}")
        print(f"  Arquivo: {item.get('archive_file')} ({item.get('archive_size_mb', 0)} MB)")
        print(f"  Gêneros compatíveis: {', '.join(item['genres'])}")
        print(f"  Inventário: {stats.get('total_files', 0)} arquivos ({stats.get('uassets', 0)} .uassets: {stats.get('meshes', 0)} meshes, {stats.get('textures', 0)} texturas, {stats.get('materials', 0)} materiais, {stats.get('blueprints', 0)} blueprints)")
        print(f"  Tags: {', '.join(item['tags'])}")
    print("\n" + "=" * 80)


def query_packages(catalog, query_term):
    query_lower = query_term.lower()
    matches = []
    for pkg_id, item in catalog["packages"].items():
        score = 0
        if query_lower in pkg_id.lower(): score += 5
        if query_lower in item["name"].lower(): score += 4
        if query_lower in item["category"].lower(): score += 3
        if any(query_lower in g.lower() for g in item["genres"]): score += 3
        if any(query_lower in t.lower() for t in item["tags"]): score += 2
        if query_lower in item["description"].lower(): score += 1

        if score > 0:
            matches.append((score, item))

    matches.sort(key=lambda x: x[0], reverse=True)
    print(f"\n🔍 Busca por '{query_term}': {len(matches)} pacotes encontrados:\n")
    for score, item in matches:
        print(f"[{item['id']}] {item['name']} ({item['category']})")
        print(f"   Gêneros: {', '.join(item['genres'])}")
        print(f"   Tags: {', '.join(item['tags'])}")
        print(f"   Local: assets_vault/{item['path']}/extracted\n")


def show_package_info(catalog, pkg_id):
    if pkg_id not in catalog["packages"]:
        print(f"[ERROR] Pacote '{pkg_id}' não encontrado no catálogo.")
        return

    item = catalog["packages"][pkg_id]
    stats = item.get("stats", {})
    print("\n" + "=" * 70)
    print(f"Detalhes do Pacote: {item['name']} (`{pkg_id}`)")
    print("=" * 70)
    print(f"Categoria: {item['category']}")
    print(f"Origem: {item['engine_source']}")
    print(f"Descrição: {item['description']}")
    print(f"Arquivo Compactado: {item.get('archive_file')} ({item.get('archive_size_mb')} MB)")
    print(f"Diretório: assets_vault/{item['path']}")
    print(f"Gêneros de Jogos: {', '.join(item['genres'])}")
    print(f"Tags: {', '.join(item['tags'])}")
    print("\nEstatísticas de Inventário:")
    for k, v in stats.items():
        print(f"  - {k}: {v}")
    print("=" * 70 + "\n")


def main():
    parser = argparse.ArgumentParser(description="Studio Asset Vault Manager")
    parser.add_argument("--list", action="store_true", help="Listar todos os pacotes do vault")
    parser.add_argument("--rebuild", action="store_true", help="Reconstruir catálogo escaneando as pastas")
    parser.add_argument("--query", help="Buscar pacotes por gênero, palavra-chave ou tag")
    parser.add_argument("--info", help="Ver detalhes de um pacote por ID")
    args = parser.parse_args()

    if args.rebuild:
        catalog = build_catalog()
    else:
        catalog = load_catalog()

    if args.list:
        list_packages(catalog)
    elif args.query:
        query_packages(catalog, args.query)
    elif args.info:
        show_package_info(catalog, args.info)
    elif not args.rebuild:
        list_packages(catalog)


if __name__ == "__main__":
    main()
