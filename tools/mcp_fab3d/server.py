#!/usr/bin/env python3
"""
tools/mcp_fab3d/server.py
Serviço MCP da Fábrica 3D (P1 — base: MyMeshy, MIT — github.com/felippeomgt/mymeshy).

Ferramentas expostas via MCP JSON-RPC 2.0 (stdio) e API Python:
  1. generate_asset
  2. image_to_3d
  3. texture_mesh
  4. export_asset
  5. get_job
  6. wait_for_job
  7. list_assets
  8. system_status

REGRA DE OURO (sem bypass):
  NADA que saia do MCP entra no jogo sem passar pelos gates completos:
  glTF-Validator -> validate_asset -> validate_visual -> Fab3dImporter + captura datada.
"""

import json
import os
import subprocess
import sys
import time
import uuid
from datetime import datetime, timezone
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from adapters import get_adapter  # noqa: E402

ROOT = Path(__file__).resolve().parent.parent.parent
WORK_DIR = ROOT / ".studio" / "mcp_fab3d_jobs"


TOOL_SPECS = [
    {"name": "generate_asset", "description": "Cria e executa um job completo de geração 3D (image_to_3d + texture_mesh) via adapter configurado (real=TRELLIS.2-4B+Hunyuan3D-Paint padrão; mock=procedural sem GPU)."},
    {"name": "image_to_3d", "description": "Converte imagem de conceito aprovada em malha 3D (.glb) via adapter ativo."},
    {"name": "texture_mesh", "description": "Aplica estágio de pintura PBR sobre uma malha/job existente."},
    {"name": "export_asset", "description": "Exporta o asset do job passando OBRIGATORIAMENTE pela ponte fab3d_bridge.py + gates (glTF-Validator -> validate_asset -> validate_visual). Sem bypass."},
    {"name": "get_job", "description": "Retorna status, metadados e artefatos de um job por job_id."},
    {"name": "wait_for_job", "description": "Aguarda conclusão de um job até timeout_sec."},
    {"name": "list_assets", "description": "Lista assets produzidos/exportados pelo serviço MCP e seu status de gate."},
    {"name": "system_status", "description": "Retorna saúde do serviço MCP, adapter ativo (real/mock), caminhos canônicos e política de gates sem bypass."},
]


