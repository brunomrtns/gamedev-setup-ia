#!/usr/bin/env python3
"""
tools/art/auto_rig.py
Script de Auto-Rigging Headless para Blender 5.0+.

Pipeline Confiável de Rigging e Skinning para Malhas 3D Geradas por IA:
1. Importa a malha 3D normalizada (.obj) e aplica imediatamente a transformação de objeto
   (transform_apply) para unificar malha e esqueleto no sistema nativo Z-Up / -Y-Forward do Blender.
2. Detecta automaticamente os marcos anatômicos reais da malha (altura da virilha/quadril,
   largura das pernas, separação lateral dos braços e base do capacete/cabeça).
3. Constrói o esqueleto bípede (Humanoid Rig) perfeitamente alinhado em Z-Up (sem conflito de 90°).
4. Calcula pesos de deformação via Proximity Skinning com Particionamento Anatômico Estrito
   (pernas não puxam tronco/mãos, braços não puxam cabeça/pernas, 100% dos vértices ponderados).
5. Gera e baka animações coerentes: 'Idle', 'RunCycle', 'Jump' e 'Slide'.
6. Valida matematicamente a orientação do esqueleto antes de exportar o arquivo .fbx.
"""

import sys
import os
import math

try:
    import bpy
    import mathutils
    import numpy as np
except ImportError as e:
    print(f"[Error] Dependência ausente no Blender Python: {e}")
    sys.exit(1)


def parse_args():
    argv = sys.argv
    if "--" not in argv:
        return None
    args = argv[argv.index("--") + 1:]
    if len(args) < 2:
        return None
    return {
        "input_obj": args[0],
        "output_fbx": args[1],
        "target_height": float(args[2]) if len(args) > 2 else 1.4
    }


def clean_scene():
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    for block in list(bpy.data.actions):
        bpy.data.actions.remove(block)
    for block in list(bpy.data.armatures):
        bpy.data.armatures.remove(block)
    for block in list(bpy.data.meshes):
        bpy.data.meshes.remove(block)


