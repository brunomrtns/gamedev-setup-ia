#!/usr/bin/env python3
"""
tools/validation/studio_validators.py
Biblioteca compartilhada V2 dos validadores determinísticos do estúdio.

Funcionalidades:
- Índice GUID -> caminho de asset (scan de .meta)
- Checagem de materiais URP Lit (_BaseMap, cores chapadas monocromáticas)
- Detecção de primitivas nativas (cubos/esferas/etc.) em cenas e prefabs
- Parser ASCII FBX (Kenney) para transformação/UV/triangulação
- Delegação de inspeção de FBX binário ao Blender headless (WSL)
"""

import os
import re
import sys
import json
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent.parent
ASSETS = ROOT / "unity" / "Assets"

# Shader canônico Universal Render Pipeline/Lit (verificado no projeto)
URP_LIT_SHADER_GUID = "933532a4fcc9baf4fa0491de14d08ed7"
URP_ALLOWED_SHADER_GUIDS = {URP_LIT_SHADER_GUID}

# Malhas built-in do Unity (fileID sem guid em MeshFilter)
BUILTIN_PRIMITIVE_MESH_IDS = {
    "10202": "Cube", "10206": "Cylinder", "10207": "Sphere",
    "10208": "Capsule", "10209": "Plane", "10210": "Quad",
}

# Materiais isentos da regra de Albedo (UI, efeitos, céu, debug)
MATERIAL_EXEMPT_PREFIXES = ("Mat_UI_", "Mat_FX_", "Mat_Skybox_", "Mat_Debug_", "Mat_Lightcone_")

# Nomes de GameObjects isentos da proibição de primitiva (utilitários invisíveis)
PRIMITIVE_EXEMPT_NAME_PATTERN = re.compile(
    r"(trigger|collider|invisible|ghost|bounds|culling|gizmo|system|sensor|blocker)", re.IGNORECASE)

_MAT_GUID_RE = re.compile(r"\{fileID: 2100000, guid: ([a-f0-9]{32}), type: 2\}")
_SHADER_GUID_RE = re.compile(r"m_Shader: \{fileID: 4800000, guid: ([a-f0-9]{32}), type: 3\}")
_TEX_PROP_RE = re.compile(r"m_TexEnvs:\n(?:.*\n)*?\s*_BaseMap:\n\s*m_Texture: \{fileID: (?:2800000, guid: ([a-f0-9]{32})|\d+)\}")
_MESH_FILEID_RE = re.compile(r"m_Mesh: \{fileID: (\d+)(?:, guid: ([a-f0-9]{32}))?")
_COLOR_RE = re.compile(
    r"_Color: \{r: ([-\d.e]+), g: ([-\d.e]+), b: ([-\d.e]+), a: ([-\d.e]+)\}")


_guid_index_cache = None


def guid_index():
    """Mapeia GUID de asset -> caminho relativo dentro de Assets (scan de .meta)."""
    global _guid_index_cache
    if _guid_index_cache is not None:
        return _guid_index_cache
    index = {}
    for meta in ASSETS.rglob("*.meta"):
        try:
            first = meta.read_text(encoding="utf-8", errors="replace").splitlines()[1]
            m = re.search(r"guid: ([a-f0-9]{32})", first)
            if m:
                asset = meta.with_suffix("")  # remove sufixo .meta -> o proprio asset
                index[m.group(1)] = asset.relative_to(ASSETS).as_posix()
        except (OSError, IndexError):
            continue
    _guid_index_cache = index
    return index


def is_builtin_mesh_guid(guid):
    """GUIDs reservados da Unity (built-in resources) — ex: 0000000000000000e000000000000000."""
    return (not guid) or guid.startswith("0000000000000000e") or set(guid) <= {"0"}


def resolve_guid(guid):
    """Retorna caminho relativo (Assets/...) do asset ou None."""
    rel = guid_index().get(guid)
    return f"Assets/{rel}" if rel else None


def read_yaml(path):
    return Path(path).read_text(encoding="utf-8", errors="replace")


# ----------------------------------------------------------------------------
# Materiais (.mat)
# ----------------------------------------------------------------------------

