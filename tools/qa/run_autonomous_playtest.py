#!/usr/bin/env python3
"""run_autonomous_playtest.py — dirige o playtest autônomo no Unity Editor vivo.

O caminho batchmode (`-runTests`) não descobre testes PlayMode neste projeto
(testcasecount=0 = falso positivo). O caminho funcional é o editor vivo via
`unity cmd`: open_scene → editor_play → eval_file (anexa bot) → console
(telemetria) → export da sessão.

Uso:
  python tools/qa/run_autonomous_playtest.py [--duration 240]
      [--scene Assets/Game/Scenes/NeonDistrict.unity] [--stop] [--keep-playing]

Saídas:
  evidence/qa/session_<id>.json   — sessão estruturada do bot
  evidence/qa/frames/<id>/        — capturas periódicas
  evidence/qa/report_<id>.md      — relatório completo do bot
  evidence/qa-report.md           — checklist persistente (append)
"""
import argparse, glob, json, os, re, shutil, subprocess, sys, time
sys.stdout.reconfigure(encoding="utf-8", errors="replace")

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
PROJ = os.path.join(ROOT, "unity")
SCENE_DEFAULT = "Assets/Game/Scenes/NeonDistrict.unity"
QA_DIR = os.path.join(ROOT, "evidence", "qa")
CHECKLIST = os.path.join(ROOT, "evidence", "qa-report.md")


def ucmd(*args, timeout=60):
    """unity cmd contra o editor vivo. Retorna (ok, texto_resultado)."""
    cmd = ["unity", "cmd", "--project-path", PROJ, "--result-only", *args]
    r = subprocess.run(cmd, capture_output=True, text=True, timeout=timeout)
    out = (r.stdout or "").strip()
    # --result-only imprime o JSON do resultado; fallback ao envelope inteiro
    if not out:
        out = (r.stderr or "").strip()
    return r.returncode == 0, out


def console_entries(tail=400):
    ok, out = ucmd("console", "--tail", str(tail), timeout=30)
    if not ok:
        return []
    try:
        data = json.loads(out)
    except json.JSONDecodeError:
        m = re.search(r"\{.*\}", out, re.S)
        data = json.loads(m.group(0)) if m else {}
    return data.get("entries", [])


def console_text(tail=400):
    return "\n".join(e.get("message", "") for e in console_entries(tail))


def editor_status():
    ok, out = ucmd("editor_status", timeout=30)
    try:
        return json.loads(out)
    except Exception:
        return {}


def ensure_playing(scene, wait=120):
    st = editor_status()
    if st.get("status") == "playing":
        print(f"[runner] já em play ({st.get('projectPath','?')})")
        return True
    print("[runner] abrindo cena e entrando em play...")
    ucmd("open_scene", "--path", scene, timeout=120)
    ucmd("editor_play", timeout=30)
    t0 = time.time()
    while time.time() - t0 < wait:
        st = editor_status()
        if st.get("status") == "playing":
            print("[runner] play mode ativo")
            time.sleep(8)  # cena estabiliza (spawn, warmup)
            return True
        time.sleep(3)
    print("[runner] ERRO: play mode não iniciou")
    return False


