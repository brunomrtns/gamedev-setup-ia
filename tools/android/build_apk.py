#!/usr/bin/env python3
"""
Builder Automatizado de APK Android para Unity
Executa a compilação do projeto em batchmode chamando o Editor C# BuildScript
e grava logs e artefatos de saída em builds/ e evidence/.
"""

import os
import sys
import argparse
import subprocess
from pathlib import Path
from datetime import datetime

ROOT_DIR = Path(__file__).resolve().parent.parent.parent
UNITY_PROJECT = ROOT_DIR / "unity"
BUILDS_DIR = ROOT_DIR / "builds"
EVIDENCE_DIR = ROOT_DIR / "evidence" / "builds"

def find_unity_exe():
    # 1. Tentar via Unity CLI se configurado
    try:
        res = subprocess.run(["unity", "editors", "--json"], stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
        if res.returncode == 0:
            import json
            data = json.loads(res.stdout)
            # data is dict with data array
            editors = data.get("data", [])
            for ed in editors:
                if ed.get("installed") and ed.get("path"):
                    return ed["path"]
    except Exception:
        pass

    # 2. Caminhos padrão do Unity Hub no Windows
    candidates = [
        Path(r"C:\Program Files\Unity\Hub\Editor\6000.3.26f1\Editor\Unity.exe"),
        Path(r"C:\Program Files\Unity\Hub\Editor\6000.0.85f1\Editor\Unity.exe"),
    ]
    for c in candidates:
        if c.exists():
            return str(c)
            
    # Busca recursiva em Program Files\Unity\Hub\Editor
    hub_editors = Path(r"C:\Program Files\Unity\Hub\Editor")
    if hub_editors.exists():
        for exe in hub_editors.glob("*/Editor/Unity.exe"):
            return str(exe)

    return "unity"

def build_android_apk(target="development", output_apk=None):
    BUILDS_DIR.mkdir(parents=True, exist_ok=True)
    EVIDENCE_DIR.mkdir(parents=True, exist_ok=True)

    if not output_apk:
        timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
        output_apk = BUILDS_DIR / f"game_{target}_{timestamp}.apk"
    else:
        output_apk = Path(output_apk)

    unity_exe = find_unity_exe()
    log_file = EVIDENCE_DIR / f"build_android_{datetime.now().strftime('%Y%m%d_%H%M%S')}.log"

    print(f"[*] Executável Unity: {unity_exe}")
    print(f"[*] Projeto Unity: {UNITY_PROJECT}")
    print(f"[*] Destino APK: {output_apk}")
    print(f"[*] Arquivo de Log: {log_file}")

    # Nome do produto derivado do arquivo de saída (neon_district.apk -> NeonDistrict)
    product_name = output_apk.stem.replace("_", " ").title().replace(" ", "")

    cmd = [
        str(unity_exe),
        "-batchmode",
        "-quit",
        "-projectPath", str(UNITY_PROJECT),
        "-buildTarget", "Android",
        "-executeMethod", "Studio.Editor.BuildPipelineHelper.BuildAndroidCommandLine",
        f"-customBuildPath={str(output_apk.resolve())}",
        f"-customBuildTarget={target}",
        f"-productName={product_name}",
        "-logFile", str(log_file.resolve())
    ]

    print(f"[*] Executando Unity em batchmode...")
    start_time = datetime.now()
    proc = subprocess.run(cmd, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)
    duration = (datetime.now() - start_time).total_seconds()

    print(f"[*] Processo finalizado com código {proc.returncode} em {duration:.1f}s.")

    if output_apk.exists() and output_apk.stat().st_size > 1024:
        size_mb = output_apk.stat().st_size / (1024 * 1024)
        print(f"[SUCCESS] APK gerado com sucesso: {output_apk} ({size_mb:.2f} MB)")
        return True, str(output_apk), str(log_file)
    else:
        print(f"[FAIL] Falha ao gerar APK. Consulte o log em: {log_file}")
        if log_file.exists():
            # Exibir últimas linhas do log
            lines = log_file.read_text(encoding="utf-8", errors="replace").splitlines()
            print("\n--- Últimas 20 linhas do log do Unity ---")
            for line in lines[-20:]:
                print(line)
        return False, None, str(log_file)

def main():
    parser = argparse.ArgumentParser(description="Build Android APK")
    parser.add_argument("--target", default="development", choices=["development", "release"], help="Perfil de build")
    parser.add_argument("--out", help="Caminho de saída do APK")
    args = parser.parse_args()

    success, apk, log = build_android_apk(target=args.target, output_apk=args.out)
    if not success:
        sys.exit(1)
    sys.exit(0)

if __name__ == "__main__":
    main()
