#!/usr/bin/env python3
"""
tools/art/normalize_fbx.py
Normalizador V2 de assets 3D existentes (executado DENTRO do Blender via WSL).

Uso canonico (a partir do Windows, com caminhos convertidos para /mnt/c/...):
  wsl blender -b -P tools/art/normalize_fbx.py -- \
      --input  /mnt/c/.../SM_Assault_Rifle_B.fbx \
      --output /mnt/c/.../SM_Assault_Rifle_B.fbx \
      --preset weapon [--forward -Z] [--up +Y] [--force-uv] [--pivot x y z]

Presets: weapon | character | prop | raw
Cura: eixos quebrados (Z-up->Y-up), pivos fora da empunhadura/base,
UVs ausentes/sobrepostas, N-gons, rotacao residual de -90 graus no Unity.

Imprime um JSON de report na ultima linha (prefixo REPORT_JSON:) para
consumo por validate_asset.py e evidencias.
"""

import sys
import os
import json

import bpy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fbx_canonical  # noqa: E402


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    opts = {"input": None, "output": None, "preset": "prop", "forward": None,
            "up": None, "force_uv": False, "pivot": None, "name": None}
    i = 0
    while i < len(argv):
        a = argv[i]
        if a == "--input":
            i += 1; opts["input"] = argv[i]
        elif a == "--output":
            i += 1; opts["output"] = argv[i]
        elif a == "--preset":
            i += 1; opts["preset"] = argv[i]
        elif a == "--forward":
            i += 1; opts["forward"] = argv[i]
        elif a == "--up":
            i += 1; opts["up"] = argv[i]
        elif a == "--force-uv":
            opts["force_uv"] = True
        elif a == "--pivot":
            opts["pivot"] = [float(argv[i + 1]), float(argv[i + 2]), float(argv[i + 3])]
            i += 2
        elif a == "--name":
            i += 1; opts["name"] = argv[i]
        i += 1
    return opts


def _ensure_addon(module):
    """Blender 5.x headless nao carrega addons core automaticamente em alguns fluxos."""
    try:
        import addon_utils
        if module not in addon_utils.modules(enabled_only=True):
            addon_utils.enable(module, default_set=True, persistent=True)
    except Exception:
        pass


def import_any(path):
    ext = os.path.splitext(path)[1].lower()
    if ext == ".fbx":
        fbx_canonical.ensure_fbx_import()
        bpy.ops.import_scene.fbx(filepath=path)
    elif ext in (".obj",):
        bpy.ops.wm.obj_import(filepath=path)
    elif ext in (".glb", ".gltf"):
        fbx_canonical._ensure_addon("io_scene_gltf2")
        bpy.ops.import_scene.gltf(filepath=path)
    elif ext == ".blend":
        bpy.ops.wm.open_mainfile(filepath=path)
    else:
        raise ValueError(f"Formato nao suportado: {ext}")
    meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    if not meshes:
        raise RuntimeError("Nenhum objeto MESH encontrado apos importacao")
    return meshes


def main():
    opts = parse_args()
    if not opts["input"]:
        print("[normalize_fbx] --input e obrigatorio")
        sys.exit(2)

    fbx_canonical.clean_scene()
    print(f"[normalize_fbx] Importando: {opts['input']}")
    meshes = import_any(opts["input"])

    name = opts["name"] or os.path.splitext(os.path.basename(opts["input"]))[0]
    out = opts["output"] or opts["input"]

    report = fbx_canonical.canonical_finalize(
        meshes, name, preset=opts["preset"], out_path=out,
        forward_hint=opts["forward"], up_hint=opts["up"],
        force_uv=opts["force_uv"], pivot_override=opts["pivot"])

    print(json.dumps(report, ensure_ascii=False))
    print("REPORT_JSON:" + json.dumps(report, ensure_ascii=False))
    print(f"[normalize_fbx] Normalizado com sucesso: {out}")


if __name__ == "__main__":
    main()
