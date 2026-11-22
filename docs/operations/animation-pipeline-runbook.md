# Runbook — Pipeline Profissional de Rigging & Animação (Fase 5.1)

> **Contexto:** Estágios 5.1-A e 5.1-B do [Plano Mestre Studio V2](../plan/studio-v2-master-plan.md).
> Combina 3 camadas 100% locais (RTX 3060 12 GB, `C:\`):
> 1. **Quaternius UAL + Unity Humanoid** (biblioteca base de mocap real já no repo);
> 2. **MoMask (`motion_bridge.py`)** — Text-to-Motion local (texto → `.bvh` → `.fbx` Mecanim);
> 3. **UniRig (`unirig_bridge.py`)** — Rigging & Skinning neural headless para criaturas e personagens fora do padrão (ex.: Dez com 4 braços).

## 1. Bípedes padrão (Estágio 5.1-A — 0 MB)
```powershell
python tools/art/ai_generator/fab3d_bridge.py --from-glb <peca.glb> `
    --name <personagem> --category player_character --auto-rig
```
Ao rodar `Fab3dImporter.RunImportBatch`, o Unity configura automaticamente:
- `ModelImporterAnimationType.Human` (Avatar Humanoid válido);
- `Fab3D_Humanoid_UAL.controller` com clips reais do **Quaternius UAL** (`Idle`, `Walk`, `Run`, `Jump`, `Hit`).

## 2. Animações sob medida a partir de texto (MoMask — `motion_bridge.py`)
```powershell
python tools/art/ai_generator/motion_bridge.py `
    --name dez_repair_kneel `
    --prompt "a person kneels down and tightens something with both hands" `
    --length 80
```
Saída: `.bvh` + `.fbx` com ossos renomeados para Mecanim + `evidence/art/motion_<name>.json`.

## 3. Rigging neural para criaturas / 4 braços (UniRig — `unirig_bridge.py`)
```powershell
python tools/art/ai_generator/unirig_bridge.py `
    --input <malha.glb> --name <criatura>
```
Executa Skeleton Prediction + Skinning Prediction + Merge em `~/.venvs/unirig` e grava `evidence/art/unirig_<name>.json`.
