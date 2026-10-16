#!/usr/bin/env python3
"""
tools/art/auto_rig.py
Script de Auto-Rigging Headless para Blender 5.0+.

Cura o Problema A (Modelo estático sem ossos e sem animação):
1. Importa a malha 3D normalizada (.obj).
2. Constrói automaticamente um esqueleto bípede padronizado (Humanoid Rig).
3. Calcula pesos de deformação de vértices via Automatic Skinning (ARMATURE_AUTO).
4. Gera e baka ações de animação essenciais para runner mobile:
   - 'RunCycle' (Loop de corrida com passadas de pernas e balanço de braços)
   - 'Jump' (Salto com flexão, propulsão aérea e amortecimento)
   - 'Slide' (Deslize com corpo rebaixado e pernas estendidas à frente)
5. Exporta para .fbx com SkinnedMeshRenderer, esqueleto e animações embutidas, pronto para Unity Mecanim.
"""

import sys
import os
import math

try:
    import bpy
    import mathutils
except ImportError:
    print("[Error] Este script deve ser executado pelo Blender: blender -b -P auto_rig.py -- [args]")
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
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    for block in bpy.data.actions:
        bpy.data.actions.remove(block)
    for block in bpy.data.armatures:
        bpy.data.armatures.remove(block)
    for block in bpy.data.meshes:
        bpy.data.meshes.remove(block)

