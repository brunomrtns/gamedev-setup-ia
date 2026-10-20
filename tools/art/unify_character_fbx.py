#!/usr/bin/env python3
"""
tools/art/unify_character_fbx.py
Pipeline Universal de Personagem — unifica FBXs MODULARES (cada parte com sua
propria armature duplicada) num UNICO FBX rigado, pronto para Unity Humanoid.

Rodado DENTRO do Blender headless (WSL). Dirigido por manifest JSON:

  {"variants": [{"name": "ped_dressed_01f_01",
                 "parts": ["char.fbx", "hair.fbx", ...],
                 "out": "saida.fbx"}, ...],
   "verify_dir": "evidence/art"   # opcional — renders rest+pose
  }

Regras (blindadas):
  - Armatures duplicadas so' sao unificadas se os ossos forem IDENTICOS
    (mesmo nome -> head/tail world, tolerancia 1e-4). Divergencia = a variante
    e' reportada e abortada, NUNCA remendada no chute.
  - LODs *_lodN (N>0) sao descartados.
  - Render de verificacao OBRIGATORIO: rest pose + pose deformada (ossos
    rotacionados) por variante — so' exporta se o render existir.
  - SEM transform_apply em malha deformada: aplica-se transform de OBJETO
    (escala 0.01 / rotacao de importacao FBX) em armature+meshes em REST POSE,
    o que preserva a skinning (weights sao por nome de osso).
"""
import sys, os, json, math

import bpy
from mathutils import Vector, Euler

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fbx_canonical

TOL = 1e-4


def import_fbx(path):
    before = set(bpy.context.scene.objects)
    bpy.ops.import_scene.fbx(filepath=path)
    return [o for o in bpy.context.scene.objects if o not in before]


def bone_signature(arm_obj):
    """nome -> (head,tail) em world space, para comparacao deterministica."""
    sig = {}
    for b in arm_obj.data.bones:
        hw = arm_obj.matrix_world @ b.head_local
        tw = arm_obj.matrix_world @ b.tail_local
        sig[b.name] = (tuple(round(v, 6) for v in hw), tuple(round(v, 6) for v in tw))
    return sig


def compare_signatures(a, b):
    """Retorna lista de divergencias; vazio = identicas."""
    diffs = []
    if set(a) != set(b):
        missing_a = sorted(set(a) - set(b))[:5]
        missing_b = sorted(set(b) - set(a))[:5]
        diffs.append(f"bone sets divergem (so em A: {missing_a}; so em B: {missing_b})")
        return diffs
    for name in a:
        (ah, at), (bh, bt) = a[name], b[name]
        for i in range(3):
            if abs(ah[i] - bh[i]) > TOL or abs(at[i] - bt[i]) > TOL:
                diffs.append(f"osso '{name}' diverge: {ah}/{at} vs {bh}/{bt}")
                break
        if len(diffs) >= 5:
            break
    return diffs


def merge_armatures(objs, report):
    """Mantem a armature com mais meshes bound; re-aponta modifiers das demais."""
    arms = [o for o in objs if o.type == 'ARMATURE']
    meshes = [o for o in objs if o.type == 'MESH']
    if not arms:
        report['error'] = 'sem armature'
        return None
    def bound_count(arm):
        return sum(1 for m in meshes
                   for mod in m.modifiers if mod.type == 'ARMATURE' and mod.object == arm)
    survivor = max(arms, key=bound_count)
    sig_surv = bone_signature(survivor)
    for arm in arms:
        if arm is survivor:
            continue
        diffs = compare_signatures(sig_surv, bone_signature(arm))
        if diffs:
            report['error'] = 'armatures divergem: ' + '; '.join(diffs)
            return None
        for m in meshes:
            for mod in m.modifiers:
                if mod.type == 'ARMATURE' and mod.object == arm:
                    mod.object = survivor
        # apaga armature duplicada e possiveis filhos vazios
        arm_name = arm.name
        for child in list(arm.children):
            if child.type == 'EMPTY':
                if child in objs:
                    objs.remove(child)
                bpy.data.objects.remove(child, do_unlink=True)
        objs.remove(arm)
        bpy.data.objects.remove(arm, do_unlink=True)
        report.setdefault('merged', []).append(arm_name)
    return survivor


def strip_lods(objs):
    kept, removed = [], 0
    for o in objs:
        if o.type != 'MESH':
            continue
        n = o.name.lower()
        idx = n.rfind('_lod')
        if idx >= 0 and n[idx + 4:].isdigit() and n[idx + 4:] != '0':
            bpy.data.objects.remove(o, do_unlink=True)
            removed += 1
        else:
            kept.append(o)
    return kept, removed


def normalize_transforms(survivor, meshes):
    """
    Normaliza transforms de importacao FBX (escala 0.01 / rotacao -90X) para
    identidade em rest pose. ORDEM OBRIGATORIA para nao explodir a hierarquia:
    1) desparenta meshes (preserva world), 2) aplica transform da armature,
    3) aplica transform de cada mesh, 4) re-parenta com inversa identidade.
    Aplicar transform do pai com filhos anexados multiplicaria a escala
    residual dos filhos (bug do render "blob minusculo").
    """
    from mathutils import Matrix
    for m in meshes:
        mw = m.matrix_world.copy()
        m.parent = None
        m.matrix_world = mw
    bpy.ops.object.select_all(action='DESELECT')
    for o in [survivor] + meshes:
        o.select_set(True)
    bpy.context.view_layer.objects.active = survivor
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    for m in meshes:
        m.parent = survivor
        m.matrix_parent_inverse = Matrix.Identity(4)
    # esqueleto no chao: menor vertice world em z=0
    bpy.context.view_layer.update()
    minz = min((m.matrix_world @ Vector(c)).z for m in meshes for c in m.bound_box)
    if abs(minz) > 1e-4:
        for o in [survivor] + meshes:
            o.location.z -= minz
    bpy.context.view_layer.update()


