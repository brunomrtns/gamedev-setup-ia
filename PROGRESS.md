# PROGRESS.md — Estado Vivo do Studio V2 (gamedev-setup-ia)

> Histórico arquivado das fases v1–v3.8 (NEON DISTRICT / Neon Rush / Abyssal Breach): [docs/operations/log-v3.x.md](docs/operations/log-v3.x.md).

## Integração Fábrica 3D — assets originais 100% locais (21/11)
- ADR-005 aceito: ponte Blender WSL (GLB print-grade -> FBX canonico) +
  Fab3dImporter URP. Sem pacotes novos e sem downloads (Blender 5.0.1 ja
  instalado no WSL). Fonte: repo 3dprinter-setup-ia (run_asset.py -> GLB 4K/8K
  BaseColor + MR 2048). Regra permanente: 100% local, servicos externos de
  geracao em nuvem PROIBIDOS (ver ADR-009 daquele repo).
- Entregue: fab3d_bridge.py + fab3d_glb_to_fbx.py (tools/art/ai_generator/),
  unity/Assets/Editor/Fab3dImporter.cs, docs/art/fab3d-asset-spec.yaml,
  docs/operations/fab3d-bridge-runbook.md, skill .agents/skills/fab3d-asset/.
- Budgets herdados do art-spec.yaml v1.0 e aplicados como gate (decimate +
  downscale ASTC): small_prop 600 tris/512px, large_prop 3000/512px,
  player 4500/1024px, enemy 2500/1024px.
- [x] Teste da ponte Modo 1 (from_glb) com `prop_caixote__pbr_4k.glb` (repo `3dprinter-setup-ia`, categoria `large_prop_building`): exit 0, `identity_transform=true`, 250.000 → 3.000 tris, BaseColor + MR 512px (`evidence/art/fab3d_bridge_prop_caixote.json`)
- [x] Fab3dImporter rodado em batchmode (exit 0): `Mat_Fab3D_prop_caixote.mat` (`_BaseMap` + `_MetallicGlossMap` + ASTC_6x6) + `prop_caixote.prefab` com `BoxCollider` (`evidence/art/fab3d-import-report.json`)
- [x] `validate_asset.py --asset unity/Assets/Game/Art/Generated/prop_caixote/prop_caixote.fbx` exit 0 (`ASSET APROVADO`)
- [x] Captura in-game da peça em cena do NEON DISTRICT: `evidence/captures/fab3d_prop_caixote.png` (via `Studio.Editor.Fab3dImporter.CapturePreviewBatch`, sem alterar `NeonDistrict.unity`)
- [ ] (adiado, NAO bloqueia) front-end de imagem local (ComfyUI + modelo
      destilado) quando houver disco livre; contrato de entrada da ponte
      permanece GLB/imagem

---

