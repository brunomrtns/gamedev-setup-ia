#!/usr/bin/env python3
"""Executa testes UTF do Unity em batchmode e imprime resumo (falhas e mensagens)."""
import re
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent.parent
UNITY = r"C:\Program Files\Unity\Hub\Editor\6000.3.26f1\Editor\Unity.exe"


def main():
    platform = sys.argv[1] if len(sys.argv) > 1 else "EditMode"
    name = sys.argv[2] if len(sys.argv) > 2 else "abyssal-editmode-results"
    (ROOT / "evidence" / "test-reports").mkdir(parents=True, exist_ok=True)
    (ROOT / "evidence" / "logs").mkdir(parents=True, exist_ok=True)
    xml = ROOT / "evidence" / "test-reports" / f"{name}.xml"
    log = ROOT / "evidence" / "logs" / f"{name}.log"
    cmd = [UNITY, "-batchmode", "-projectPath", str(ROOT / "unity"), "-runTests",
           "-testPlatform", platform, "-testResults", str(xml), "-logFile", str(log)]
    p = subprocess.run(cmd)
    print("unity rc:", p.returncode)
    if not xml.exists():
        print("sem XML; ultimas linhas do log:")
        for l in log.read_text(encoding="utf-8", errors="replace").splitlines()[-40:]:
            print(l)
        sys.exit(2)
    x = xml.read_text(encoding="utf-8", errors="replace")
    print(re.findall(r"<test-run[^>]*>", x)[:1])
    for m in re.finditer(r'<test-case[^>]*?name="([^"]+)"[^>]*?result="Failed"', x):
        print("FAILED:", m.group(1))
    for m in re.finditer(r"<message><!\[CDATA\[(.*?)\]\]>", x, re.S):
        print("MSG:", m.group(1)[:400])
    sys.exit(p.returncode)


if __name__ == "__main__":
    main()
