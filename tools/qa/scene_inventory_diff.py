#!/usr/bin/env python3
"""scene_inventory_diff.py — mapa de dano entre dois inventarios de cena
(gerados por unity/Assets/Editor/SceneInventoryTool.cs).

Regra A6.6: auditoria por GEOMETRIA (posicao/bounds/layer), nunca por nome.
Nome aparece apenas como rotulo do relatorio.

Classificacao de colisores (missao v3.8 M2):
  OK      — colisor <= visual + 10% e objeto legitimo
  INFLADO — colisor maior que visual + 10%
  LIXO    — sem renderer no ramo (fantasma/decal/volume) ou collider em objeto
            cuja identidade visual nao existe
Uso:
  python tools/qa/scene_inventory_diff.py --pre evidence/qa/v38/inv_pre.json \
      --post evidence/qa/v38/inv_head.json --out evidence/qa/v38/m1_damage_map.md
"""
import argparse
import json
import math
import os
from collections import defaultdict

# --- geometria da malha viaria (portado de NeonDistrictSceneBuilder.OnRoadway) ---
LINES = 6
PITCH = 52.0
STREET_W = 12.0
AVENUE_W = 16.0
AVENUE_J = 3
OBD_XS = [182.0, 234.0]
X_MIN, X_MAX = -147.0, 284.0   # OBD_ROAD_END
Z_MIN, Z_MAX = -147.0, 147.0


def street_lines():
    return [(i - (LINES - 1) * 0.5) * PITCH for i in range(LINES)]


def road_corridors():
    """Lista de retangulos (cx, cz, hx, hz) de via dirigivel."""
    rects = []
    lines = street_lines()
    for j, lat in enumerate(lines):
        hw = (AVENUE_W if j == AVENUE_J else STREET_W) * 0.5
        rects.append((None, lat, None, hw, "EW", hw))  # expandido abaixo
    # EW: faixa completa x in (X_MIN, X_MAX), z ± hw
    out = []
    for j, lat in enumerate(lines):
        hw = (AVENUE_W if j == AVENUE_J else STREET_W) * 0.5
        out.append(("EW", (X_MIN + X_MAX) * 0.5, lat, (X_MAX - X_MIN) * 0.5, hw))
    for lat in lines:
        out.append(("NS", lat, (Z_MIN + Z_MAX) * 0.5, STREET_W * 0.5, (Z_MAX - Z_MIN) * 0.5))
    for sx in OBD_XS:
        out.append(("NS", sx, (Z_MIN + Z_MAX) * 0.5, STREET_W * 0.5, (Z_MAX - Z_MIN) * 0.5))
    return out


def on_roadway(x, z):
    lines = street_lines()
    for j, lat in enumerate(lines):
        hw = (AVENUE_W if j == AVENUE_J else STREET_W) * 0.5
        if abs(z - lat) < hw and X_MIN < x < X_MAX:
            return True
    for lat in lines:
        if abs(x - lat) < STREET_W * 0.5 and Z_MIN < z < Z_MAX:
            return True
    for sx in OBD_XS:
        if abs(x - sx) < STREET_W * 0.5 and Z_MIN < z < Z_MAX:
            return True
    return False


def collider_hits_road(cb):
    """cb = [cx,cy,cz,sx,sy,sz] — colisor intersecta corredor de via em altura de carro?"""
    cx, cy, cz, sx, sy, sz = cb
    if cy + sy * 0.5 < 0.05:      # enterrado
        return False
    if cy - sy * 0.5 > 3.0:       # acima da altura de veiculo
        return False
    ex, ez = sx * 0.5, sz * 0.5
    for _, rcx, rcz, rhx, rhz in road_corridors():
        if abs(cx - rcx) < ex + rhx and abs(cz - rcz) < ez + rhz:
            return True
    return False


def load(path):
    with open(path, "r", encoding="utf8") as f:
        return json.load(f)


def index_by(objects):
    by = defaultdict(list)
    for o in objects:
        by[o["name"]].append(o)
    return by


def near(objs, pos, tol=1.5):
    x, _, z = pos
    best, bd = None, tol
    for o in objs:
        d = math.hypot(o["pos"][0] - x, o["pos"][2] - z)
        if d < bd:
            best, bd = o, d
    return best


