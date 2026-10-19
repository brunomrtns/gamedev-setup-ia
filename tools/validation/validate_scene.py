#!/usr/bin/env python3
"""
Validador Determinístico de Cenas Unity (.unity) — V2 (Endurecido)

V2 (Quality Gate V2 / AGENTS.md):
- Detecta Missing Scripts (fileID 0) e referências nulas.
- Inspeciona TODOS os MeshRenderer da cena:
  * FALHA se houver materiais não resolvidos (magenta/incompatível).
  * FALHA se materiais Lit estiverem sem texturas (_BaseMap) vinculadas ou com
    cores chapadas monocromáticas.
- FALHA se forem detectadas primitivas GameObject nativas (Cube/Sphere/Plane/...)
  visíveis no lugar de prefabs catalogados do pipeline modular.
- PrefabInstances devem resolver para prefabs existentes.

Uso:
  python tools/validation/validate_scene.py --scene <caminho> [--strict]
"""

import sys
import argparse
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import studio_validators as sv  # noqa: E402


def validate_scene_file(scene_path):
    p = Path(scene_path)
    if not p.exists():
        return False, [f"Arquivo de cena não encontrado: {scene_path}"], []

    ok, issues, warnings = sv.check_scene(p)
    return ok, issues, warnings


def main():
    parser = argparse.ArgumentParser(description="Validador de Cena Unity V2")
    parser.add_argument("--scene", required=True, help="Caminho relativo ou absoluto da cena .unity")
    parser.add_argument("--strict", action="store_true", help="Falha mesmo com avisos")
    args = parser.parse_args()

    print(f"[*] Validando cena: {args.scene}")
    is_valid, errors, warnings = validate_scene_file(args.scene)

    for w in warnings:
        print(f"[WARN] {w}")

    if errors:
        for e in errors:
            print(f"[FAIL] {e}")
        print("\n[RESULTADO] VALIDAÇÃO REPROVADA.")
        sys.exit(1)

    if args.strict and warnings:
        print("\n[RESULTADO] VALIDAÇÃO REPROVADA (Modo Strict).")
        sys.exit(1)

    print("\n[RESULTADO] VALIDAÇÃO APROVADA: cena íntegra, URP Lit texturizado e sem primitivas nativas.")
    sys.exit(0)


if __name__ == "__main__":
    main()
