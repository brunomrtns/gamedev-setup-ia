#!/usr/bin/env python3
"""
tools/assetstore/import_new_packages.py
Importa automaticamente .unitypackage baixados da Unity Asset Store.

Varre o cache da Asset Store (%APPDATA%/Unity/Asset Store-5.x), detecta
pacotes novos ou alterados (por nome+tamanho) e importa cada um no projeto
via Unity batchmode (-importPackage). Estado persistido em
.studio/imported_packages.json — reexecutar é seguro (idempotente).

Uso:
  python tools/assetstore/import_new_packages.py            # importa só os novos
  python tools/assetstore/import_new_packages.py --all      # reimporta tudo
  python tools/assetstore/import_new_packages.py --dry-run  # só lista
"""

import argparse
import json
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent.parent
UNITY_PROJECT = ROOT / "unity"
UNITY_EXE = Path(r"C:\Program Files\Unity\Hub\Editor\6000.3.26f1\Editor\Unity.exe")
STATE_PATH = ROOT / ".studio" / "imported_packages.json"
LOG_DIR = ROOT / "evidence" / "logs"

CANDIDATE_CACHE_DIRS = [
    Path.home() / "AppData" / "Roaming" / "Unity" / "Asset Store-5.x",
    Path.home() / "AppData" / "Local" / "Unity" / "Asset Store-5.x",
]


def find_packages():
    """Retorna {nome_do_pacote: Path} de todos os .unitypackage no cache."""
    found = {}
    for cache in CANDIDATE_CACHE_DIRS:
        if not cache.exists():
            continue
        for pkg in cache.rglob("*.unitypackage"):
            found[pkg.name] = pkg
    return found


def load_state():
    if STATE_PATH.exists():
        return json.loads(STATE_PATH.read_text(encoding="utf-8"))
    return {}


def save_state(state):
    STATE_PATH.parent.mkdir(parents=True, exist_ok=True)
    STATE_PATH.write_text(json.dumps(state, indent=2, ensure_ascii=False), encoding="utf-8")


def import_package(pkg_path: Path) -> bool:
    LOG_DIR.mkdir(parents=True, exist_ok=True)
    slug = "".join(c if c.isalnum() else "_" for c in pkg_path.stem)[:48]
    log_file = LOG_DIR / f"import-{slug}.log"
    print(f"[*] Importando: {pkg_path.name} ({pkg_path.stat().st_size // (1024*1024)} MB)")
    cmd = [
        str(UNITY_EXE), "-batchmode", "-quit",
        "-projectPath", str(UNITY_PROJECT),
        "-importPackage", str(pkg_path),
        "-logFile", str(log_file),
    ]
    res = subprocess.run(cmd)
    ok = res.returncode == 0
    print(f"    -> {'OK' if ok else 'FALHOU'} (log: {log_file.relative_to(ROOT)})")
    return ok


def main():
    ap = argparse.ArgumentParser(description="Importador de .unitypackage da Asset Store")
    ap.add_argument("--all", action="store_true", help="Reimporta todos os pacotes")
    ap.add_argument("--dry-run", action="store_true", help="Apenas lista, sem importar")
    args = ap.parse_args()

    if not UNITY_EXE.exists():
        print(f"[FAIL] Unity não encontrado: {UNITY_EXE}")
        sys.exit(2)

    packages = find_packages()
    if not packages:
        print("[*] Nenhum .unitypackage encontrado no cache da Asset Store.")
        sys.exit(0)

    state = load_state()
    pending = []
    for name, path in sorted(packages.items()):
        size = path.stat().st_size
        if args.all or state.get(name, {}).get("size") != size:
            pending.append((name, path))

    if not pending:
        print(f"[*] Nada novo — {len(packages)} pacote(s) já importados.")
        sys.exit(0)

    print(f"[*] {len(pending)} pacote(s) novo(s)/alterado(s) para importar:")
    for name, path in pending:
        print(f"    - {name}")

    if args.dry_run:
        sys.exit(0)

    failures = []
    for name, path in pending:
        if import_package(path):
            state[name] = {"size": path.stat().st_size, "path": str(path)}
            save_state(state)
        else:
            failures.append(name)

    if failures:
        print(f"\n[FAIL] {len(failures)} pacote(s) falharam: {failures}")
        sys.exit(1)
    print(f"\n[OK] {len(pending)} pacote(s) importados com sucesso.")
    sys.exit(0)


if __name__ == "__main__":
    main()
