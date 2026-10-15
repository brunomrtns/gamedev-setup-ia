#!/usr/bin/env python3
"""
Studio Task Runner — Motor de Orquestração Persistente baseado em DAG
Permite que o estúdio execute tarefas sequencialmente conforme dependências,
grave evidências, sobreviva a interrupções de sessão e atualize o PROGRESS.md.
"""

import os
import sys
import json
import time
import argparse
import subprocess
from pathlib import Path
from datetime import datetime

ROOT_DIR = Path(__file__).resolve().parent.parent.parent
TASKS_DIR = ROOT_DIR / ".studio" / "tasks"
EVIDENCE_DIR = ROOT_DIR / "evidence"
PROGRESS_FILE = ROOT_DIR / "PROGRESS.md"

def ensure_dirs():
    TASKS_DIR.mkdir(parents=True, exist_ok=True)
    EVIDENCE_DIR.mkdir(parents=True, exist_ok=True)

def load_all_tasks():
    ensure_dirs()
    tasks = {}
    for task_file in sorted(TASKS_DIR.glob("*.json")):
        try:
            with open(task_file, "r", encoding="utf-8") as f:
                data = json.load(f)
                tasks[data["id"]] = data
        except Exception as e:
            print(f"[WARN] Falha ao ler tarefa {task_file.name}: {e}")
    return tasks

def save_task(task_data):
    ensure_dirs()
    task_id = task_data["id"]
    task_file = TASKS_DIR / f"{task_id}.json"
    with open(task_file, "w", encoding="utf-8") as f:
        json.dump(task_data, f, indent=2, ensure_ascii=False)

def add_task(task_id, title, stage, command=None, dependencies=None):
    dependencies = dependencies or []
    task = {
        "id": task_id,
        "title": title,
        "stage": stage,
        "command": command,
        "dependencies": dependencies,
        "status": "pending",  # pending, in_progress, completed, failed, blocked
        "evidence_files": [],
        "created_at": datetime.now().isoformat(),
        "updated_at": datetime.now().isoformat(),
        "error_message": None,
        "retries": 0,
        "max_retries": 3
    }
    save_task(task)
    print(f"[OK] Tarefa criada: {task_id} — {title}")
    sync_progress()
    return task

def get_next_runnable_task(tasks):
    for task_id, task in tasks.items():
        if task["status"] in ("pending", "failed") and task.get("retries", 0) < task.get("max_retries", 3):
            # Verificar se todas as dependências estão completas
            deps_ok = True
            for dep_id in task.get("dependencies", []):
                dep = tasks.get(dep_id)
                if not dep or dep["status"] != "completed":
                    deps_ok = False
                    break
            if deps_ok:
                return task
    return None

def run_task(task):
    task_id = task["id"]
    print(f"\n[EXEC] Iniciando tarefa: {task_id} — {task['title']}")
    task["status"] = "in_progress"
    task["updated_at"] = datetime.now().isoformat()
    save_task(task)
    sync_progress()

    cmd = task.get("command")
    if not cmd:
        print(f"[INFO] Tarefa {task_id} não possui comando automatizado de shell. Requer ação do agente.")
        return False

    start_time = time.time()
    try:
        proc = subprocess.run(
            cmd,
            shell=True,
            cwd=str(ROOT_DIR),
            stdout=subprocess.PIPE,
            stderr=subprocess.STDOUT,
            text=True,
            encoding="utf-8",
            errors="replace"
        )
        elapsed = time.time() - start_time
        
        # Registrar evidência de log
        log_dir = EVIDENCE_DIR / "logs"
        log_dir.mkdir(parents=True, exist_ok=True)
        log_file = log_dir / f"{task_id}.log"
        with open(log_file, "w", encoding="utf-8") as lf:
            lf.write(proc.stdout)

        if proc.returncode == 0:
            task["status"] = "completed"
            task["error_message"] = None
            if str(log_file) not in task["evidence_files"]:
                task["evidence_files"].append(str(log_file.relative_to(ROOT_DIR)))
            print(f"[SUCCESS] Tarefa {task_id} concluída com sucesso em {elapsed:.1f}s.")
        else:
            task["retries"] = task.get("retries", 0) + 1
            task["status"] = "failed"
            task["error_message"] = f"Comando falhou com código {proc.returncode}."
            print(f"[FAIL] Tarefa {task_id} falhou com código {proc.returncode}. Tentativa {task['retries']}/{task['max_retries']}.")
    except Exception as e:
        task["retries"] = task.get("retries", 0) + 1
        task["status"] = "failed"
        task["error_message"] = str(e)
        print(f"[ERROR] Exceção ao executar tarefa {task_id}: {e}")

    task["updated_at"] = datetime.now().isoformat()
    save_task(task)
    sync_progress()
    return task["status"] == "completed"

