#!/usr/bin/env python3
"""Verifica log de build Unity (erros/warnings) e faz smoke-test do executável Windows."""
import glob
import subprocess
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent.parent
exe = ROOT / "builds" / "AbyssalBreach_Windows" / "AbyssalBreach.exe"

logs = sorted(glob.glob(str(ROOT / "evidence" / "builds" / "build_windows_*.log")))
log = Path(logs[-1])
lines = log.read_text(encoding="utf-8", errors="replace").splitlines()
errs = [l for l in lines if "error CS" in l or "Build Finished, Result: Failure" in l or "Exception:" in l]
warns = [l for l in lines if "warning CS" in l]
result = [l for l in lines if "Build Finished" in l or "Build succeeded" in l.lower()]
print("log:", log.name)
print("result lines:", result[-2:])
print("errors:", len(errs), "| C# warnings:", len(warns))
for l in (errs + warns)[:10]:
    print("  ", l)

files = [p for p in exe.parent.rglob("*") if p.is_file()]
total = sum(p.stat().st_size for p in files) / 1048576
print(f"build dir: {len(files)} files, {total:.1f} MB")

player_log = ROOT / "evidence" / "logs" / "abyssal_player_smoke.log"
proc = subprocess.Popen([str(exe), "-batchmode", "-nographics", "-logFile", str(player_log)])
time.sleep(12)
alive = proc.poll() is None
print("exe alive after 12s:", alive, "| exit code:", proc.poll())
if alive:
    proc.terminate()
if player_log.exists():
    pl = player_log.read_text(encoding="utf-8", errors="replace").splitlines()
    bad = [l for l in pl if "Exception" in l or "NullReference" in l or "Error" in l]
    print("player log lines:", len(pl), "| error-ish lines:", len(bad))
    for l in bad[:10]:
        print("  ", l)
sys.exit(0 if not errs and alive else 1)
