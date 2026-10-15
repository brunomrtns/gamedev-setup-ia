#!/usr/bin/env python3
"""
Capturador Automatizado de Cenas Unity
Invoca o Unity Editor para renderizar uma cena específica e salvar
a imagem gerada em formato PNG sob evidence/captures/.
"""

import sys
import os
import argparse
import subprocess
from pathlib import Path

ROOT_DIR = Path(__file__).resolve().parent.parent.parent
UNITY_PROJECT = ROOT_DIR / "unity"
CAPTURES_DIR = ROOT_DIR / "evidence" / "captures"

def find_unity_exe():
    candidates = [
        Path(r"C:\Program Files\Unity\Hub\Editor\6000.3.26f1\Editor\Unity.exe"),
        Path(r"C:\Program Files\Unity\Hub\Editor\6000.0.85f1\Editor\Unity.exe"),
    ]
    for c in candidates:
        if c.exists():
            return str(c)
    return "unity"

def capture_scene(scene_path, output_png=None):
    CAPTURES_DIR.mkdir(parents=True, exist_ok=True)
    if not output_png:
        output_png = CAPTURES_DIR / "calibration_view.png"
    else:
        output_png = Path(output_png)

    unity_exe = find_unity_exe()
    log_file = CAPTURES_DIR / "capture.log"

    print(f"[*] Capturando cena: {scene_path}")
    print(f"[*] Destino PNG: {output_png}")

    cmd = [
        unity_exe,
        "-batchmode",
        "-quit",
        "-projectPath", str(UNITY_PROJECT),
        "-executeMethod", "Studio.Editor.StudioAutomation.CaptureScreenshotBatch",
        f"-screenshotPath={str(output_png.resolve())}",
        "-logFile", str(log_file.resolve())
    ]

    proc = subprocess.run(cmd, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)
    
    if output_png.exists() and output_png.stat().st_size > 0:
        size_kb = output_png.stat().st_size / 1024
        print(f"[SUCCESS] Captura realizada com sucesso: {output_png} ({size_kb:.1f} KB)")
        return True, str(output_png)
    else:
        print(f"[FAIL] Falha ao capturar screenshot. Verifique o log: {log_file}")
        return False, None

def main():
    parser = argparse.ArgumentParser(description="Capturador de Cenas Unity")
    parser.add_argument("--scene", default="Assets/Game/Scenes/Calibration.unity", help="Caminho da cena")
    parser.add_argument("--out", help="Caminho de saída da imagem PNG")
    args = parser.parse_args()

    success, path = capture_scene(args.scene, args.out)
    if not success:
        sys.exit(1)
    sys.exit(0)

if __name__ == "__main__":
    main()