def eval_file(name):
    path = os.path.join(ROOT, "tools", "qa", name).replace(os.sep, "/")
    ok, out = ucmd("eval_file", path, timeout=60)
    return ok, out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--duration", type=int, default=230,
                    help="segundos de sessão antes do finish (bot auto-encerra ~210s)")
    ap.add_argument("--scene", default=SCENE_DEFAULT)
    ap.add_argument("--stop", action="store_true",
                    help="editor_stop ao final")
    ap.add_argument("--keep-playing", action="store_true",
                    help="não para o play mode ao final")
    ap.add_argument("--poll", type=int, default=20)
    args = ap.parse_args()

    os.makedirs(QA_DIR, exist_ok=True)

    if not ensure_playing(args.scene):
        sys.exit(2)

    # limpa o console ANTES: markers de sessões antigas não contaminam
    # a verificação do attach nem os polls de status/export
    ucmd("clear_console", timeout=30)

    print("[runner] anexando AutonomousPlaytester...")
    ok, out = eval_file("attach_playtester.cs")
    time.sleep(2)
    txt = console_text(60)
    if "[QA-ATTACH] ok" not in txt:
        print("[runner] ERRO: bot não anexou"); print(txt[-1500:])
        sys.exit(3)
    print("[runner] bot anexado")

    t0 = time.time()
    report = None
    session_file = None
    while time.time() - t0 < args.duration:
        time.sleep(args.poll)
        eval_file("playtester_status.cs")
        time.sleep(1)
        tail = console_text(400)
        for line in tail.splitlines():
            if "[QA-STATUS]" in line:
                print("  " + line.strip())
        if "[QA-EXPORT]" in tail:
            print("[runner] bot auto-encerrou (budget)")
            break

    print("[runner] exportando sessão...")
    eval_file("playtester_finish.cs")
    time.sleep(3)

    tail = console_text(4000)
    m = re.search(r"===QA_REPORT_BEGIN===\n(.*)\n===QA_REPORT_END===",
                  tail, re.S)
    if m:
        report = m.group(1)
    # fallback robusto: relatório gravado em arquivo pelo finish.cs
    rep_tmp = os.path.join(PROJ, "Temp", "qa_report.md")
    if not report and os.path.exists(rep_tmp) \
            and time.time() - os.path.getmtime(rep_tmp) < 600:
        report = open(rep_tmp, encoding="utf-8").read()
    me = re.findall(r"\[QA-EXPORT\] (.+session_[\w]+\.json)", tail)
    if me:
        session_file = me[-1].strip()

    # salva relatório
    sid = "unknown"
    if session_file and os.path.exists(session_file):
        try:
            sid = json.load(open(session_file, encoding="utf-8"))["sessionId"]
        except Exception:
            sid = os.path.basename(session_file)[8:-5]
    else:
        sess = sorted(glob.glob(os.path.join(QA_DIR, "session_*.json")),
                      key=os.path.getmtime)
        if sess and time.time() - os.path.getmtime(sess[-1]) < 600:
            session_file = sess[-1]
            sid = os.path.basename(session_file)[8:-5]

    rep_path = os.path.join(QA_DIR, f"report_{sid}.md")
    if report:
        with open(rep_path, "w", encoding="utf-8") as f:
            f.write(report + "\n")
        print(f"[runner] relatório → {rep_path}")
        print("\n" + report)
    else:
        print("[runner] AVISO: relatório não encontrado no console")

    # copia frames do bot (Temp/qa_frames dentro do projeto)
    frames_src = os.path.join(PROJ, "Temp", "qa_frames")
    frames_dst = os.path.join(QA_DIR, "frames", sid)
    n_frames = 0
    if os.path.isdir(frames_src):
        os.makedirs(frames_dst, exist_ok=True)
        for png in glob.glob(os.path.join(frames_src, "*.png")):
            shutil.copy2(png, frames_dst); n_frames += 1
    print(f"[runner] {n_frames} frames → {frames_dst}")

    # append no checklist persistente
    if report:
        stamp = time.strftime("%Y-%m-%d %H:%M")
        with open(CHECKLIST, "a", encoding="utf-8") as f:
            f.write(f"\n## Sessão {sid} — {stamp} (editor vivo, input real)\n")
            scen = [l for l in report.splitlines()
                    if re.match(r" - (Pass|Fail|Blocked|Invalid|Inconclusive|RUNNING)\b", l)]
            for l in scen:
                mark = {"Pass": "[x]", "Fail": "[ ]", "Blocked": "[~]",
                        "Invalid": "[~]", "Inconclusive": "[~]",
                        "RUNNING": "[~]"}.get(l.split()[1], "[~]")
                f.write(f"- {mark} {l.strip()}\n")
            f.write(f"- evidências: `{os.path.relpath(rep_path, ROOT)}`, "
                    f"`{os.path.relpath(session_file or '', ROOT)}`, "
                    f"`{os.path.relpath(frames_dst, ROOT)}`\n")
        print(f"[runner] checklist atualizado → {CHECKLIST}")

    if args.stop and not args.keep_playing:
        ucmd("editor_stop", timeout=30)
        print("[runner] editor_stop")
    sys.exit(0 if report else 4)


if __name__ == "__main__":
    main()
