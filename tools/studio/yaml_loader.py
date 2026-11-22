#!/usr/bin/env python3
"""
tools/studio/yaml_loader.py
Carregador YAML 100% spec-compliant para scripts Windows stdlib-only:
tenta PyYAML local e, se ausente no Windows, delega ao PyYAML do WSL.
"""

import json
import os
import subprocess
from pathlib import Path


def to_wsl_path(raw_path) -> str:
    s = str(raw_path).strip()
    if s.startswith("/"):
        return s
    p = Path(s).resolve()
    drive = p.drive.replace(":", "").lower()
    return f"/mnt/{drive}{p.as_posix().split(':', 1)[-1]}"


def load_yaml(path) -> dict:
    p = Path(path).resolve()
    if not p.exists():
        raise FileNotFoundError(f"Arquivo YAML nao encontrado: {p}")
    if p.suffix.lower() == ".json":
        return json.loads(p.read_text(encoding="utf-8"))

    try:
        import yaml  # type: ignore
        return yaml.safe_load(p.read_text(encoding="utf-8")) or {}
    except ImportError:
        pass

    wsl_py = os.getenv("STUDIO_WSL_2D_PYTHON", "/home/bruno/.venvs/hunyuan/bin/python")
    code = "import yaml, json, sys; print(json.dumps(yaml.safe_load(open(sys.argv[1], encoding='utf-8')) or {}, ensure_ascii=False))"
    res = subprocess.run(
        ["wsl", wsl_py, "-c", code, to_wsl_path(p)],
        capture_output=True, text=True, encoding="utf-8", errors="replace",
    )
    if res.returncode != 0:
        # Fallback para python3 do sistema no WSL
        res = subprocess.run(
            ["wsl", "python3", "-c", code, to_wsl_path(p)],
            capture_output=True, text=True, encoding="utf-8", errors="replace",
        )
    if res.returncode != 0:
        raise RuntimeError(f"Falha ao parsear YAML '{p}': {res.stderr.strip()}")
    return json.loads(res.stdout)