def create_humanoid_rig(mesh_obj, height=1.4):
    # Calcular dimensões
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
    
    # Criar armadura
    armature_data = bpy.data.armatures.new(name="Humanoid_Armature")
    armature_obj = bpy.data.objects.new("Armature", armature_data)
    bpy.context.collection.objects.link(armature_obj)
    
    bpy.context.view_layer.objects.active = armature_obj
    bpy.ops.object.mode_set(mode='EDIT')
    edit_bones = armature_data.edit_bones
    
    # Proporções anatômicas estilizadas para droide/robô
    y_hips = y_min + h_mesh * 0.50
    y_spine = y_min + h_mesh * 0.65
    y_chest = y_min + h_mesh * 0.78
    y_neck = y_min + h_mesh * 0.88
    y_head = y_min + h_mesh * 0.98
    
    x_leg = width * 0.22
    y_knee = y_min + h_mesh * 0.26
    y_foot = y_min + h_mesh * 0.04
    
    x_arm = width * 0.38
    y_shoulder = y_min + h_mesh * 0.78
    y_elbow = y_min + h_mesh * 0.56
    y_hand = y_min + h_mesh * 0.38
    
    # 1. Coluna Central
    b_root = edit_bones.new("Root")
    b_root.head = (0, y_min, z_mid)
    b_root.tail = (0, y_min + 0.1, z_mid)
    
    b_hips = edit_bones.new("Hips")
    b_hips.parent = b_root
    b_hips.head = (0, y_hips, z_mid)
    b_hips.tail = (0, y_spine, z_mid)
    
    b_spine = edit_bones.new("Spine")
    b_spine.parent = b_hips
    b_spine.head = (0, y_spine, z_mid)
    b_spine.tail = (0, y_chest, z_mid)
    
    b_chest = edit_bones.new("Chest")
    b_chest.parent = b_spine
    b_chest.head = (0, y_chest, z_mid)
    b_chest.tail = (0, y_neck, z_mid)
    
    b_neck = edit_bones.new("Neck")
    b_neck.parent = b_chest
    b_neck.head = (0, y_neck, z_mid)
    b_neck.tail = (0, y_head, z_mid)
    
    b_head = edit_bones.new("Head")
    b_head.parent = b_neck
    b_head.head = (0, y_head, z_mid)
    b_head.tail = (0, y_max, z_mid)
    
    # 2. Pernas (Left / Right)
    # Perna Esquerda
    b_upleg_l = edit_bones.new("UpperLeg.L")
    b_upleg_l.parent = b_hips
    b_upleg_l.head = (-x_leg, y_hips, z_mid)
    b_upleg_l.tail = (-x_leg, y_knee, z_mid + 0.03)
    
    b_lowleg_l = edit_bones.new("LowerLeg.L")
    b_lowleg_l.parent = b_upleg_l
    b_lowleg_l.head = (-x_leg, y_knee, z_mid + 0.03)
    b_lowleg_l.tail = (-x_leg, y_foot, z_mid)
    
    b_foot_l = edit_bones.new("Foot.L")
    b_foot_l.parent = b_lowleg_l
    b_foot_l.head = (-x_leg, y_foot, z_mid)
    b_foot_l.tail = (-x_leg, y_min, z_mid + depth * 0.3)
    
    # Perna Direita
    b_upleg_r = edit_bones.new("UpperLeg.R")
    b_upleg_r.parent = b_hips
    b_upleg_r.head = (x_leg, y_hips, z_mid)
    b_upleg_r.tail = (x_leg, y_knee, z_mid + 0.03)
    
    b_lowleg_r = edit_bones.new("LowerLeg.R")
    b_lowleg_r.parent = b_upleg_r
    b_lowleg_r.head = (x_leg, y_knee, z_mid + 0.03)
    b_lowleg_r.tail = (x_leg, y_foot, z_mid)
    
    b_foot_r = edit_bones.new("Foot.R")
    b_foot_r.parent = b_lowleg_r
    b_foot_r.head = (x_leg, y_foot, z_mid)
    b_foot_r.tail = (x_leg, y_min, z_mid + depth * 0.3)
    
    # 3. Braços (Left / Right)
    # Braço Esquerdo
    b_arm_l = edit_bones.new("UpperArm.L")
    b_arm_l.parent = b_chest
    b_arm_l.head = (-x_leg * 1.3, y_shoulder, z_mid)
    b_arm_l.tail = (-x_arm, y_elbow, z_mid - 0.02)
    
    b_forearm_l = edit_bones.new("LowerArm.L")
    b_forearm_l.parent = b_arm_l
    b_forearm_l.head = (-x_arm, y_elbow, z_mid - 0.02)
    b_forearm_l.tail = (-x_arm, y_hand, z_mid)
    
    b_hand_l = edit_bones.new("Hand.L")
    b_hand_l.parent = b_forearm_l
    b_hand_l.head = (-x_arm, y_hand, z_mid)
    b_hand_l.tail = (-x_arm, y_hand - 0.08, z_mid + 0.03)
    
    # Braço Direito
    b_arm_r = edit_bones.new("UpperArm.R")
    b_arm_r.parent = b_chest
    b_arm_r.head = (x_leg * 1.3, y_shoulder, z_mid)
    b_arm_r.tail = (x_arm, y_elbow, z_mid - 0.02)
    
    b_forearm_r = edit_bones.new("LowerArm.R")
    b_forearm_r.parent = b_arm_r
    b_forearm_r.head = (x_arm, y_elbow, z_mid - 0.02)
    b_forearm_r.tail = (x_arm, y_hand, z_mid)
    
    b_hand_r = edit_bones.new("Hand.R")
    b_hand_r.parent = b_forearm_r
    b_hand_r.head = (x_arm, y_hand, z_mid)
    b_hand_r.tail = (x_arm, y_hand - 0.08, z_mid + 0.03)
    
    bpy.ops.object.mode_set(mode='OBJECT')
    return armature_obj

def bind_skin(mesh_obj, armature_obj):
    bpy.ops.object.select_all(action='DESELECT')
    mesh_obj.select_set(True)
    armature_obj.select_set(True)
    bpy.context.view_layer.objects.active = armature_obj
    
    # Vinculação com Pesos Automáticos
    try:
        bpy.ops.object.parent_set(type='ARMATURE_AUTO')
        print("[auto_rig] Deformação de malha vinculada com pesos automáticos (ARMATURE_AUTO).")
    except Exception as e:
        print(f"[auto_rig] Aviso em ARMATURE_AUTO ({e}), tentando ARMATURE_ENVELOPE...")
        bpy.ops.object.parent_set(type='ARMATURE_ENVELOPE')

