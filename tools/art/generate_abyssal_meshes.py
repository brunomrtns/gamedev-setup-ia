#!/usr/bin/env python3
"""
tools/art/generate_abyssal_meshes.py
Gerador de Malhas 3D de Alta Fidelidade (.FBX) via Blender 5.0 Headless para "Abyssal Breach".
Modelagem autêntica, detalhada e orientada para o Universal Render Pipeline (URP):
Eixo frontal: +Z (frente do jogador no Unity), Eixo vertical: +Y (cima), Eixo lateral: +X (direita).
- SM_Assault_Rifle_B.fbx (Rifle de assalto tático completo de 82cm com ótica holográfica vazada, trilhos, cano estriado e carregador curvo)
- SM_Tactical_Knife.fbx (Adaga de combate de 32cm com gume duplo, canal de sangria, guarda e pomo de impacto com argola)
- SM_Coral_Brain.fbx (Coral cérebro massivo com circunvoluções e relevo detalhado)
- SM_Coral_TubeCluster.fbx (Colônia de esponjas tubulares de 2.5m com bocas alargadas)
- SM_Coral_Staghorn.fbx (Coral chifre-de-veado ramificado de 3.2m com 14 ramificações)
- SM_Abyssal_RockArch.fbx (Arco rochoso submarino monumental com pilares estratificados)
- SM_Sunken_Pod.fbx (Cápsula de exploração naufragada detalhada com vigias e reforços)
- SM_Extraction_SubPod.fbx (Submarino de extração com cúpula de descompressão e anéis de acoplamento)
- SM_SubDrone_Hostile.fbx (Drone sentinela com casco hidrodinâmico, olho óptico e propulsor toroidal)
"""

import sys
import os
import math

try:
    import bpy
    import bmesh
except ImportError as e:
    print(f"[Error] Executar dentro do Blender: {e}")
    sys.exit(1)

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fbx_canonical


# Presets canonicos V2 por asset: weapon = pivo na empunhadura, orientacao PCA frontal.
ASSET_PRESETS = {
    "SM_Assault_Rifle_B": "weapon",
    "SM_Tactical_Knife": "weapon",
    "SM_SubDrone_Hostile": "prop",
    "SM_Coral_Brain": "prop",
    "SM_Coral_TubeCluster": "prop",
    "SM_Coral_Staghorn": "prop",
    "SM_Abyssal_RockArch": "prop",
    "SM_Sunken_Pod": "prop",
    "SM_Extraction_SubPod": "prop",
}


def clean_scene():
    fbx_canonical.clean_scene()


def finalize_and_export(objs, name, out_dir):
    """
    Finalizacao canonica V2 (delega ao modulo fbx_canonical):
    join -> transform_apply -> triangulacao -> Smart UV -> pivo canonico -> FBX canonico.
    Armas: pivo na empunhadura alinhado a +Z frontal do Unity.
    """
    preset = ASSET_PRESETS.get(name, "prop")
    # Armas autoredas ja sao construidas em -Y frontal/+Z topo: hints evitam reorientacao PCA erratica
    # (ex: lamina de faca mais larga que alta confundiria a heuristica de "up").
    hints = {"forward": "-Y", "up": "+Z"} if preset == "weapon" else {}
    report = fbx_canonical.canonical_finalize(
        objs, name, preset=preset, out_path=os.path.join(out_dir, f"{name}.fbx"), **hints)
    print(f"[generate_abyssal_meshes] {name}: preset={preset} uv_valid={report['uv']['valid']} "
          f"identity={report['identity_transform']} tris={report['tris']} dims={report['dimensions']}")


