#!/usr/bin/env python3
"""
tools/validation/validate_gltf.py
Gate 0 do pipeline 3D (DOC 3 §2 e §3-B): valida arquivos glTF/GLB usando o
KhronosGroup/glTF-Validator oficial (Apache-2.0, via gltf-validator / gltf-transform validate)
antes da conversão/import no Unity.

Grava relatório JSON em evidence/validation/gltf_validator_<nome>.json.
Retorna exit 0 se numErrors == 0; exit 1 caso contrário.
"""

import argparse
import json
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent.parent
VALIDATION_DIR = ROOT / "evidence" / "validation"

NODE_SCRIPT = r"""
const fs = require('fs');
const path = require('path');
const npmRoot = process.argv[1];
const glbPath = process.argv[2];
const outPath = process.argv[3];
const validator = require(path.join(npmRoot, 'gltf-validator'));
const bytes = new Uint8Array(fs.readFileSync(glbPath));
validator.validateBytes(bytes, { uri: path.basename(glbPath), maxIssues: 100 })
  .then((report) => {
    fs.writeFileSync(outPath, JSON.stringify(report, null, 2), 'utf8');
    console.log(JSON.stringify({
      validatorVersion: report.validatorVersion,
      numErrors: report.issues.numErrors,
      numWarnings: report.issues.numWarnings,
      numInfos: report.issues.numInfos,
      numHints: report.issues.numHints
    }));
    process.exit(report.issues.numErrors === 0 ? 0 : 1);
  })
  .catch((err) => {
    console.error('GLTF_VALIDATOR_ERROR:', err);
    process.exit(2);
  });
"""


def find_npm_root() -> str:
    res = subprocess.run(["npm.cmd" if sys.platform == "win32" else "npm", "root", "-g"],
                         capture_output=True, text=True)
    if res.returncode == 0 and res.stdout.strip():
        return res.stdout.strip()
    return r"C:\Users\Bruno\AppData\Roaming\fnm\aliases\default\node_modules"


def validate_gltf(glb_path: Path, asset_name: str = None, out_json: Path = None):
    glb_path = glb_path.resolve()
    if not glb_path.exists():
        print(f"[validate_gltf] Arquivo não encontrado: {glb_path}")
        return False, None

    name = asset_name or glb_path.stem
    VALIDATION_DIR.mkdir(parents=True, exist_ok=True)
    report_path = Path(out_json) if out_json else (VALIDATION_DIR / f"gltf_validator_{name}.json")
    report_path.parent.mkdir(parents=True, exist_ok=True)

    npm_root = find_npm_root()
    cmd = ["node", "-e", NODE_SCRIPT, npm_root, str(glb_path), str(report_path)]
    proc = subprocess.run(cmd, capture_output=True, text=True)
    if proc.stdout.strip():
        print(f"[validate_gltf] {name}: {proc.stdout.strip()}")
    if proc.stderr.strip():
        print(f"[validate_gltf] stderr: {proc.stderr.strip()}")
    if report_path.exists():
        rel = report_path.relative_to(ROOT).as_posix() if report_path.is_relative_to(ROOT) else str(report_path)
        print(f"[validate_gltf] Relatório salvo em: {rel}")
    return proc.returncode == 0, report_path


def main():
    ap = argparse.ArgumentParser(description="Gate Khronos glTF-Validator (Apache-2.0)")
    ap.add_argument("--input", required=True, help="Caminho do arquivo .glb ou .gltf")
    ap.add_argument("--name", default=None, help="Nome lógico do asset para o relatório")
    ap.add_argument("--out", default=None, help="Caminho opcional do JSON de saída")
    args = ap.parse_args()

    ok, _ = validate_gltf(Path(args.input), asset_name=args.name,
                          out_json=Path(args.out) if args.out else None)
    sys.exit(0 if ok else 1)


if __name__ == "__main__":
    main()
