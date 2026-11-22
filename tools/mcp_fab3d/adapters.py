#!/usr/bin/env python3
"""
tools/mcp_fab3d/adapters.py
Adapters de geração para o serviço MCP da Fábrica 3D (P1 — base: MyMeshy, MIT).

- RealFab3DAdapter (PADRÃO): chama o pipeline real da Fábrica 3D
  (TripoSG padrão [decisão do dono 2026-11-22 / benchmark Fase C] ou
  TRELLIS.2-4B fallback + Hunyuan3D-Paint + pack_glb modo game via
  3dprinter-setup-ia, usando runners e cache HF já existentes — zero download).
- MockProceduralAdapter (config/env FAB3D_ADAPTER=mock): gera um GLB PBR
  canônico via Blender headless SEM GPU (suportando engine=triposg|trellis2),
  permitindo testar toda a fila MCP e os gates offline.
"""

import json
import os
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent.parent
SUPPORTED_ENGINES = ("triposg", "trellis2")
DEFAULT_ENGINE = "triposg"


def _validate_engine(engine: str | None) -> str:
    chosen = (engine or os.environ.get("FAB3D_ENGINE", DEFAULT_ENGINE)).strip().lower()
    if chosen not in SUPPORTED_ENGINES:
        raise ValueError(
            f"Engine '{chosen}' não suportada para produção de jogo. "
            f"Permitidas: {SUPPORTED_ENGINES} (trellis1/triposr descontinuados; TripoAI-nuvem banido)."
        )
    return chosen


def _to_wsl(p: Path) -> str:
    p = p.resolve()
    drive = p.drive.replace(":", "").lower()
    return f"/mnt/{drive}{p.as_posix().split(':', 1)[-1]}"


class BaseFab3DAdapter:
    name = "base"
    engine = DEFAULT_ENGINE

    def image_to_3d(self, asset_name: str, image_path: str, out_dir: Path, category: str, engine: str = None) -> dict:
        raise NotImplementedError

    def texture_mesh(self, asset_name: str, mesh_path: Path, out_dir: Path) -> dict:
        raise NotImplementedError


