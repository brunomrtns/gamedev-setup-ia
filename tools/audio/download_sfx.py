#!/usr/bin/env python3
"""
download_sfx.py — baixa SFX do Freesound por categoria para o jogo.

Rode no WSL (venv game-ai3d tem requests+dotenv):
    wsl /home/bruno/.venvs/game-ai3d/bin/python tools/audio/download_sfx.py \
        --dotenv /home/bruno/Desenvolvimento/brunointegrations/video-generate/.env \
        --out /mnt/c/Users/Bruno/workspace/gamedev-setup-ia/unity/Assets/Game/Audio/SFX

Categorias: explosion, engine_loop, crash, impact_metal, glass, horn,
gunshot, siren. Cada uma baixa 2 amostras CC0/CC-BY.
"""
import argparse, os, sys, time
from pathlib import Path

SFX_QUERIES = {
    "explosion":    ["explosion big", "explosion debris"],
    "engine_loop":  ["car engine idle loop", "engine acceleration"],
    "crash":        ["car crash metal", "vehicle crash impact"],
    "impact_metal": ["metal impact clang", "metal hit loud"],
    "glass":        ["glass break shatter", "glass smash"],
    "horn":         ["car horn honk", "car horn angry"],
    "gunshot":      ["gunshot rifle", "ak47 shot"],
    "siren":        ["police siren wail", "siren distant"],
}


def main():
    p = argparse.ArgumentParser()
    p.add_argument("--dotenv", required=True)
    p.add_argument("--out", required=True)
    p.add_argument("--per", type=int, default=2)
    a = p.parse_args()

    token = None
    for line in open(a.dotenv, encoding="utf-8", errors="ignore"):
        if line.startswith("FS_SECRET_KEY"):
            token = line.split("=", 1)[1].strip().strip('"').strip("'")
    if not token:
        print("[sfx] FS_SECRET_KEY ausente no .env"); sys.exit(1)

    import requests
    out = Path(a.out); out.mkdir(parents=True, exist_ok=True)
    base = "https://freesound.org/apiv2"
    hdr = {"Authorization": f"Token {token}"}

    for cat, queries in SFX_QUERIES.items():
        got = 0
        for q in queries:
            if got >= a.per: break
            try:
                r = requests.get(f"{base}/search/text/",
                    params={"query": q, "fields": "id,name,previews,license",
                            "page_size": 8, "sort": "score"},
                    headers=hdr, timeout=15).json()
                res = r.get("results", [])
                if not res: print(f"[sfx]   '{q}': 0 resultados ({r})")
                for s in res:
                    prev = s.get("previews", {})
                    url = prev.get("preview-hq-mp3") or prev.get("preview-lq-mp3")
                    if not url: continue
                    fp = out / f"{cat}_{s['id']}.mp3"
                    if fp.exists(): got += 1; continue
                    data = requests.get(url, headers=hdr, timeout=30).content
                    fp.write_bytes(data)
                    print(f"[sfx] {cat}: {s['name'][:50]} -> {fp.name}")
                    got += 1
                    time.sleep(0.4)  # rate limit
                    if got >= a.per: break
            except Exception as e:
                print(f"[sfx] {cat}/{q} erro: {e}")
            time.sleep(0.6)
        print(f"[sfx] {cat}: {got} amostras")


if __name__ == "__main__":
    main()
