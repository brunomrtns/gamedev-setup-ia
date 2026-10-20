#!/usr/bin/env python3
"""
tools/art/inspect_character_fbx.py
Inventário de FBX de personagem (rodado DENTRO do Blender headless WSL).

Para cada FBX passado: importa numa cena limpa e reporta em JSON:
  - objetos por tipo
  - armatures: nome, contagem de ossos, hash dos nomes dos ossos,
    head/tail world de ossos-chave (para comparar armatures duplicadas)
  - meshes: nome, vértices, materiais (slots), vertex groups, modificador
    armature → armature alvo
Uso:
  wsl blender -b -P tools/art/inspect_character_fbx.py -- <fbx1> [fbx2 ...] --json <out.json>
"""
import sys, os, json, hashlib

import bpy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fbx_canonical


def matrix_list(m):
    return [[round(v, 5) for v in row] for row in m]


def bone_signature(arm_obj):
    """Assinatura dos ossos: nome -> head/tail world (edit_bones não disponível
    fora de edit mode; usar arm.bones com matrix_world do objeto)."""
    arm = arm_obj.data
    sig = {}
    for b in arm.bones:
        hw = arm_obj.matrix_world @ b.head_local
        tw = arm_obj.matrix_world @ b.tail_local
        sig[b.name] = {
            "head": [round(v, 5) for v in hw],
            "tail": [round(v, 5) for v in tw],
        }
    return sig


def inspect(path):
    fbx_canonical.clean_scene()
    try:
        bpy.ops.import_scene.fbx(filepath=path)
    except Exception as e:
        return {"file": path, "error": str(e)}

    rep = {"file": path, "objects": [], "armatures": {}, "meshes": []}
    for obj in bpy.context.scene.objects:
        rep["objects"].append({"name": obj.name, "type": obj.type,
                               "parent": obj.parent.name if obj.parent else None})
        if obj.type == 'ARMATURE':
            rep["armatures"][obj.name] = {
                "bone_count": len(obj.data.bones),
                "bones": sorted(b.name for b in obj.data.bones),
                "matrix_world": matrix_list(obj.matrix_world),
                "signature": bone_signature(obj),
            }
        elif obj.type == 'MESH':
            arm_mods = [m.object.name if m.object else None
                        for m in obj.modifiers if m.type == 'ARMATURE']
            rep["meshes"].append({
                "name": obj.name,
                "verts": len(obj.data.vertices),
                "materials": [s.material.name if s.material else None
                              for s in obj.material_slots],
                "vgroup_count": len(obj.vertex_groups),
                "armature_mods": arm_mods,
                "matrix_world": matrix_list(obj.matrix_world),
            })
    return rep


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    out_json = None
    files = []
    i = 0
    while i < len(argv):
        if argv[i] == "--json" and i + 1 < len(argv):
            out_json = argv[i + 1]; i += 2
        else:
            files.append(argv[i]); i += 1

    fbx_canonical.ensure_fbx_import()
    results = [inspect(f) for f in files]
    txt = json.dumps(results, indent=1)
    if out_json:
        os.makedirs(os.path.dirname(out_json), exist_ok=True)
        with open(out_json, "w") as fh:
            fh.write(txt)
    print("INSPECT_JSON_BEGIN")
    print(txt[:6000])
    print("INSPECT_JSON_END")


main()