class RealFab3DAdapter(BaseFab3DAdapter):
    """Rota REAL padrão: TripoSG (padrão) / TRELLIS.2-4B (fallback) + Hunyuan3D-Paint + pack_glb modo game."""

    name = "real_triposg_hunyuan_paint"

    def __init__(self, fab3d_repo: Path = None, engine: str = None):
        self.fab3d_repo = fab3d_repo or Path(
            os.environ.get("FAB3D_REPO", r"C:\Users\Bruno\workspace\3dprinter-setup-ia")
        )
        self.engine = _validate_engine(engine)
        self.name = f"real_{self.engine}_hunyuan_paint"

    def _run_shape_engine(self, chosen_engine: str, image_path: str, shape_dir: Path) -> Path:
        shape_dir.mkdir(parents=True, exist_ok=True)
        if chosen_engine == "triposg":
            runner = self.fab3d_repo / "tools" / "generator" / "triposg_runner.py"
            if not runner.exists():
                raise RuntimeError(f"triposg_runner.py ausente em {runner}")
            cmd = [
                "wsl", "bash", "-lc",
                f"TRIPOSG_HOME=/home/bruno/tools/TripoSG /home/bruno/.venvs/triposg/bin/python "
                f"{_to_wsl(runner)} --image {_to_wsl(Path(image_path))} --output-dir {_to_wsl(shape_dir)} --seed 42"
            ]
        else:
            runner = Path("/home/bruno/printer3d-work/trellis2_runner.py")
            cmd = [
                "wsl", "/home/bruno/.venvs/trellis2/bin/python",
                str(runner), "--image", _to_wsl(Path(image_path)), "--output-dir", _to_wsl(shape_dir), "--seed", "42"
            ]
        res = subprocess.run(cmd, capture_output=True, text=True)
        mesh_obj = shape_dir / "mesh.obj"
        if res.returncode != 0 or not mesh_obj.exists():
            raise RuntimeError(f"Shape engine '{chosen_engine}' falhou (exit {res.returncode}): {res.stderr[-800:]}")
        return mesh_obj

    def image_to_3d(self, asset_name: str, image_path: str, out_dir: Path, category: str, engine: str = None) -> dict:
        chosen_engine = _validate_engine(engine or self.engine)
        runner = self.fab3d_repo / "pipeline" / "run_asset.py"
        if not runner.exists():
            raise RuntimeError(f"Runner real da Fábrica 3D ausente em {runner}")
        out_dir.mkdir(parents=True, exist_ok=True)
        shape_dir = out_dir / f"shape_{chosen_engine}"
        fallback_used = False
        fallback_reason = None
        try:
            mesh_obj = self._run_shape_engine(chosen_engine, image_path, shape_dir)
        except RuntimeError as exc:
            if chosen_engine == "triposg":
                fallback_used = True
                fallback_reason = str(exc)[:300]
                chosen_engine = "trellis2"
                mesh_obj = self._run_shape_engine("trellis2", image_path, out_dir / "shape_trellis2_fallback")
            else:
                raise

        cfg_path = out_dir / f"{asset_name}_job_config.json"
        cfg_path.write_text(
            json.dumps(
                {
                    "id": asset_name,
                    "variant_id": "pbr",
                    "mesh": _to_wsl(mesh_obj),
                    "uv": _to_wsl(mesh_obj),
                    "tier": "single",
                    "input_images": [_to_wsl(Path(image_path))],
                    "outputs": {
                        "mode_8k": "pbr_8k",
                        "mode_4k": "pbr_4k",
                        "glb_names": [f"{asset_name}__pbr_8k.glb", f"{asset_name}__pbr_4k.glb"],
                    },
                    "engine": chosen_engine,
                    "fallback_used": fallback_used,
                },
                indent=2,
            ),
            encoding="utf-8",
        )
        res = subprocess.run(
            ["wsl", "/home/bruno/.venvs/hunyuan/bin/python", _to_wsl(runner), "--config", _to_wsl(cfg_path), "--out", _to_wsl(out_dir / "pipe_out"), "--resume"],
            capture_output=True,
            text=True,
        )
        if res.returncode != 0:
            raise RuntimeError(f"RealFab3DAdapter falhou (exit {res.returncode}): {res.stderr[-800:]}")
        glbs = sorted(out_dir.rglob("*__pbr_4k.glb")) or sorted(out_dir.rglob("*.glb"), key=lambda g: g.stat().st_size, reverse=True)
        if not glbs:
            raise RuntimeError("RealFab3DAdapter não produziu nenhum .glb")
        return {
            "glb_path": str(glbs[0]),
            "adapter": self.name,
            "engine": chosen_engine,
            "fallback_used": fallback_used,
            "fallback_reason": fallback_reason,
            "textured": True,
        }

    def texture_mesh(self, asset_name: str, mesh_path: Path, out_dir: Path) -> dict:
        return {"glb_path": str(mesh_path), "adapter": self.name, "engine": self.engine, "textured": True, "note": "Hunyuan3D-Paint integrado no pipeline real"}