def detect_anatomy_landmarks(verts):
    """
    Detecta automaticamente a anatomia da malha no espaço Z-Up do Blender:
    - X: Esquerda (-) / Direita (+)
    - Y: Frente (-) / Trás (+)
    - Z: Baixo (0) / Cima (Altura H)
    """
    vx = verts[:, 0]
    vy = verts[:, 1]
    vz = verts[:, 2]

    x_min, x_max = float(vx.min()), float(vx.max())
    y_min, y_max = float(vy.min()), float(vy.max())
    z_min, z_max = float(vz.min()), float(vz.max())

    width = x_max - x_min
    depth = y_max - y_min
    h_mesh = z_max - z_min
    y_mid = (y_min + y_max) * 0.5

    # 1. Detectar altura da virilha (z_hips) procurando a primeira fatia horizontal (de baixo para cima)
    # onde o vão central entre as pernas (|x| < 0.035 * width) passa a ter vértices conectados.
    z_crotch = z_min + h_mesh * 0.35  # fallback seguro
    center_tol = max(0.035, width * 0.035)
    for pct in range(15, 55, 2):
        z0 = z_min + h_mesh * ((pct - 1.5) / 100.0)
        z1 = z_min + h_mesh * ((pct + 1.5) / 100.0)
        sl = verts[(vz >= z0) & (vz <= z1)]
        if len(sl) == 0:
            continue
        center_count = int(np.sum(np.abs(sl[:, 0]) < center_tol))
        if center_count > 25:
            z_crotch = z_min + h_mesh * (pct / 100.0)
            break

    # 2. Calcular centro X de cada perna (abaixo da virilha)
    leg_verts = verts[(vz >= z_min + h_mesh * 0.05) & (vz <= z_crotch * 0.90)]
    right_leg_verts = leg_verts[leg_verts[:, 0] > 0.02]
    if len(right_leg_verts) > 20:
        x_leg = float(np.median(right_leg_verts[:, 0]))
        leg_outer_x = float(np.percentile(right_leg_verts[:, 0], 95))
    else:
        x_leg = width * 0.15
        leg_outer_x = width * 0.23

    # Limiar lateral onde terminam as pernas/tronco e começam os braços
    x_arm_split = max(leg_outer_x * 1.12, x_leg * 1.65)

    # 3. Detectar extensão vertical dos braços (vértices com |x| > x_arm_split)
    arm_verts = verts[(np.abs(vx) > x_arm_split) & (vz > z_min + h_mesh * 0.20) & (vz < z_min + h_mesh * 0.85)]
    if len(arm_verts) > 50:
        z_hand = float(np.percentile(arm_verts[:, 2], 8))
        z_shoulder = float(np.percentile(arm_verts[:, 2], 92))
        x_arm_tip = float(np.percentile(np.abs(arm_verts[:, 0]), 90))
    else:
        z_hand = z_crotch
        z_shoulder = z_min + h_mesh * 0.58
        x_arm_tip = width * 0.42

    z_hips = z_crotch
    z_knee = z_min + (z_hips - z_min) * 0.52
    z_foot = z_min + (z_hips - z_min) * 0.12

    torso_span = max(0.15, z_shoulder - z_hips)
    z_spine = z_hips + torso_span * 0.45
    z_chest = z_hips + torso_span * 0.85
    z_neck = z_shoulder + h_mesh * 0.04
    z_head = min(z_max - 0.05, z_neck + h_mesh * 0.08)
    z_elbow = (z_shoulder + z_hand) * 0.5

    landmarks = {
        "width": width,
        "depth": depth,
        "h_mesh": h_mesh,
        "z_min": z_min,
        "z_max": z_max,
        "y_mid": y_mid,
        "z_hips": z_hips,
        "z_spine": z_spine,
        "z_chest": z_chest,
        "z_neck": z_neck,
        "z_head": z_head,
        "z_knee": z_knee,
        "z_foot": z_foot,
        "z_shoulder": z_shoulder,
        "z_elbow": z_elbow,
        "z_hand": z_hand,
        "x_leg": x_leg,
        "x_arm_split": x_arm_split,
        "x_arm_tip": x_arm_tip,
    }
    print(
        f"[auto_rig] Marcos Anatômicos (Z-Up): H={h_mesh:.2f}m | "
        f"z_hips={z_hips:.2f}m | z_knee={z_knee:.2f}m | z_shoulder={z_shoulder:.2f}m | "
        f"z_neck={z_neck:.2f}m | x_leg={x_leg:.2f}m | x_arm_split={x_arm_split:.2f}m"
    )
    return landmarks


