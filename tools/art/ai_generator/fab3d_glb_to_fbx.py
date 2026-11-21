#!/usr/bin/env python3
"""
tools/art/ai_generator/fab3d_glb_to_fbx.py
Etapa Blender (wsl blender -b -P) da ponte Fábrica 3D -> Unity (ADR-005).

Importa o GLB print-grade publicado pela Fábrica 3D, decima a malha ate o
budget de tris da categoria (docs/art/art-spec.yaml), extrai e reduz as
texturas (BaseColor + MetallicRoughness) para o tamanho do art-spec e exporta
FBX canonico via fbx_canonical.canonical_finalize (pivô/eixos/triangulacao/UV).

Uso canonico (a partir do Windows, caminhos convertidos para /mnt/...):
  wsl blender -b -P tools/art/ai_generator/fab3d_glb_to_fbx.py -- \
      --input /mnt/c/.../peca.glb \
      --out-dir /mnt/c/.../Generated/caixa_neon \
      --name caixa_neon --preset prop --max-tris 600 --texture-size 512

Presets: prop | weapon | character | raw (mesma tabela do normalize_fbx.py).
Imprime REPORT_JSON:{...} na ultima linha (padrao dos tools do studio, para
consumo por evidencias e validate_asset.py).
"""

import json
import os
import sys

import bpy

_HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(_HERE))  # tools/art/
import fbx_canonical  # noqa: E402
import normalize_fbx as nf  # noqa: E402  (import_any ja habilita o addon glTF)


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    opts = {"input": None, "out-dir": None, "name": None, "preset": "prop",
            "max-tris": 0, "texture-size": 512}
    i = 0
    while i < len(argv):
        a = argv[i]
        if a == "--input":
            i += 1; opts["input"] = argv[i]
        elif a == "--out-dir":
            i += 1; opts["out-dir"] = argv[i]
        elif a == "--name":
            i += 1; opts["name"] = argv[i]
        elif a == "--preset":
            i += 1; opts["preset"] = argv[i]
        elif a == "--max-tris":
            i += 1; opts["max-tris"] = int(argv[i])
        elif a == "--texture-size":
            i += 1; opts["texture-size"] = int(argv[i])
        i += 1
    return opts


def count_tris(objs):
    total = 0
    for o in objs:
        o.data.calc_loop_triangles()
        total += len(o.data.loop_triangles)
    return total


def decimate(objs, budget):
    """Decimate proporcional ate o budget (tolerancia de 2%); 0/negativo = pular."""
    before = count_tris(objs)
    if budget <= 0 or before <= budget:
        return {"before": before, "after": before, "applied": False}
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    ratio = budget / before
    after = before
    for _ in range(8):
        for o in objs:
            mod = o.modifiers.new("Fab3dDecimate", "DECIMATE")
            mod.ratio = ratio
        for o in objs:
            bpy.context.view_layer.objects.active = o
            bpy.ops.object.modifier_apply(modifier="Fab3dDecimate")
        after = count_tris(objs)
        if after <= budget * 1.02:
            break
        ratio = max(ratio * (budget / after) * 0.98, 0.001)
    return {"before": before, "after": after, "applied": True,
            "final_ratio": round(ratio, 4)}


def save_image_png(img, path):
    """Salva uma imagem do Blender como PNG no caminho pedido (packed ou nao)."""
    if img.packed_file:
        try:
            img.unpack(method="USE_ORIGINAL")
        except Exception:
            pass
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()


def extract_textures(tex_size, out_dir, name):
    """Classifica BaseColor (maior area, normalmente JPEG 4K/8K) e MR (PNG com
    'metal'/'rough' no nome, senao 2a maior). Reduz para tex_size e salva PNG."""
    imgs = [i for i in bpy.data.images
            if i.source == "FILE" and i.size[0] > 8 and i.size[1] > 8]
    if not imgs:
        return {"basecolor": None, "mr": None, "note": "nenhuma textura no GLB"}
    imgs.sort(key=lambda i: i.size[0] * i.size[1], reverse=True)
    base = imgs[0]
    mr = next((i for i in imgs[1:]
               if "metal" in i.name.lower() or "rough" in i.name.lower()),
              imgs[1] if len(imgs) > 1 else None)
    paths = {"basecolor_orig": [base.size[0], base.size[1]],
             "mr_orig": [mr.size[0], mr.size[1]] if mr else None}
    if base.size[0] > tex_size or base.size[1] > tex_size:
        base.scale(tex_size, tex_size)
    bc_path = os.path.join(out_dir, f"{name}_basecolor.png")
    save_image_png(base, bc_path)
    paths["basecolor"] = bc_path
    if mr:
        if mr.size[0] > tex_size or mr.size[1] > tex_size:
            mr.scale(tex_size, tex_size)
        mr_path = os.path.join(out_dir, f"{name}_mr.png")
        save_image_png(mr, mr_path)
        paths["mr"] = mr_path
    else:
        paths["mr"] = None
        paths["note"] = paths.get("note", "MR nao encontrado no GLB")
    return paths


def main():
    opts = parse_args()
    if not opts["input"] or not opts["out-dir"]:
        print("[fab3d_glb_to_fbx] --input e --out-dir sao obrigatorios")
        sys.exit(2)

    os.makedirs(opts["out-dir"], exist_ok=True)
    fbx_canonical.clean_scene()
    print(f"[fab3d_glb_to_fbx] Importando GLB: {opts['input']}")
    meshes = nf.import_any(opts["input"])

    dec = decimate(meshes, opts["max-tris"])
    print(f"[fab3d_glb_to_fbx] Decimate: {dec}")

    tex = extract_textures(opts["texture-size"], opts["out-dir"], opts["name"])
    print(f"[fab3d_glb_to_fbx] Texturas: {tex['basecolor']} | MR: {tex['mr']}")

    out_fbx = os.path.join(opts["out-dir"], f"{opts['name']}.fbx")
    report = fbx_canonical.canonical_finalize(
        meshes, opts["name"], preset=opts["preset"], out_path=out_fbx)
    report["decimate"] = dec
    report["textures"] = {k: v for k, v in tex.items()
                          if k in ("basecolor", "mr", "note")}
    report["source_glb"] = os.path.basename(opts["input"])
    report["texture_size"] = opts["texture-size"]

    print(json.dumps(report, ensure_ascii=False))
    print("REPORT_JSON:" + json.dumps(report, ensure_ascii=False))
    print(f"[fab3d_glb_to_fbx] FBX canonico exportado: {out_fbx}")


if __name__ == "__main__":
    main()