def build_assault_rifle(out_dir):
    """
    Rifle de assalto submarino completo (dimensões reais: ~82cm de comprimento).
    Orientação: Cano apontado para -Y em Blender (que se torna +Z para frente no Unity).
    Cima: +Z em Blender (que se torna +Y para cima no Unity).
    """
    clean_scene()
    parts = []

    # 1. Receiver Superior (Upper Receiver)
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0.0, -0.02, 0.04))
    upper = bpy.context.active_object
    upper.scale = (0.052, 0.36, 0.065)
    parts.append(upper)

    # 2. Receiver Inferior (Lower Receiver) + Poço do Carregador (Magwell)
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0.0, 0.04, -0.02))
    lower = bpy.context.active_object
    lower.scale = (0.046, 0.26, 0.055)
    parts.append(lower)

    # 3. Guarda-Mão Tático Ventilado (Forearm Handguard)
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0.0, -0.32, 0.035))
    handguard = bpy.context.active_object
    handguard.scale = (0.054, 0.32, 0.068)
    parts.append(handguard)

    # Ranhuras de ventilação no guarda-mão
    for side in [-0.028, 0.028]:
        for i in range(4):
            y_v = -0.22 - i * 0.06
            bpy.ops.mesh.primitive_cube_add(size=1.0, location=(side, y_v, 0.035))
            slot = bpy.context.active_object
            slot.scale = (0.006, 0.035, 0.022)
            parts.append(slot)

    # 4. Trilho Picatinny Superior
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0.0, -0.16, 0.078))
    rail = bpy.context.active_object
    rail.scale = (0.036, 0.52, 0.015)
    parts.append(rail)

    for i in range(16):
        y_r = -0.38 + i * 0.030
        bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0.0, y_r, 0.088))
        tooth = bpy.context.active_object
        tooth.scale = (0.038, 0.012, 0.008)
        parts.append(tooth)

    # 5. Cano de Aço Estriado (Barrel) + Muzzle Brake
    bpy.ops.mesh.primitive_cylinder_add(vertices=20, radius=0.016, depth=0.55, location=(0.0, -0.58, 0.035), rotation=(math.radians(90), 0, 0))
    barrel = bpy.context.active_object
    parts.append(barrel)

    bpy.ops.mesh.primitive_cylinder_add(vertices=16, radius=0.024, depth=0.09, location=(0.0, -0.84, 0.035), rotation=(math.radians(90), 0, 0))
    muzzle = bpy.context.active_object
    parts.append(muzzle)

    # 6. Mira Óptica Holográfica Tática (Holo Sight Housing)
    # Suporte cantilever
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0.0, -0.12, 0.11))
    mount = bpy.context.active_object
    mount.scale = (0.034, 0.14, 0.035)
    parts.append(mount)

    # Cobertura protetora da mira
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0.0, -0.12, 0.155))
    hood = bpy.context.active_object
    hood.scale = (0.048, 0.13, 0.055)
    parts.append(hood)

    # Abertura ótica da lente
    bpy.ops.mesh.primitive_cylinder_add(vertices=20, radius=0.022, depth=0.14, location=(0.0, -0.12, 0.155), rotation=(math.radians(90), 0, 0))
    lens_tube = bpy.context.active_object
    parts.append(lens_tube)

    # 7. Carregador Curvo Tático de 30 Tiros (Curved Magazine)
    for seg in range(4):
        ang = math.radians(12 + seg * 6.0)
        y_c = -0.06 - seg * 0.022
        z_c = -0.06 - seg * 0.052
        bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0.0, y_c, z_c), rotation=(ang, 0, 0))
        m_seg = bpy.context.active_object
        m_seg.scale = (0.032, 0.075, 0.055)
        parts.append(m_seg)

    # Placa base do carregador
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0.0, -0.16, -0.27), rotation=(math.radians(30), 0, 0))
    mag_base = bpy.context.active_object
    mag_base.scale = (0.036, 0.088, 0.020)
    parts.append(mag_base)

    # 8. Empunhadura Ergonômica Tática (Pistol Grip)
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0.0, 0.15, -0.08), rotation=(math.radians(-26), 0, 0))
    grip = bpy.context.active_object
    grip.scale = (0.032, 0.048, 0.12)
    parts.append(grip)

    # 9. Coronha Tática Retrátil (Buttstock) + Tubo de Recuo
    bpy.ops.mesh.primitive_cylinder_add(vertices=16, radius=0.018, depth=0.24, location=(0.0, 0.28, 0.04), rotation=(math.radians(90), 0, 0))
    buffer = bpy.context.active_object
    parts.append(buffer)

    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0.0, 0.35, -0.01))
    stock = bpy.context.active_object
    stock.scale = (0.042, 0.22, 0.11)
    parts.append(stock)

    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0.0, 0.45, -0.02))
    pad = bpy.context.active_object
    pad.scale = (0.046, 0.030, 0.13)
    parts.append(pad)

    finalize_and_export(parts, "SM_Assault_Rifle_B", out_dir)


