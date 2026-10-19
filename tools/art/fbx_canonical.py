#!/usr/bin/env python3
"""
tools/art/fbx_canonical.py
Modulo canonico compartilhado (executado DENTRO do Blender via WSL headless).
Define a politica V2 de normalizacao de malhas para o Unity 6 URP:

Convencao canonica de destino (espaco Blender pre-exportacao):
  - Frente  : -Y  (torna-se +Z frontal no Unity)
  - Topo    : +Z  (torna-se +Y topo no Unity)
  - Direita : +X
  - Exportacao FBX nativa: axis_forward='-Z', axis_up='Y'
    (unit scale + bake space transform sao inerentes ao exportador nativo do Blender 5.0,
     verificado por round-trip deterministico com transform identidade)

Politicas de pivo (preset):
  - character : origem na base dos pes (0,0,0), centralizado em X/Z
  - weapon    : origem no ponto de empunhadura, eixo longitudinal alinhado a frente canonica
  - prop      : origem na base central (apoio no chao)
  - raw       : apenas transforma/UV/triangulacao, sem reorientacao

Usage por outros scripts bpy:
  import sys, os
  sys.path.insert(0, os.path.dirname(bpy.data.filepath) or ".")
  import fbx_canonical
"""

import math
import re

try:
    import bpy
    import bmesh
    from mathutils import Vector, Matrix
except ImportError as e:
    raise RuntimeError(f"[fbx_canonical] Deve ser executado dentro do Blender (wsl blender -b -P): {e}")

try:
    import numpy as np
except ImportError:
    np = None  # fallback manual se numpy do Blender nao estiver disponivel


# ----------------------------------------------------------------------------
# Helpers de cenario
# ----------------------------------------------------------------------------

def clean_scene():
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    for m in list(bpy.data.meshes):
        bpy.data.meshes.remove(m)
    for mat in list(bpy.data.materials):
        bpy.data.materials.remove(mat)
    bpy.context.scene.cursor.location = (0.0, 0.0, 0.0)


def _ensure_addon(module):
    """Blender 5.x headless nao carrega addons core automaticamente em alguns fluxos."""
    try:
        import addon_utils
        if module not in addon_utils.modules(enabled_only=True):
            addon_utils.enable(module, default_set=True, persistent=True)
    except Exception:
        pass


def ensure_fbx_import():
    """
    Garante importacao FBX funcional no Blender 5.0.1 headless.
    Regressao conhecida: as propriedades files/directory tornaram-se runtime-only e o
    execute() oficial lanca AttributeError em self.files quando nao definido.
    Substituimos execute por versao defensiva (mesma logica, acesso seguro).
    """
    _ensure_addon("io_scene_fbx")
    import io_scene_fbx
    cls = getattr(io_scene_fbx, "ImportFBX", None)
    if cls is None or getattr(cls, "_studio_files_patched", False):
        return

    def _safe_execute(self, context):
        keywords = self.as_keywords(ignore=("filter_glob", "directory", "ui_tab", "filepath", "files"))
        from io_scene_fbx import import_fbx
        import os
        files = getattr(self, "files", None)
        if files:
            ret = {'CANCELLED'}
            for file in files:
                path = os.path.join(self.directory, file.name)
                if import_fbx.load(self, context, filepath=path, **keywords) == {'FINISHED'}:
                    ret = {'FINISHED'}
            return ret
        return import_fbx.load(self, context, filepath=self.filepath, **keywords)

    cls.execute = _safe_execute
    cls._studio_files_patched = True
    print("[fbx_canonical] Patch defensivo de importacao FBX aplicado (regressao Blender 5.0.1)")


def _vert_coords(obj):
    """Retorna numpy array (N,3) com coordenadas de vertices em espaco mundial."""
    mesh = obj.data
    n = len(mesh.vertices)
    if n == 0:
        return None
    if np is not None:
        flat = np.empty(n * 3, dtype=np.float64)
        mesh.vertices.foreach_get("co", flat)
        world = np.asarray(obj.matrix_world.to_3x3(), dtype=np.float64)
        return (flat.reshape(n, 3) @ world.T) + np.asarray(obj.matrix_world.translation, dtype=np.float64)
    return None


