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
import math

import bpy
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fbx_canonical  # noqa: E402


def _poly_uv_area(me, uv_layer, poly):
    pts = [uv_layer[li].uv for li in poly.loop_indices]
    if len(pts) < 3:
        return 0.0
    area = 0.0
    p0 = pts[0]
    for i in range(1, len(pts) - 1):
        a = pts[i] - p0
        b = pts[i + 1] - p0
        area += abs(a.x * b.y - a.y * b.x) * 0.5
    return area


def inspect(path):
    fbx_canonical.clean_scene()
    ext = os.path.splitext(path)[1].lower()
    if ext in (".glb", ".gltf"):
        fbx_canonical._ensure_addon("io_scene_gltf2")
        bpy.ops.import_scene.gltf(filepath=path)
    else:
        fbx_canonical.ensure_fbx_import()
        bpy.ops.import_scene.fbx(filepath=path)
    meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    if not meshes:
        return {"file": path, "status": "error", "error": "sem malhas"}

    uv_valid_all = True
    uv_min_area = 1e9
    uv_inside01_min = 1.0
    total_uv_area = 0.0
    total_world_area = 0.0
    identity_all = True
    total_tris = 0
    ngons = 0
    mat_slots = 0

    all_world_verts = []
    for o in meshes:
        rep = fbx_canonical.uv_report(o)
        uv_valid_all = uv_valid_all and rep["valid"]
        uv_min_area = min(uv_min_area, rep.get("area", 0.0))
        uv_inside01_min = min(uv_inside01_min, rep.get("inside01", 0.0))
        total_uv_area += rep.get("area", 0.0)

        me = o.data
        mw = o.matrix_world
        for v in me.vertices:
            all_world_verts.append(mw @ v.co)

        for p in me.polygons:
            n_v = len(p.vertices)
            if n_v == 3:
                total_tris += 1
            elif n_v == 4:
                total_tris += 2
            elif n_v > 4:
                ngons += 1
                total_tris += (n_v - 2)
            # World-space polygon area approximation using object scale
            sx, sy, sz = o.scale
            scale_factor = (abs(sx) * abs(sy) * abs(sz)) ** (1.0 / 3.0) if (sx and sy and sz) else 1.0
            total_world_area += p.area * (scale_factor ** 2)

        mat_slots += len(o.material_slots)
        loc, rot, scl = o.location, o.rotation_euler, o.scale
        identity = (abs(loc.x) < 1e-4 and abs(loc.y) < 1e-4 and abs(loc.z) < 1e-4
                    and abs(rot.x) < 1e-3 and abs(rot.y) < 1e-3 and abs(rot.z) < 1e-3
                    and abs(scl.x - 1) < 1e-3 and abs(scl.y - 1) < 1e-3 and abs(scl.z - 1) < 1e-3)
        identity_all = identity_all and identity

    if all_world_verts:
        xs = [v.x for v in all_world_verts]
        ys = [v.y for v in all_world_verts]
        zs = [v.z for v in all_world_verts]
        x_min, x_max = min(xs), max(xs)
        y_min, y_max = min(ys), max(ys)
        z_min, z_max = min(zs), max(zs)
        dx = x_max - x_min
        dy = y_max - y_min
        dz = z_max - z_min

        # Torso slice (35%..70% of Z height in Blender Z-up = Y height in Unity Y-up)
        z_lo = z_min + 0.35 * dz
        z_hi = z_min + 0.70 * dz
        torso_pts = [v for v in all_world_verts if z_lo <= v.z <= z_hi]
        if torso_pts:
            torso_dx = max(v.x for v in torso_pts) - min(v.x for v in torso_pts)
            torso_dy = max(v.y for v in torso_pts) - min(v.y for v in torso_pts)
        else:
            torso_dx, torso_dy = dx, dy
    else:
        x_min = x_max = y_min = y_max = z_min = z_max = 0.0
        dx = dy = dz = torso_dx = torso_dy = 0.0

    uv_world_ratio = total_uv_area / max(total_world_area, 1e-6)

    return {
        "file": path, "status": "ok",
        "mesh_count": len(meshes), "tris": total_tris, "ngons": ngons,
        "uv_valid": uv_valid_all, "uv_min_area": round(uv_min_area, 5),
        "uv_total_area": round(total_uv_area, 5),
        "uv_inside01": round(uv_inside01_min, 4),
        "mesh_area_m2": round(total_world_area, 5),
        "uv_world_ratio": round(uv_world_ratio, 5),
        "identity_transform": identity_all,
        "material_slots": mat_slots,
        "bounds_unity_y_up": {
            "width_x": round(dx, 4),
            "height_y": round(dz, 4),
            "depth_z": round(dy, 4),
            "y_min": round(z_min, 4),
            "y_max": round(z_max, 4),
        },
        "torso_bounds": {
            "width_x": round(torso_dx, 4),
            "depth_z": round(torso_dy, 4),
        },
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