class Fab3DMCPService:
    def __init__(self, adapter_mode: str = None, work_dir: Path = None, engine: str = None):
        self.adapter_mode = (adapter_mode or os.environ.get("FAB3D_ADAPTER", "real")).strip().lower()
        self.adapter = get_adapter(self.adapter_mode, engine=engine)
        self.engine = self.adapter.engine
        self.work_dir = Path(work_dir) if work_dir else WORK_DIR
        self.work_dir.mkdir(parents=True, exist_ok=True)
        self._jobs = {}

    def system_status(self) -> dict:
        return {
            "service": "studio-fab3d-mcp",
            "version": "1.1.0",
            "attribution": "Baseado em MyMeshy (MIT, github.com/felippeomgt/mymeshy)",
            "adapter_mode": self.adapter_mode,
            "adapter_name": self.adapter.name,
            "engine": self.engine,
            "fallback_engine": "trellis2",
            "default_production_route": "TripoSG (padrão) / TRELLIS.2-4B (fallback) + Hunyuan3D-Paint + pack_glb modo game",
            "gate_bypass_allowed": False,
            "mandatory_gates": [
                "glTF-Validator (tools/validation/validate_gltf.py)",
                "validate_asset (tools/validation/validate_asset.py)",
                "validate_visual (tools/validation/validate_visual.py)",
                "Fab3dImporter + captura datada (unity/Assets/Editor/Fab3dImporter.cs)",
            ],
            "active_jobs": len(self._jobs),
            "timestamp": datetime.now(timezone.utc).isoformat(),
        }

    def image_to_3d(self, asset_name: str, image_path: str = "", category: str = "small_prop_item", engine: str = None) -> dict:
        job_id = f"job_{asset_name}_{uuid.uuid4().hex[:8]}"
        job_dir = self.work_dir / job_id
        job_dir.mkdir(parents=True, exist_ok=True)
        job = {
            "job_id": job_id,
            "asset_name": asset_name,
            "category": category,
            "image_path": image_path,
            "adapter": self.adapter.name,
            "engine": engine or self.engine,
            "stage": "image_to_3d",
            "status": "running",
            "created_at": datetime.now(timezone.utc).isoformat(),
            "gates_passed": False,
        }
        self._jobs[job_id] = job
        try:
            res = self.adapter.image_to_3d(asset_name, image_path, job_dir, category, engine=engine)
            job.update(res)
            job["status"] = "completed"
            job["completed_at"] = datetime.now(timezone.utc).isoformat()
        except Exception as exc:
            job["status"] = "failed"
            job["error"] = str(exc)
        return job

    def texture_mesh(self, job_id: str) -> dict:
        job = self._jobs.get(job_id)
        if not job:
            return {"error": f"job_id não encontrado: {job_id}"}
        if job.get("status") != "completed" or not job.get("glb_path"):
            return {"error": f"job {job_id} não possui malha concluída para texturização"}
        job["stage"] = "texture_mesh"
        res = self.adapter.texture_mesh(job["asset_name"], Path(job["glb_path"]), self.work_dir / job_id)
        job.update(res)
        return job

    def generate_asset(self, asset_name: str, image_path: str = "", category: str = "small_prop_item", engine: str = None) -> dict:
        job = self.image_to_3d(asset_name=asset_name, image_path=image_path, category=category, engine=engine)
        if job.get("status") == "completed":
            job = self.texture_mesh(job["job_id"])
            job["stage"] = "generate_asset_done"
        return job

    def get_job(self, job_id: str) -> dict:
        return self._jobs.get(job_id, {"error": f"job_id não encontrado: {job_id}"})

    def wait_for_job(self, job_id: str, timeout_sec: float = 30.0) -> dict:
        deadline = time.time() + timeout_sec
        while time.time() < deadline:
            job = self._jobs.get(job_id)
            if not job:
                return {"error": f"job_id não encontrado: {job_id}"}
            if job["status"] in ("completed", "failed"):
                return job
            time.sleep(0.2)
        return {"job_id": job_id, "status": "timeout"}

    def export_asset(self, job_id: str, out_dir: str = None) -> dict:
        """Exporta obrigatoriamente via fab3d_bridge.py (glTF-Validator -> validate_asset -> validate_visual). Sem bypass."""
        job = self._jobs.get(job_id)
        if not job or not job.get("glb_path"):
            return {"exported": False, "error": f"job inválido ou sem GLB: {job_id}"}

        target_out = Path(out_dir).resolve() if out_dir else (self.work_dir / job_id / "exported_game_asset")
        bridge = ROOT / "tools" / "art" / "ai_generator" / "fab3d_bridge.py"
        cmd = [
            sys.executable,
            str(bridge),
            "--from-glb", str(job["glb_path"]),
            "--name", job["asset_name"],
            "--category", job["category"],
            "--out", str(target_out),
        ]
        proc = subprocess.run(cmd, capture_output=True, text=True)
        # Limpa eventuais PNGs soltos criados pelo unpack do Blender no CWD
        for junk in ("Image_0.png", "Image_0.jpg", "Image_1.png", "Image_1.jpg"):
            jp = ROOT / junk
            if jp.exists():
                jp.unlink(missing_ok=True)

        ok = proc.returncode == 0
        job["exported"] = ok
        job["export_dir"] = str(target_out)
        job["gates_passed"] = ok
        job["gate_reports"] = {
            "gltf_validator": f"evidence/validation/gltf_validator_{job['asset_name']}.json",
            "validate_visual": f"evidence/validation/validate_visual_{job['asset_name']}.json",
            "bridge_report": f"evidence/art/fab3d_bridge_{job['asset_name']}.json",
        }
        if not ok:
            job["export_error"] = (proc.stdout + "\n" + proc.stderr)[-1200:]
        return {
            "job_id": job_id,
            "asset_name": job["asset_name"],
            "exported": ok,
            "gates_passed": ok,
            "export_dir": str(target_out),
            "gate_reports": job["gate_reports"],
            "exit_code": proc.returncode,
            "error": job.get("export_error"),
        }

    def list_assets(self) -> dict:
        return {
            "count": len(self._jobs),
            "assets": [
                {
                    "job_id": j["job_id"],
                    "asset_name": j["asset_name"],
                    "category": j["category"],
                    "status": j["status"],
                    "gates_passed": j.get("gates_passed", False),
                    "adapter": j.get("adapter"),
                }
                for j in self._jobs.values()
            ],
        }

    def dispatch(self, tool_name: str, arguments: dict) -> dict:
        if tool_name == "system_status":
            return self.system_status()
        if tool_name == "generate_asset":
            return self.generate_asset(**arguments)
        if tool_name == "image_to_3d":
            return self.image_to_3d(**arguments)
        if tool_name == "texture_mesh":
            return self.texture_mesh(**arguments)
        if tool_name == "export_asset":
            return self.export_asset(**arguments)
        if tool_name == "get_job":
            return self.get_job(**arguments)
        if tool_name == "wait_for_job":
            return self.wait_for_job(**arguments)
        if tool_name == "list_assets":
            return self.list_assets()
        return {"error": f"Ferramenta MCP desconhecida: {tool_name}"}


def run_stdio_mcp():
    svc = Fab3DMCPService()
    for line in sys.stdin:
        line = line.strip()
        if not line:
            continue
        req = json.loads(line)
        req_id = req.get("id")
        method = req.get("method")
        if method == "initialize":
            resp = {
                "jsonrpc": "2.0",
                "id": req_id,
                "result": {
                    "protocolVersion": "2024-11-05",
                    "capabilities": {"tools": {}},
                    "serverInfo": {"name": "studio-fab3d-mcp", "version": "1.0.0"},
                },
            }
        elif method == "tools/list":
            resp = {"jsonrpc": "2.0", "id": req_id, "result": {"tools": TOOL_SPECS}}
        elif method == "tools/call":
            params = req.get("params", {})
            out = svc.dispatch(params.get("name", ""), params.get("arguments", {}))
            resp = {
                "jsonrpc": "2.0",
                "id": req_id,
                "result": {"content": [{"type": "text", "text": json.dumps(out, ensure_ascii=False)}]},
            }
        else:
            resp = {"jsonrpc": "2.0", "id": req_id, "error": {"code": -32601, "message": f"Method not found: {method}"}}
        sys.stdout.write(json.dumps(resp) + "\n")
        sys.stdout.flush()


if __name__ == "__main__":
    run_stdio_mcp()
