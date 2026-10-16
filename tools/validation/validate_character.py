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


def check_anatomical_separation(obj_path: Path, target_height: float):
    """
    Analisa seções transversais da malha 3D normalizada (Y-Up) para verificar:
    1. Separação entre perna esquerda e perna direita (fenda central em X ≈ 0 na altura das canelas/coxas).
    2. Envergadura lateral de braços (A-pose / T-pose) vs largura da cintura.
    """
    verts = []
    with obj_path.open("r", encoding="utf-8", errors="ignore") as f:
        for line in f:
            if line.startswith("v "):
                parts = line.strip().split()
                if len(parts) >= 4:
                    verts.append((float(parts[1]), float(parts[2]), float(parts[3])))

    if not verts:
        return False, "Sem vértices para análise anatômica."

    # 1. Verificar separação das pernas entre 12% e 28% da altura
    leg_slice = [v for v in verts if 0.12 * target_height <= v[1] <= 0.28 * target_height]
    if not leg_slice:
        return False, "Região inferior das pernas (12%-28% da altura) está vazia."

    left_leg_pts = [v for v in leg_slice if v[0] > 0.04]
    right_leg_pts = [v for v in leg_slice if v[0] < -0.04]
    center_gap_pts = [v for v in leg_slice if abs(v[0]) <= 0.025]

    has_two_legs = len(left_leg_pts) > 20 and len(right_leg_pts) > 20
    gap_ratio = len(center_gap_pts) / max(1, len(leg_slice))

    # 2. Verificar envergadura superior (braços/ombros entre 45% e 65% da altura)
    upper_slice = [v for v in verts if 0.45 * target_height <= v[1] <= 0.65 * target_height]
    arm_span = (max(v[0] for v in upper_slice) - min(v[0] for v in upper_slice)) if upper_slice else 0.0

    msg = (
        f"Anatomia -> Pernas separadas={has_two_legs} "
        f"(L={len(left_leg_pts)}, R={len(right_leg_pts)}, GapRatio={gap_ratio:.2f}) | "
        f"Envergadura superior={arm_span:.3f}m"
    )
    if not has_two_legs or gap_ratio > 0.35:
        return False, f"Pernas fundidas ou sem separação clara para rigging bípede ({msg})"

    return True, msg


def validate_character_dir(
    char_dir: Path,
    target_height: float = 1.4,
    rig_type: str = "humanoid",
    expected_clips=("RunCycle", "Jump", "Slide")
) -> bool:
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
                anat_ok, anat_msg = check_anatomical_separation(norm_obj, target_height)
                print(f"[validate_character] {anat_msg}")
                if not anat_ok:
                    errors.append(anat_msg)

    if rig_type == "humanoid":
        if not fbx_file.exists() or fbx_file.stat().st_size < 50 * 1024:
            errors.append("Arquivo robot_character.fbx ausente ou corrompido (< 50 KB)")
        else:
            clips = check_fbx_animations(fbx_file, expected_clips=expected_clips)
            print(f"[validate_character] Clipes detectados no FBX: {clips}")
            for clip_name, present in clips.items():
                if not present:
                    errors.append(f"Clipe de animação obrigatório ausente no FBX: {clip_name}")

    if errors:
        print("[validate_character] REPROVADO:")
        for e in errors:
            print(f"  - {e}")
        return False

    print("[validate_character] APROVADO: Todos os critérios geométricos, anatômicos, de textura e animação foram atendidos.")
    return True


def main():
    parser = argparse.ArgumentParser(description="Validador de Personagem 3D do Estúdio")
    parser.add_argument("--dir", default="unity/Assets/Game/Art/Characters/RobotAI", help="Diretório do personagem")
    parser.add_argument("--target-height", type=float, default=1.4, help="Altura esperada em metros")
    parser.add_argument("--rig-type", default="humanoid", choices=["humanoid", "drone", "vehicle"])
    parser.add_argument("--clips", default="RunCycle,Jump,Slide", help="Lista de clipes esperados separados por vírgula")
    args = parser.parse_args()

    expected_clips = tuple(c.strip() for c in args.clips.split(",") if c.strip())
    root = Path(__file__).resolve().parent.parent.parent
    char_dir = root / args.dir
    ok = validate_character_dir(
        char_dir,
        target_height=args.target_height,
        rig_type=args.rig_type,
        expected_clips=expected_clips
    )
    sys.exit(0 if ok else 1)


if __name__ == "__main__":
    main()