def _aim_camera(cam, target, dist, elevation=0.45):
    d = Vector((dist * 0.85, -dist * 0.85, dist * elevation))
    cam.location = Vector(target) + d
    direction = Vector(target) - cam.location
    cam.rotation_euler = direction.to_track_quat('-Z', 'Y').to_euler()


def verify_render(survivor, meshes, out_png, pose=None):
    """Render Workbench (sempre funciona headless) — prova visual da skinning."""
    if pose:
        bpy.context.view_layer.objects.active = survivor
        bpy.ops.object.mode_set(mode='POSE')
        for bname, rot in pose.items():
            pb = survivor.pose.bones.get(bname)
            if pb:
                pb.rotation_mode = 'XYZ'
                pb.rotation_euler = Euler(rot, 'XYZ')
        bpy.ops.object.mode_set(mode='OBJECT')
        bpy.context.view_layer.update()
    scn = bpy.context.scene
    scn.render.engine = 'BLENDER_WORKBENCH'
    scn.display.shading.light = 'STUDIO'
    scn.display.shading.color_type = 'TEXTURE'
    scn.render.resolution_x = scn.render.resolution_y = 720
    scn.render.filepath = out_png
    cam = bpy.data.objects.new('VerifyCam', bpy.data.cameras.new('VerifyCam'))
    scn.collection.objects.link(cam)
    scn.camera = cam
    # bbox combinada
    pts = [m.matrix_world @ Vector(c) for m in meshes for c in m.bound_box]
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    center, size = (lo + hi) / 2, max(hi - lo)
    _aim_camera(cam, center + Vector((0, 0, size * 0.05)), size * 1.6)
    bpy.ops.render.render(write_still=True)
    ok = os.path.exists(out_png) and os.path.getsize(out_png) > 10000
    bpy.data.objects.remove(cam, do_unlink=True)
    if pose:
        bpy.context.view_layer.objects.active = survivor
        bpy.ops.object.mode_set(mode='POSE')
        bpy.ops.pose.select_all(action='SELECT')
        bpy.ops.pose.rot_clear()
        bpy.ops.object.mode_set(mode='OBJECT')
    return ok


TEST_POSE = {
    'Head': (math.radians(25), 0, math.radians(15)),
    'LeftArm': (0, math.radians(-45), 0),
    'LeftUpLeg': (math.radians(-30), 0, 0),
    'RightArm': (0, math.radians(45), 0),
}


def process_variant(var, verify_dir):
    rep = {'name': var['name'], 'parts': var['parts']}
    fbx_canonical.clean_scene()
    objs = []
    for p in var['parts']:
        objs += import_fbx(p)
    survivor = merge_armatures(objs, rep)
    if survivor is None:
        return rep
    meshes, removed = strip_lods(objs)
    rep['lod_removed'] = removed
    rep['meshes'] = [m.name for m in meshes]
    # vertex groups orfas (grupo sem osso correspondente) = erro de merge
    bone_names = {b.name for b in survivor.data.bones}
    orphan = sorted({g.name for m in meshes for g in m.vertex_groups
                     if g.name not in bone_names})
    if orphan:
        rep['warning_orphan_vgroups'] = orphan
    normalize_transforms(survivor, meshes)
    if verify_dir:
        os.makedirs(verify_dir, exist_ok=True)
        if not verify_render(survivor, meshes,
                             os.path.join(verify_dir, f"unify_{var['name']}_rest.png")):
            rep['error'] = 'render rest falhou'
            return rep
        if not verify_render(survivor, meshes,
                             os.path.join(verify_dir, f"unify_{var['name']}_pose.png"),
                             pose=TEST_POSE):
            rep['error'] = 'render pose falhou'
            return rep
    # export: exportador nativo do Blender 5 so aceita filepath/axis_*;
    # a cena contem exatamente armature+meshes lod0 apos clean+strip.
    os.makedirs(os.path.dirname(var['out']), exist_ok=True)
    bpy.ops.export_scene.fbx(
        filepath=os.path.abspath(var['out']),
        axis_forward='-Z', axis_up='Y',
    )
    rep['exported'] = var['out']
    return rep


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    manifest_path = argv[0]
    only = argv[argv.index('--only') + 1] if '--only' in argv else None
    manifest = json.load(open(manifest_path))
    verify_dir = manifest.get('verify_dir', 'evidence/art')
    fbx_canonical.ensure_fbx_import()
    results = []
    for var in manifest['variants']:
        if only and var['name'] != only:
            continue
        r = process_variant(var, verify_dir)
        results.append(r)
        print('VARIANT_RESULT ' + json.dumps(r))
    print('UNIFY_JSON_BEGIN')
    print(json.dumps(results, indent=1))
    print('UNIFY_JSON_END')


main()