## Studio V2 — Fábrica 100% Local de Criação de Games (21/11)
- **Decisão de ambiente:** disco `D:\` é **defeituoso** e proibido; todo modelo/cache/repo fica somente em `C:\`.
- **Limpeza Modo 1:** arquivos temporários do `prop_caixote` removidos de `unity/Assets/`; código da ponte, `CapturePreviewBatch`, docs e evidências commitados (`78c921f1`).
- [x] **Fase 1 — Organização + Roadmap + Agentes:**
  - `docs/roadmap/studio-v2-local-pipeline.md` (matriz dos 8 pilares 100% locais em `C:\` / RTX 3060 12 GB)
  - `docs/standards/studio-git-workflow.md` (Studio Core vs. Jogo Novo, Git LFS, Definition of Done)
  - `docs/art/art-bible.template.yaml` (template de Bíblia Visual + prompts-base)
  - `.agents/skills/studio-art-director/SKILL.md` (Diretor de Arte)
  - `.agents/skills/studio-strict-reviewer/SKILL.md` (Revisor Criterioso com poder de veto)
  - `AGENTS.md` atualizado (Princípio 9)
- [x] **Fase 2 — TTS local em `C:\` (XTTS-v2 WSL já em cache, 0 MB download):**
  - `tools/audio/tts_bridge.py` + `tools/audio/tts_xtts_worker.py` (orquestrador Windows stdlib + worker CUDA WSL)
  - `docs/audio/voice-cast.template.json` + `docs/operations/tts-bridge-runbook.md` + `.agents/skills/studio-voice-tts/SKILL.md`
  - Smoke test PT-BR (`smoke_test`, 2 falas, 7.138s total, 24 kHz, `-1.0 dBFS`, `cuda`, `errors: []`): `evidence/audio/tts_bridge_smoke_test.json`
- [x] **Fase 3 — Cutscene Builder declarativo (Unity Timeline + VO/legendas + keyframes):**
  - `unity/Assets/Game/Scripts/UI/CutscenePlayer.cs` (runtime: câmera rail suave, letterbox, legendas sincronizadas ao `PlayableDirector`)
  - `unity/Assets/Editor/CutsceneBuilder.cs` (+ `Unity.Timeline` em `Studio.Editor.asmdef`): compila JSON → `TimelineAsset` (`AudioTrack` ligado ao `vo_manifest.json` da Fase 2) + Prefab + capturas de keyframes sem alterar a cena
  - `tools/studio/build_cutscene.py`, `docs/narrative/cutscene.template.json`, `docs/operations/cutscene-builder-runbook.md`, `.agents/skills/studio-cutscene-director/SKILL.md`
  - Smoke test batchmode Unity 6.3 (`intro_exemplo`, 2 shots, 7.0s, exit 0, `errors: []`): `evidence/cutscenes/cutscene_intro_exemplo.json` + `evidence/captures/cutscene_intro_exemplo_*.png`
- [x] **Fase 3b — Integração TTS → Cutscene (`audio_clips_bound: 2`, 0 MB):**
  - `tts_bridge.py` (`integration_vo`, 2 falas PT-BR, 8.577s) → `vo_manifest.json` → `CutsceneBuilder.cs` (`total_duration_sec: 9.277`, `audio_clips_bound: 2`, `TimelineAsset` + Prefab + 2 keyframes, `errors: []`). Evidências: `evidence/cutscenes/cutscene_integration_vo.json`, `evidence/audio/tts_bridge_integration_vo.json`, `evidence/captures/cutscene_integration_vo_shot_0{1,2}*.png`. Arquivos de teste limpos de `unity/Assets/`.
- [x] **Estória G2 — Adapters de Schema para o CONCERTO + Env Vars (G5):**
  - `tools/studio/yaml_loader.py`, `tools/audio/dialogue_adapter.py`, `tools/studio/cutscene_adapter.py`, `concept_bridge.py` (`prompt_base` + `--character` + `--fuso` + `STUDIO_WSL_2D_PYTHON`), `tts_bridge.py` (`STUDIO_WSL_TTS_PYTHON`), `build_cutscene.py` (`STUDIO_UNITY_EXE`).
  - Validação contra os 3 YAMLs reais do CONCERTO (`art-bible.yaml`, `dialogues.yaml`, `cutscene_01_abertura.yaml` — exit 0 nos 3): `evidence/studio/g2_adapters_validation.json`.
- [x] **Fase 4 — Conceito 2D / Texturas Tileáveis / UI local (`stabilityai/sdxl-turbo` em `C:\`, CUDA):**
  - Smoke test `--mode concept` (`smoke_porca_concept.png`, 512×512, `art-bible.yaml` do CONCERTO) e `--mode tileable` (`smoke_piso_oficina_tileable.png`, 512×512, 115 camadas `Conv2d` circulares sem emenda), inspecionados visualmente: `evidence/art/concept_smoke_porca.json`, `evidence/art/concept_smoke_piso_oficina.json`, `evidence/concepts/smoke_*.png`.
- [x] **Fase 5 — Personagens Animados (`Fab3D → auto_rig.py → Unity`, 0 MB):**
  - `fab3d_bridge.py --auto-rig` + `fab3d_glb_to_fbx.py` + `Fab3dImporter.cs` (`SkinnedMeshRenderer`, `ModelImporterAnimationType.Generic`, contagem de `AnimationClip`s).
  - Smoke test `humanoide_dragonborn__pbr_4k.glb` (249.997 → 4.500 tris, 18 ossos, 4 clips `Idle/Jump/RunCycle/Slide`, `validate_asset` exit 0, `animationsImported: 4`, `errors: []`): `evidence/art/fab3d_bridge_smoke_dragonborn.json`, `evidence/art/fab3d-import-report.json`, `evidence/captures/fab3d_smoke_dragonborn.png`. Arquivos de teste limpos de `unity/Assets/`.
- [x] **Fase 6 — SFX & Música local (EBU R128 CC0/local, 0 MB):**
  - `tools/audio/audio_bridge.py`, `docs/operations/audio-sfx-runbook.md`, `.agents/skills/studio-audio-designer/SKILL.md`.
  - Smoke test SFX (`-16 LUFS`, `-1.0 dBFS`, WAV 44.1 kHz) + Música (`-18 LUFS`, OGG loop, `errors: []`): `evidence/audio/audio_bridge_smoke_sfx.json`, `evidence/audio/audio_bridge_smoke_music.json`.
- **DoD Macro do Studio V2: 6/6 GATES VERDES** — ambiente 100% configurado e pronto para receber `game-concerto`.
- **Registro de auditoria (21/11, pós-review `review-upgrade-animacao-fase5.md` + `auditoria-fluxos-gamedev.md`):**
  - Aprovações registradas: limpeza `cuda_*.run` (+9 GB WSL) + sobras Crash CTR (+51,94 GB → `171,46 GB` livres em `C:\`), download `sdxl-turbo` (~6,5 GB) e pushes até `14085584`.
  - [x] **Estágio 5.1-A (P1-7):** `Fab3dImporter.cs` → `ModelImporterAnimationType.Human` + `Fab3D_Humanoid_UAL.controller` com 5 clips Quaternius UAL (`humanoidAvatarsValid: 1`, `ualClipsBound: 5`, `errors: []`, commit `88cb26d7`).
  - [x] **P0-1 + P1-6:** `unity/Assets/Game/Scripts/UI/DialoguePresenter.cs` (diálogos/barks em gameplay, bilíngue `pt-BR`/`en`, callback de amplitude p/ lip-sync).
  - [x] **P0-2:** `tools/audio/validate_voice_samples.py` (validação de duração/clipping/silêncio/sample rate antes do XTTS-v2; smoke test `bruno.wav` PASS em `evidence/audio/voice_sample_smoke.json`).
  - [x] **P0-3:** `schema` explícito (`cutscene-spec-v1`, `vo-manifest-v1`) com falha alta em `CutsceneBuilder.cs` + suíte EditMode `StudioV2ContractTests.cs` (**5/5 PASSED** em Unity 6.3 batchmode: `evidence/test-reports/studio-v2-contracts.xml`).
  - [x] **P1-4:** `tools/audio/audio_bridge.py` atualizado para EBU R128 `loudnorm` **two-pass** (`linear=true` + `measured_output_i` em `evidence/audio/audio_bridge_smoke_{sfx,music}.json`).
  - [x] **P1-8:** `unity/Assets/Game/Scripts/Gameplay/InteractionSystem.cs` (`IInteractable`, `InteractableBase` com `IkHandTarget`, `PlayerInteractor`).
  - [x] **P2-11:** histórico v1–v3.8 arquivado em `docs/operations/log-v3.x.md`.
  - [x] **Estágio 5.1-B (Animação & Rigging Neural 100% local em `C:\`):**
    - **MoMask** (`~/tools/momask-codes` + `~/.venvs/momask`, 188 MB checkpoints): `tools/art/ai_generator/motion_bridge.py` + `bvh_to_fbx_blender.py` (texto → `.bvh` → `.fbx` Mecanim 22 juntas, exit 0: `evidence/art/motion_smoke_repair_kneel.json`).
    - **UniRig** (`~/tools/UniRig` + `~/.venvs/unirig`, download seletivo 5,54 GB `skeleton/` + `skin/`, pulando `data/`): `tools/art/ai_generator/unirig_bridge.py` (Skeleton 13s + Skinning 5s + Merge 1.3s em `criatura_dragao__pbr_4k.glb`, exit 0: `evidence/art/unirig_smoke_dragao.json`).
    - Runbook & skill: `docs/operations/animation-pipeline-runbook.md` + `.agents/skills/studio-animation-director/SKILL.md`.

---

## Fix Permanente de Visibilidade e Gates Visuais — DOC 3 (`feat/visual-gates`, 22/11)
- **Justificativa Unity MCP (Rota B em vez de Rota A):** adotada a Rota B (`CoderGamester/mcp-unity` v1.5.0, licença MIT, em `C:\Users\Bruno\tools\mcp-unity`) porque o pacote oficial da Rota A (`com.unity.ai.assistant`) está em beta fechado por entitlement de conta Unity (ausente no `PackageCache` local do Unity 6000.3.26f1) e o symlink `C:\WINDOWS\system32\unity.exe` aponta para um binário inexistente (`C:\Program Files\Unity Hub\resources\unity.exe`).
- [x] **Etapa 0 — Smoke Test de Visibilidade (MCP / Captura):** `Calibration.unity` (fixture de QA commitada com `Mat_PBR_ConcretePanel.mat` registrado em `LICENSES-ARTE.md`) capturada e inspecionada multimodalmente (`evidence/captures/mcp_smoke_test_20261122.png`).
- [x] **Item A — `tools/validation/validate_visual.py` (+ `tools/art/inspect_fbx.py` expandido):**
  - Checa orientação Y-up (`height_y > max(width_x, depth_z)` e `height_y >= 1.6 * min_horiz` para bípede), budget de triângulos/N-gons, densidade UV/texel (`uv_world_ratio`), material e escala em metros.
  - Provado `exit 0` no `smoke_dragonborn` em pé (`evidence/validation/validate_visual_smoke_dragonborn.json`) e `exit 1` (`ORIENTATION_FAIL`) na fixture de regressão permanente `tests/fixtures/visual_gate/fixture_biped_deitado/` (`evidence/validation/validate_visual_fixture_biped_deitado.json`).
- [x] **Item B — Ordem Canônica de Gates (`glTF-Validator` → `validate_asset` → `validate_visual` → `Fab3dImporter` → Captura → `validate_scene` → `run_unity_tests`):**
  - `tools/validation/validate_gltf.py` (Khronos `glTF-Validator` Apache-2.0) integrado ao `fab3d_bridge.py` e ao orquestrador `tools/qa/run_visual_gates.py`; documentado em `docs/operations/fab3d-bridge-runbook.md` (`evidence/validation/gltf_validator_smoke_dragonborn.json`).
- [x] **Item C — Captura Automática Datada Pós-Import (`Fab3dImporter.cs`):**
  - `ImportAll(autoCapture: true)` gera `evidence/captures/fab3d_<nome>_<YYYYMMDD_HHMMSS>.png` em todo import; provado em `evidence/captures/fab3d_smoke_dragonborn_20261122_154155.png` e registrado em `evidence/art/fab3d-import-report.json`.
- [x] **Item D — `AGENTS.md` (Visibilidade Unity MCP + Institucionalização & BAN LIST):**
  - Adicionada seção §1.1 com as 4 regras inegociáveis de visibilidade MCP + BAN LIST verbatim (agente de código nunca gera arte final; TripoAI/TripoSR banido permanentemente; arte final só via Fábrica 3D `TRELLIS.2-4B + Hunyuan3D-Paint + pack_glb modo game`; placeholders só de fonte CC0/Kenney; regra anti-improviso exigindo ADR).
- [x] **Item E — `docs/standards/asset-quality.md`:**
  - Adicionada seção §9 com texel density world-scale (`1 tile = 1–2 m`, proibido UV `1×1` esticado), orientação canônica `Y-up` `+Z-frente` com branch explícito `is_biped: true/false`, proibição de decimação bruta sem retopo (detalhe em normal-map) e tabela de budgets por classe da `art-spec.yaml`.
- [x] **Item F — `.agents/skills/studio-scene-director/SKILL.md` (NOVA):**
  - Método anti-lixo **C1–C5** (C1 Diagrama Top-Down → C2 Greybox Fechado Jogável → C3 Kit CC0 + PBR World-Scale → C4 Composição & Câmeras → C5 Substituição In-Place pelos Props da Fábrica 3D).
- [x] **Item G — `.agents/skills/studio-strict-reviewer/SKILL.md`:**
  - Revisão multimodal obrigatória (abertura das capturas PNG + 1 frase descritiva + veto por reconhecimento vs. `art-bible.yaml`, veto por personagem deitado, veto por texel density errada e veto por fundo *void*).
- [x] **Item H — `LICENSES-ARTE.md` (Raiz):**
  - Tabela completa de ferramentas e packs da §2 do DOC 3 com licenças verificadas na fonte oficial em 22/11/2026, proveniência CC0 do `Mat_PBR_ConcretePanel` / `Calibration.unity`, modo game da Fábrica 3D (`TRELLIS.2-4B` MIT) e carrimbo explícito no `Hunyuan3D-2`.
- [x] **Item I — Receita Canônica "Novo Jogo do Zero" (`AGENTS.md` §4):**
  - Adicionada **Etapa 0** (Unity MCP Rota A→B→C + smoke test de captura verde sobre `Calibration.unity` antes do passo 1) e sequência C1–C5 + nova ordem de gates.
- [x] **Item J — Higiene de Geradores (`docs/standards/studio-git-workflow.md` §1):**
  - Checklist permanente proibindo geradores/pesos de terceiros ou scripts experimentais avulsos dentro de `gamedev-setup-ia/tools/art/` (movidos para `C:\Users\Bruno\tools\`).
- [x] **Smoke Test Final Modo 1 (`prop_caixote`, `is_biped: false`):**
  - `glTF-Validator` exit 0 (`evidence/validation/gltf_validator_prop_caixote.json`) → `validate_asset` exit 0 → `validate_visual` v1.1.0 exit 0 pelo branch explícito `is_biped: false` (`branch: prop_grounded_uniform_scale`, `evidence/validation/validate_visual_prop_caixote.json`) → `Fab3dImporter` exit 0 com captura automática datada (`evidence/captures/fab3d_prop_caixote_20261122_160228.png`). Arquivos temporários de import limpos de `unity/Assets/`.
