#!/usr/bin/env python3
"""drivability_check.py — teste PERMANENTE de dirigibilidade (missão v3.8 M2.3).

Padrão editor vivo (igual run_autonomous_playtest.py): open_scene →
editor_play → eval_file(_drv_attach) → poll [DRV-STATUS] até [DRV-DONE] →
lê Temp/drivability_report.json → copia para evidence/qa/v38/.

Sucesso (exit 0) = 0 bloqueios INVISÍVEIS na rota inteira.
  --scene   cena (default NeonDistrict)
  --budget  segundos máx de varredura (default 1000)
  --tag     sufixo do arquivo de evidência (default timestamp)
  --stop    para o play mode ao final
"""
import glob
import json
import os
import re
import subprocess
import sys
import time

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
PROJ = os.path.join(ROOT, "unity")
OUT_DIR = os.path.join(ROOT, "evidence", "qa", "v38")


def ucmd(*args, timeout=60):
    cmd = ["unity", "cmd", "--project-path", PROJ, "--result-only", *args]
    r = subprocess.run(cmd, capture_output=True, text=True, timeout=timeout)
    out = (r.stdout or r.stderr or "").strip()
    return r.returncode == 0, out


def console_text(tail=400):
    ok, out = ucmd("console", "--tail", str(tail), timeout=30)
    if not ok:
        return ""
    try:
        data = json.loads(out)
    except json.JSONDecodeError:
        m = re.search(r"\{.*\}", out, re.S)
        data = json.loads(m.group(0)) if m else {}
    return "\n".join(e.get("message", "") for e in data.get("entries", []))


def editor_status():
    ok, out = ucmd("editor_status", timeout=30)
    try:
        return json.loads(out)
    except Exception:
        return {}


def ensure_playing(scene):
    st = editor_status()
    if st.get("status") == "playing":
        return True
    ucmd("open_scene", "--path", scene, timeout=120)
    ucmd("editor_play", timeout=30)
    t0 = time.time()
    while time.time() - t0 < 120:
        st = editor_status()
        if st.get("status") == "playing":
            time.sleep(8)  # spawn/warmup
            return True
        time.sleep(3)
    return False


def main():
    scene = "Assets/Game/Scenes/NeonDistrict.unity"
    budget = 1000
    stop = False
    tag = time.strftime("%Y%m%d_%H%M%S")
    args = sys.argv[1:]
    i = 0
    while i < len(args):
        if args[i] == "--scene":
            scene = args[i + 1]; i += 2
        elif args[i] == "--budget":
            budget = int(args[i + 1]); i += 2
        elif args[i] == "--tag":
            tag = args[i + 1]; i += 2
        elif args[i] == "--stop":
            stop = True; i += 1
        else:
            print(f"[drv] arg desconhecido: {args[i]}"); i += 1

    os.makedirs(OUT_DIR, exist_ok=True)

    if not ensure_playing(scene):
        print("[drv] ERRO: play mode não iniciou")
        sys.exit(2)

    ucmd("clear_console", timeout=30)
    ok, out = ucmd("eval_file",
                   os.path.join(ROOT, "tools", "qa", "_drv_attach.cs").replace(os.sep, "/"),
                   timeout=60)
    time.sleep(2)
    txt = console_text(80)
    if "[DRV-ATTACH] ok" not in txt:
        print("[drv] ERRO: probe não anexou")
        print(txt[-1500:])
        sys.exit(3)
    print("[drv] probe anexado — varrendo vias...")

    t0 = time.time()
    last_status = ""
    while time.time() - t0 < budget:
        time.sleep(12)
        tail = console_text(400)
        statuses = [l.strip() for l in tail.splitlines() if "[DRV-STATUS]" in l]
        blocks = [l.strip() for l in tail.splitlines() if "[DRV-BLOCK]" in l]
        if statuses and statuses[-1] != last_status:
            last_status = statuses[-1]
            print("  " + last_status)
        if "[DRV-RESUMO]" in tail:
            resumo = [l.strip() for l in tail.splitlines() if "[DRV-RESUMO]" in l]
            print("  " + (resumo[-1] if resumo else "[DRV-RESUMO] ?"))
            break
        if blocks:
            print("  " + blocks[-1])

    # pega o relatório (console markers ou arquivo Temp)
    report = None
    tail = console_text(4000)
    m = re.search(r"===DRV_BEGIN===(\{.*\})===DRV_END===", tail, re.S)
    if m:
        report = json.loads(m.group(1))
    tmp = os.path.join(PROJ, "Temp", "drivability_report.json")
    if report is None and os.path.exists(tmp) \
            and time.time() - os.path.getmtime(tmp) < budget + 120:
        report = json.load(open(tmp, encoding="utf8"))

    if report is None:
        print("[drv] ERRO: relatório não encontrado")
        sys.exit(4)

    out_path = os.path.join(OUT_DIR, f"drivability_{tag}.json")
    with open(out_path, "w", encoding="utf8") as f:
        json.dump(report, f, indent=1, ensure_ascii=False)

    inv = report.get("invisible", 0)
    by_cls = {}
    for b in report.get("blocks", []):
        by_cls[b["cls"]] = by_cls.get(b["cls"], 0) + 1
    print(f"\n[drv] RESULTADO: blocks={report.get('blocksTotal')} invisible={inv} "
          f"por_classe={by_cls} semChao={report.get('skippedNoGround')} "
          f"quedas={report.get('fell')} motivo={report.get('finished')}")
    print(f"[drv] evidência → {out_path}")
    if inv > 0:
        print("[drv] FALHOU: ainda há bloqueios invisíveis")
        for b in report.get("blocks", []):
            if b["cls"] == "INVISIVEL":
                print(f"   INV @({b['pos'][0]:.1f},{b['pos'][1]:.1f}) "
                      f"{b['collider']} seg={b['seg']}")

    if stop:
        ucmd("editor_stop", timeout=30)
    sys.exit(0 if inv == 0 else 5)


if __name__ == "__main__":
    main()