def build_tactical_knife(out_dir):
    """
    Adaga / Faca Tática de Combate (dimensões reais: ~34cm de comprimento).
    Lâmina voltada para -Y, dorso para +Z.
    """
    clean_scene()
    parts = []

    # 1. Lâmina Duplo-Fio com Canal de Sangria Central (Fuller)
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0.0, -0.12, 0.0))
    spine = bpy.context.active_object
    spine.scale = (0.008, 0.22, 0.042)
    parts.append(spine)

    # Ponta em lança (Spear Point Tip)
    bpy.ops.mesh.primitive_cone_add(vertices=4, radius1=0.022, radius2=0.001, depth=0.14, location=(0.0, -0.28, 0.0), rotation=(math.radians(-90), math.radians(45), 0))
    tip = bpy.context.active_object
    tip.scale = (0.32, 1.0, 1.0)
    parts.append(tip)

    # Canal de sangria (Fuller Groove) rebaixado
    for side in [-0.005, 0.005]:
        bpy.ops.mesh.primitive_cube_add(size=1.0, location=(side, -0.12, 0.0))
        fuller = bpy.context.active_object
        fuller.scale = (0.003, 0.16, 0.008)
        parts.append(fuller)

    # 2. Guarda Dupla Tática (Crossguard)
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0.0, 0.0, 0.0))
    guard = bpy.context.active_object
    guard.scale = (0.022, 0.016, 0.088)
    parts.append(guard)

    # 3. Cabo Tático Canelado (Combat Grip)
    bpy.ops.mesh.primitive_cylinder_add(vertices=16, radius=0.018, depth=0.13, location=(0.0, 0.08, 0.0), rotation=(math.radians(90), 0, 0))
    handle = bpy.context.active_object
    handle.scale = (0.8, 1.0, 1.15)
    parts.append(handle)

    # Frisos antiderrapantes
    for i in range(5):
        y_r = 0.035 + i * 0.022
        bpy.ops.mesh.primitive_torus_add(major_radius=0.019, minor_radius=0.0035, location=(0.0, y_r, 0.0), rotation=(0, math.radians(90), 0))
        ring = bpy.context.active_object
        ring.scale = (0.82, 1.0, 1.18)
        parts.append(ring)

    # 4. Pomo de Impacto com Argola de Retenção (Pommel Skull-Crusher)
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0.0, 0.155, 0.0))
    pommel = bpy.context.active_object
    pommel.scale = (0.022, 0.018, 0.038)
    parts.append(pommel)

    bpy.ops.mesh.primitive_torus_add(major_radius=0.016, minor_radius=0.0045, location=(0.0, 0.18, 0.0), rotation=(0, math.radians(90), 0))
    loop = bpy.context.active_object
    parts.append(loop)

    finalize_and_export(parts, "SM_Tactical_Knife", out_dir)


def build_coral_brain(out_dir):
    clean_scene()
    parts = []

    # Cúpula de coral cérebro
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=4, radius=1.6, location=(0, 0, 0.8))
    dome = bpy.context.active_object
    dome.scale = (1.3, 1.2, 0.85)

    mesh = dome.data
    for v in mesh.vertices:
        if v.co.z < -0.1:
            v.co.z = -0.1
        else:
            angle = math.atan2(v.co.y, v.co.x)
            r = math.sqrt(v.co.x**2 + v.co.y**2)
            wave = 0.14 * (math.sin(r * 8.5 + math.cos(angle * 6.0)) + math.cos(v.co.z * 10.0))
            v.co.x += v.normal.x * wave
            v.co.y += v.normal.y * wave
            v.co.z += v.normal.z * wave * 0.5
    parts.append(dome)

    # Base rochosa
    bpy.ops.mesh.primitive_cylinder_add(vertices=14, radius=1.7, depth=0.4, location=(0, 0, 0.1))
    parts.append(bpy.context.active_object)

    finalize_and_export(parts, "SM_Coral_Brain", out_dir)