def create_humanoid_rig(mesh_obj):
    verts = np.array([v.co for v in mesh_obj.data.vertices], dtype=np.float32)
    lm = detect_anatomy_landmarks(verts)

    z_min = lm["z_min"]
    z_max = lm["z_max"]
    y_mid = lm["y_mid"]
    depth = lm["depth"]

    z_hips = lm["z_hips"]
    z_spine = lm["z_spine"]
    z_chest = lm["z_chest"]
    z_neck = lm["z_neck"]
    z_head = lm["z_head"]

    x_leg = lm["x_leg"]
    z_knee = lm["z_knee"]
    z_foot = lm["z_foot"]

    x_shoulder = lm["x_arm_split"] * 0.95
    x_elbow = (x_shoulder + lm["x_arm_tip"]) * 0.5
    x_hand = lm["x_arm_tip"]
    z_shoulder = lm["z_shoulder"]
    z_elbow = lm["z_elbow"]
    z_hand = lm["z_hand"]

    armature_data = bpy.data.armatures.new(name="Humanoid_Armature")
    armature_obj = bpy.data.objects.new("Armature", armature_data)
    bpy.context.collection.objects.link(armature_obj)

    bpy.context.view_layer.objects.active = armature_obj
    bpy.ops.object.mode_set(mode='EDIT')
    edit_bones = armature_data.edit_bones

    # Convenção Blender Z-Up, -Y-Forward:
    # Todos os ossos construídos verticalmente ao longo de Z!
    bones_spec = [
        ("Root", None, (0.0, y_mid, z_min), (0.0, y_mid, z_min + 0.10)),
        ("Hips", "Root", (0.0, y_mid, z_hips), (0.0, y_mid, z_spine)),
        ("Spine", "Hips", (0.0, y_mid, z_spine), (0.0, y_mid, z_chest)),
        ("Chest", "Spine", (0.0, y_mid, z_chest), (0.0, y_mid, z_neck)),
        ("Neck", "Chest", (0.0, y_mid, z_neck), (0.0, y_mid, z_head)),
        ("Head", "Neck", (0.0, y_mid, z_head), (0.0, y_mid, z_max)),

        # Perna Esquerda (-X)
        ("UpperLeg.L", "Hips", (-x_leg, y_mid, z_hips), (-x_leg, y_mid - 0.01, z_knee)),
        ("LowerLeg.L", "UpperLeg.L", (-x_leg, y_mid - 0.01, z_knee), (-x_leg, y_mid, z_foot)),
        ("Foot.L", "LowerLeg.L", (-x_leg, y_mid, z_foot), (-x_leg, y_mid - depth * 0.22, z_min)),

        # Perna Direita (+X)
        ("UpperLeg.R", "Hips", (x_leg, y_mid, z_hips), (x_leg, y_mid - 0.01, z_knee)),
        ("LowerLeg.R", "UpperLeg.R", (x_leg, y_mid - 0.01, z_knee), (x_leg, y_mid, z_foot)),
        ("Foot.R", "LowerLeg.R", (x_leg, y_mid, z_foot), (x_leg, y_mid - depth * 0.22, z_min)),

        # Braço Esquerdo (-X)
        ("UpperArm.L", "Chest", (-x_shoulder, y_mid, z_shoulder), (-x_elbow, y_mid + 0.01, z_elbow)),
        ("LowerArm.L", "UpperArm.L", (-x_elbow, y_mid + 0.01, z_elbow), (-x_hand, y_mid, z_hand)),
        ("Hand.L", "LowerArm.L", (-x_hand, y_mid, z_hand), (-x_hand, y_mid - 0.02, z_hand - 0.06)),

        # Braço Direito (+X)
        ("UpperArm.R", "Chest", (x_shoulder, y_mid, z_shoulder), (x_elbow, y_mid + 0.01, z_elbow)),
        ("LowerArm.R", "UpperArm.R", (x_elbow, y_mid + 0.01, z_elbow), (x_hand, y_mid, z_hand)),
        ("Hand.R", "LowerArm.R", (x_hand, y_mid, z_hand), (x_hand, y_mid - 0.02, z_hand - 0.06)),
    ]

    created_bones = {}
    for name, parent, head, tail in bones_spec:
        b = edit_bones.new(name)
        b.head = head
        b.tail = tail
        b.roll = 0.0
        if parent and parent in created_bones:
            b.parent = created_bones[parent]
        created_bones[name] = b

    bpy.ops.object.mode_set(mode='OBJECT')
    return armature_obj, bones_spec, lm