def create_animation_actions(armature_obj):
    bpy.context.view_layer.objects.active = armature_obj
    bpy.ops.object.mode_set(mode='POSE')
    pose = armature_obj.pose
    
    # Helper para rotacionar ossos no eixo local X (pitch/flexão)
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

    # 1. RunCycle (24 frames, loop perfeito)
    act_run = bpy.data.actions.new(name="RunCycle")
    armature_obj.animation_data_create()
    armature_obj.animation_data.action = act_run
    
    # Frame 0 & 24: Perna Esquerda na frente, Direita atrás
    for f in [0, 24]:
        key_bone_rot(act_run, "UpperLeg.L", f, -32)
        key_bone_rot(act_run, "LowerLeg.L", f, 18)
        key_bone_rot(act_run, "UpperLeg.R", f, 28)
        key_bone_rot(act_run, "LowerLeg.R", f, 40)
        key_bone_rot(act_run, "UpperArm.L", f, 25)
        key_bone_rot(act_run, "UpperArm.R", f, -25)
        key_bone_rot(act_run, "Chest", f, 0, 0, -4)
        key_bone_loc(act_run, "Hips", f, (0, 0, 0))
        
    # Frame 6: Passing pose 1
    key_bone_rot(act_run, "UpperLeg.L", 6, -5)
    key_bone_rot(act_run, "LowerLeg.L", 6, 35)
    key_bone_rot(act_run, "UpperLeg.R", 6, 5)
    key_bone_rot(act_run, "LowerLeg.R", 6, 15)
    key_bone_rot(act_run, "UpperArm.L", 6, 0)
    key_bone_rot(act_run, "UpperArm.R", 6, 0)
    key_bone_loc(act_run, "Hips", 6, (0, 0.04, 0))
    
    # Frame 12: Perna Direita na frente, Esquerda atrás
    key_bone_rot(act_run, "UpperLeg.L", 12, 28)
    key_bone_rot(act_run, "LowerLeg.L", 12, 40)
    key_bone_rot(act_run, "UpperLeg.R", 12, -32)
    key_bone_rot(act_run, "LowerLeg.R", 12, 18)
    key_bone_rot(act_run, "UpperArm.L", 12, -25)
    key_bone_rot(act_run, "UpperArm.R", 12, 25)
    key_bone_rot(act_run, "Chest", 12, 0, 0, 4)
    key_bone_loc(act_run, "Hips", 12, (0, 0, 0))
    
    # Frame 18: Passing pose 2
    key_bone_rot(act_run, "UpperLeg.L", 18, 5)
    key_bone_rot(act_run, "LowerLeg.L", 18, 15)
    key_bone_rot(act_run, "UpperLeg.R", 18, -5)
    key_bone_rot(act_run, "LowerLeg.R", 18, 35)
    key_bone_rot(act_run, "UpperArm.L", 18, 0)
    key_bone_rot(act_run, "UpperArm.R", 18, 0)
    key_bone_loc(act_run, "Hips", 18, (0, 0.04, 0))
    
    # 2. Jump (24 frames)
    act_jump = bpy.data.actions.new(name="Jump")
    armature_obj.animation_data.action = act_jump
    # 0: Crouch
    key_bone_rot(act_jump, "UpperLeg.L", 0, -40)
    key_bone_rot(act_jump, "LowerLeg.L", 0, 60)
    key_bone_rot(act_jump, "UpperLeg.R", 0, -40)
    key_bone_rot(act_jump, "LowerLeg.R", 0, 60)
    key_bone_loc(act_jump, "Hips", 0, (0, -0.15, 0))
    # 8: Apex / Air
    key_bone_rot(act_jump, "UpperLeg.L", 8, -10)
    key_bone_rot(act_jump, "LowerLeg.L", 8, 20)
    key_bone_rot(act_jump, "UpperLeg.R", 8, -10)
    key_bone_rot(act_jump, "LowerLeg.R", 8, 20)
    key_bone_rot(act_jump, "UpperArm.L", 8, -50)
    key_bone_rot(act_jump, "UpperArm.R", 8, -50)
    key_bone_loc(act_jump, "Hips", 8, (0, 0.25, 0))
    # 18: Landing
    key_bone_rot(act_jump, "UpperLeg.L", 18, -35)
    key_bone_rot(act_jump, "LowerLeg.L", 18, 55)
    key_bone_rot(act_jump, "UpperLeg.R", 18, -35)
    key_bone_rot(act_jump, "LowerLeg.R", 18, 55)
    key_bone_loc(act_jump, "Hips", 18, (0, -0.12, 0))
    # 24: Recovery
    key_bone_rot(act_jump, "UpperLeg.L", 24, 0)
    key_bone_rot(act_jump, "LowerLeg.L", 24, 0)
    key_bone_rot(act_jump, "UpperLeg.R", 24, 0)
    key_bone_rot(act_jump, "LowerLeg.R", 24, 0)
    key_bone_rot(act_jump, "UpperArm.L", 24, 0)
    key_bone_rot(act_jump, "UpperArm.R", 24, 0)
    key_bone_loc(act_jump, "Hips", 24, (0, 0, 0))
    
    # 3. Slide (16 frames)
    act_slide = bpy.data.actions.new(name="Slide")
    armature_obj.animation_data.action = act_slide
    for f in [0, 8, 16]:
        key_bone_rot(act_slide, "Hips", f, 20, 0, 0)
        key_bone_rot(act_slide, "UpperLeg.L", f, -70)
        key_bone_rot(act_slide, "LowerLeg.L", f, 15)
        key_bone_rot(act_slide, "UpperLeg.R", f, -55)
        key_bone_rot(act_slide, "LowerLeg.R", f, 30)
        key_bone_rot(act_slide, "Chest", f, -25)
        key_bone_rot(act_slide, "UpperArm.L", f, 40)
        key_bone_rot(act_slide, "UpperArm.R", f, 40)
        key_bone_loc(act_slide, "Hips", f, (0, -0.28, 0.1))

    # Deixar RunCycle como ativa por padrão
    armature_obj.animation_data.action = act_run
    bpy.ops.object.mode_set(mode='OBJECT')

