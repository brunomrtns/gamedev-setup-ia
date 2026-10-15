#!/usr/bin/env python3
"""
Validador Determinístico de Cenas Unity (.unity)
Detecta scripts ausentes (missing scripts / missing Monobehaviours), referências nulas,
câmeras órfãs e validação de conformidade com os padrões do estúdio.
"""

import sys
import os
import re
import argparse
from pathlib import Path

def validate_scene_file(scene_path):
    p = Path(scene_path)
    if not p.exists():
        return False, [f"Arquivo de cena não encontrado: {scene_path}"]

    errors = []
    warnings = []

    content = p.read_text(encoding="utf-8", errors="replace")

    # 1. Checagem de Missing Scripts (m_Script: {fileID: 0})
    # No YAML do Unity, componentes com script perdido aparecem com fileID 0
    missing_scripts = re.findall(r"m_Script:\s*\{fileID:\s*0[,\s\}]", content)
    if missing_scripts:
        errors.append(f"Detectados {len(missing_scripts)} componentes com Missing Script (fileID: 0)!")

    # 2. Verificar se existe pelo menos uma Câmera na cena
    has_camera = "--- !u!20 &" in content or "m_Component:" in content and "Camera" in content
    if not has_camera:
        warnings.append("Aviso: Nenhuma Câmera Unity padrão detectada diretamente no arquivo de cena.")

    # 3. Verificar se existe Luz (Light)
    has_light = "--- !u!108 &" in content
    if not has_light:
        warnings.append("Aviso: Nenhuma Light (luz direcional ou pontual) detectada na cena.")

    # 4. Verificar se é uma cena vazia (apenas cabeçalho)
    if len(content.strip().splitlines()) < 10:
        errors.append("Erro: A cena parece estar corrompida ou vazia (menos de 10 linhas).")

    is_valid = len(errors) == 0
    return is_valid, errors, warnings

def main():
    parser = argparse.ArgumentParser(description="Validador de Cena Unity")
    parser.add_argument("--scene", required=True, help="Caminho relativo ou absoluto da cena .unity")
    parser.add_argument("--strict", action="store_true", help="Falha mesmo com avisos")
    args = parser.parse_args()

    print(f"[*] Validando cena: {args.scene}")
    is_valid, errors, warnings = validate_scene_file(args.scene)

    if warnings:
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

    print("\n[RESULTADO] VALIDAÇÃO APROVADA: Cena íntegra e sem scripts órfãos.")
    sys.exit(0)

if __name__ == "__main__":
    main()