def check_material(mat_path):
    """
    Valida um material URP para renderização 3D.
    Retorna (ok, issues, warnings).
    Falha se: shader não-URP-Lit (para materiais de geometria), _BaseMap nulo,
    ou cor chapada monocromática sem textura.
    """
    issues, warnings = [], []
    name = Path(mat_path).stem
    content = read_yaml(mat_path)

    if name.startswith(MATERIAL_EXEMPT_PREFIXES):
        return True, [], []

    shader_m = _SHADER_GUID_RE.search(content)
    if not shader_m:
        issues.append(f"{name}: sem shader resolvido (possível magenta/InternalErrorShader)")
        return False, issues, warnings
    if shader_m.group(1) not in URP_ALLOWED_SHADER_GUIDS:
        # Materiais de geometria devem ser URP Lit; desconhecidos avisam, errados falham
        resolved = resolve_guid(shader_m.group(1))
        if resolved and ("/URP/" in resolved or "Universal" in resolved):
            warnings.append(f"{name}: shader URP não-Lit ({resolved})")
        else:
            issues.append(f"{name}: shader incompatível guid={shader_m.group(1)} "
                          f"(resolved={resolved}) — magenta/incompatible")

    # _BaseMap: presente com guid de textura válido
    base_m = re.search(r"_BaseMap:\s*\n\s*m_Texture: \{fileID: 2800000, guid: ([a-f0-9]{32})", content)
    has_base = bool(base_m)
    if not has_base:
        null_m = re.search(r"_BaseMap:\s*\n\s*m_Texture: \{fileID: 0\}", content)
        if null_m or not re.search(r"_BaseMap:", content):
            # Sem Albedo: permitido apenas se emissivo texturizado OU isento
            emis_m = re.search(r"_EmissionMap:\s*\n\s*m_Texture: \{fileID: 2800000, guid: ([a-f0-9]{32})", content)
            if not emis_m:
                color = _COLOR_RE.search(content)
                if color:
                    r, g, b = float(color.group(1)), float(color.group(2)), float(color.group(3))
                    mono = abs(r - g) < 0.02 and abs(g - b) < 0.02
                    issues.append(f"{name}: _BaseMap (Albedo) nulo"
                                  + (" com cor chapada monocromática" if mono else ""))
                else:
                    issues.append(f"{name}: _BaseMap (Albedo) nulo")
        else:
            warnings.append(f"{name}: _BaseMap presente mas não-resolvido")
    else:
        if not resolve_guid(base_m.group(1)):
            issues.append(f"{name}: _BaseMap aponta para textura inexistente (guid {base_m.group(1)})")

    return len(issues) == 0, issues, warnings


# ----------------------------------------------------------------------------
# Prefabs
# ----------------------------------------------------------------------------

def split_blocks(content):
    """Divide YAML Unity em blocos: [(class, id, corpo)]."""
    blocks = []
    parts = re.split(r"^--- !u!(\d+) &(\d+)\s*$", content, flags=re.MULTILINE)
    for i in range(1, len(parts), 3):
        blocks.append((parts[i], parts[i + 1], parts[i + 2]))
    return blocks