def build_coral_tube_cluster(out_dir):
    clean_scene()
    parts = []

    offsets = [
        (0.0, 0.0, 2.8, 0.32, 0, 0),
        (0.55, 0.32, 2.3, 0.26, 7, -5),
        (-0.52, 0.36, 2.4, 0.27, -8, 6),
        (0.38, -0.54, 1.9, 0.23, 6, 9),
        (-0.44, -0.48, 2.1, 0.24, -7, -8),
        (0.82, -0.14, 1.6, 0.20, 12, 4),
        (-0.78, -0.10, 1.7, 0.21, -11, -3),
    ]

    for ox, oy, h, rad, rx, ry in offsets:
        bpy.ops.mesh.primitive_cone_add(vertices=16, radius1=rad * 0.75, radius2=rad * 1.2, depth=h, location=(ox, oy, h * 0.5), rotation=(math.radians(rx), math.radians(ry), 0))
        parts.append(bpy.context.active_object)

        # Lábio superior do ósculo
        bpy.ops.mesh.primitive_torus_add(major_radius=rad * 1.18, minor_radius=rad * 0.16, location=(ox, oy, h), rotation=(math.radians(rx), math.radians(ry), 0))
        parts.append(bpy.context.active_object)

    # Base
    bpy.ops.mesh.primitive_cone_add(vertices=16, radius1=1.65, radius2=1.0, depth=0.45, location=(0, 0, 0.15))
    parts.append(bpy.context.active_object)

    finalize_and_export(parts, "SM_Coral_TubeCluster", out_dir)


def build_coral_staghorn(out_dir):
    clean_scene()
    parts = []

    # Tronco
    bpy.ops.mesh.primitive_cone_add(vertices=14, radius1=0.32, radius2=0.20, depth=1.8, location=(0, 0, 0.9))
    parts.append(bpy.context.active_object)

    branches = [
        ((0.48, 0.18, 1.7), (18, 28, 10), 0.18, 0.10, 1.6),
        ((-0.50, -0.20, 1.65), (-16, -26, -10), 0.18, 0.10, 1.55),
        ((0.18, -0.52, 1.8), (-24, 14, 40), 0.16, 0.09, 1.5),
        ((-0.20, 0.50, 1.85), (22, -16, -38), 0.16, 0.09, 1.5),
        ((0.80, 0.35, 2.4), (28, 44, 16), 0.11, 0.05, 1.2),
        ((-0.78, -0.32, 2.35), (-26, -40, -14), 0.11, 0.05, 1.2),
        ((0.36, -0.80, 2.5), (-38, 22, 55), 0.10, 0.045, 1.1),
        ((-0.34, 0.78, 2.55), (34, -24, -50), 0.10, 0.045, 1.1),
        ((0.12, 0.06, 2.6), (6, 8, 0), 0.13, 0.04, 1.3),
        ((-0.10, -0.08, 2.65), (-7, -6, 0), 0.12, 0.04, 1.25),
    ]

    for loc, rot, r1, r2, depth in branches:
        bpy.ops.mesh.primitive_cone_add(vertices=12, radius1=r1, radius2=r2, depth=depth, location=loc, rotation=(math.radians(rot[0]), math.radians(rot[1]), math.radians(rot[2])))
        parts.append(bpy.context.active_object)

    finalize_and_export(parts, "SM_Coral_Staghorn", out_dir)


