#!/usr/bin/env python3
"""v38_road_mats.py — inventario de materiais nos pisos de via (por geometria:
placas planas <0.3m de altura e >8m de largura)."""
import collections
import json
import sys

objs = json.load(open(sys.argv[1], encoding="utf8"))
mats = collections.Counter()
rows = collections.defaultdict(list)
for o in objs:
    r = o.get("rend")
    if not r:
        continue
    _, _, _, sx, sy, sz = r["b"]
    if sy < 0.3 and max(sx, sz) > 8:
        for m in r["mats"].split("|"):
            mats[m] += 1
            rows[m].append(f"{o['name']}@({o['pos'][0]:.0f},{o['pos'][2]:.0f})")
print("== Materiais em placas de via (plana+larga):")
for m, n in mats.most_common():
    print(f"{n:4d}  {m}")
print("\n== Amostra por material:")
for m in mats:
    print(f"\n{m}:")
    for s in rows[m][:6]:
        print("   ", s)
