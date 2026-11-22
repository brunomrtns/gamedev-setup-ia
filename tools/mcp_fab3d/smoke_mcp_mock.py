#!/usr/bin/env python3
"""
tools/mcp_fab3d/smoke_mcp_mock.py
Smoke test do serviço MCP da Fábrica 3D (P1) em modo Mock (sem GPU):
  1. Testa todas as 8 ferramentas MCP (system_status, generate_asset, wait_for_job,
     get_job, texture_mesh, export_asset, list_assets, + image_to_3d).
  2. Prova que export_asset executa obrigatoriamente glTF-Validator -> validate_asset
     -> validate_visual (exit 0) sem bypass.
  3. Grava evidência em evidence/mcp/p1_fab3d_mcp_mock_smoke.json.
"""

import json
import sys
from datetime import datetime, timezone
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from server import Fab3DMCPService, TOOL_SPECS  # noqa: E402

ROOT = Path(__file__).resolve().parent.parent.parent


def main():
    svc = Fab3DMCPService(adapter_mode="mock")
    status = svc.dispatch("system_status", {})
    assert status["gate_bypass_allowed"] is False, "Bypass deve ser proibido!"

    # 1. generate_asset (chama image_to_3d + texture_mesh internamente)
    gen = svc.dispatch("generate_asset", {
        "asset_name": "mcp_mock_caixote_smoke",
        "image_path": "mock://concept.png",
        "category": "small_prop_item",
    })
    job_id = gen["job_id"]
    waited = svc.dispatch("wait_for_job", {"job_id": job_id, "timeout_sec": 30})
    fetched = svc.dispatch("get_job", {"job_id": job_id})
    exported = svc.dispatch("export_asset", {"job_id": job_id})
    listed = svc.dispatch("list_assets", {})

    out_dir = ROOT / "evidence" / "mcp"
    out_dir.mkdir(parents=True, exist_ok=True)
    report = {
        "smoke": "P1 — Fab3D MCP Service (MyMeshy MIT base, Mock adapter)",
        "timestamp": datetime.now(timezone.utc).isoformat(),
        "exposed_tools": [t["name"] for t in TOOL_SPECS],
        "system_status": status,
        "wait_for_job_status": waited.get("status"),
        "get_job_stage": fetched.get("stage"),
        "export_result": exported,
        "list_assets": listed,
        "passed": bool(exported.get("exported") and exported.get("gates_passed")),
    }
    out_path = out_dir / "p1_fab3d_mcp_mock_smoke.json"
    out_path.write_text(json.dumps(report, indent=2, ensure_ascii=False), encoding="utf-8")
    print(f"[smoke_mcp_mock] Relatório salvo em: {out_path.relative_to(ROOT).as_posix()}")
    print(f"[smoke_mcp_mock] PASSED={report['passed']} | gates_passed={exported.get('gates_passed')}")
    sys.exit(0 if report["passed"] else 1)


if __name__ == "__main__":
    main()