def branch_bounds(objs):
    """Union dos bounds de renderer de uma lista de objetos."""
    xs0 = ys0 = zs0 = 1e9
    xs1 = ys1 = zs1 = -1e9
    any_r = False
    for o in objs:
        r = o.get("rend")
        if not r:
            continue
        cx, cy, cz, sx, sy, sz = r["b"]
        any_r = True
        xs0 = min(xs0, cx - sx / 2); xs1 = max(xs1, cx + sx / 2)
        ys0 = min(ys0, cy - sy / 2); ys1 = max(ys1, cy + sy / 2)
        zs0 = min(zs0, cz - sz / 2); zs1 = max(zs1, cz + sz / 2)
    if not any_r:
        return None
    return (xs0, ys0, zs0, xs1, ys1, zs1)


def vol(b):
    return max(0.0, b[3] - b[0]) * max(0.0, b[4] - b[1]) * max(0.0, b[5] - b[2])


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--pre", required=True)
    ap.add_argument("--post", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--json", default=None)
    args = ap.parse_args()

    pre, post = load(args.pre), load(args.post)
    pre_by, post_by = index_by(pre), index_by(post)

    # ----- objetos adicionados / removidos -----
    added_names = set(post_by) - set(pre_by)
    removed_names = set(pre_by) - set(post_by)

    def group_count(names, idx):
        return sum(len(idx[n]) for n in names)

    # ----- colisores: por objeto em ambos -----
    collider_added = []   # (obj_post, collider)
    collider_removed = []
    matched = 0
    for name in set(pre_by) & set(post_by):
        used = set()
        for po in post_by[name]:
            cand = near([o for i, o in enumerate(pre_by[name]) if i not in used], po["pos"])
            if cand is None:
                continue
            used.add(pre_by[name].index(cand))
            matched += 1
            pc_keys = [(c["t"], tuple(round(v, 2) for v in c["b"])) for c in cand.get("cols", [])]
            for c in po.get("cols", []):
                key = (c["t"], tuple(round(v, 2) for v in c["b"]))
                if key not in pc_keys:
                    collider_added.append((po, c))
            for c in cand.get("cols", []):
                key = (c["t"], tuple(round(v, 2) for v in c["b"]))
                if key not in [(x["t"], tuple(round(v, 2) for v in x["b"])) for x in po.get("cols", [])]:
                    collider_removed.append((po, c))

    # objetos NOVOS que ja nascem com collider (ex: *_BoundsCollider criados)
    for name in added_names:
        for po in post_by[name]:
            for c in po.get("cols", []):
                collider_added.append((po, c))

    # ----- classificacao geometrica dos colisores ADICIONADOS -----
    # arvore por path para achar irmaos/filhos
    post_paths = {o["path"]: o for o in post}
    rows = []
    for po, c in collider_added:
        cb = c["b"]
        path = po["path"]
        parent_path = "/".join(path.split("/")[:-1])
        # visual do ramo: filhos do proprio objeto + irmaos (caso _BoundsCollider)
        branch = [o for p, o in post_paths.items()
                  if p == path or p.startswith(path + "/")]
        vis = branch_bounds(branch)
        if po["name"].endswith("_BoundsCollider") and parent_path:
            sib = [o for p, o in post_paths.items()
                   if p.startswith(parent_path + "/") and not p.startswith(path)
                   and not o["name"].endswith("_BoundsCollider")]
            sb = branch_bounds(sib)
            if sb:
                vis = (min(vis[0], sb[0]) if vis else sb[0], sb[1], sb[2],
                       max(vis[3], sb[3]) if vis else sb[3], sb[4], sb[5]) if vis else sb
                vis = branch_bounds(sib + branch)
        has_visual = vis is not None and vol(vis) > 0.01
        flat_visual = has_visual and (vis[4] - vis[1]) < 0.25 and (vis[3] - vis[0]) > 2.0
        if not has_visual:
            verdict = "LIXO-sem-visual"
        elif flat_visual:
            verdict = "LIXO-decal/plano-com-colisor"
        else:
            # inflacao: extensao do colisor vs extensao visual por eixo
            ext_c = [cb[3], cb[4], cb[5]]
            ext_v = [vis[3] - vis[0], vis[4] - vis[1], vis[5] - vis[2]]
            ratio = [ext_c[i] / ext_v[i] if ext_v[i] > 0.01 else 0 for i in range(3)]
            xz_ratio = max(ratio[0], ratio[2])
            if xz_ratio > 1.10:
                verdict = f"INFLADO(x{xz_ratio:.2f})"
            else:
                verdict = "OK"
        rows.append({
            "path": path, "pos": po["pos"], "type": c["t"],
            "cb": cb, "trigger": c.get("trig", 0), "on": c.get("on", 1),
            "verdict": verdict, "on_road": collider_hits_road(cb),
        })

    # ----- materiais de pisos grandes (placas planas largas = via/calçada) -----
    mat_changes = []
    for name in set(pre_by) & set(post_by):
        for po in post_by[name]:
            cand = near(pre_by[name], po["pos"])
            if not cand:
                continue
            r, cr = po.get("rend"), cand.get("rend")
            if not r or not cr:
                continue
            _, _, _, sx, sy, sz = r["b"]
            if sy < 0.3 and max(sx, sz) > 8:   # placa plana larga
                if r["mats"] != cr["mats"]:
                    mat_changes.append((po["path"], po["pos"], cr["mats"], r["mats"]))

    # ----- relatorio -----
    os.makedirs(os.path.dirname(args.out), exist_ok=True)
    L = []
    L.append("# M1 — Mapa de dano entre revisões da cena\n")
    L.append(f"- PRE: `{os.path.basename(args.pre)}` · POST: `{os.path.basename(args.post)}`")
    L.append(f"- Objetos: PRE={len(pre)} POST={len(post)} · "
             f"adicionados(+nomes únicos)={len(added_names)} ({group_count(added_names, post_by)} instâncias) · "
             f"removidos={len(removed_names)} ({group_count(removed_names, pre_by)} instâncias) · "
             f"pareados={matched}")
    L.append(f"- Colisores ADICIONADOS no POST: **{len(collider_added)}** · removidos: {len(collider_removed)}\n")

    L.append("## Colisores adicionados — classificação por geometria\n")
    L.append("| Veredito | Qtd | Na via | Exemplos |")
    L.append("|---|---|---|---|")
    by_verdict = defaultdict(list)
    for r in rows:
        by_verdict[r["verdict"]].append(r)
    for v in sorted(by_verdict, key=lambda k: -len(by_verdict[k])):
        rs = by_verdict[v]
        onr = sum(1 for r in rs if r["on_road"])
        ex = "; ".join(f"{r['path'].split('/')[-1]}@({r['pos'][0]:.0f},{r['pos'][2]:.0f})"
                       for r in rs[:4])
        L.append(f"| {v} | {len(rs)} | {onr} | {ex} |")

    L.append("\n## Detalhe — colisores adicionados QUE TOCAM A VIA (bloqueio potencial)\n")
    L.append("| Objeto | Pos | Tipo | Bounds (cx,cy,cz / sx,sy,sz) | Veredito |")
    L.append("|---|---|---|---|---|")
    for r in sorted((r for r in rows if r["on_road"]), key=lambda r: r["verdict"]):
        cb = r["cb"]
        L.append(f"| `{r['path']}` | ({r['pos'][0]:.1f},{r['pos'][2]:.1f}) | {r['type']} | "
                 f"({cb[0]:.1f},{cb[1]:.1f},{cb[2]:.1f} / {cb[3]:.1f},{cb[4]:.1f},{cb[5]:.1f}) | {r['verdict']} |")

    L.append("\n## Detalhe — colisores adicionados FORA da via (inventário completo)\n")
    off = [r for r in rows if not r["on_road"]]
    L.append(f"{len(off)} itens (tabela completa no JSON)")
    for r in off[:40]:
        cb = r["cb"]
        L.append(f"- `{r['path']}` @({r['pos'][0]:.1f},{r['pos'][2]:.1f}) {r['type']} "
                 f"[{cb[3]:.1f}x{cb[4]:.1f}x{cb[5]:.1f}] → {r['verdict']}")

    L.append("\n## Materiais trocados em placas planas largas (via/calçada)\n")
    if mat_changes:
        L.append("| Objeto | Pos | Antes | Depois |")
        L.append("|---|---|---|---|")
        for path, pos, a, d in mat_changes[:60]:
            L.append(f"| `{path}` | ({pos[0]:.1f},{pos[2]:.1f}) | {a} | {d} |")
    else:
        L.append("(nenhum)")

    L.append("\n## Nomes de objetos adicionados (agregado por nome)\n")
    for n in sorted(added_names, key=lambda n: -len(post_by[n]))[:50]:
        L.append(f"- `{n}` × {len(post_by[n])}")

    with open(args.out, "w", encoding="utf8") as f:
        f.write("\n".join(L))

    if args.json:
        with open(args.json, "w", encoding="utf8") as f:
            json.dump({"rows": rows,
                       "mat_changes": [list(m) for m in mat_changes],
                       "added": sorted(added_names), "removed": sorted(removed_names)},
                      f, indent=1, ensure_ascii=False)

    print(f"[diff] colisores adicionados={len(collider_added)} "
          f"(na via={sum(1 for r in rows if r['on_road'])}) · trocas de material em pisos={len(mat_changes)}")
    print(f"[diff] relatorio: {args.out}")


if __name__ == "__main__":
    main()