def sync_progress():
    tasks = load_all_tasks()
    if not tasks:
        return

    # Gerar tabela de status para PROGRESS.md
    lines = []
    lines.append("## Fila de Tarefas Autônomas Persistentes (.studio/tasks/)\n")
    lines.append("| ID | Estágio | Tarefa | Status | Dependências | Evidências |")
    lines.append("|---|---|---|---|---|---|")
    
    icon_map = {
        "completed": "[x] Concluído",
        "in_progress": "[~] Em execução",
        "pending": "[ ] Pendente",
        "failed": "[!] Falhou",
        "blocked": "[!] Bloqueado"
    }
    
    for tid, t in sorted(tasks.items()):
        status_str = icon_map.get(t["status"], t["status"])
        deps = ", ".join(t.get("dependencies", [])) or "—"
        evs = ", ".join([f"[`{Path(e).name}`]({e})" for e in t.get("evidence_files", [])]) or "—"
        lines.append(f"| `{t['id']}` | {t['stage']} | {t['title']} | {status_str} | {deps} | {evs} |")

    content_table = "\n".join(lines) + "\n"

    # Atualizar ou mesclar em PROGRESS.md
    if PROGRESS_FILE.exists():
        current_text = PROGRESS_FILE.read_text(encoding="utf-8")
        marker_start = "<!-- TASKS_START -->"
        marker_end = "<!-- TASKS_END -->"
        if marker_start in current_text and marker_end in current_text:
            parts = current_text.split(marker_start)
            rest = parts[1].split(marker_end)
            new_text = parts[0] + marker_start + "\n\n" + content_table + "\n" + marker_end + rest[1]
            PROGRESS_FILE.write_text(new_text, encoding="utf-8")
        else:
            new_text = current_text + f"\n\n{marker_start}\n\n{content_table}\n{marker_end}\n"
            PROGRESS_FILE.write_text(new_text, encoding="utf-8")

def main():
    parser = argparse.ArgumentParser(description="Studio Task Runner")
    parser.add_argument("--list", action="store_true", help="Listar tarefas")
    parser.add_argument("--run-next", action="store_true", help="Executar próxima tarefa pronta no DAG")
    parser.add_argument("--run-all", action="store_true", help="Executar todas as tarefas possíveis até parar")
    parser.add_argument("--add", nargs=3, metavar=("ID", "TITLE", "STAGE"), help="Adicionar tarefa")
    parser.add_argument("--cmd", help="Comando associado à tarefa adicionada")
    parser.add_argument("--deps", help="Dependências separadas por vírgula")
    parser.add_argument("--complete", metavar="ID", help="Marcar tarefa como concluída manualmente")
    parser.add_argument("--evidence", help="Caminho do arquivo de evidência")
    parser.add_argument("--sync", action="store_true", help="Sincronizar PROGRESS.md")

    args = parser.parse_args()

    if args.add:
        tid, title, stage = args.add
        deps = [d.strip() for d in args.deps.split(",")] if args.deps else []
        add_task(tid, title, stage, command=args.cmd, dependencies=deps)
        return

    if args.complete:
        tasks = load_all_tasks()
        if args.complete in tasks:
            t = tasks[args.complete]
            t["status"] = "completed"
            t["updated_at"] = datetime.now().isoformat()
            if args.evidence and args.evidence not in t["evidence_files"]:
                t["evidence_files"].append(args.evidence)
            save_task(t)
            sync_progress()
            print(f"[OK] Tarefa {args.complete} marcada como concluída.")
        else:
            print(f"[ERROR] Tarefa {args.complete} não encontrada.")
        return

    if args.sync:
        sync_progress()
        print("[OK] PROGRESS.md sincronizado.")
        return

    if args.run_next:
        tasks = load_all_tasks()
        next_task = get_next_runnable_task(tasks)
        if next_task:
            run_task(next_task)
        else:
            print("[INFO] Nenhuma tarefa elegível pronta para execução no momento.")
        return

    if args.run_all:
        while True:
            tasks = load_all_tasks()
            next_task = get_next_runnable_task(tasks)
            if not next_task:
                print("[INFO] Não há mais tarefas elegíveis para executar.")
                break
            success = run_task(next_task)
            if not success:
                print(f"[WARN] Parando loop de execução autônoma devido à falha na tarefa {next_task['id']}.")
                break
        return

    # Default: list
    tasks = load_all_tasks()
    print(f"\nTotal de Tarefas no DAG: {len(tasks)}")
    for tid, t in sorted(tasks.items()):
        status = t["status"].upper()
        deps = f"(deps: {', '.join(t.get('dependencies', []))})" if t.get("dependencies") else ""
        print(f"[{status:^11}] {t['id']:<10} | {t['stage']:<12} | {t['title']} {deps}")

if __name__ == "__main__":
    main()