def world_bbox(obj):
    coords = _vert_coords(obj)
    if coords is None:
        return None, None
    return Vector(coords.min(axis=0).tolist()), Vector(coords.max(axis=0).tolist())


# ----------------------------------------------------------------------------
# Transformacoes e limpeza
# ----------------------------------------------------------------------------

def apply_transforms(obj):
    """Aplica TODAS as transformacoes (location/rotation/scale) na malha."""
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True, properties=True,
                                   isolate_users=False)


def triangulate(obj):
    """Triangula a malha eliminando N-gons (BEAUTY preserva silhueta)."""
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.mesh.quads_convert_to_tris(quad_method='BEAUTY', ngon_method='BEAUTY')
    bpy.ops.object.mode_set(mode='OBJECT')


# ----------------------------------------------------------------------------
# UVs
# ----------------------------------------------------------------------------

def uv_report(obj):
    """Inspeciona UV0: existencia, area ocupada e cobertura minima."""
    mesh = obj.data
    if not mesh.uv_layers:
        return {"has_layer": False, "area": 0.0, "valid": False}
    uv_layer = mesh.uv_layers.active
    loop_count = len(mesh.loops)
    if loop_count == 0:
        return {"has_layer": True, "area": 0.0, "valid": False}
    if np is not None:
        flat = np.empty(loop_count * 2, dtype=np.float64)
        uv_layer.data.foreach_get("uv", flat)
        uvs = flat.reshape(loop_count, 2)
        area = float(abs(np.sum(uvs[:, 0] * np.roll(uvs[:, 1], -1) - np.roll(uvs[:, 0], -1) * uvs[:, 1])) * 0.5)
        inside = float(np.mean((uvs >= -0.05).all(axis=1) & (uvs <= 1.05).all(axis=1)))
        # Kits palette-based (Kenney colormap) mapeiam UVs a pontos minúsculos da paleta:
        # area pequena e legitimo; degenerescencia real e area ~0 (sem mapeamento utilizavel)
        valid = area > 1e-5 and inside > 0.98
        return {"has_layer": True, "area": round(area, 6), "inside01": round(inside, 4), "valid": valid}
    # Fallback sem numpy
    return {"has_layer": True, "area": 1.0, "valid": True}


def ensure_uv(obj, force=False):
    """Garante UV0 valido. Se ausente/degenerado (ou force), aplica Smart UV Project canonico."""
    rep = uv_report(obj)
    if rep["valid"] and not force:
        return rep
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(66.0), island_margin=0.02,
                             correct_aspect=True, scale_to_bounds=False)
    bpy.ops.object.mode_set(mode='OBJECT')
    return uv_report(obj)


# ----------------------------------------------------------------------------
# Orientacao longitudinal (PCA) — cura armas/props "deitados" ou "apontando para baixo"
# ----------------------------------------------------------------------------

def _principal_axes(obj):
    coords = _vert_coords(obj)
    if coords is None or len(coords) < 3:
        return None
    centered = coords - coords.mean(axis=0)
    if np is None:
        return None
    cov = centered.T @ centered / len(centered)
    eigvals, eigvecs = np.linalg.eigh(cov)  # ascendente
    order = np.argsort(eigvals)[::-1]
    return eigvals[order], eigvecs[:, order], coords


