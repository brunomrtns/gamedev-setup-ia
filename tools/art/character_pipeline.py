#!/usr/bin/env python3
"""
tools/art/character_pipeline.py
Orquestrador Canônico do Pipeline de Personagens 3D para o Estúdio Autônomo.

Executa o fluxo completo ponta a ponta:
1. Reconstrução Neural 3D (TripoSR via WSL / GPU local RTX 3060).
2. Normalização de Eixos e Pivôs (Pés em Y=0, centro em X=0/Z=0, escala para 1.4m).
3. Processamento de Texturas PBR & Máscara de Emissão URP (Isola visor neon e evita modelo azul lavado).
4. Auto-Rigging & Geração de Animações (Blender headless no WSL com esqueleto Humanoid, pesos e clips RunCycle/Jump/Slide).
5. Publicação no projeto Unity (Assets/Game/Art/Characters/...).
"""

import os
import sys
import argparse
import subprocess
from pathlib import Path

def run_command(cmd, cwd=None):
    print(f"\n[character_pipeline] Executando: {' '.join(cmd) if isinstance(cmd, list) else cmd}")
    res = subprocess.run(cmd, cwd=cwd, shell=isinstance(cmd, str))
    if res.returncode != 0:
        print(f"[character_pipeline] Falha no comando (código {res.returncode})")
        return False
    return True

def convert_to_wsl_path(win_path: str) -> str:
    p = Path(win_path).resolve()
    # Exemplo: C:\Users\Bruno\... -> /mnt/c/Users/Bruno/...
    drive = p.drive.replace(":", "").lower()
    path_without_drive = p.as_posix().split(":", 1)[-1]
    return f"/mnt/{drive}{path_without_drive}"

def main():
    parser = argparse.ArgumentParser(description="Pipeline Autônomo de Personagens 3D do Estúdio")
    parser.add_argument("--image", default="tools/art/robot_concept.jpg", help="Caminho do concept 2D")
    parser.add_argument("--output-dir", default="unity/Assets/Game/Art/Characters/RobotAI", help="Diretório de saída no Unity")
    parser.add_argument("--target-height", type=float, default=1.4, help="Altura do personagem em metros (padrão: 1.4)")
    parser.add_argument("--rig-type", choices=["humanoid", "drone"], default="humanoid", help="Tipo de personagem: humanoid ou drone")
    parser.add_argument("--run-triposr", action="store_true", help="Forçar nova geração via TripoSR")
    args = parser.parse_args()

    project_root = Path(__file__).resolve().parent.parent.parent
    output_dir = project_root / args.output_dir
    output_dir.mkdir(parents=True, exist_ok=True)
    
    raw_mesh = output_dir / "robot_mesh.obj"
    raw_tex = output_dir / "robot_texture.png"
    concept_img = project_root / args.image

    # Etapa 1: TripoSR (se necessário ou se a malha bruta não existir)
    if args.run_triposr or not raw_mesh.exists() or not raw_tex.exists():
        print("=== ETAPA 1: Reconstrução Neural 3D via TripoSR (WSL + CUDA) ===")
        wsl_img = convert_to_wsl_path(str(concept_img))
        wsl_out = convert_to_wsl_path(str(output_dir))
        triposr_dir = convert_to_wsl_path(str(project_root / "tools/art/TripoSR"))
        
        tripo_cmd = [
            "wsl",
            "/home/bruno/.venvs/game-ai3d/bin/python",
            f"{triposr_dir}/run.py",
            wsl_img,
            "--output-dir", wsl_out,
            "--bake-texture",
            "--texture-resolution", "2048"
        ]
        if not run_command(tripo_cmd):
            print("[Error] Falha na execução do TripoSR.")
            sys.exit(1)
            
        # Mover resultados do TripoSR para nomes canônicos
        wsl_sub_dir = f"{wsl_out}/0"
        move_cmd = f"wsl sh -c 'cp {wsl_sub_dir}/mesh.obj {wsl_out}/robot_mesh.obj && cp {wsl_sub_dir}/texture.png {wsl_out}/robot_texture.png'"
        run_command(move_cmd)

    # Etapa 2: Normalização de Eixos e Pivôs (Pés em Y=0, escala 1.4m)
    print("\n=== ETAPA 2: Normalização de Eixos, Escala e Pivô (Y-Up, Z-Forward) ===")
    norm_mesh = output_dir / "robot_normalized.obj"
    norm_script = project_root / "tools/art/normalize_mesh.py"
    
    norm_cmd = [
        "wsl",
        "/home/bruno/.venvs/game-ai3d/bin/python",
        convert_to_wsl_path(str(norm_script)),
        "--input", convert_to_wsl_path(str(raw_mesh)),
        "--output", convert_to_wsl_path(str(norm_mesh)),
        "--target-height", str(args.target_height)
    ]
    if not run_command(norm_cmd):
        print("[Error] Falha na normalização geométrica.")
        sys.exit(1)

    # Etapa 3: Separação de Texturas PBR e Máscara de Emissão URP
    print("\n=== ETAPA 3: Processamento de Texturas PBR & Máscara de Emissão ===")
    tex_script = project_root / "tools/art/process_textures.py"
    tex_cmd = [
        "wsl",
        "/home/bruno/.venvs/game-ai3d/bin/python",
        convert_to_wsl_path(str(tex_script)),
        "--input", convert_to_wsl_path(str(raw_tex)),
        "--output-dir", convert_to_wsl_path(str(output_dir))
    ]
    if not run_command(tex_cmd):
        print("[Error] Falha no processamento de texturas.")
        sys.exit(1)

    # Etapa 4: Auto-Rigging no WSL se Humanoid
    if args.rig_type == "humanoid":
        print("\n=== ETAPA 4: Auto-Rigging & Geração de Animações (Blender Headless) ===")
        auto_rig_script = project_root / "tools/art/auto_rig.py"
        fbx_output = output_dir / "robot_character.fbx"
        
        blender_cmd = [
            "wsl",
            "blender",
            "-b",
            "-P", convert_to_wsl_path(str(auto_rig_script)),
            "--",
            convert_to_wsl_path(str(norm_mesh)),
            convert_to_wsl_path(str(fbx_output)),
            str(args.target_height)
        ]
        if not run_command(blender_cmd):
            print("[Error] Falha no auto-rigging com Blender.")
            sys.exit(1)

    print("\n=======================================================")
    print("[OK] PIPELINE DE PERSONAGEM CONCLUIDO COM SUCESSO!")
    print(f"Diretorio de Entrega: {output_dir}")
    print("Arquivos gerados:")
    for f in output_dir.glob("*"):
        if f.is_file() and not f.name.endswith(".meta"):
            size_kb = f.stat().st_size / 1024
            print(f"  - {f.name} ({size_kb:.1f} KB)")
    print("=======================================================\n")

if __name__ == "__main__":
    main()
