#!/usr/bin/env python3
"""
tools/art/ai_generator/bvh_to_fbx_blender.py
Converte um arquivo .bvh gerado pelo MoMask (22 juntas HumanML3D, 20 fps) em
.fbx pronto para o Unity Humanoid, renomeando os ossos para nomes padrao Mecanim
(Hips, Spine, LeftUpLeg, RightUpLeg, LeftArm, Head...) via Blender 5.0 headless.
"""

import json
import os
import sys
import bpy

# Mapeamento HumanML3D/MoMask (22 juntas) -> convencao Unity Humanoid Mecanim
HUMANML3D_TO_MECANIM = {
    "Hips": "Hips",
    "Pelvis": "Hips",
    "LeftHip": "LeftUpLeg",
    "LeftUpLeg": "LeftUpLeg",
    "LeftKnee": "LeftLeg",
    "LeftLeg": "LeftLeg",
    "LeftAnkle": "LeftFoot",
    "LeftFoot": "LeftFoot",
    "LeftToe": "LeftToeBase",
    "RightHip": "RightUpLeg",
    "RightUpLeg": "RightUpLeg",
    "RightKnee": "RightLeg",
    "RightLeg": "RightLeg",
    "RightAnkle": "RightFoot",
    "RightFoot": "RightFoot",
    "RightToe": "RightToeBase",
    "Spine": "Spine",
    "Spine1": "Spine",
    "Spine2": "Chest",
    "Spine3": "UpperChest",
    "Chest": "Chest",
    "Neck": "Neck",
    "Head": "Head",
    "LeftCollar": "LeftShoulder",
    "LeftShoulder": "LeftArm",
    "LeftElbow": "LeftForeArm",
    "LeftWrist": "LeftHand",
    "RightCollar": "RightShoulder",
    "RightShoulder": "RightArm",
    "RightElbow": "RightForeArm",
    "RightWrist": "RightHand",
}


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if len(argv) < 2:
        print("Uso: blender -b -P bvh_to_fbx_blender.py -- <input.bvh> <output.fbx>")
        sys.exit(2)
    in_bvh, out_fbx = argv[0], argv[1]

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_anim.bvh(filepath=in_bvh)

    arms = [o for o in bpy.data.objects if o.type == "ARMATURE"]
    if not arms:
        print("[bvh_to_fbx] Nenhuma armature importada do BVH", file=sys.stderr)
        sys.exit(1)
    arm = arms[0]
    bpy.context.view_layer.objects.active = arm

    renamed = {}
    bpy.ops.object.mode_set(mode="EDIT")
    for b in arm.data.edit_bones:
        new_name = HUMANML3D_TO_MECANIM.get(b.name)
        if new_name and new_name != b.name:
            old = b.name
            b.name = new_name
            renamed[old] = b.name
    bpy.ops.object.mode_set(mode="OBJECT")

    os.makedirs(os.path.dirname(os.path.abspath(out_fbx)), exist_ok=True)
    bpy.ops.export_scene.fbx(
        filepath=out_fbx,
        axis_forward="-Z",
        axis_up="Y",
    )

    frame_start = int(bpy.context.scene.frame_start)
    frame_end = int(bpy.context.scene.frame_end)
    report = {
        "input_bvh": in_bvh,
        "output_fbx": out_fbx,
        "bones_count": len(arm.data.bones),
        "bones_renamed": len(renamed),
        "frames": max(1, frame_end - frame_start + 1),
        "fps": bpy.context.scene.render.fps,
    }
    print("REPORT_JSON:" + json.dumps(report, ensure_ascii=False))


if __name__ == "__main__":
    main()
