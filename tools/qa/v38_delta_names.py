#!/usr/bin/env python3
"""Analisa o delta de contagem por nome entre dois inventarios."""
import collections
import json
import sys

pre = json.load(open(sys.argv[1], encoding="utf8"))
post = json.load(open(sys.argv[2], encoding="utf8"))
pre_c = collections.Counter(o["name"] for o in pre)
post_c = collections.Counter(o["name"] for o in post)
rows = []
for n in set(pre_c) | set(post_c):
    d = post_c[n] - pre_c[n]
    if d != 0:
        rows.append((d, n, pre_c[n], post_c[n]))
rows.sort()
print("DELTA  NAME  (pre->post)")
for d, n, p, q in rows:
    print(f"{d:+5d}  {n}  ({p}->{q})")