def bind_skin_robust(mesh_obj, armature_obj, bones_spec, lm):
    """
    Skinning geométrico com particionamento anatômico estrito no espaço Z-Up.
    Impede que as pernas puxem vértices das mãos/tronco e que os braços puxem a cabeça.
    """
    print("[auto_rig] Calculando pesos de skinning com isolamento anatômico (Z-Up)...")

    mesh_obj.parent = armature_obj
    mesh_obj.matrix_parent_inverse = armature_obj.matrix_world.inverted()

    arm_mod = mesh_obj.modifiers.new(name="Armature", type='ARMATURE')
    arm_mod.object = armature_obj
    arm_mod.use_vertex_groups = True

    verts = np.array([v.co for v in mesh_obj.data.vertices], dtype=np.float32)
    n_verts = len(verts)

    vx = verts[:, 0]
    vy = verts[:, 1]
    vz = verts[:, 2]

    z_hips = lm["z_hips"]
    z_neck = lm["z_neck"]
    z_shoulder = lm["z_shoulder"]
    x_arm_split = lm["x_arm_split"]

    deform_bones = [b[0] for b in bones_spec if b[0] != "Root"]
    bone_segments = []
    for name in deform_bones:
        b = armature_obj.data.bones[name]
        head = np.array(b.head_local, dtype=np.float32)
        tail = np.array(b.tail_local, dtype=np.float32)
        bone_segments.append((name, head, tail))

    n_bones = len(bone_segments)
    distances = np.zeros((n_verts, n_bones), dtype=np.float32)

    is_leg_bone = lambda n: ("Leg" in n) or ("Foot" in n)
    is_arm_bone = lambda n: ("Arm" in n) or ("Hand" in n)
    is_head_bone = lambda n: n in ("Neck", "Head")
    is_torso_bone = lambda n: n in ("Hips", "Spine", "Chest")

    for j, (name, A, B) in enumerate(bone_segments):
        AB = B - A
        AB_len_sq = float(np.dot(AB, AB)) + 1e-8
        AP = verts - A
        t = np.clip(np.sum(AP * AB, axis=1) / AB_len_sq, 0.0, 1.0)
        closest = A + t[:, np.newaxis] * AB
        d = np.linalg.norm(verts - closest, axis=1)

        # 1. Isolamento Esquerda (-X) vs Direita (+X)
        if ".L" in name:
            d = np.where(vx > 0.01, d + 10.0, d)
        elif ".R" in name:
            d = np.where(vx < -0.01, d + 10.0, d)

        # 2. Isolamento por Região Anatômica (Z é a altura!)
        if is_leg_bone(name):
            # Pernas só deformam abaixo do quadril (z <= z_hips + 0.03) e dentro da largura das pernas (|x| < x_arm_split)
            outside_leg = (vz > z_hips + 0.03) | (np.abs(vx) >= x_arm_split)
            d = np.where(outside_leg, d + 10.0, d)
        elif is_arm_bone(name):
            # Braços só deformam a região lateral (|x| >= x_arm_split * 0.88) e abaixo do pescoço (z <= z_neck + 0.02)
            outside_arm = (np.abs(vx) < x_arm_split * 0.88) | (vz > z_neck + 0.02) | (vz < z_hips - 0.10)
            d = np.where(outside_arm, d + 10.0, d)
        elif is_head_bone(name):
            # Cabeça/pescoço só deformam acima de z_shoulder
            outside_head = (vz < z_shoulder) | (np.abs(vx) > x_arm_split * 1.05)
            d = np.where(outside_head, d + 10.0, d)
        elif is_torso_bone(name):
            # Tronco foca na coluna central entre z_hips - 0.04 e z_neck + 0.04
            outside_torso = (vz < z_hips - 0.04) | (vz > z_neck + 0.04) | (np.abs(vx) >= x_arm_split * 0.95)
            d = np.where(outside_torso, d + 5.0, d)

        distances[:, j] = d

    eps = 0.015
    raw_weights = 1.0 / (distances + eps) ** 4

    # Selecionar os 2 ossos mais próximos por vértice para deformação limpa e firme em partes robóticas
    top_indices = np.argsort(-raw_weights, axis=1)[:, :2]
    top_weights = np.take_along_axis(raw_weights, top_indices, axis=1)
    sum_weights = np.sum(top_weights, axis=1, keepdims=True)
    top_weights = top_weights / np.maximum(sum_weights, 1e-8)

    bone_to_vg = {}
    for name in deform_bones:
        bone_to_vg[name] = mesh_obj.vertex_groups.new(name=name)

    for k in range(2):
        bone_idx_col = top_indices[:, k]
        w_col = top_weights[:, k]

        for b_idx, (name, _, _) in enumerate(bone_segments):
            mask = (bone_idx_col == b_idx) & (w_col > 0.02)
            if not np.any(mask):
                continue
            v_indices = np.where(mask)[0]
            v_weights = w_col[mask]

            unique_bins = np.round(v_weights * 20.0) / 20.0
            for bin_w in np.unique(unique_bins):
                if bin_w <= 0.01:
                    continue
                bin_mask = (unique_bins == bin_w)
                sub_indices = v_indices[bin_mask].tolist()
                bone_to_vg[name].add(sub_indices, float(bin_w), 'ADD')

    weighted_verts = sum(1 for v in mesh_obj.data.vertices if len(v.groups) > 0)
    print(f"[auto_rig] Skinning concluído: {weighted_verts}/{n_verts} vértices vinculados.")


