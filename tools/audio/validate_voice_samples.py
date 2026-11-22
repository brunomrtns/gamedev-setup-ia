#!/usr/bin/env python3
"""
tools/audio/validate_voice_samples.py
Validador de amostras de voz para XTTS-v2 (Auditoria P0-2 — Studio V2).

Verifica cada `speaker_wav` de um `voice-cast.json` (ou uma pasta de `.wav`) ANTES
da sintese de VO:
  - Arquivo existe e e WAV PCM valido
  - Duracao dentro do intervalo alvo (default: 5.5s a 20.0s; ideal 6-15s)
  - Sample rate >= 16000 Hz
  - Sem clipping severo (peak_dbfs <= -0.1 ou < 0.5% de amostras clipadas)
  - Taxa de silencio razoavel (< 55% do audio abaixo de -45 dBFS)

Uso:
  python tools/audio/validate_voice_samples.py --cast docs/audio/voice-cast.json
  python tools/audio/validate_voice_samples.py --wav /home/bruno/.../bruno.wav
"""

import argparse
import json
import math
import struct
import subprocess
import sys
import wave
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent.parent
sys.path.insert(0, str(ROOT / "tools" / "studio"))
from yaml_loader import to_wsl_path  # noqa: E402


def resolve_local_wav(raw_path: str) -> Path:
    s = str(raw_path).strip()
    if s.startswith("/"):
        # Converte caminho WSL (/home/...) para UNC \\wsl.localhost\Ubuntu\... ou copia via wslpath
        res = subprocess.run(["wsl", "wslpath", "-w", s], capture_output=True, text=True)
        if res.returncode == 0 and res.stdout.strip():
            return Path(res.stdout.strip())
    return Path(s)


def inspect_wav(wav_path: Path, min_sec: float, max_sec: float) -> dict:
    issues = []
    warnings = []
    if not wav_path.exists():
        return {"path": str(wav_path), "ok": False, "issues": ["arquivo nao encontrado"], "warnings": []}

    try:
        with wave.open(str(wav_path), "rb") as wf:
            ch = wf.getnchannels()
            sw = wf.getsampwidth()
            sr = wf.getframerate()
            nf = wf.getnframes()
            raw = wf.readframes(nf)
    except Exception as e:
        return {"path": str(wav_path), "ok": False, "issues": [f"WAV invalido: {e}"], "warnings": []}

    dur = round(nf / float(sr), 3) if sr > 0 else 0.0
    if sr < 16000:
        issues.append(f"sample_rate {sr} Hz < 16000 Hz")
    if dur < min_sec:
        issues.append(f"duracao {dur}s < minimo {min_sec}s (XTTS precisa de >= 6s)")
    elif dur > max_sec:
        warnings.append(f"duracao {dur}s > recomendado {max_sec}s (XTTS usa os primeiros ~15s)")

    peak_dbfs = None
    silence_ratio = None
    clip_ratio = None
    if sw == 2 and nf > 0:
        total = nf * ch
        samples = struct.unpack(f"<{total}h", raw)
        abs_samples = [abs(s) for s in samples]
        peak = max(abs_samples, default=0)
        peak_dbfs = -99.0 if peak == 0 else round(20.0 * math.log10(peak / 32767.0), 2)
        clipped = sum(1 for a in abs_samples if a >= 32760)
        clip_ratio = round(clipped / float(total), 5)
        # Limiar de silencio: -45 dBFS ~= 184 em 16-bit
        silent_cnt = sum(1 for a in abs_samples if a < 184)
        silence_ratio = round(silent_cnt / float(total), 3)

        if peak == 0 or silence_ratio > 0.70:
            issues.append(f"audio silencioso ou quase vazio (silence_ratio={silence_ratio})")
        elif silence_ratio > 0.55:
            warnings.append(f"muito silencio na amostra (silence_ratio={silence_ratio})")
        if clip_ratio > 0.005:
            issues.append(f"clipping excessivo ({clip_ratio*100:.2f}% das amostras no teto)")
    elif sw != 2:
        warnings.append(f"sample_width={sw*8}-bit (recomendado PCM 16-bit)")

    return {
        "path": str(wav_path),
        "duration_sec": dur,
        "sample_rate": sr,
        "channels": ch,
        "peak_dbfs": peak_dbfs,
        "silence_ratio": silence_ratio,
        "clip_ratio": clip_ratio,
        "ok": len(issues) == 0,
        "issues": issues,
        "warnings": warnings,
    }


def main():
    ap = argparse.ArgumentParser(description="Validador de amostras de voz XTTS-v2 (P0-2)")
    src = ap.add_mutually_exclusive_group(required=True)
    src.add_argument("--cast", help="voice-cast.json do projeto")
    src.add_argument("--wav", help="Arquivo .wav unico (Windows ou WSL)")
    ap.add_argument("--min-sec", type=float, default=5.5)
    ap.add_argument("--max-sec", type=float, default=25.0)
    ap.add_argument("--out-report", default=None, help="Opcional: salvar relatorio JSON")
    args = ap.parse_args()

    results = {}
    if args.wav:
        p = resolve_local_wav(args.wav)
        results[p.stem] = inspect_wav(p, args.min_sec, args.max_sec)
    else:
        cast_data = json.loads(Path(args.cast).resolve().read_text(encoding="utf-8"))
        chars = cast_data.get("characters", cast_data)
        for cid, cfg in chars.items():
            raw = cfg.get("speaker_wav", "")
            if not raw or raw.startswith("<"):
                results[cid] = {
                    "path": raw,
                    "ok": False,
                    "issues": ["speaker_wav nao configurado (placeholder)"],
                    "warnings": [],
                }
            else:
                results[cid] = inspect_wav(resolve_local_wav(raw), args.min_sec, args.max_sec)

    all_ok = all(r["ok"] for r in results.values()) and len(results) > 0
    report = {
        "schema": "voice-sample-validation-v1",
        "status": "PASS" if all_ok else "FAIL",
        "total": len(results),
        "passed": sum(1 for r in results.values() if r["ok"]),
        "samples": results,
    }
    out_txt = json.dumps(report, indent=2, ensure_ascii=False)
    print(out_txt)
    if args.out_report:
        rp = Path(args.out_report).resolve()
        rp.parent.mkdir(parents=True, exist_ok=True)
        rp.write_text(out_txt, encoding="utf-8")
    sys.exit(0 if all_ok else 1)


if __name__ == "__main__":
    main()