class MockProceduralAdapter(BaseFab3DAdapter):
    """Adapter Mock procedural (sem GPU) via Blender headless para CI/smoke dos gates."""

    name = "mock_procedural_blender"

    def __init__(self, engine: str = None):
        self.engine = _validate_engine(engine)

    def image_to_3d(self, asset_name: str, image_path: str, out_dir: Path, category: str, engine: str = None) -> dict:
        chosen_engine = _validate_engine(engine or self.engine)
        out_dir.mkdir(parents=True, exist_ok=True)
        glb_out = out_dir / f"{asset_name}__mock_{chosen_engine}_game.glb"
        bpy_script = out_dir / "_mock_gen.py"
        is_biped = category in ("player_character", "enemy_character", "biped")

        code = f"""
import bpy, math, os

for o in list(bpy.data.objects): bpy.data.objects.remove(o, do_unlink=True)
for m in list(bpy.data.meshes): bpy.data.meshes.remove(m)
for mat in list(bpy.data.materials): bpy.data.materials.remove(mat)

if {str(is_biped)}:
    for sx in (-1, 1):
        bpy.ops.mesh.primitive_cylinder_add(vertices=12, radius=0.075, depth=0.80, location=(sx*0.10, 0, 0.40))
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0, 0, 1.08))
    bpy.context.active_object.scale = (0.38, 0.22, 0.56)
    bpy.ops.mesh.primitive_uv_sphere_add(segments=12, ring_count=8, radius=0.14, location=(0, 0, 1.54))
else:
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0, 0, 0.55))
    bpy.context.active_object.scale = (1.1, 1.1, 1.1)
    bpy.ops.mesh.primitive_cylinder_add(vertices=12, radius=0.22, depth=0.35, location=(0, 0, 1.25))

meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
bpy.ops.object.select_all(action='DESELECT')
for o in meshes: o.select_set(True)
bpy.context.view_layer.objects.active = meshes[0]
if len(meshes) > 1:
    bpy.ops.object.join()
obj = bpy.context.view_layer.objects.active
obj.name = "{asset_name}"
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
bpy.ops.object.mode_set(mode='EDIT')
bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.mesh.quads_convert_to_tris(quad_method='BEAUTY', ngon_method='BEAUTY')
bpy.ops.uv.smart_project(angle_limit=math.radians(66.0), island_margin=0.02, scale_to_bounds=True)
bpy.ops.object.mode_set(mode='OBJECT')

# Packed PBR BaseColor + MetallicRoughness image so GLB is self-contained
img_bc = bpy.data.images.new(name="{asset_name}_bc", width=256, height=256)
px = [0.78, 0.52, 0.22, 1.0] * (256 * 256)
img_bc.pixels = px
img_bc.pack()

mat = bpy.data.materials.new(name="M_{asset_name}")
mat.use_nodes = True
bsdf = mat.node_tree.nodes.get("Principled BSDF")
tex_node = mat.node_tree.nodes.new("ShaderNodeTexImage")
tex_node.image = img_bc
mat.node_tree.links.new(tex_node.outputs["Color"], bsdf.inputs["Base Color"])
obj.data.materials.clear()
obj.data.materials.append(mat)

bpy.ops.export_scene.gltf(filepath="{_to_wsl(glb_out)}", export_format='GLB')
"""
        bpy_script.write_text(code, encoding="utf-8")
        res = subprocess.run(
            ["wsl", "blender", "-b", "-P", _to_wsl(bpy_script)],
            capture_output=True,
            text=True,
        )
        bpy_script.unlink(missing_ok=True)
        if res.returncode != 0 or not glb_out.exists():
            raise RuntimeError(f"MockProceduralAdapter falhou: {res.stderr[-600:]}")
        return {
            "glb_path": str(glb_out),
            "adapter": self.name,
            "engine": chosen_engine,
            "fallback_used": False,
            "textured": True,
        }

    def texture_mesh(self, asset_name: str, mesh_path: Path, out_dir: Path) -> dict:
        return {
            "glb_path": str(mesh_path),
            "adapter": self.name,
            "engine": self.engine,
            "textured": True,
            "note": "Mock PBR BaseColor já empacotado no GLB",
        }


def get_adapter(mode: str = None, engine: str = None) -> BaseFab3DAdapter:
    chosen = (mode or os.environ.get("FAB3D_ADAPTER", "real")).strip().lower()
    chosen_engine = _validate_engine(engine)
    if chosen == "mock":
        return MockProceduralAdapter(engine=chosen_engine)
    return RealFab3DAdapter(engine=chosen_engine)
