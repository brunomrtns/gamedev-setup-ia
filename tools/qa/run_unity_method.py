#!/usr/bin/env python3
"""Executa um método estático do Editor Unity em batchmode e imprime linhas filtradas do log."""
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent.parent
UNITY = r"C:\Program Files\Unity\Hub\Editor\6000.3.26f1\Editor\Unity.exe"


def main():
    method = sys.argv[1]
    tag = sys.argv[2] if len(sys.argv) > 2 else "unity_method"
    pattern = sys.argv[3] if len(sys.argv) > 3 else ""
    extra = sys.argv[4:]
    (ROOT / "evidence" / "logs").mkdir(parents=True, exist_ok=True)
    log = ROOT / "evidence" / "logs" / f"{tag}.log"
    cmd = [UNITY, "-batchmode", "-projectPath", str(ROOT / "unity"),
           "-executeMethod", method, "-logFile", str(log)] + extra
    p = subprocess.run(cmd)
    print("unity rc:", p.returncode)
    lines = log.read_text(encoding="utf-8", errors="replace").splitlines()
    sel = [l for l in lines if pattern and pattern in l] if pattern else lines[-40:]
    for l in sel[-80:]:
        print(l)
    errs = [l for l in lines if "error CS" in l or "Exception" in l]
    print(f"compile/exception lines: {len(errs)}")
    for l in errs[:15]:
        print(l)
    sys.exit(p.returncode)


if __name__ == "__main__":
    main()
