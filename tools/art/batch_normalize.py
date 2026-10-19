#!/usr/bin/env python3
"""
tools/art/batch_normalize.py
Normalizador em lote (UMA unica sessao Blender) para kits modulares e colecoes.

Uso:
  wsl blender -b -P tools/art/batch_normalize.py -- /mnt/c/.../manifest.json

Manifesto JSON: [ {"input": "...", "output": "...", "preset": "kit|prop|weapon|raw",
                    "forward": "-Z", "up": "+Y", "force_uv": false}, ... ]

Preset "kit": triangula, garante UV0 e export FBX canonico, PRESERVANDO o pivo
original do autor (essencial para snapping de pecas modulares).

Imprime uma linha REPORT_JSON:<json> por arquivo para auditoria deterministica.
"""

import sys
import os
import json

import bpy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fbx_canonical  # noqa: E402


def import_any(path):
    ext = os.path.splitext(path)[1].lower()
    if ext == ".fbx":
        fbx_canonical.ensure_fbx_import()
        bpy.ops.import_scene.fbx(filepath=path)
    elif ext == ".obj":
        bpy.ops.wm.obj_import(filepath=path)
    elif ext in (".glb", ".gltf"):
        fbx_canonical._ensure_addon("io_scene_gltf2")
        bpy.ops.import_scene.gltf(filepath=path)
    else:
        raise ValueError(f"Formato nao suportado: {ext}")
    meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    if not meshes:
        raise RuntimeError(f"Nenhum MESH em {path}")
    return meshes


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if not argv:
        print("[batch_normalize] Uso: blender -b -P batch_normalize.py -- manifest.json")
        sys.exit(2)

    with open(argv[0], "r", encoding="utf-8") as f:
        entries = json.load(f)

    ok, fail = 0, 0
    for entry in entries:
        try:
            fbx_canonical.clean_scene()
            meshes = import_any(entry["input"])
            name = os.path.splitext(os.path.basename(entry["input"]))[0]
            report = fbx_canonical.canonical_finalize(
                meshes, name,
                preset=entry.get("preset", "raw"),
                out_path=entry.get("output", entry["input"]),
                forward_hint=entry.get("forward"),
                up_hint=entry.get("up"),
                force_uv=entry.get("force_uv", False))
            report["status"] = "ok"
            ok += 1
            print("REPORT_JSON:" + json.dumps(report, ensure_ascii=False))
        except Exception as e:
            fail += 1
            print("REPORT_JSON:" + json.dumps(
                {"input": entry["input"], "status": "error", "error": str(e)},
                ensure_ascii=False))

    print(f"BATCH_DONE ok={ok} fail={fail}")
    sys.exit(0 if fail == 0 else 1)


if __name__ == "__main__":
    main()