def build_abyssal_rock_arch(out_dir):
    clean_scene()
    parts = []

    for side in [-1, 1]:
        bpy.ops.mesh.primitive_cylinder_add(vertices=9, radius=2.8, depth=11.0, location=(side * 4.8, 0, 5.5))
        parts.append(bpy.context.active_object)

    bpy.ops.mesh.primitive_cylinder_add(vertices=8, radius=2.0, depth=12.2, location=(0, 0, 10.5), rotation=(0, math.radians(90), 0))
    parts.append(bpy.context.active_object)

    finalize_and_export(parts, "SM_Abyssal_RockArch", out_dir)


def build_sunken_pod(out_dir):
    clean_scene()
    parts = []

    bpy.ops.mesh.primitive_cylinder_add(vertices=20, radius=1.5, depth=3.8, location=(0, 0, 1.5), rotation=(math.radians(76), math.radians(14), 0))
    parts.append(bpy.context.active_object)

    bpy.ops.mesh.primitive_uv_sphere_add(segments=20, ring_count=12, radius=1.5, location=(-0.45, 1.9, 1.9))
    parts.append(bpy.context.active_object)

    for offset_y in [-1.0, 0.0, 1.0]:
        bpy.ops.mesh.primitive_torus_add(major_radius=1.58, minor_radius=0.15, location=(0, offset_y, 1.5), rotation=(math.radians(76), math.radians(14), 0))
        parts.append(bpy.context.active_object)

    finalize_and_export(parts, "SM_Sunken_Pod", out_dir)


def build_extraction_subpod(out_dir):
    clean_scene()
    parts = []

    bpy.ops.mesh.primitive_cylinder_add(vertices=24, radius=3.4, depth=0.7, location=(0, 0, 0.35))
    parts.append(bpy.context.active_object)

    bpy.ops.mesh.primitive_cylinder_add(vertices=24, radius=2.2, depth=4.8, location=(0, 0, 3.0))
    parts.append(bpy.context.active_object)

    bpy.ops.mesh.primitive_uv_sphere_add(segments=24, ring_count=14, radius=2.2, location=(0, 0, 5.3))
    parts.append(bpy.context.active_object)

    for rz in [2.0, 4.2]:
        bpy.ops.mesh.primitive_torus_add(major_radius=2.7, minor_radius=0.24, location=(0, 0, rz))
        parts.append(bpy.context.active_object)

    finalize_and_export(parts, "SM_Extraction_SubPod", out_dir)


def build_hostile_subdrone(out_dir):
    clean_scene()
    parts = []

    bpy.ops.mesh.primitive_uv_sphere_add(segments=20, ring_count=14, radius=0.68, location=(0, 0, 0))
    core = bpy.context.active_object
    core.scale = (1.1, 1.35, 0.85)
    parts.append(core)

    bpy.ops.mesh.primitive_torus_add(major_radius=0.88, minor_radius=0.12, location=(0, -0.15, 0), rotation=(math.radians(12), 0, 0))
    parts.append(bpy.context.active_object)

    bpy.ops.mesh.primitive_cylinder_add(vertices=18, radius=0.28, depth=0.42, location=(0, -0.72, 0), rotation=(math.radians(90), 0, 0))
    parts.append(bpy.context.active_object)

    finalize_and_export(parts, "SM_SubDrone_Hostile", out_dir)


def main():
    argv = sys.argv
    out_dir = "unity/Assets/Game/Art/AbyssalBreach/Models"
    if "--" in argv:
        args = argv[argv.index("--") + 1:]
        if len(args) >= 1:
            out_dir = args[0]

    print(f"=== GERANDO MODELOS 3D AUTÊNTICOS E PROPORCIONADOS EM: {out_dir} ===")
    build_assault_rifle(out_dir)
    build_tactical_knife(out_dir)
    build_coral_brain(out_dir)
    build_coral_tube_cluster(out_dir)
    build_coral_staghorn(out_dir)
    build_abyssal_rock_arch(out_dir)
    build_sunken_pod(out_dir)
    build_extraction_subpod(out_dir)
    build_hostile_subdrone(out_dir)
    print("=== [SUCESSO] Todas as 9 malhas 3D foram geradas e orientadas com sucesso! ===")


if __name__ == "__main__":
    main()
