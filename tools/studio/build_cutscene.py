#!/usr/bin/env python3
"""
tools/studio/build_cutscene.py
CLI Windows (stdlib puro) para compilar cutscenes declarativas (JSON -> Unity Timeline +
Prefab + Capturas de Keyframes) via Unity 6.3 batchmode — Studio V2 Fase 3.

Uso:
  python tools/studio/build_cutscene.py --spec docs/narrative/cutscene_intro.json --capture-keyframes
"""

import argparse
import json
import os
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent.parent
DEFAULT_UNITY = os.getenv(
    "STUDIO_UNITY_EXE",
    r"C:\Program Files\Unity\Hub\Editor\6000.3.26f1\Editor\Unity.exe",
)


def main():
    ap = argparse.ArgumentParser(description="Cutscene Builder declarativo (Studio V2 Fase 3)")
    ap.add_argument("--spec", required=True, help="Caminho para cutscene_<id>.json")
    ap.add_argument("--capture-keyframes", action="store_true",
                    help="Captura 1 frame central por shot em evidence/captures/")
    ap.add_argument("--dry-run-assets", action="store_true",
                    help="Nao grava .playable/.prefab em Assets/ (util para smoke test/preview)")
    ap.add_argument("--unity-exe", default=DEFAULT_UNITY, help="Caminho do Unity.exe")
    ap.add_argument("--log-file", default=None, help="Arquivo de log do Unity")
    args = ap.parse_args()

    spec_path = Path(args.spec).resolve()
    if not spec_path.exists():
        print(f"[build_cutscene] Spec nao encontrado: {spec_path}")
        sys.exit(2)

    spec_data = json.loads(spec_path.read_text(encoding="utf-8"))
    cutscene_id = spec_data.get("cutscene_id", "unknown")

    unity_exe = Path(args.unity_exe)
    if not unity_exe.exists():
        print(f"[build_cutscene] Unity.exe nao encontrado: {unity_exe}")
        sys.exit(2)

    log_file = Path(args.log_file).resolve() if args.log_file else \
        ROOT / "evidence" / "cutscenes" / f"unity_cutscene_{cutscene_id}.log"
    log_file.parent.mkdir(parents=True, exist_ok=True)

    cmd = [
        str(unity_exe),
        "-batchmode", "-quit",
        "-projectPath", str(ROOT / "unity"),
        "-executeMethod", "Studio.Editor.CutsceneBuilder.RunBuildBatch",
        f"-cutsceneJson={spec_path}",
        f"-captureKeyframes={'true' if args.capture_keyframes else 'false'}",
        f"-dryRunAssets={'true' if args.dry_run_assets else 'false'}",
        "-logFile", str(log_file),
    ]
    print(f"[build_cutscene] Executando CutsceneBuilder para '{cutscene_id}'...")
    res = subprocess.run(cmd)
    report_path = ROOT / "evidence" / "cutscenes" / f"cutscene_{cutscene_id}.json"

    if res.returncode != 0:
        print(f"[build_cutscene] FALHA (exit {res.returncode}). Ver log: {log_file}")
        sys.exit(res.returncode)

    if report_path.exists():
        report = json.loads(report_path.read_text(encoding="utf-8"))
        print(json.dumps(report, indent=2, ensure_ascii=False))
        if report.get("errors"):
            sys.exit(1)

    print(f"[build_cutscene] CONCLUIDO: {report_path}")


if __name__ == "__main__":
    main()
