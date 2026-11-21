#!/usr/bin/env python3
"""
tools/art/ai_generator/fab3d_bridge.py
Ponte Fábrica 3D -> Unity (ADR-005). Orquestrador Windows, stdlib puro.

Consome o GLB "print-grade" publicado pela pipeline da Fábrica 3D (repo
3dprinter-setup-ia, pipeline/run_asset.py) e o converte em asset canônico de
jogo sob o Quality Gate V2:

  1. (Modo 2) gera o GLB chamando run_asset.py no repo da Fábrica
  2. staging: copia o GLB para unity/Assets/Game/Art/Generated/<nome>/
  3. Blender WSL headless (fab3d_glb_to_fbx.py): importa o GLB, decima ate o
     budget da categoria, extrai/reduz texturas e exporta FBX canonico
  4. grava fab3d_manifest.json (para o Fab3dImporter) e
     evidence/art/fab3d_bridge_<nome>.json

Modo 1 — teste da ponte com GLB ja publicado (recomendado primeiro):
  python tools/art/ai_generator/fab3d_bridge.py --from-glb C:\\...\\peca.glb ^
      --name caixa_neon --category small_prop_item

Modo 2 — geracao completa (requer ambiente da Fabrica operacional):
  python tools/art/ai_generator/fab3d_bridge.py ^
      --asset-config C:\\...\\3dprinter-setup-ia\\assets\\<id>.json ^
      --fab3d-repo C:\\...\\3dprinter-setup-ia --name <asset_id>

Regra permanente: 100% local — nenhum passo chama servico externo de
geracao/texturizacao (ADR-005; ADR-009 do repo 3dprinter-setup-ia).
"""

import argparse
import json
import shutil
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent.parent.parent
BPY_SCRIPT = Path(__file__).resolve().parent / "fab3d_glb_to_fbx.py"

# Sincronizado com docs/art/art-spec.yaml v1.0 (nao parseia YAML para
# manter o orquestrador stdlib-only). Alterar lá = alterar aqui.
CATEGORIES = {
    "small_prop_item":    {"max_tris": 600,  "texture_size": 512,  "astc": "ASTC_6x6", "collider": "box",  "preset": "prop"},
    "large_prop_building": {"max_tris": 3000, "texture_size": 512,  "astc": "ASTC_6x6", "collider": "box",  "preset": "prop"},
    "player_character":   {"max_tris": 4500, "texture_size": 1024, "astc": "ASTC_4x4", "collider": "none", "preset": "character"},
    "enemy_character":    {"max_tris": 2500, "texture_size": 1024, "astc": "ASTC_4x4", "collider": "none", "preset": "character"},
}


def wsl_path(win_path) -> str:
    p = Path(win_path).resolve()
    drive = p.drive.replace(":", "").lower()
    return f"/mnt/{drive}{p.as_posix().split(':', 1)[-1]}"


def run(cmd, desc):
    print(f"[fab3d_bridge] {desc}: {' '.join(str(c) for c in cmd)}")
    res = subprocess.run([str(c) for c in cmd])
    if res.returncode != 0:
        print(f"[fab3d_bridge] FALHA em '{desc}' (exit {res.returncode})")
        sys.exit(res.returncode)


def run_capture(cmd, desc):
    print(f"[fab3d_bridge] {desc}: {' '.join(str(c) for c in cmd)}")
    res = subprocess.run([str(c) for c in cmd], capture_output=True, text=True)
    print(res.stdout[-4000:] if res.stdout else "")
    if res.returncode != 0:
        print(f"[fab3d_bridge] FALHA em '{desc}' (exit {res.returncode})")
        print(res.stderr[-4000:] if res.stderr else "")
        sys.exit(res.returncode)
    return res.stdout


def parse_report(stdout):
    for line in reversed(stdout.splitlines()):
        if line.startswith("REPORT_JSON:"):
            return json.loads(line[len("REPORT_JSON:"):])
    print("[fab3d_bridge] REPORT_JSON nao encontrado na saida do Blender")
    sys.exit(1)


