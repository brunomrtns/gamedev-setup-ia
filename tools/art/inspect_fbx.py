#!/usr/bin/env python3
"""
tools/art/inspect_fbx.py
Inspetor deterministico de FBX binarios via Blender headless (WSL).
Uso: wsl blender -b -P tools/art/inspect_fbx.py -- manifest.json
Manifesto: {"files": ["/mnt/c/.../a.fbx", ...]}
Saida: uma linha REPORT_JSON:<json> por arquivo + INSPECT_DONE.
"""

import sys
import os
import json

import bpy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fbx_canonical  # noqa: E402


def inspect(path):
    fbx_canonical.clean_scene()
    fbx_canonical.ensure_fbx_import()
    bpy.ops.import_scene.fbx(filepath=path)
    meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    if not meshes:
        return {"file": path, "status": "error", "error": "sem malhas"}

    uv_valid_all = True
    uv_min_area = 1e9
    identity_all = True
    total_tris = 0
    ngons = 0
    mat_slots = 0
    for o in meshes:
        rep = fbx_canonical.uv_report(o)
        uv_valid_all = uv_valid_all and rep["valid"]
        uv_min_area = min(uv_min_area, rep.get("area", 0.0))
        total_tris += sum(1 for p in o.data.polygons if len(p.vertices) == 3)
        ngons += sum(1 for p in o.data.polygons if len(p.vertices) > 4)
        mat_slots += len(o.material_slots)
        loc, rot, scl = o.location, o.rotation_euler, o.scale
        identity = (abs(loc.x) < 1e-4 and abs(loc.y) < 1e-4 and abs(loc.z) < 1e-4
                    and abs(rot.x) < 1e-3 and abs(rot.y) < 1e-3 and abs(rot.z) < 1e-3
                    and abs(scl.x - 1) < 1e-3 and abs(scl.y - 1) < 1e-3 and abs(scl.z - 1) < 1e-3)
        identity_all = identity_all and identity

    return {
        "file": path, "status": "ok",
        "mesh_count": len(meshes), "tris": total_tris, "ngons": ngons,
        "uv_valid": uv_valid_all, "uv_min_area": round(uv_min_area, 5),
        "identity_transform": identity_all,
        "material_slots": mat_slots,
    }


def main():
    argv = sys.argv[sys.argv.index("--") + 1:]
    with open(argv[0], "r", encoding="utf-8") as f:
        files = json.load(f)["files"]

    for path in files:
        try:
            rep = inspect(path)
        except Exception as e:
            rep = {"file": path, "status": "error", "error": str(e)}
        print("REPORT_JSON:" + json.dumps(rep, ensure_ascii=False))
    print("INSPECT_DONE")
    sys.exit(0)


if __name__ == "__main__":
    main()
