#!/usr/bin/env python3
"""
tools/audio/tts_xtts_worker.py
Etapa WSL (XTTS-v2 CUDA) da ponte de vozes do Studio V2 (Fase 2).

Carrega o modelo XTTS-v2 (ja em cache em ~/.local/share/tts) uma unica vez na GPU
e sintetiza todas as falas de um roteiro dialogues.json a partir de um elenco
definido em voice-cast.json (vozes gravadas pelo dono ou amostras prontas).

Imprime REPORT_JSON:{...} na ultima linha (padrao dos tools do studio) e grava
vo_manifest.json com a duracao exata em segundos de cada fala (contrato direto
para o CutsceneBuilder da Fase 3 e legendas no Unity).
"""

import argparse
import json
import math
import os
import struct
import sys
import wave
from pathlib import Path

os.environ.setdefault("COQUI_TOS_AGREED", "1")


def analyze_and_normalize_wav(wav_path: Path, target_peak_db: float = -1.0) -> dict:
    """Le um WAV PCM 16-bit, mede duracao/pico e aplica normalizacao suave de pico."""
    with wave.open(str(wav_path), "rb") as wf:
        n_channels = wf.getnchannels()
        sampwidth = wf.getsampwidth()
        framerate = wf.getframerate()
        n_frames = wf.getnframes()
        raw = wf.readframes(n_frames)

    duration = n_frames / float(framerate) if framerate > 0 else 0.0
    if sampwidth != 2 or n_frames == 0:
        return {
            "duration_sec": round(duration, 3),
            "sample_rate": framerate,
            "channels": n_channels,
            "peak_dbfs": None,
            "normalized": False,
        }

    total_samples = n_frames * n_channels
    samples = list(struct.unpack(f"<{total_samples}h", raw))
    peak = max((abs(s) for s in samples), default=0)
    if peak == 0:
        return {
            "duration_sec": round(duration, 3),
            "sample_rate": framerate,
            "channels": n_channels,
            "peak_dbfs": -99.0,
            "normalized": False,
            "silent": True,
        }

    target_amp = int(32767 * (10.0 ** (target_peak_db / 20.0)))
    gain = target_amp / float(peak)
    # Evita amplificar ruido se ja estiver perto do alvo; aplica ganho limitado
    gain = min(gain, 6.0)
    norm_samples = [max(-32768, min(32767, int(round(s * gain)))) for s in samples]
    new_peak = max((abs(s) for s in norm_samples), default=1)
    peak_dbfs = round(20.0 * math.log10(new_peak / 32767.0), 2)

    with wave.open(str(wav_path), "wb") as wf:
        wf.setnchannels(n_channels)
        wf.setsampwidth(2)
        wf.setframerate(framerate)
        wf.writeframes(struct.pack(f"<{total_samples}h", *norm_samples))

    return {
        "duration_sec": round(duration, 3),
        "sample_rate": framerate,
        "channels": n_channels,
        "peak_dbfs": peak_dbfs,
        "normalized": True,
        "silent": False,
    }


def main():
    ap = argparse.ArgumentParser(description="Worker WSL XTTS-v2 do Studio V2")
    ap.add_argument("--cast", required=True, help="JSON de elenco de vozes (voice-cast.json)")
    ap.add_argument("--dialogues", required=True, help="JSON de falas da cena")
    ap.add_argument("--out-dir", required=True, help="Diretorio de saida dos WAVs + vo_manifest.json")
    ap.add_argument("--scene", required=True, help="Nome/slug da cena")
    ap.add_argument("--model", default="tts_models/multilingual/multi-dataset/xtts_v2")
    ap.add_argument("--device", default="auto", choices=["auto", "cuda", "cpu"])
    args = ap.parse_args()

    cast_data = json.loads(Path(args.cast).read_text(encoding="utf-8"))
    cast = cast_data.get("characters", cast_data)
    dlg_data = json.loads(Path(args.dialogues).read_text(encoding="utf-8"))
    lines = dlg_data.get("lines", dlg_data if isinstance(dlg_data, list) else [])

    if not lines:
        print("[tts_xtts_worker] Nenhuma fala encontrada em --dialogues", file=sys.stderr)
        sys.exit(2)

    out_dir = Path(args.out_dir)
    out_dir.mkdir(parents=True, exist_ok=True)

    import torch
    from TTS.api import TTS

    use_gpu = (args.device == "cuda") or (args.device == "auto" and torch.cuda.is_available())
    device = "cuda" if use_gpu else "cpu"
    print(f"[tts_xtts_worker] Carregando {args.model} em {device}...")
    tts = TTS(model_name=args.model, progress_bar=False).to(device)

    manifest_lines = []
    errors = []

    for idx, line in enumerate(lines, start=1):
        line_id = line.get("id") or f"{args.scene}_{idx:03d}"
        char_id = line["character"]
        text = line["text"].strip()
        if not text:
            errors.append(f"{line_id}: texto vazio")
            continue

        char_cfg = cast.get(char_id)
        if not char_cfg:
            errors.append(f"{line_id}: personagem '{char_id}' nao definido no voice-cast")
            continue

        language = line.get("language") or char_cfg.get("language", "pt")
        speaker_wav = char_cfg.get("speaker_wav")
        if not speaker_wav or not Path(speaker_wav).exists():
            errors.append(f"{line_id}: speaker_wav ausente para '{char_id}' ({speaker_wav})")
            continue

        out_wav = out_dir / f"{line_id}.wav"
        print(f"[tts_xtts_worker] ({idx}/{len(lines)}) {line_id} [{char_id}/{language}]: {text[:60]}")
        tts.tts_to_file(
            text=text,
            speaker_wav=str(speaker_wav),
            language=language,
            file_path=str(out_wav),
        )

        stats = analyze_and_normalize_wav(out_wav)
        if stats.get("silent") or stats["duration_sec"] < 0.15:
            errors.append(f"{line_id}: audio silencioso ou curto demais ({stats['duration_sec']}s)")
            continue

        manifest_lines.append({
            "id": line_id,
            "character": char_id,
            "display_name": char_cfg.get("display_name", char_id),
            "language": language,
            "text": text,
            "wav_file": out_wav.name,
            "duration_sec": stats["duration_sec"],
            "sample_rate": stats["sample_rate"],
            "peak_dbfs": stats["peak_dbfs"],
        })

    manifest = {
        "scene": args.scene,
        "model": args.model,
        "device": device,
        "total_lines": len(manifest_lines),
        "total_duration_sec": round(sum(l["duration_sec"] for l in manifest_lines), 3),
        "lines": manifest_lines,
        "errors": errors,
    }
    (out_dir / "vo_manifest.json").write_text(
        json.dumps(manifest, indent=2, ensure_ascii=False), encoding="utf-8"
    )

    print("REPORT_JSON:" + json.dumps(manifest, ensure_ascii=False))
    if errors:
        sys.exit(1)


if __name__ == "__main__":
    main()