def create_animation_actions(armature_obj):
    """
    Cria animações no espaço local dos ossos (Blender Z-Up, -Y-Forward):
    - Para UpperLeg / LowerLeg / UpperArm (apontam para baixo -Z):
      * rx < 0 move o membro para FRENTE (-Y no Blender / +Z na Unity)
      * rx > 0 move o membro para TRÁS (+Y no Blender / -Z na Unity)
    - Para Hips / Spine / Chest (apontam para cima +Z):
      * location.y > 0 sobe (+Z no Blender / +Y na Unity)
      * location.y < 0 abaixa (-Z no Blender / -Y na Unity)
      * rx > 0 inclina o tronco para FRENTE (-Y no Blender / +Z na Unity)
    """
    bpy.context.view_layer.objects.active = armature_obj
    bpy.ops.object.mode_set(mode='POSE')
    pose = armature_obj.pose

    def reset_pose():
        for pb in pose.bones:
            pb.rotation_mode = 'XYZ'
            pb.rotation_euler = (0.0, 0.0, 0.0)
            pb.location = (0.0, 0.0, 0.0)

    def key_bone_rot(bone_name, frame, rx_deg, ry_deg=0.0, rz_deg=0.0):
        if bone_name not in pose.bones:
            return
        pb = pose.bones[bone_name]
        pb.rotation_mode = 'XYZ'
        pb.rotation_euler = (math.radians(rx_deg), math.radians(ry_deg), math.radians(rz_deg))
        pb.keyframe_insert(data_path="rotation_euler", frame=frame)

    def key_bone_loc(bone_name, frame, loc):
        if bone_name not in pose.bones:
            return
        pb = pose.bones[bone_name]
        pb.location = loc
        pb.keyframe_insert(data_path="location", frame=frame)

    armature_obj.animation_data_create()

    # 1. Idle (30 frames)
    act_idle = bpy.data.actions.new(name="Idle")
    armature_obj.animation_data.action = act_idle
    reset_pose()
    for f in [0, 30]:
        key_bone_loc("Hips", f, (0.0, 0.0, 0.0))
        key_bone_rot("Spine", f, 2.0, 0.0, 0.0)
        key_bone_rot("UpperArm.L", f, 4.0, 0.0, 0.0)
        key_bone_rot("UpperArm.R", f, 4.0, 0.0, 0.0)
    key_bone_loc("Hips", 15, (0.0, -0.015, 0.0))
    key_bone_rot("Spine", 15, 5.0, 0.0, 0.0)
    key_bone_rot("UpperArm.L", 15, -4.0, 0.0, 0.0)
    key_bone_rot("UpperArm.R", 15, -4.0, 0.0, 0.0)

    # 2. RunCycle (24 frames, loop de corrida)
    act_run = bpy.data.actions.new(name="RunCycle")
    armature_obj.animation_data.action = act_run
    reset_pose()

    # Frame 0 & 24: Perna Esquerda à frente (-38°), Direita atrás (+32°)
    for f in [0, 24]:
        key_bone_rot("UpperLeg.L", f, -38.0)
        key_bone_rot("LowerLeg.L", f, 18.0)
        key_bone_rot("UpperLeg.R", f, 32.0)
        key_bone_rot("LowerLeg.R", f, 42.0)
        key_bone_rot("UpperArm.L", f, 28.0)
        key_bone_rot("LowerArm.L", f, -20.0)
        key_bone_rot("UpperArm.R", f, -28.0)
        key_bone_rot("LowerArm.R", f, -20.0)
        key_bone_rot("Spine", f, 6.0, -4.0, 0.0)
        key_bone_loc("Hips", f, (0.0, -0.025, 0.0))

    # Frame 6: Passagem 1 (impulso aéreo, joelho direito sobe à frente)
    key_bone_rot("UpperLeg.L", 6, 4.0)
    key_bone_rot("LowerLeg.L", 6, 12.0)
    key_bone_rot("UpperLeg.R", 6, -18.0)
    key_bone_rot("LowerLeg.R", 6, 60.0)
    key_bone_rot("UpperArm.L", 6, 0.0)
    key_bone_rot("LowerArm.L", 6, -15.0)
    key_bone_rot("UpperArm.R", 6, 0.0)
    key_bone_rot("LowerArm.R", 6, -15.0)
    key_bone_rot("Spine", 6, 6.0, 0.0, 0.0)
    key_bone_loc("Hips", 6, (0.0, 0.035, 0.0))

    # Frame 12: Perna Direita à frente (-38°), Esquerda atrás (+32°)
    key_bone_rot("UpperLeg.L", 12, 32.0)
    key_bone_rot("LowerLeg.L", 12, 42.0)
    key_bone_rot("UpperLeg.R", 12, -38.0)
    key_bone_rot("LowerLeg.R", 12, 18.0)
    key_bone_rot("UpperArm.L", 12, -28.0)
    key_bone_rot("LowerArm.L", 12, -20.0)
    key_bone_rot("UpperArm.R", 12, 28.0)
    key_bone_rot("LowerArm.R", 12, -20.0)
    key_bone_rot("Spine", 12, 6.0, 4.0, 0.0)
    key_bone_loc("Hips", 12, (0.0, -0.025, 0.0))

    # Frame 18: Passagem 2 (impulso aéreo, joelho esquerdo sobe à frente)
    key_bone_rot("UpperLeg.L", 18, -18.0)
    key_bone_rot("LowerLeg.L", 18, 60.0)
    key_bone_rot("UpperLeg.R", 18, 4.0)
    key_bone_rot("LowerLeg.R", 18, 12.0)
    key_bone_rot("UpperArm.L", 18, 0.0)
    key_bone_rot("LowerArm.L", 18, -15.0)
    key_bone_rot("UpperArm.R", 18, 0.0)
    key_bone_rot("LowerArm.R", 18, -15.0)
    key_bone_rot("Spine", 18, 6.0, 0.0, 0.0)
    key_bone_loc("Hips", 18, (0.0, 0.035, 0.0))

    # 3. Jump (24 frames)
    act_jump = bpy.data.actions.new(name="Jump")
    armature_obj.animation_data.action = act_jump
    reset_pose()

    # Frame 0: Impulso inicial
    key_bone_rot("UpperLeg.L", 0, -25.0)
    key_bone_rot("LowerLeg.L", 0, 45.0)
    key_bone_rot("UpperLeg.R", 0, -25.0)
    key_bone_rot("LowerLeg.R", 0, 45.0)
    key_bone_rot("UpperArm.L", 0, 25.0)
    key_bone_rot("UpperArm.R", 0, 25.0)
    key_bone_rot("Spine", 0, 10.0)
    key_bone_loc("Hips", 0, (0.0, -0.06, 0.0))

    # Frame 6 a 14: Postura atlética no ar (joelhos flexionados, braços erguidos)
    for f in [6, 12]:
        key_bone_rot("UpperLeg.L", f, -42.0)
        key_bone_rot("LowerLeg.L", f, 55.0)
        key_bone_rot("UpperLeg.R", f, -15.0)
        key_bone_rot("LowerLeg.R", f, 65.0)
        key_bone_rot("UpperArm.L", f, -45.0)
        key_bone_rot("UpperArm.R", f, -45.0)
        key_bone_rot("Spine", f, 8.0)
        key_bone_loc("Hips", f, (0.0, 0.08, 0.0))

    # Frame 19: Pouso
    key_bone_rot("UpperLeg.L", 19, -25.0)
    key_bone_rot("LowerLeg.L", 19, 45.0)
    key_bone_rot("UpperLeg.R", 19, -25.0)
    key_bone_rot("LowerLeg.R", 19, 45.0)
    key_bone_rot("UpperArm.L", 19, 15.0)
    key_bone_rot("UpperArm.R", 19, 15.0)
    key_bone_rot("Spine", 19, 10.0)
    key_bone_loc("Hips", 19, (0.0, -0.05, 0.0))

    # Frame 24: Retorno
    key_bone_rot("UpperLeg.L", 24, 0.0)
    key_bone_rot("LowerLeg.L", 24, 0.0)
    key_bone_rot("UpperLeg.R", 24, 0.0)
    key_bone_rot("LowerLeg.R", 24, 0.0)
    key_bone_rot("UpperArm.L", 24, 0.0)
    key_bone_rot("UpperArm.R", 24, 0.0)
    key_bone_rot("Spine", 24, 0.0)
    key_bone_loc("Hips", 24, (0.0, 0.0, 0.0))

    # 4. Slide / Agachamento Dinâmico (16 frames)
    act_slide = bpy.data.actions.new(name="Slide")
    armature_obj.animation_data.action = act_slide
    reset_pose()
    for f in [0, 8, 16]:
        key_bone_loc("Hips", f, (0.0, -0.18, 0.0))
        key_bone_rot("Spine", f, 28.0, 0.0, 0.0)
        key_bone_rot("Chest", f, 15.0, 0.0, 0.0)
        key_bone_rot("Neck", f, -18.0, 0.0, 0.0)  # mantém o olhar para frente enquanto abaixa o tronco
        key_bone_rot("UpperLeg.L", f, -48.0)
        key_bone_rot("LowerLeg.L", f, 55.0)
        key_bone_rot("UpperLeg.R", f, -20.0)
        key_bone_rot("LowerLeg.R", f, 70.0)
        key_bone_rot("UpperArm.L", f, 35.0)
        key_bone_rot("UpperArm.R", f, 35.0)

    armature_obj.animation_data.action = act_run
    bpy.ops.object.mode_set(mode='OBJECT')