def check_prefab(prefab_path):
    """
    Valida prefab de renderização 3D:
    - Materiais com URP Lit + _BaseMap (sem cores chapadas monocromáticas)
    - Sem malhas primitivas nativas (Cube/Sphere/...) no lugar de prefabs/malhas de pipeline
    Retorna (ok, issues, warnings).
    """
    issues, warnings = [], []
    content = read_yaml(prefab_path)
    name = Path(prefab_path).stem

    gameobject_names = {}
    for cls, oid, body in split_blocks(content):
        if cls == "1":  # GameObject
            nm = re.search(r"m_Name: (.+)", body)
            gameobject_names[oid] = nm.group(1).strip() if nm else ""

    # Materiais referenciados (inline ou overrides de PrefabInstance)
    mat_guids = set(_MAT_GUID_RE.findall(content))
    for g in mat_guids:
        mat_rel = resolve_guid(g)
        if not mat_rel:
            issues.append(f"{name}: material não resolvido guid={g} (renderiza magenta)")
            continue
        mat_path = ASSETS / mat_rel[len("Assets/"):]
        if mat_path.suffix == ".mat":
            ok, sub_issues, sub_warns = check_material(mat_path)
            issues += [f"{name}: {i}" for i in sub_issues]
            warnings += [f"{name}: {w}" for w in sub_warns]

    # Primitivas nativas
    for cls, oid, body in split_blocks(content):
        if cls == "33":  # MeshFilter
            m = _MESH_FILEID_RE.search(body)
            if m and is_builtin_mesh_guid(m.group(2)) and m.group(1) in BUILTIN_PRIMITIVE_MESH_IDS:
                go = re.search(r"m_GameObject: \{fileID: (\d+)\}", body)
                go_name = gameobject_names.get(go.group(1), "") if go else ""
                if not PRIMITIVE_EXEMPT_NAME_PATTERN.search(go_name):
                    issues.append(
                        f"{name}: primitiva nativa {BUILTIN_PRIMITIVE_MESH_IDS[m.group(1)]} "
                        f"em '{go_name}' — use prefabs do pipeline modular")

    return len(issues) == 0, issues, warnings


# ----------------------------------------------------------------------------
# Cenas
# ----------------------------------------------------------------------------

def check_scene(scene_path):
    """
    Validação V2 de cena .unity:
    - Sem Missing Scripts (fileID 0)
    - Todos os materiais de MeshRenderer resolvidos e URP Lit com _BaseMap
    - Sem primitivas nativas visíveis (CreatePrimitive) fora de isenções utilitárias
    - PrefabInstances apontando para prefabs existentes
    Retorna (ok, issues, warnings).
    """
    issues, warnings = [], []
    content = read_yaml(scene_path)

    missing_scripts = re.findall(r"m_Script:\s*\{fileID:\s*0[,\s\}]", content)
    if missing_scripts:
        issues.append(f"{len(missing_scripts)} componentes com Missing Script (fileID: 0)")

    gameobject_names = {}
    for cls, oid, body in split_blocks(content):
        if cls == "1":
            nm = re.search(r"m_Name: (.+)", body)
            gameobject_names[oid] = nm.group(1).strip() if nm else ""

    # 1. Materiais inline de MeshRenderer
    meshrenderer_materials = set()
    for cls, oid, body in split_blocks(content):
        if cls == "23":  # MeshRenderer
            meshrenderer_materials.update(_MAT_GUID_RE.findall(body))
    for g in meshrenderer_materials:
        mat_rel = resolve_guid(g)
        if not mat_rel:
            issues.append(f"material não resolvido guid={g} em MeshRenderer (magenta)")
            continue
        mat_path = ASSETS / mat_rel[len("Assets/"):]
        if mat_path.suffix == ".mat":
            ok, sub_issues, sub_warns = check_material(mat_path)
            issues += sub_issues
            warnings += sub_warns

    # 2. Primitivas nativas com MeshRenderer
    renderers_by_go = set()
    for cls, oid, body in split_blocks(content):
        if cls == "23":
            go = re.search(r"m_GameObject: \{fileID: (\d+)\}", body)
            if go:
                renderers_by_go.add(go.group(1))
    for cls, oid, body in split_blocks(content):
        if cls == "33":  # MeshFilter
            m = _MESH_FILEID_RE.search(body)
            if m and is_builtin_mesh_guid(m.group(2)) and m.group(1) in BUILTIN_PRIMITIVE_MESH_IDS:
                go = re.search(r"m_GameObject: \{fileID: (\d+)\}", body)
                go_id = go.group(1) if go else None
                go_name = gameobject_names.get(go_id, "")
                if go_id not in renderers_by_go:
                    continue  # sem MeshRenderer: utilitário invisível
                if PRIMITIVE_EXEMPT_NAME_PATTERN.search(go_name):
                    continue
                issues.append(f"primitiva nativa {BUILTIN_PRIMITIVE_MESH_IDS[m.group(1)]} visível "
                              f"em '{go_name}' — substitua por prefab modular texturizado")

    # 3. Câmera e luz (avisos de conformidade)
    if "--- !u!20 &" not in content:
        warnings.append("nenhuma câmera padrão detectada no arquivo de cena")
    if "--- !u!108 &" not in content:
        warnings.append("nenhuma Light detectada na cena")
    if len(content.strip().splitlines()) < 10:
        issues.append("cena corrompida ou vazia (< 10 linhas)")

    # 4. PrefabInstances resolvem para prefabs existentes
    prefab_guids = set(re.findall(r"m_SourcePrefab: \{fileID: 100100000, guid: ([a-f0-9]{32})", content))
    for g in prefab_guids:
        rel = resolve_guid(g)
        # fonte válida: .prefab OU asset de modelo (.fbx/.obj — PrefabInstance
        # de modelo é legítimo no Unity; ex: variantes danificadas)
        if not rel or not rel.endswith((".prefab", ".fbx", ".FBX", ".obj")):
            issues.append(f"PrefabInstance com fonte inexistente guid={g}")

    return len(issues) == 0, issues, warnings


