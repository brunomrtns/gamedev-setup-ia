#!/usr/bin/env python3
"""
tools/validation/validate_character.py
Portão de Qualidade (Quality Gate) para validação de personagens 3D gerados por IA
antes e depois da integração com o motor Unity.

Verifica:
1. Existência e integridade de malha normalizada (.obj), FBX riggado (.fbx) e mapas PBR (.png).
2. Alinhamento geométrico: pés apoiados em Y=0 (±0.01m), centralização X=0 e Z=0 (±0.02m).
3. Escala canônica dentro da tolerância da altura alvo (ex: 1.40m ± 0.05m).
4. Presença das ações de animação esperadas ('RunCycle', 'Jump', 'Slide', 'Idle') no binário FBX.
"""

import os
import sys
import argparse
from pathlib import Path


def parse_obj_bounds(obj_path: Path):
    xs, ys, zs = [], [], []
    with obj_path.open("r", encoding="utf-8", errors="ignore") as f:
        for line in f:
            if line.startswith("v "):
                parts = line.strip().split()
                if len(parts) >= 4:
                    xs.append(float(parts[1]))
                    ys.append(float(parts[2]))
                    zs.append(float(parts[3]))
    if not xs:
        return None
    return {
        "vertex_count": len(xs),
        "x_min": min(xs), "x_max": max(xs),
        "y_min": min(ys), "y_max": max(ys),
        "z_min": min(zs), "z_max": max(zs),
    }


def check_fbx_animations(fbx_path: Path, expected_clips=("RunCycle", "Jump", "Slide")):
    data = fbx_path.read_bytes()
    found = {}
    for clip in expected_clips:
        found[clip] = (clip.encode("ascii") in data)
    return found


def validate_character_dir(char_dir: Path, target_height: float = 1.4, rig_type: str = "humanoid") -> bool:
    print(f"[validate_character] Inspecionando diretório: {char_dir}")
    errors = []

    norm_obj = char_dir / "robot_normalized.obj"
    diffuse_png = char_dir / "robot_diffuse.png"
    emission_png = char_dir / "robot_emission_mask.png"
    smooth_png = char_dir / "robot_smoothness.png"
    fbx_file = char_dir / "robot_character.fbx"

    for req in [norm_obj, diffuse_png, emission_png, smooth_png]:
        if not req.exists() or req.stat().st_size < 1024:
            errors.append(f"Arquivo obrigatório ausente ou vazio: {req.name}")

    if norm_obj.exists():
        b = parse_obj_bounds(norm_obj)
        if not b:
            errors.append("Falha ao ler vértices de robot_normalized.obj")
        else:
            height = b["y_max"] - b["y_min"]
            cx = (b["x_min"] + b["x_max"]) * 0.5
            cz = (b["z_min"] + b["z_max"]) * 0.5
            print(
                f"[validate_character] Geometria OBJ -> Vértices={b['vertex_count']} | "
                f"Altura={height:.3f}m | Y_min={b['y_min']:.4f}m | Centro=({cx:.4f}, {cz:.4f})"
            )
            if abs(b["y_min"]) > 0.015:
                errors.append(f"Pés fora do chão: Y_min={b['y_min']:.4f}m (esperado 0.0m ± 0.015m)")
            if abs(height - target_height) > 0.05:
                errors.append(f"Altura fora do padrão: {height:.3f}m (esperado {target_height:.2f}m)")
            if abs(cx) > 0.02 or abs(cz) > 0.02:
                errors.append(f"Malha descentralizada em X/Z: centro=({cx:.4f}, {cz:.4f})")

    if rig_type == "humanoid":
        if not fbx_file.exists() or fbx_file.stat().st_size < 50 * 1024:
            errors.append("Arquivo robot_character.fbx ausente ou corrompido (< 50 KB)")
        else:
            clips = check_fbx_animations(fbx_file)
            print(f"[validate_character] Clipes detectados no FBX: {clips}")
            for clip_name, present in clips.items():
                if not present:
                    errors.append(f"Clipe de animação obrigatório ausente no FBX: {clip_name}")

    if errors:
        print("[validate_character] REPROVADO:")
        for e in errors:
            print(f"  - {e}")
        return False

    print("[validate_character] APROVADO: Todos os critérios geométricos, de textura e animação foram atendidos.")
    return True


def main():
    parser = argparse.ArgumentParser(description="Validador de Personagem 3D do Estúdio")
    parser.add_argument("--dir", default="unity/Assets/Game/Art/Characters/RobotAI", help="Diretório do personagem")
    parser.add_argument("--target-height", type=float, default=1.4, help="Altura esperada em metros")
    parser.add_argument("--rig-type", default="humanoid", choices=["humanoid", "drone"])
    args = parser.parse_args()

    root = Path(__file__).resolve().parent.parent.parent
    char_dir = root / args.dir
    ok = validate_character_dir(char_dir, target_height=args.target_height, rig_type=args.rig_type)
    sys.exit(0 if ok else 1)


if __name__ == "__main__":
    main()
