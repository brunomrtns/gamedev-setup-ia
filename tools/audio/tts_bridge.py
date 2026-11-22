#!/usr/bin/env python3
"""
tools/audio/tts_bridge.py
Ponte TTS 100% Local (Windows -> WSL XTTS-v2 CUDA -> Unity VO) — Studio V2 Fase 2.

Orquestrador Windows em Python stdlib puro (mesmo padrao de fab3d_bridge.py):
  1. Le o elenco de vozes (--cast voice-cast.json) e o roteiro (--dialogues)
  2. Resolve caminhos de speaker_wav (Windows ou WSL) para caminhos /mnt/... ou /home/...
  3. Invoca a venv XTTS-v2 ja existente no WSL (0 MB de download)
  4. Grava os WAVs normalizados + vo_manifest.json em unity/Assets/Game/Audio/VO/<scene>/
  5. Grava evidencia em evidence/audio/tts_bridge_<scene>.json

Uso:
  python tools/audio/tts_bridge.py ^
      --cast docs/audio/voice-cast.json ^
      --dialogues docs/narrative/dialogues_intro.json ^
      --scene intro

Regra permanente: 100% local, somente C:\\ (D:\\ proibido). Nenhuma API externa.
"""

import argparse
import json
import os
import subprocess
import sys
import tempfile
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent.parent
WORKER_SCRIPT = Path(__file__).resolve().parent / "tts_xtts_worker.py"
DEFAULT_WSL_PYTHON = os.getenv(
    "STUDIO_WSL_TTS_PYTHON",
    "/home/bruno/Desenvolvimento/brunointegrations/video-generate/.venv/bin/python",
)


def to_wsl_path(raw_path: str) -> str:
    """Converte caminho Windows (C:\\...) para /mnt/c/...; mantem caminhos Linux (/home/...)."""
    s = str(raw_path).strip()
    if s.startswith("/"):
        return s
    p = Path(s).resolve()
    drive = p.drive.replace(":", "").lower()
    return f"/mnt/{drive}{p.as_posix().split(':', 1)[-1]}"


def parse_report(stdout: str) -> dict:
    for line in reversed(stdout.splitlines()):
        if line.startswith("REPORT_JSON:"):
            return json.loads(line[len("REPORT_JSON:"):])
    print("[tts_bridge] REPORT_JSON nao encontrado na saida do worker XTTS")
    sys.exit(1)


def main():
    ap = argparse.ArgumentParser(description="Ponte TTS Local (XTTS-v2 WSL -> Unity VO)")
    ap.add_argument("--cast", required=True, help="JSON do elenco de vozes (voice-cast.json)")
    ap.add_argument("--dialogues", required=True, help="JSON de falas da cena")
    ap.add_argument("--scene", required=True, help="Slug da cena (ex.: intro, santuario_01)")
    ap.add_argument("--out", default=None,
                    help="Diretorio de saida (default: unity/Assets/Game/Audio/VO/<scene>)")
    ap.add_argument("--wsl-python", default=DEFAULT_WSL_PYTHON,
                    help="Python da venv XTTS-v2 no WSL")
    ap.add_argument("--device", default="auto", choices=["auto", "cuda", "cpu"])
    args = ap.parse_args()

    cast_path = Path(args.cast).resolve()
    dlg_path = Path(args.dialogues).resolve()
    if not cast_path.exists():
        print(f"[tts_bridge] Elenco nao encontrado: {cast_path}")
        sys.exit(2)
    if not dlg_path.exists():
        print(f"[tts_bridge] Roteiro nao encontrado: {dlg_path}")
        sys.exit(2)

    # Prepara copia temporaria do cast com caminhos speaker_wav convertidos para WSL
    cast_data = json.loads(cast_path.read_text(encoding="utf-8"))
    chars = cast_data.get("characters", cast_data)
    resolved_chars = {}
    for cid, cfg in chars.items():
        c = dict(cfg)
        if c.get("speaker_wav"):
            c["speaker_wav"] = to_wsl_path(c["speaker_wav"])
        resolved_chars[cid] = c

    out_dir = Path(args.out).resolve() if args.out else \
        ROOT / "unity" / "Assets" / "Game" / "Audio" / "VO" / args.scene
    out_dir.mkdir(parents=True, exist_ok=True)

    with tempfile.NamedTemporaryFile("w", suffix=".json", delete=False, encoding="utf-8") as tf:
        json.dump({"characters": resolved_chars}, tf, indent=2, ensure_ascii=False)
        resolved_cast_path = Path(tf.name)

    try:
        cmd = [
            "wsl", args.wsl_python, to_wsl_path(WORKER_SCRIPT),
            "--cast", to_wsl_path(resolved_cast_path),
            "--dialogues", to_wsl_path(dlg_path),
            "--out-dir", to_wsl_path(out_dir),
            "--scene", args.scene,
            "--device", args.device,
        ]
        print(f"[tts_bridge] Sintetizando cena '{args.scene}' via XTTS-v2 WSL...")
        res = subprocess.run(cmd, capture_output=True, text=True)
        if res.stdout:
            print(res.stdout[-4000:])
        if res.returncode != 0:
            print(f"[tts_bridge] FALHA no worker XTTS (exit {res.returncode})")
            if res.stderr:
                print(res.stderr[-4000:])
            sys.exit(res.returncode)
    finally:
        resolved_cast_path.unlink(missing_ok=True)

    report = parse_report(res.stdout)
    evidence_dir = ROOT / "evidence" / "audio"
    evidence_dir.mkdir(parents=True, exist_ok=True)
    ev_path = evidence_dir / f"tts_bridge_{args.scene}.json"
    ev_path.write_text(json.dumps(report, indent=2, ensure_ascii=False), encoding="utf-8")

    print(f"""
[tts_bridge] VOZES GERADAS COM SUCESSO: {out_dir}
[tts_bridge] Falas: {report.get('total_lines')} | Duracao total: {report.get('total_duration_sec')}s | Device: {report.get('device')}
[tts_bridge] Evidencia: {ev_path}
""")


if __name__ == "__main__":
    main()