def validate_rig_orientation(armature_obj):
    """
    Verificação automática antes de exportar:
    Confirma que a cabeça está acima do quadril e o quadril acima dos pés no espaço Z-Up do Blender,
    inclusive durante a avaliação da pose de corrida.
    """
    bpy.context.view_layer.update()
    pb_head = armature_obj.pose.bones["Head"]
    pb_hips = armature_obj.pose.bones["Hips"]
    pb_foot_l = armature_obj.pose.bones["Foot.L"]

    head_z = (armature_obj.matrix_world @ pb_head.head).z
    hips_z = (armature_obj.matrix_world @ pb_hips.head).z
    foot_z = (armature_obj.matrix_world @ pb_foot_l.head).z

    print(f"[auto_rig] Validação de Postura Z-Up -> Head.z={head_z:.3f}m, Hips.z={hips_z:.3f}m, Foot.L.z={foot_z:.3f}m")
    if not (head_z > hips_z > foot_z):
        raise RuntimeError(
            f"[auto_rig] ERRO DE ORIENTAÇÃO: O esqueleto não está em pé! "
            f"(Head.z={head_z:.3f}, Hips.z={hips_z:.3f}, Foot.z={foot_z:.3f})"
        )


def main():
    cfg = parse_args()
    if not cfg:
        print("[auto_rig] Uso: blender -b -P auto_rig.py -- <input_obj> <output_fbx> [target_height]")
        sys.exit(1)

    print(f"[auto_rig] Iniciando Auto-Rigging de: {cfg['input_obj']}")
    clean_scene()

    bpy.ops.wm.obj_import(filepath=cfg["input_obj"])
    mesh_objs = [obj for obj in bpy.context.selected_objects if obj.type == 'MESH']
    if not mesh_objs:
        print("[auto_rig] Erro: nenhuma malha importada do arquivo OBJ.")
        sys.exit(1)

    mesh_obj = mesh_objs[0]
    mesh_obj.name = "Character_Mesh"

    # CRÍTICO: Aplicar a rotação de importação (+90° X) na geometria para que mesh_obj e armature_obj
    # compartilhem exatamente o mesmo sistema de coordenadas Z-Up com rotação (0, 0, 0).
    bpy.ops.object.select_all(action='DESELECT')
    mesh_obj.select_set(True)
    bpy.context.view_layer.objects.active = mesh_obj
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

    print(f"[auto_rig] Malha normalizada em Z-Up: {mesh_obj.name} ({len(mesh_obj.data.vertices)} vértices, rot={tuple(mesh_obj.rotation_euler)}).")

    armature, bones_spec, lm = create_humanoid_rig(mesh_obj)
    bind_skin_robust(mesh_obj, armature, bones_spec, lm)
    create_animation_actions(armature)
    validate_rig_orientation(armature)

    os.makedirs(os.path.dirname(os.path.abspath(cfg["output_fbx"])), exist_ok=True)

    bpy.ops.object.select_all(action='DESELECT')
    mesh_obj.select_set(True)
    armature.select_set(True)
    bpy.context.view_layer.objects.active = armature

    bpy.ops.export_scene.fbx(
        filepath=cfg["output_fbx"],
        axis_forward='-Z',
        axis_up='Y'
    )
    print(f"[auto_rig] FBX exportado e validado com sucesso em: {cfg['output_fbx']}")


if __name__ == "__main__":
    main()
