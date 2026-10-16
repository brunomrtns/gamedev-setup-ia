#!/usr/bin/env python3
"""
Builder Automatizado de Executável Windows Standalone (.exe) para Unity
Compila o projeto em batchmode para Windows 64-bit e grava logs em evidence/builds/.
"""

import os
import sys
import argparse
import subprocess
from pathlib import Path
from datetime import datetime

ROOT_DIR = Path(__file__).resolve().parent.parent.parent
UNITY_PROJECT = ROOT_DIR / "unity"
BUILDS_DIR = ROOT_DIR / "builds" / "NeonRush_Windows"
EVIDENCE_DIR = ROOT_DIR / "evidence" / "builds"

def find_unity_exe():
    try:
        res = subprocess.run(["unity", "editors", "--json"], stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
        if res.returncode == 0:
            import json
            data = json.loads(res.stdout)
            editors = data.get("data", [])
            for ed in editors:
                if ed.get("installed") and ed.get("path"):
                    return ed["path"]
    except Exception:
        pass

    candidates = [
        Path(r"C:\Program Files\Unity\Hub\Editor\6000.3.26f1\Editor\Unity.exe"),
        Path(r"C:\Program Files\Unity\Hub\Editor\6000.0.85f1\Editor\Unity.exe"),
    ]
    for c in candidates:
        if c.exists():
            return str(c)

    hub_editors = Path(r"C:\Program Files\Unity\Hub\Editor")
    if hub_editors.exists():
        for exe in hub_editors.glob("*/Editor/Unity.exe"):
            return str(exe)

    return "unity"

def build_windows(output_exe=None):
    BUILDS_DIR.mkdir(parents=True, exist_ok=True)
    EVIDENCE_DIR.mkdir(parents=True, exist_ok=True)

    if not output_exe:
        output_exe = BUILDS_DIR / "NeonRush.exe"
    else:
        output_exe = Path(output_exe)

    unity_exe = find_unity_exe()
    log_file = EVIDENCE_DIR / f"build_windows_{datetime.now().strftime('%Y%m%d_%H%M%S')}.log"

    print(f"[*] Executável Unity: {unity_exe}")
    print(f"[*] Projeto Unity: {UNITY_PROJECT}")
    print(f"[*] Destino Windows EXE: {output_exe}")
    print(f"[*] Arquivo de Log: {log_file}")

    cmd = [
        str(unity_exe),
        "-batchmode",
        "-quit",
        "-projectPath", str(UNITY_PROJECT),
        "-buildTarget", "Win64",
        "-executeMethod", "Studio.Editor.BuildPipelineHelper.BuildWindowsCommandLine",
        f"-customBuildPath={str(output_exe.resolve())}",
        "-logFile", str(log_file.resolve())
    ]

    print("[*] Executando compilação Windows em batchmode...")
    start_time = datetime.now()
    proc = subprocess.run(cmd, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)
    duration = (datetime.now() - start_time).total_seconds()

    print(f"[*] Processo finalizado com código {proc.returncode} em {duration:.1f}s.")

    if output_exe.exists() and output_exe.stat().st_size > 1024:
        size_mb = output_exe.stat().st_size / (1024 * 1024)
        print(f"[SUCCESS] Executável Windows gerado com sucesso: {output_exe} ({size_mb:.2f} MB)")
        return True, str(output_exe), str(log_file)
    else:
        print(f"[FAIL] Falha ao gerar executável Windows. Consulte o log em: {log_file}")
        if log_file.exists():
            lines = log_file.read_text(encoding="utf-8", errors="replace").splitlines()
            print("\n--- Últimas 25 linhas do log do Unity ---")
            for line in lines[-25:]:
                print(line)
        return False, None, str(log_file)

def main():
    parser = argparse.ArgumentParser(description="Build Windows Standalone (.exe)")
    parser.add_argument("--out", help="Caminho de saída do executável")
    args = parser.parse_args()

    success, exe, log = build_windows(output_exe=args.out)
    if not success:
        sys.exit(1)
    sys.exit(0)

if __name__ == "__main__":
    main()
