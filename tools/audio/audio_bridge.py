#!/usr/bin/env python3
"""
tools/audio/audio_bridge.py
Ponte de SFX & Musica 100% Local (Studio V2 — Fase 6).

Consome uma pasta local de audios brutos (CC0, foley gravado ou loops de musica),
normaliza loudness via ffmpeg EBU R128 (`-16 LUFS / -1 dBTP` para SFX;
`-18 LUFS / -1.5 dBTP` para musica), exporta WAV (SFX) ou OGG (musica),
gera `audio_manifest.json` e grava evidencia em `evidence/audio/audio_bridge_<pack>.json`.

Zero APIs externas, 0 MB de modelos baixados. Usa ffmpeg local ou via WSL.
"""

import argparse
import json
import math
import shutil
import struct
import subprocess
import sys
import wave
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent.parent
sys.path.insert(0, str(ROOT / "tools" / "studio"))
from yaml_loader import to_wsl_path  # noqa: E402

AUDIO_EXTS = {".wav", ".mp3", ".ogg", ".flac", ".m4a"}


def detect_ffmpeg() -> str:
    if shutil.which("ffmpeg"):
        return "native"
    res = subprocess.run(["wsl", "which", "ffmpeg"], capture_output=True, text=True)
    if res.returncode == 0 and res.stdout.strip():
        return "wsl"
    return "none"


def measure_wav(path: Path) -> dict:
    with wave.open(str(path), "rb") as wf:
        ch = wf.getnchannels()
        sw = wf.getsampwidth()
        sr = wf.getframerate()
        nf = wf.getnframes()
        raw = wf.readframes(nf)
    dur = round(nf / float(sr), 3) if sr > 0 else 0.0
    peak_dbfs = None
    silent = False
    if sw == 2 and nf > 0:
        samples = struct.unpack(f"<{nf * ch}h", raw)
        peak = max((abs(s) for s in samples), default=0)
        if peak == 0:
            peak_dbfs = -99.0
            silent = True
        else:
            peak_dbfs = round(20.0 * math.log10(peak / 32767.0), 2)
    return {
        "duration_sec": dur,
        "sample_rate": sr,
        "channels": ch,
        "peak_dbfs": peak_dbfs,
        "silent": silent,
    }


def normalize_file(src: Path, dst: Path, kind: str, ffmpeg_mode: str) -> dict:
    target_i = -16.0 if kind == "sfx" else -18.0
    target_tp = -1.0 if kind == "sfx" else -1.5
    af = f"loudnorm=I={target_i}:TP={target_tp}:LRA=11"

    dst.parent.mkdir(parents=True, exist_ok=True)
    if ffmpeg_mode == "native":
        cmd = ["ffmpeg", "-y", "-i", str(src), "-af", af, "-ar", "44100", str(dst)]
    elif ffmpeg_mode == "wsl":
        cmd = ["wsl", "ffmpeg", "-y", "-i", to_wsl_path(src), "-af", af, "-ar", "44100", to_wsl_path(dst)]
    else:
        raise RuntimeError("ffmpeg nao encontrado nem no Windows nem no WSL")

    res = subprocess.run(cmd, capture_output=True, text=True)
    if res.returncode != 0:
        raise RuntimeError(f"ffmpeg falhou em {src.name}: {res.stderr[-400:]}")

    info = {
        "id": src.stem,
        "source": src.name,
        "output_file": dst.name,
        "kind": kind,
        "loop": kind == "music",
        "target_lufs": target_i,
        "target_tp_dbtp": target_tp,
        "size_bytes": dst.stat().st_size,
    }
    if dst.suffix.lower() == ".wav":
        info.update(measure_wav(dst))
    else:
        # Mede duracao via ffprobe
        probe_cmd = (
            ["ffprobe", "-v", "error", "-show_entries", "format=duration", "-of", "default=nw=1:nk=1", str(dst)]
            if ffmpeg_mode == "native"
            else ["wsl", "ffprobe", "-v", "error", "-show_entries", "format=duration", "-of", "default=nw=1:nk=1", to_wsl_path(dst)]
        )
        pr = subprocess.run(probe_cmd, capture_output=True, text=True)
        try:
            info["duration_sec"] = round(float(pr.stdout.strip()), 3)
        except Exception:
            info["duration_sec"] = 0.0
        info["silent"] = info["duration_sec"] <= 0.05
    return info


def main():
    ap = argparse.ArgumentParser(description="Ponte de SFX & Musica Local (EBU R128)")
    ap.add_argument("--input-dir", required=True, help="Pasta com arquivos de audio brutos (CC0/foley)")
    ap.add_argument("--pack", required=True, help="Nome do pacote (ex.: caldeiral_sfx, tema_oficina)")
    ap.add_argument("--kind", default="sfx", choices=["sfx", "music"])
    ap.add_argument("--out-dir", default=None, help="Pasta de saida (default: unity/Assets/Game/Audio/<SFX|Music>/<pack>)")
    args = ap.parse_args()

    in_dir = Path(args.input_dir).resolve()
    if not in_dir.exists():
        print(f"[audio_bridge] Pasta de entrada nao encontrada: {in_dir}")
        sys.exit(2)

    files = sorted(f for f in in_dir.iterdir() if f.is_file() and f.suffix.lower() in AUDIO_EXTS)
    if not files:
        print(f"[audio_bridge] Nenhum arquivo de audio encontrado em {in_dir}")
        sys.exit(2)

    ffmpeg_mode = detect_ffmpeg()
    if ffmpeg_mode == "none":
        print("[audio_bridge] ffmpeg nao disponivel no Windows nem no WSL")
        sys.exit(2)

    sub = "SFX" if args.kind == "sfx" else "Music"
    out_dir = Path(args.out_dir).resolve() if args.out_dir else \
        ROOT / "unity" / "Assets" / "Game" / "Audio" / sub / args.pack
    out_dir.mkdir(parents=True, exist_ok=True)

    ext = ".wav" if args.kind == "sfx" else ".ogg"
    clips = []
    errors = []
    for f in files:
        dst = out_dir / f"{f.stem}{ext}"
        try:
            info = normalize_file(f, dst, args.kind, ffmpeg_mode)
            if info.get("silent") or info.get("duration_sec", 0) < 0.05:
                errors.append(f"{f.name}: audio silencioso ou curto demais")
            else:
                clips.append(info)
        except Exception as e:
            errors.append(str(e))

    manifest = {
        "pack": args.pack,
        "kind": args.kind,
        "ffmpeg_mode": ffmpeg_mode,
        "total_clips": len(clips),
        "clips": clips,
        "errors": errors,
    }
    (out_dir / "audio_manifest.json").write_text(
        json.dumps(manifest, indent=2, ensure_ascii=False), encoding="utf-8"
    )

    ev_dir = ROOT / "evidence" / "audio"
    ev_dir.mkdir(parents=True, exist_ok=True)
    ev_path = ev_dir / f"audio_bridge_{args.pack}.json"
    ev_path.write_text(json.dumps(manifest, indent=2, ensure_ascii=False), encoding="utf-8")

    print(json.dumps(manifest, indent=2, ensure_ascii=False))
    if errors:
        sys.exit(1)


if __name__ == "__main__":
    main()
