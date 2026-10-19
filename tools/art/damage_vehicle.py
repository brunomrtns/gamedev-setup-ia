#!/usr/bin/env python3
"""
damage_vehicle.py — gera variantes danificadas/explodidas de um FBX de veículo.

Rode via WSL:
    wsl blender -b -P tools/art/damage_vehicle.py -- \
        --input "C:/path/car.fbx" --output "C:/path/Variants/" \
        --variants wrecked_light,wrecked_severe,wrecked_burned

Para cada variante:
  wrecked_light   — amassado leve (frente+lateral, ~0.10m), tom mais escuro
  wrecked_severe  — amassado profundo + entorta painéis, arranhões
  wrecked_burned  — malha retorcida forte + vértices "derretidos" (carbonizado)
Exporta <nome>__<variante>.fbx normalizado (escala aplicada, y-up).
"""
import bpy, sys, os, math, random, argparse


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    p = argparse.ArgumentParser()
    p.add_argument("--input", required=True)
    p.add_argument("--output", required=True)
    p.add_argument("--variants", default="wrecked_light,wrecked_severe,wrecked_burned")
    return p.parse_args(argv)


def clean_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def import_fbx(path):
    if hasattr(bpy.ops.wm, "fbx_import"):
        bpy.ops.wm.fbx_import(filepath=path)
    else:
        bpy.ops.import_scene.fbx(filepath=path)
    return [o for o in bpy.context.scene.objects if o.type == 'MESH']


def verts_world(obj):
    mw = obj.matrix_world
    return [mw @ v.co for v in obj.data.vertices]


def dent(obj, meshes_bounds, amount, radius, spots, seed):
    """Amassa: desloca vértices para dentro (contrário à normal) dentro de
    esferas de influência posicionadas em pontos-chave da carroceria."""
    rng = random.Random(seed)
    min_z, max_z = meshes_bounds
    for v in obj.data.vertices:
        vw = obj.matrix_world @ v.co
        move = mathutils_zero()
        for (cx, cy, cz, r, power) in spots:
            dx, dy, dz = vw.x - cx, vw.y - cy, vw.z - cz
            d = math.sqrt(dx * dx + dy * dy + dz * dz)
            if d < r:
                f = (1.0 - d / r) * amount * power
                n = v.normal
                move += mathutils_vec(-n.x * f, -n.y * f, -n.z * f)
        # ruído de amassado fino
        nz = amount * 0.25
        move += mathutils_vec(rng.uniform(-nz, nz), rng.uniform(-nz, nz), rng.uniform(-nz, nz))
        v.co += obj.matrix_world.inverted() @ (vw + move) - v.co


def mathutils_zero():
    from mathutils import Vector
    return Vector((0, 0, 0))


def mathutils_vec(x, y, z):
    from mathutils import Vector
    return Vector((x, y, z))


def make_spots(bounds, level, rng):
    """Pontos de amassado: frente (capô), laterais, traseira por nível."""
    (minx, miny, minz, maxx, maxy, maxz) = bounds
    zmid = (minz + maxz) * 0.5
    zlow = minz + (maxz - minz) * 0.25
    spots = []
    if level >= 1:  # leve: frente + uma lateral
        spots += [
            (0, maxy - 0.15, zmid, 0.9, 1.0),      # capô/frente
            (minx + 0.05, (miny + maxy) * 0.5, zlow, 0.7, 0.8),  # lateral esq
        ]
    if level >= 2:  # severo: + traseira + outra lateral + teto
        spots += [
            (0, miny + 0.15, zmid, 0.9, 1.0),
            (maxx - 0.05, (miny + maxy) * 0.5, zlow, 0.7, 0.8),
            (0, (miny + maxy) * 0.5, maxz - 0.1, 0.8, 0.7),  # teto
        ]
    if level >= 3:  # queimado: retorção aleatória espalhada
        for _ in range(6):
            spots.append((rng.uniform(minx, maxx), rng.uniform(miny, maxz),
                          rng.uniform(minx, maxx) * 0.0 + rng.uniform(minz, maxz),
                          0.6, rng.uniform(0.6, 1.3)))
    return spots


def darken_materials(mult):
    for m in bpy.data.materials:
        for node in getattr(m.node_tree, "nodes", []) if m.use_nodes else []:
            if node.type == 'BSDF_PRINCIPLED':
                bc = node.inputs.get("Base Color")
                if bc: bc.default_value = (bc.default_value[0] * mult, bc.default_value[1] * mult, bc.default_value[2] * mult, 1.0)


def burn_materials():
    for m in bpy.data.materials:
        if not m.use_nodes: continue
        for node in m.node_tree.nodes:
            if node.type == 'BSDF_PRINCIPLED':
                bc = node.inputs.get("Base Color")
                if bc: bc.default_value = (0.02, 0.02, 0.025, 1.0)
                rough = node.inputs.get("Roughness")
                if rough: rough.default_value = 0.95
                metal = node.inputs.get("Metallic")
                if metal: metal.default_value = 0.4


def bounds_of(meshes):
    xs, ys, zs = [], [], []
    for o in meshes:
        for v in verts_world(o):
            xs.append(v.x); ys.append(v.y); zs.append(v.z)
    return min(xs), min(ys), min(zs), max(xs), max(ys), max(zs)


def process(fbx_in, out_dir, base, variant, seed):
    clean_scene()
    meshes = import_fbx(fbx_in)
    if not meshes:
        print(f"[damage] SEM MESH em {fbx_in}")
        return
    b = bounds_of(meshes)
    rng = random.Random(seed)
    level = {"wrecked_light": 1, "wrecked_severe": 2, "wrecked_burned": 3}.get(variant, 1)
    amount = {1: 0.08, 2: 0.30, 3: 0.42}[level]
    spots = make_spots(b, level, rng)
    for o in meshes:
        dent(o, (b[2], b[5]), amount, 0.8, spots, seed)
    if variant == "wrecked_burned":
        burn_materials()
    elif variant == "wrecked_severe":
        darken_materials(0.45)
    else:
        darken_materials(0.7)
    # normalização: aplica escala/rotação, pivô no chão
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    out = os.path.join(out_dir, f"{base}__{variant}.fbx")
    if hasattr(bpy.ops.export_scene, "fbx"):
        bpy.ops.export_scene.fbx(filepath=out)
    else:
        bpy.ops.wm.fbx_export(filepath=out)
    print(f"[damage] -> {out}")


def main():
    a = parse_args()
    os.makedirs(a.output, exist_ok=True)
    base = os.path.splitext(os.path.basename(a.input))[0]
    for i, v in enumerate([x.strip() for x in a.variants.split(",") if x.strip()]):
        process(a.input, a.output, base, v, seed=1000 + i)


if __name__ == "__main__":
    main()
