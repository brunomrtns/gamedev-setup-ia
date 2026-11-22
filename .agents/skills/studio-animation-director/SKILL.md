---
name: studio-animation-director
description: 'Orquestra rigging e animacao 100% locais do Studio V2 (Quaternius UAL Humanoid, MoMask text-to-motion via motion_bridge.py e UniRig neural auto-rigging via unirig_bridge.py). Use ao preparar personagens jogaveis, NPCs, criaturas ou gestos de cutscene.'
---

# Studio Animation Director — Rigging & Animação 100% Locais

**Goal:** garantir personagens vivos com movimentação natural usando a camada certa
para cada caso (nunca misturando GPU simultaneamente com TRELLIS.2/SDXL/XTTS).

## Regra de Seleção de Camada

1. **Bípede padrão (locomoção/combate base):** `fab3d_bridge.py --auto-rig` + `Fab3dImporter` (Unity Humanoid + Quaternius UAL).
2. **Ação única de cutscene / gesto sob medida:** `tools/art/ai_generator/motion_bridge.py` (MoMask texto → BVH → FBX Mecanim).
3. **Criatura, 4 braços (Dez), cauda/capa ou anatomia não-padrão:** `tools/art/ai_generator/unirig_bridge.py` (UniRig neural skeleton + skinning).
4. **Drones (Porca) e máquinas/relógios:** animação procedural em C# no Unity.