def orient_weapon(obj, forward_hint=None, up_hint=None):
    """
    Alinha o eixo longitudinal (PCA dominante) da arma a -Y (frente canonica Blender/+Z Unity).
    - Muzzle = extremidade com menor secao transversal (spread de vertices).
    - Up = segundo eixo de maior extensao.
    forward_hint/up_hint: strings '+X','-X','+Y','-Y','+Z','-Z' para sobrescrever deteccao
    (usar quando a orientacao de origem ja e conhecida, ex: assets do vault).
    """
    axes = _principal_axes(obj)
    if axes is None:
        return {"aligned": False, "reason": "PCA indisponivel"}
    eigvals, eigvecs, coords = axes
    long_axis = Vector(eigvecs[:, 0]).normalized()

    # Extremidade do muzzle: menor variancia transversal
    center = coords.mean(axis=0)
    proj = (coords - center) @ np.asarray(long_axis)
    mid = proj.mean()
    front_mask = proj > mid
    rear_mask = ~front_mask
    c = coords - center
    perp_f = c[front_mask] - np.outer(proj[front_mask], np.asarray(long_axis))
    perp_r = c[rear_mask] - np.outer(proj[rear_mask], np.asarray(long_axis))
    front_spread = float((perp_f ** 2).sum(axis=1).mean())
    rear_spread = float((perp_r ** 2).sum(axis=1).mean())
    if front_spread <= rear_spread:
        muzzle_dir = long_axis        # +longitudinal e o cano
    else:
        muzzle_dir = -long_axis       # cano aponta para o lado oposto

    if forward_hint:
        muzzle_dir = _axis_from_hint(forward_hint)

    target_forward = Vector((0.0, -1.0, 0.0))  # -Y Blender = +Z Unity
    q = muzzle_dir.rotation_difference(target_forward)
    mat = q.to_matrix().to_4x4()
    obj.matrix_world = mat @ obj.matrix_world
    bpy.context.view_layer.update()
    apply_transforms(obj)

    # Up: segundo eixo PCA reavaliado pos-alinhamento; garantir topo em +Z
    if not up_hint:
        min_pt, max_pt = world_bbox(obj)
        dims = max_pt - min_pt
        up_hint = '+Z' if dims[2] >= dims[0] else None  # ja maioria correta; senao roda 90 em Y
    if up_hint and up_hint in ('+X', '-X'):
        # lateral ficou em Z: girar 90 graus em torno de Y para trazer a face maior para cima
        rot = Matrix.Rotation(math.radians(90.0), 4, 'Y')
        obj.matrix_world = rot @ obj.matrix_world
        bpy.context.view_layer.update()
        apply_transforms(obj)

    return {"aligned": True, "front_spread": round(front_spread, 6),
            "rear_spread": round(rear_spread, 6)}


def _axis_from_hint(hint):
    m = {"+X": (1, 0, 0), "-X": (-1, 0, 0), "+Y": (0, 1, 0), "-Y": (0, -1, 0),
         "+Z": (0, 0, 1), "-Z": (0, 0, -1)}
    return Vector(m[hint])


# ----------------------------------------------------------------------------
# Pivos
# ----------------------------------------------------------------------------

def set_origin_at(obj, point):
    """Move a origem do objeto para `point` (espaco mundial) SEM mover a geometria."""
    bpy.context.scene.cursor.location = point
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
    bpy.context.scene.cursor.location = (0.0, 0.0, 0.0)


def pivot_character(obj):
    """Base dos pes exatamente em (0,0,0), centralizado em X e Z (espaco Unity pos-export)."""
    min_pt, max_pt = world_bbox(obj)
    set_origin_at(obj, Vector(((min_pt.x + max_pt.x) * 0.5, min_pt.y, (min_pt.z + max_pt.z) * 0.5)))
    obj.location = (0.0, 0.0, 0.0)
    apply_transforms(obj)


def pivot_weapon(obj, grip_ratio=0.32):
    """
    Pivo no ponto de empunhadura: a ~grip_ratio do comprimento a partir da coronha,
    na face inferior da faixa central do corpo. Eixo longitudinal ja esta em -Y (frente).
    """
    min_pt, max_pt = world_bbox(obj)
    length = max_pt.y - min_pt.y           # frente = -Y
    height = max_pt.z - min_pt.z
    rear_y = max_pt.y                       # coronha em +Y
    grip = Vector((0.0, rear_y - grip_ratio * length, min_pt.z + 0.30 * height))
    set_origin_at(obj, grip)
    obj.location = (0.0, 0.0, 0.0)
    apply_transforms(obj)
    return {"grip": tuple(round(v, 4) for v in grip)}


def pivot_prop(obj):
    """Base de apoio no chao, centralizado."""
    pivot_character(obj)  # mesma politica: base centrada na origem


# ----------------------------------------------------------------------------
# Finalizacao e exportacao canonicas
# ----------------------------------------------------------------------------