# ----------------------------------------------------------------------------
# FBX
# ----------------------------------------------------------------------------

def is_binary_fbx(path):
    try:
        with open(path, "rb") as f:
            return f.read(21).startswith(b"Kaydara FBX Binary")
    except OSError:
        return False


def check_ascii_fbx(path):
    """Parser determinístico para FBX ASCII (Kenney): rotação, UV, materiais."""
    issues, warnings = [], []
    try:
        content = Path(path).read_text(encoding="utf-8", errors="replace")
    except OSError as e:
        return False, [f"ilegível: {e}"], warnings

    if "LayerElementUV" not in content or re.search(r"\bUV:\s*\*", content) is None and "UVIndex" not in content:
        issues.append("sem UV0 (LayerElementUV ausente)")

    for axis, val in re.findall(r'Property: "Lcl (\w+)", "Lcl (\w+)",\s*"(-?[\d.]+)"', content):
        num = float(val)
        if abs(num) > 0.001 and abs(abs(num) - 360.0) > 0.001:
            issues.append(f"rotação residual no root: Lcl {axis}={num} (conversão de eixos descompensada)")

    if "Model:" in content and "Geometry:" not in content:
        warnings.append("sem geometria explícita (nó de modelo vazio)")
    return len(issues) == 0, issues, warnings


def inspect_binary_fbxs(paths, timeout=1500):
    """
    Delega inspeção de FBX binários ao Blender headless (WSL) em UMA sessão.
    Retorna dict caminho->report ou None em falha de delegação.
    """
    if not paths:
        return {}
    manifest = ROOT / ".studio" / "tmp_inspect_manifest.json"
    manifest.parent.mkdir(parents=True, exist_ok=True)

    def to_wsl(p):
        p = Path(p)
        if not p.drive:
            return p.as_posix()
        drive = p.drive.replace(":", "").lower()
        return f"/mnt/{drive}{p.as_posix().split(':', 1)[-1]}"

    files = [to_wsl(str(ROOT / p)) if not Path(p).is_absolute() else to_wsl(p) for p in paths]
    manifest.write_text(json.dumps({"files": files}), encoding="utf-8")

    script = to_wsl(str(ROOT / "tools" / "art" / "inspect_fbx.py"))
    prefix = [] if os.name != "nt" else ["wsl"]
    cmd = prefix + ["blender", "-b", "-P", script, "--", to_wsl(str(manifest))]
    try:
        proc = subprocess.run(cmd, capture_output=True, text=True, timeout=timeout, encoding="utf-8", errors="replace")
    except (OSError, subprocess.TimeoutExpired) as e:
        print(f"[WARN] Inspeção Blender indisponível: {e}")
        return None

    reports = {}
    for line in (proc.stdout or "").splitlines():
        if line.startswith("REPORT_JSON:"):
            rep = json.loads(line[len("REPORT_JSON:"):])
            key = rep["file"].replace("/mnt/c/", "C:/").replace("/", "\\")
            reports[key] = rep
    return reports