def main():
    cfg = parse_args()
    if not cfg:
        print("[auto_rig] Uso: blender -b -P auto_rig.py -- <input_obj> <output_fbx> [target_height]")
        sys.exit(1)
        
    print(f"[auto_rig] Iniciando Auto-Rigging de: {cfg['input_obj']}")
    clean_scene()
    
    # Importar OBJ
    bpy.ops.wm.obj_import(filepath=cfg["input_obj"])
    mesh_objs = [obj for obj in bpy.context.selected_objects if obj.type == 'MESH']
    if not mesh_objs:
        print("[auto_rig] Erro: nenhuma malha importada do arquivo OBJ.")
        sys.exit(1)
        
    mesh_obj = mesh_objs[0]
    mesh_obj.name = "Character_Mesh"
    print(f"[auto_rig] Malha carregada: {mesh_obj.name} com {len(mesh_obj.data.vertices)} vértices.")
    
    # Criar esqueleto
    armature = create_humanoid_rig(mesh_obj, height=cfg["target_height"])
    print("[auto_rig] Esqueleto bípede criado com hierarquia Humanoid padrão.")
    
    # Fazer skinning (pesos automáticos)
    bind_skin(mesh_obj, armature)
    
    # Gerar animações essenciais
    create_animation_actions(armature)
    print("[auto_rig] Animações 'RunCycle', 'Jump' e 'Slide' criadas e bakeadas.")
    
    # Exportar FBX para Unity
    os.makedirs(os.path.dirname(os.path.abspath(cfg["output_fbx"])), exist_ok=True)
    
    # Selecionar malha e esqueleto
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