def main():
    ap = argparse.ArgumentParser(description="Ponte Fabrica 3D -> Unity (ADR-005)")
    src = ap.add_mutually_exclusive_group(required=True)
    src.add_argument("--from-glb", help="GLB ja publicado pela Fabrica (Modo 1)")
    src.add_argument("--asset-config", help="assets/<id>.json do repo da Fabrica (Modo 2)")
    ap.add_argument("--fab3d-repo", default="", help="Caminho local do repo 3dprinter-setup-ia (Modo 2)")
    ap.add_argument("--fab3d-python", default="python", help="Python que roda o run_asset.py (Modo 2)")
    ap.add_argument("--glb-index", type=int, default=0,
                    help="Indice do GLB no ranking por tamanho (0 = maior; Modo 2/variantes)")
    ap.add_argument("--name", required=True, help="asset_id canonico (ex.: caixa_neon)")
    ap.add_argument("--category", default="small_prop_item", choices=sorted(CATEGORIES))
    ap.add_argument("--preset", default=None, choices=["prop", "weapon", "character", "raw"],
                    help="Sobrescreve o preset padrao da categoria")
    ap.add_argument("--collider", default=None, choices=["box", "none"],
                    help="Sobrescreve o colisor padrao da categoria")
    ap.add_argument("--max-tris", type=int, default=None, help="Sobrescreve o budget da categoria")
    ap.add_argument("--texture-size", type=int, default=None, help="Sobrescreve o tamanho da categoria")
    ap.add_argument("--out", default=None,
                    help="Diretorio de saida (default: unity/Assets/Game/Art/Generated/<name>)")
    args = ap.parse_args()

    cat = CATEGORIES[args.category]
    preset = args.preset or cat["preset"]
    collider = args.collider or cat["collider"]
    max_tris = args.max_tris if args.max_tris is not None else cat["max_tris"]
    tex_size = args.texture_size if args.texture_size is not None else cat["texture_size"]

    # 1. Obtencao do GLB
    if args.from_glb:
        glb_src = Path(args.from_glb).resolve()
        if not glb_src.exists():
            print(f"[fab3d_bridge] GLB nao encontrado: {glb_src}")
            sys.exit(2)
        source_mode = "from_glb"
    else:
        if not args.fab3d_repo:
            print("[fab3d_bridge] Modo 2 exige --fab3d-repo")
            sys.exit(2)
        repo = Path(args.fab3d_repo).resolve()
        runner = repo / "pipeline" / "run_asset.py"
        if not runner.exists():
            print(f"[fab3d_bridge] run_asset.py nao encontrado em {runner}")
            sys.exit(2)
        gen_out = ROOT / ".fab3d_tmp" / args.name
        gen_out.mkdir(parents=True, exist_ok=True)
        run([args.fab3d_python, str(runner), "--config", str(Path(args.asset_config).resolve()),
             "--out", str(gen_out)], "Geracao na Fabrica (run_asset.py)")
        glbs = sorted(gen_out.rglob("*.glb"), key=lambda g: g.stat().st_size, reverse=True)
        if args.glb_index >= len(glbs):
            print(f"[fab3d_bridge] --glb-index {args.glb_index} fora do range "
                  f"({len(glbs)} GLBs encontrados)")
            sys.exit(2)
        glb_src = glbs[args.glb_index]
        source_mode = "fab3d_run"
    print(f"[fab3d_bridge] GLB selecionado ({source_mode}): {glb_src}")

    # 2. Staging
    out_dir = Path(args.out).resolve() if args.out else \
        ROOT / "unity" / "Assets" / "Game" / "Art" / "Generated" / args.name
    if out_dir.exists():
        shutil.rmtree(out_dir)
    out_dir.mkdir(parents=True)
    glb_staged = out_dir / glb_src.name
    shutil.copy2(glb_src, glb_staged)

    # 3. Blender WSL: GLB -> FBX canonico + texturas
    bpy_args = ["--input", wsl_path(glb_staged), "--out-dir", wsl_path(out_dir),
                "--name", args.name, "--preset", preset,
                "--max-tris", str(max_tris), "--texture-size", str(tex_size)]
    stdout = run_capture(["wsl", "blender", "-b", "-P", wsl_path(BPY_SCRIPT),
                          "--", *bpy_args], "Blender WSL (decimate + texturas + FBX)")
    report = parse_report(stdout)
    if not report.get("identity_transform"):
        print("[fab3d_bridge] identity_transform=false — FBX nao canonico, abortando")
        sys.exit(1)
    dec = report.get("decimate", {})
    if dec.get("applied") and dec.get("after", 0) > max_tris * 1.05:
        print(f"[fab3d_bridge] tris {dec.get('after')} acima do budget {max_tris}")
        sys.exit(1)

    # 4. Manifest p/ Fab3dImporter + evidencia
    manifest = {
        "asset_name": args.name,
        "category": args.category,
        "preset": preset,
        "texture_size": tex_size,
        "astc": cat["astc"],
        "collider": collider,
        "max_tris": max_tris,
        "tris_final": dec.get("after"),
        "source_mode": source_mode,
        "source_glb": glb_src.name,
    }
    manifest_path = out_dir / "fab3d_manifest.json"
    manifest_path.write_text(json.dumps(manifest, indent=2), encoding="utf-8")

    evidence_dir = ROOT / "evidence" / "art"
    evidence_dir.mkdir(parents=True, exist_ok=True)
    (evidence_dir / f"fab3d_bridge_{args.name}.json").write_text(
        json.dumps({"manifest": manifest, "blender_report": report}, indent=2),
        encoding="utf-8")

    print(f"""
[fab3d_bridge] PONTE CONCLUIDA: {out_dir}
[fab3d_bridge] Proximos passos (ver docs/operations/fab3d-bridge-runbook.md):
  1. Unity: menu Studio > Fab3D > Import Generated Assets (exit 0 obrigatorio)
  2. python tools/validation/validate_asset.py --asset {out_dir}/{args.name}.fbx
  3. Evidencia visual: prefab em cena + captura em evidence/captures/
""")


if __name__ == "__main__":
    main()
