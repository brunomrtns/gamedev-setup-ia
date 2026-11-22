#!/usr/bin/env python3
"""
tools/mcp_fab3d/adapters.py
Adapters de geração para o serviço MCP da Fábrica 3D (P1 — base: MyMeshy, MIT).

- RealFab3DAdapter (PADRÃO): chama o pipeline real da Fábrica 3D
  (TRELLIS.2-4B + Hunyuan3D-Paint + pack_glb modo game via 3dprinter-setup-ia).
- MockProceduralAdapter (config/env FAB3D_ADAPTER=mock): gera um GLB PBR
  canônico via Blender headless SEM GPU, permitindo testar toda a fila MCP
  e os gates (glTF-Validator -> validate_asset -> validate_visual) offline.
"""

import json
import os
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent.parent


def _to_wsl(p: Path) -> str:
    p = p.resolve()
    drive = p.drive.replace(":", "").lower()
    return f"/mnt/{drive}{p.as_posix().split(':', 1)[-1]}"


class BaseFab3DAdapter:
    name = "base"

    def image_to_3d(self, asset_name: str, image_path: str, out_dir: Path, category: str) -> dict:
        raise NotImplementedError

    def texture_mesh(self, asset_name: str, mesh_path: Path, out_dir: Path) -> dict:
        raise NotImplementedError


class RealFab3DAdapter(BaseFab3DAdapter):
    """Rota REAL padrão: TRELLIS.2-4B + Hunyuan3D-Paint + pack_glb modo game."""

    name = "real_trellis2_hunyuan_paint"

    def __init__(self, fab3d_repo: Path = None):
        self.fab3d_repo = fab3d_repo or Path(
            os.environ.get("FAB3D_REPO", r"C:\Users\Bruno\workspace\3dprinter-setup-ia")
        )

    def image_to_3d(self, asset_name: str, image_path: str, out_dir: Path, category: str) -> dict:
        runner = self.fab3d_repo / "pipeline" / "run_asset.py"
        if not runner.exists():
            raise RuntimeError(f"Runner real da Fábrica 3D ausente em {runner}")
        out_dir.mkdir(parents=True, exist_ok=True)
        cfg_path = out_dir / f"{asset_name}_job_config.json"
        cfg_path.write_text(
            json.dumps(
                {
                    "asset_id": asset_name,
                    "input_image": image_path,
                    "category": category,
                    "generator": "TRELLIS.2-4B",
                    "painter": "Hunyuan3D-Paint",
                    "pack_mode": "game",
                },
                indent=2,
            ),
            encoding="utf-8",
        )
        res = subprocess.run(
            ["python", str(runner), "--config", str(cfg_path), "--out", str(out_dir)],
            capture_output=True,
            text=True,
        )
        if res.returncode != 0:
            raise RuntimeError(f"RealFab3DAdapter falhou (exit {res.returncode}): {res.stderr[-800:]}")
        glbs = sorted(out_dir.rglob("*.glb"), key=lambda g: g.stat().st_size, reverse=True)
        if not glbs:
            raise RuntimeError("RealFab3DAdapter não produziu nenhum .glb")
        return {"glb_path": str(glbs[0]), "adapter": self.name, "textured": True}

    def texture_mesh(self, asset_name: str, mesh_path: Path, out_dir: Path) -> dict:
        return {"glb_path": str(mesh_path), "adapter": self.name, "textured": True, "note": "Hunyuan3D-Paint integrado no pipeline real"}


class MockProceduralAdapter(BaseFab3DAdapter):
    """Adapter Mock procedural (sem GPU) via Blender headless para CI/smoke dos gates."""

    name = "mock_procedural_blender"

    def image_to_3d(self, asset_name: str, image_path: str, out_dir: Path, category: str) -> dict:
        out_dir.mkdir(parents=True, exist_ok=True)
        glb_out = out_dir / f"{asset_name}__mock_game.glb"
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
        return {"glb_path": str(glb_out), "adapter": self.name, "textured": True}

    def texture_mesh(self, asset_name: str, mesh_path: Path, out_dir: Path) -> dict:
        return {
            "glb_path": str(mesh_path),
            "adapter": self.name,
            "textured": True,
            "note": "Mock PBR BaseColor já empacotado no GLB",
        }


def get_adapter(mode: str = None) -> BaseFab3DAdapter:
    chosen = (mode or os.environ.get("FAB3D_ADAPTER", "real")).strip().lower()
    if chosen == "mock":
        return MockProceduralAdapter()
    return RealFab3DAdapter()