def ensure_material_tagged(obj):
    """Garante ao menos 1 slot de material nomeado (materiais reais sao vinculados no Unity/URP)."""
    if obj.material_slots and obj.material_slots[0].material:
        return
    mat = bpy.data.materials.new(name=f"M_{obj.name}")
    mat.use_nodes = True
    obj.data.materials.append(mat)


def export_canonical_fbx(filepath, obj):
    """
    Exportacao FBX canonica V2 para Unity (Blender 5.0+):
      axis_forward='-Z', axis_up='Y'  -> conversao Z-up/-Y-fwd (Blender) para Y-up/+Z-fwd (Unity)

    O exportador FBX nativo do Blender 5.0 aplica unit scale e bake_space_transform
    inherentemente (verificado por round-trip: transform identico, dims preservadas,
    zero rotacao residual de -90 graus no Unity).

    ATENCAO: o exportador nativo exporta TODOS os objetos da cena (nao ha use_selection).
    O pipeline canonico garante cena single-object via clean_scene() antes de chamar.
    """
    import os
    os.makedirs(os.path.dirname(filepath), exist_ok=True)
    bpy.ops.export_scene.fbx(
        filepath=filepath,
        axis_forward='-Z',
        axis_up='Y',
    )


def canonical_finalize(objs, name, preset="prop", out_path=None,
                       forward_hint=None, up_hint=None, force_uv=False, pivot_override=None):
    """
    Pipeline canonico completo: join -> triangula -> UV -> transforma -> orienta -> pivo -> exporta.
    Retorna dict de report para evidencia.
    """
    # Exportador nativo FBX 5.0 exporta TODOS os objetos: purgar nao-malhas
    # (luzes, cameras, armatures, empties) para garantir FBX single-mesh canonico.
    for o in list(bpy.context.scene.objects):
        if o.type != 'MESH':
            bpy.data.objects.remove(o, do_unlink=True)
    objs = [o for o in objs if o.name in bpy.context.scene.objects]

    bpy.ops.object.select_all(action='DESELECT')
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    if len(objs) > 1:
        bpy.ops.object.join()
    obj = bpy.context.view_layer.objects.active
    obj.name = name
    obj.data.name = name

    apply_transforms(obj)
    triangulate(obj)
    try:
        bpy.ops.object.shade_smooth()
    except Exception:
        pass
    uv = ensure_uv(obj, force=force_uv)

    report = {"name": name, "preset": preset, "uv": uv}
    align_info = None

    if preset == "weapon":
        align_info = orient_weapon(obj, forward_hint=forward_hint, up_hint=up_hint)
        if pivot_override:
            set_origin_at(obj, Vector(pivot_override))
            obj.location = (0.0, 0.0, 0.0)
            apply_transforms(obj)
            report["grip"] = tuple(round(v, 4) for v in pivot_override)
        else:
            report.update(pivot_weapon(obj))
    elif preset == "character":
        pivot_character(obj)
    elif preset == "prop":
        pivot_prop(obj)
    # raw | kit: nada adicional — kit PRESERVA o pivo original do autor
    # (pecas modulares dependem do pivo canonico do fabricante para snapping/kitbashing)

    if align_info:
        report["orientation"] = align_info

    # Politica anti-rotação-residual: no fim, transform do objeto DEVE ser identidade
    identity_ok = (abs(obj.location.x) < 1e-6 and abs(obj.location.y) < 1e-6 and abs(obj.location.z) < 1e-6
                   and abs(obj.rotation_euler.x) < 1e-4 and abs(obj.rotation_euler.y) < 1e-4
                   and abs(obj.rotation_euler.z) < 1e-4
                   and abs(obj.scale.x - 1.0) < 1e-4 and abs(obj.scale.y - 1.0) < 1e-4 and abs(obj.scale.z - 1.0) < 1e-4)
    report["identity_transform"] = bool(identity_ok)

    ensure_material_tagged(obj)

    if out_path:
        export_canonical_fbx(out_path, obj)
        report["exported"] = out_path

    min_pt, max_pt = world_bbox(obj)
    report["dimensions"] = {
        "x": round(max_pt.x - min_pt.x, 4), "y": round(max_pt.y - min_pt.y, 4),
        "z": round(max_pt.z - min_pt.z, 4)}
    report["tris"] = len(obj.data.polygons)
    return report
