#!/usr/bin/env python3
"""
tools/art/auto_rig.py
Script de Auto-Rigging Headless para Blender 5.0+.

Pipeline de Rigging e Skinning Robusto para Malhas 3D de IA:
1. Importa a malha 3D normalizada (.obj).
2. Constrói automaticamente um esqueleto bípede padronizado (Humanoid Rig).
3. Calcula pesos reais de deformação via Proximity Skinning Geométrico Vetorizado
   (imune a malhas não-manifólio do TripoSR/NeRF, garantindo 100% dos vértices vinculados).
4. Gera e baka ações de animação essenciais para runner mobile:
   - 'RunCycle' (Loop contínuo de corrida com passadas expressivas e balanço de braços)
   - 'Jump' (Salto com contração pré-pulo, encolhimento no ar e amortecimento)
   - 'Slide' (Deslize rasteiro com corpo rebaixado e perna frontal estendida)
   - 'Idle' (Postura de prontidão com respiração suave)
5. Exporta para .fbx com SkinnedMeshRenderer, esqueleto e animações embutidas, pronto para Unity Mecanim.
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

def create_humanoid_rig(mesh_obj, height=1.4):
    verts = [v.co for v in mesh_obj.data.vertices]
    xs = [v.x for v in verts]
    ys = [v.y for v in verts]
    zs = [v.z for v in verts]
    
    width = max(xs) - min(xs)
    h_mesh = max(ys) - min(ys)
    depth = max(zs) - min(zs)
    
    y_min = min(ys)
    y_max = max(ys)
    z_mid = (max(zs) + min(zs)) * 0.5
    
    armature_data = bpy.data.armatures.new(name="Humanoid_Armature")
    armature_obj = bpy.data.objects.new("Armature", armature_data)
    bpy.context.collection.objects.link(armature_obj)
    
    bpy.context.view_layer.objects.active = armature_obj
    bpy.ops.object.mode_set(mode='EDIT')
    edit_bones = armature_data.edit_bones
    
    # Proporções anatômicas estilizadas para droide/robô
    y_hips = y_min + h_mesh * 0.48
    y_spine = y_min + h_mesh * 0.62
    y_chest = y_min + h_mesh * 0.74
    y_neck = y_min + h_mesh * 0.86
    y_head = y_min + h_mesh * 0.98
    
    x_leg = width * 0.22
    y_knee = y_min + h_mesh * 0.26
    y_foot = y_min + h_mesh * 0.04
    
    x_arm = width * 0.38
    y_shoulder = y_min + h_mesh * 0.78
    y_elbow = y_min + h_mesh * 0.56
    y_hand = y_min + h_mesh * 0.38
    
    bones_spec = [
        ("Root", None, (0, y_min, z_mid), (0, y_min + 0.1, z_mid)),
        ("Hips", "Root", (0, y_hips, z_mid), (0, y_spine, z_mid)),
        ("Spine", "Hips", (0, y_spine, z_mid), (0, y_chest, z_mid)),
        ("Chest", "Spine", (0, y_chest, z_mid), (0, y_neck, z_mid)),
        ("Neck", "Chest", (0, y_neck, z_mid), (0, y_head, z_mid)),
        ("Head", "Neck", (0, y_head, z_mid), (0, y_max, z_mid)),
        
        # Perna Esquerda
        ("UpperLeg.L", "Hips", (-x_leg, y_hips, z_mid), (-x_leg, y_knee, z_mid + 0.03)),
        ("LowerLeg.L", "UpperLeg.L", (-x_leg, y_knee, z_mid + 0.03), (-x_leg, y_foot, z_mid)),
        ("Foot.L", "LowerLeg.L", (-x_leg, y_foot, z_mid), (-x_leg, y_min, z_mid + depth * 0.3)),
        
        # Perna Direita
        ("UpperLeg.R", "Hips", (x_leg, y_hips, z_mid), (x_leg, y_knee, z_mid + 0.03)),
        ("LowerLeg.R", "UpperLeg.R", (x_leg, y_knee, z_mid + 0.03), (x_leg, y_foot, z_mid)),
        ("Foot.R", "LowerLeg.R", (x_leg, y_foot, z_mid), (x_leg, y_min, z_mid + depth * 0.3)),
        
        # Braço Esquerdo
        ("UpperArm.L", "Chest", (-x_leg * 1.3, y_shoulder, z_mid), (-x_arm, y_elbow, z_mid - 0.02)),
        ("LowerArm.L", "UpperArm.L", (-x_arm, y_elbow, z_mid - 0.02), (-x_arm, y_hand, z_mid)),
        ("Hand.L", "LowerArm.L", (-x_arm, y_hand, z_mid), (-x_arm, y_hand - 0.08, z_mid + 0.03)),
        
        # Braço Direito
        ("UpperArm.R", "Chest", (x_leg * 1.3, y_shoulder, z_mid), (x_arm, y_elbow, z_mid - 0.02)),
        ("LowerArm.R", "UpperArm.R", (x_arm, y_elbow, z_mid - 0.02), (x_arm, y_hand, z_mid)),
        ("Hand.R", "LowerArm.R", (x_arm, y_hand, z_mid), (x_arm, y_hand - 0.08, z_mid + 0.03)),
    ]
    
    created_bones = {}
    for name, parent, head, tail in bones_spec:
        b = edit_bones.new(name)
        b.head = head
        b.tail = tail
        if parent and parent in created_bones:
            b.parent = created_bones[parent]
        created_bones[name] = b
        
    bpy.ops.object.mode_set(mode='OBJECT')
    return armature_obj, bones_spec

def bind_skin_robust(mesh_obj, armature_obj, bones_spec):
    """
    Skinning geométrico por proximidade segmentar com filtragem anatômica.
    Garante que 100% dos vértices recebam pesos de deformação válidos,
    superando a limitação do ARMATURE_AUTO em malhas NeRF/AI não-manifólio.
    """
    print("[auto_rig] Iniciando Skinning Geométrico Robusto...")
    
    # 1. Vincular objeto ao esqueleto com modificador Armature
    mesh_obj.parent = armature_obj
    arm_mod = mesh_obj.modifiers.new(name="Armature", type='ARMATURE')
    arm_mod.object = armature_obj
    arm_mod.use_vertex_groups = True
    
    verts = np.array([v.co for v in mesh_obj.data.vertices])
    n_verts = len(verts)
    
    vx = verts[:, 0]
    vy = verts[:, 1]
    vz = verts[:, 2]
    
    width = vx.max() - vx.min()
    
    # Lista de ossos deformadores (Root é omitido de deformações para evitar rigidez global)
    deform_bones = [b[0] for b in bones_spec if b[0] != "Root"]
    
    bone_segments = []
    for name in deform_bones:
        b = armature_obj.data.bones[name]
        head = np.array(b.head_local)
        tail = np.array(b.tail_local)
        bone_segments.append((name, head, tail))
        
    n_bones = len(bone_segments)
    distances = np.zeros((n_verts, n_bones), dtype=np.float32)
    
    for j, (name, A, B) in enumerate(bone_segments):
        AB = B - A
        AB_len_sq = float(np.dot(AB, AB))
        AP = verts - A
        t = np.clip(np.sum(AP * AB, axis=1) / AB_len_sq, 0.0, 1.0)
        closest = A + t[:, np.newaxis] * AB
        d = np.linalg.norm(verts - closest, axis=1)
        
        # Filtragem Anatômica e Isolamento Lateral
        if ".L" in name:
            d = np.where(vx > 0.015, d + 2.5, d)
        elif ".R" in name:
            d = np.where(vx < -0.015, d + 2.5, d)
        elif name in ["Hips", "Spine", "Chest", "Neck", "Head"]:
            d = np.where(np.abs(vx) > width * 0.35, d + 0.6, d)
            
        distances[:, j] = d
        
    # Peso inversamente proporcional ao cubo da distância
    eps = 0.02
    raw_weights = 1.0 / (distances + eps)**3
    
    # Selecionar os 4 ossos com maior influência para cada vértice (Padrão de Game Engines)
    top_indices = np.argsort(-raw_weights, axis=1)[:, :4]
    top_weights = np.take_along_axis(raw_weights, top_indices, axis=1)
    
    # Normalizar para somar exatamente 1.0
    sum_weights = np.sum(top_weights, axis=1, keepdims=True)
    top_weights = top_weights / sum_weights
    
    # Criar grupos de vértices no Blender
    bone_to_vg = {}
    for name in deform_bones:
        bone_to_vg[name] = mesh_obj.vertex_groups.new(name=name)
        
    # Atribuição eficiente em lotes quantizados
    for k in range(4):
        bone_idx_col = top_indices[:, k]
        w_col = top_weights[:, k]
        
        for b_idx, (name, _, _) in enumerate(bone_segments):
            mask = (bone_idx_col == b_idx) & (w_col > 0.01)
            if not np.any(mask):
                continue
            v_indices = np.where(mask)[0]
            v_weights = w_col[mask]
            
            # Agrupar em fatias de 0.05
            unique_bins = np.round(v_weights * 20.0) / 20.0
            for bin_w in np.unique(unique_bins):
                if bin_w <= 0.01: continue
                bin_mask = (unique_bins == bin_w)
                sub_indices = v_indices[bin_mask].tolist()
                bone_to_vg[name].add(sub_indices, float(bin_w), 'ADD')
                
    weighted_verts = sum(1 for v in mesh_obj.data.vertices if len(v.groups) > 0)
    print(f"[auto_rig] Skinning concluído com sucesso: {weighted_verts}/{n_verts} vértices vinculados a ossos.")

def create_animation_actions(armature_obj):
    bpy.context.view_layer.objects.active = armature_obj
    bpy.ops.object.mode_set(mode='POSE')
    pose = armature_obj.pose
    
    def key_bone_rot(action, bone_name, frame, rx_deg, ry_deg=0, rz_deg=0):
        if bone_name not in pose.bones: return
        pb = pose.bones[bone_name]
        pb.rotation_mode = 'XYZ'
        pb.rotation_euler = (math.radians(rx_deg), math.radians(ry_deg), math.radians(rz_deg))
        pb.keyframe_insert(data_path="rotation_euler", frame=frame)
        
    def key_bone_loc(action, bone_name, frame, loc):
        if bone_name not in pose.bones: return
        pb = pose.bones[bone_name]
        pb.location = loc
        pb.keyframe_insert(data_path="location", frame=frame)

    # 1. Idle (30 frames, loop suave de respiração)
    act_idle = bpy.data.actions.new(name="Idle")
    armature_obj.animation_data_create()
    armature_obj.animation_data.action = act_idle
    for f in [0, 30]:
        key_bone_loc(act_idle, "Hips", f, (0, 0, 0))
        key_bone_rot(act_idle, "Chest", f, 0, 0, 0)
        key_bone_rot(act_idle, "UpperArm.L", f, -5, 0, 8)
        key_bone_rot(act_idle, "UpperArm.R", f, -5, 0, -8)
    key_bone_loc(act_idle, "Hips", 15, (0, -0.02, 0))
    key_bone_rot(act_idle, "Chest", 15, 3, 0, 0)
    key_bone_rot(act_idle, "UpperArm.L", 15, -2, 0, 10)
    key_bone_rot(act_idle, "UpperArm.R", 15, -2, 0, -10)

    # 2. RunCycle (24 frames, loop perfeito de corrida expressiva)
    act_run = bpy.data.actions.new(name="RunCycle")
    armature_obj.animation_data.action = act_run
    
    # Frame 0 & 24: Perna Esquerda na frente, Direita atrás
    for f in [0, 24]:
        key_bone_rot(act_run, "UpperLeg.L", f, 35)
        key_bone_rot(act_run, "LowerLeg.L", f, 20)
        key_bone_rot(act_run, "UpperLeg.R", f, -30)
        key_bone_rot(act_run, "LowerLeg.R", f, 45)
        key_bone_rot(act_run, "UpperArm.L", f, -32)
        key_bone_rot(act_run, "LowerArm.L", f, 25)
        key_bone_rot(act_run, "UpperArm.R", f, 32)
        key_bone_rot(act_run, "LowerArm.R", f, 25)
        key_bone_rot(act_run, "Chest", f, 0, 0, -5)
        key_bone_loc(act_run, "Hips", f, (0, -0.03, 0))
        
    # Frame 6: Passing pose 1 (Perna direita avança)
    key_bone_rot(act_run, "UpperLeg.L", 6, 0)
    key_bone_rot(act_run, "LowerLeg.L", 6, 15)
    key_bone_rot(act_run, "UpperLeg.R", 6, 10)
    key_bone_rot(act_run, "LowerLeg.R", 6, 65)
    key_bone_rot(act_run, "UpperArm.L", 6, 0)
    key_bone_rot(act_run, "LowerArm.L", 6, 15)
    key_bone_rot(act_run, "UpperArm.R", 6, 0)
    key_bone_rot(act_run, "LowerArm.R", 6, 15)
    key_bone_rot(act_run, "Chest", 6, 0, 0, 0)
    key_bone_loc(act_run, "Hips", 6, (0, 0.04, 0))
    
    # Frame 12: Perna Direita na frente, Esquerda atrás
    key_bone_rot(act_run, "UpperLeg.L", 12, -30)
    key_bone_rot(act_run, "LowerLeg.L", 12, 45)
    key_bone_rot(act_run, "UpperLeg.R", 12, 35)
    key_bone_rot(act_run, "LowerLeg.R", 12, 20)
    key_bone_rot(act_run, "UpperArm.L", 12, 32)
    key_bone_rot(act_run, "LowerArm.L", 12, 25)
    key_bone_rot(act_run, "UpperArm.R", 12, -32)
    key_bone_rot(act_run, "LowerArm.R", 12, 25)
    key_bone_rot(act_run, "Chest", 12, 0, 0, 5)
    key_bone_loc(act_run, "Hips", 12, (0, -0.03, 0))
    
    # Frame 18: Passing pose 2 (Perna esquerda avança)
    key_bone_rot(act_run, "UpperLeg.L", 18, 10)
    key_bone_rot(act_run, "LowerLeg.L", 18, 65)
    key_bone_rot(act_run, "UpperLeg.R", 18, 0)
    key_bone_rot(act_run, "LowerLeg.R", 18, 15)
    key_bone_rot(act_run, "UpperArm.L", 18, 0)
    key_bone_rot(act_run, "LowerArm.L", 18, 15)
    key_bone_rot(act_run, "UpperArm.R", 18, 0)
    key_bone_rot(act_run, "LowerArm.R", 18, 15)
    key_bone_rot(act_run, "Chest", 18, 0, 0, 0)
    key_bone_loc(act_run, "Hips", 18, (0, 0.04, 0))

    # 3. Jump (24 frames)
    act_jump = bpy.data.actions.new(name="Jump")
    armature_obj.animation_data.action = act_jump
    # Frame 0: Agachamento de impulso
    key_bone_rot(act_jump, "UpperLeg.L", 0, -45)
    key_bone_rot(act_jump, "LowerLeg.L", 0, 70)
    key_bone_rot(act_jump, "UpperLeg.R", 0, -45)
    key_bone_rot(act_jump, "LowerLeg.R", 0, 70)
    key_bone_rot(act_jump, "UpperArm.L", 0, 30)
    key_bone_rot(act_jump, "UpperArm.R", 0, 30)
    key_bone_loc(act_jump, "Hips", 0, (0, -0.18, 0))
    # Frame 8: Ápice no ar (joelhos recolhidos e braços abertos)
    key_bone_rot(act_jump, "UpperLeg.L", 8, 45)
    key_bone_rot(act_jump, "LowerLeg.L", 8, 60)
    key_bone_rot(act_jump, "UpperLeg.R", 8, 45)
    key_bone_rot(act_jump, "LowerLeg.R", 8, 60)
    key_bone_rot(act_jump, "UpperArm.L", 8, -60)
    key_bone_rot(act_jump, "UpperArm.R", 8, -60)
    key_bone_loc(act_jump, "Hips", 8, (0, 0.28, 0))
    # Frame 16: Pouso com amortecimento
    key_bone_rot(act_jump, "UpperLeg.L", 16, -40)
    key_bone_rot(act_jump, "LowerLeg.L", 16, 65)
    key_bone_rot(act_jump, "UpperLeg.R", 16, -40)
    key_bone_rot(act_jump, "LowerLeg.R", 16, 65)
    key_bone_rot(act_jump, "UpperArm.L", 16, 20)
    key_bone_rot(act_jump, "UpperArm.R", 16, 20)
    key_bone_loc(act_jump, "Hips", 16, (0, -0.15, 0))
    # Frame 24: Recuperação de postura
    key_bone_rot(act_jump, "UpperLeg.L", 24, 0)
    key_bone_rot(act_jump, "LowerLeg.L", 24, 0)
    key_bone_rot(act_jump, "UpperLeg.R", 24, 0)
    key_bone_rot(act_jump, "LowerLeg.R", 24, 0)
    key_bone_rot(act_jump, "UpperArm.L", 24, 0)
    key_bone_rot(act_jump, "UpperArm.R", 24, 0)
    key_bone_loc(act_jump, "Hips", 24, (0, 0, 0))

    # 4. Slide (16 frames, deslizamento rasante)
    act_slide = bpy.data.actions.new(name="Slide")
    armature_obj.animation_data.action = act_slide
    for f in [0, 8, 16]:
        key_bone_rot(act_slide, "Hips", f, 25, 0, 0)
        key_bone_rot(act_slide, "Chest", f, -30, 0, 0)
        key_bone_rot(act_slide, "UpperLeg.L", f, 65)
        key_bone_rot(act_slide, "LowerLeg.L", f, 10)
        key_bone_rot(act_slide, "UpperLeg.R", f, -45)
        key_bone_rot(act_slide, "LowerLeg.R", f, 80)
        key_bone_rot(act_slide, "UpperArm.L", f, 40)
        key_bone_rot(act_slide, "UpperArm.R", f, 40)
        key_bone_loc(act_slide, "Hips", f, (0, -0.32, 0.1))

    # Ativar RunCycle como ação padrão
    armature_obj.animation_data.action = act_run
    bpy.ops.object.mode_set(mode='OBJECT')

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
    print(f"[auto_rig] Malha carregada: {mesh_obj.name} com {len(mesh_obj.data.vertices)} vértices.")
    
    armature, bones_spec = create_humanoid_rig(mesh_obj, height=cfg["target_height"])
    print("[auto_rig] Esqueleto bípede criado com hierarquia Humanoid padrão.")
    
    bind_skin_robust(mesh_obj, armature, bones_spec)
    
    create_animation_actions(armature)
    print("[auto_rig] Animações 'Idle', 'RunCycle', 'Jump' e 'Slide' criadas e bakeadas.")
    
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
    print(f"[auto_rig] FBX exportado com sucesso em: {cfg['output_fbx']}")

if __name__ == "__main__":
    main()
