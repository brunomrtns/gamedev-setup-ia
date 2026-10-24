#!/usr/bin/env python3
"""v38_corridor_audit.py — auditoria estatica de corredores de via.

Lista TODO colisor que intersecta um corredor de via em altura de veiculo
(fundo < 3 m), classificando:
  PISO     — colisor-fino junto ao chao (topo < 1 m): a propria via
  OBSTACULO-COM-VISUAL — topo >= 1 m e renderer proximo ( poste/muro real )
  OBSTACULO-SEM-VISUAL — topo >= 1 m sem renderer: colisao invisivel
Uso: python tools/qa/v38_corridor_audit.py evidence/qa/v38/inv_head.json
"""
import json
import math
import sys
from collections import defaultdict

LINES = [(i - 2.5) * 52.0 for i in range(6)]
OBD_XS = [182.0, 234.0]
X_MIN, X_MAX = -147.0, 284.0
Z_MIN, Z_MAX = -147.0, 147.0


def corridors():
    out = []
    for j, lat in enumerate(LINES):
        hw = (16.0 if j == 3 else 12.0) * 0.5
        out.append(("EW", (X_MIN + X_MAX) / 2, lat, (X_MAX - X_MIN) / 2, hw))
    for lat in LINES:
        out.append(("NS", lat, (Z_MIN + Z_MAX) / 2, 6.0, (Z_MAX - Z_MIN) / 2))
    for sx in OBD_XS:
        out.append(("NS", sx, (Z_MIN + Z_MAX) / 2, 6.0, (Z_MAX - Z_MIN) / 2))
    return out


def main():
    objs = json.load(open(sys.argv[1], encoding="utf8"))
    paths = {o["path"]: o for o in objs}
    rows = []
    for o in objs:
        for c in o.get("cols", []):
            cx, cy, cz, sx, sy, sz = c["b"]
            if cy - sy / 2 > 3.0 or cy + sy / 2 < 0.05:
                continue
            ex, ez = sx / 2, sz / 2
            hit = None
            for _, rcx, rcz, rhx, rhz in corridors():
                if abs(cx - rcx) < ex + rhx and abs(cz - rcz) < ez + rhz:
                    hit = True
                    break
            if not hit:
                continue
            top = cy + sy / 2
            if top < 1.0:
                cls = "PISO"
            else:
                # visual: renderer no proprio ramo (filhos) ou irmaos nao-collider
                branch = [p for p in paths if p == o["path"] or p.startswith(o["path"] + "/")]
                parent = "/".join(o["path"].split("/")[:-1])
                if o["name"].endswith("_BoundsCollider") and parent:
                    branch += [p for p in paths
                               if p.startswith(parent + "/") and not p.startswith(o["path"])]
                vis = False
                near = (cx, cz)
                for p in branch:
                    r = paths[p].get("rend")
                    if not r:
                        continue
                    bx, by, bz, bsx, bsy, bsz = r["b"]
                    if math.hypot(bx - near[0], bz - near[1]) < 2.0:
                        vis = True
                        break
                cls = "OBSTACULO-COM-VISUAL" if vis else "OBSTACULO-SEM-VISUAL"
            rows.append((cls, o["name"], o["path"], cx, cz, sx, sy, sz))

    by = defaultdict(list)
    for r in rows:
        by[r[0]].append(r)
    print(f"total no corredor: {len(rows)}")
    for cls in by:
        print(f"\n== {cls}: {len(by[cls])}")
        agg = defaultdict(int)
        for r in by[cls]:
            agg[r[1]] += 1
        for n, k in sorted(agg.items(), key=lambda kv: -kv[1]):
            print(f"   {n} x{k}")
    inv = by.get("OBSTACULO-SEM-VISUAL", [])
    if inv:
        print("\n-- detalhe SEM-VISUAL (primeiros 30):")
        for r in inv[:30]:
            print(f"   {r[2]} @({r[3]:.1f},{r[4]:.1f}) [{r[5]:.1f}x{r[6]:.1f}x{r[7]:.1f}]")
    obs = by.get("OBSTACULO-COM-VISUAL", [])
    if obs:
        print("\n-- detalhe OBSTACULO-COM-VISUAL (primeiros 40):")
        for r in obs[:40]:
            print(f"   {r[2]} @({r[3]:.1f},{r[4]:.1f}) [{r[5]:.1f}x{r[6]:.1f}x{r[7]:.1f}]")


if __name__ == "__main__":
    main()
