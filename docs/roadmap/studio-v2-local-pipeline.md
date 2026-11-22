# Roadmap — Studio V2: Fábrica 100% Local de Criação de Games (Vibe-Coding Profissional)

> **Missão:** operar um estúdio completo de criação de jogos 3D profissionais com IA,
> **100% local** na máquina do dono (RTX 3060 12 GB, Windows + WSL2, Unity 6.3 URP),
> onde cada disciplina (conceito 2D, modelagem 3D, rigging/animação, voz/TTS, SFX/música,
> cutscenes, direção artística, revisão rigorosa e Git) possui uma ponte determinística,
> versionável e validada por Quality Gate.

## 0. Restrições permanentes de hardware e governança

1. **100% local (não negociável):** nenhum serviço pago ou em nuvem para geração de
   imagem, 3D, textura, voz ou áudio (ADR-005 deste repo; ADR-009 do `3dprinter-setup-ia`).
2. **Armazenamento somente em `C:\`:** o disco `D:\` é **defeituoso** e não deve ser
   usado para modelos, caches, repos ou projetos. Todos os modelos locais ficam em
   `C:\` (ex.: `C:\ai-models\`) mantendo margem saudável livre para Windows/Unity/WSL.
3. **VRAM alvo:** RTX 3060 12 GB — modelos pesados rodam em etapas sequenciais (nunca
   carregar ComfyUI + TRELLIS.2 + TTS simultaneamente na VRAM).
4. **Estado em disco:** trabalho só é considerado concluído quando registrado em
   `PROGRESS.md` + `evidence/`. `git push` exige autorização explícita do dono.

---

## 1. Matriz de Pilares do Estúdio

| # | Pilar | Status | Tecnologia 100% Local | Critério de Pronto (Gate) |
| --- | --- | --- | --- | --- |
| 1 | **Engine & Automação Unity** | **Pronto** | Unity 6.3 URP + batchmode + `validate_asset.py` / `validate_scene.py` | Build/import batch exit 0 + capturas sem magenta |
| 2 | **Assets 3D Estáticos** | **Pronto (Modo 1)** | `3dprinter-setup-ia` + `fab3d_bridge.py` + `Fab3dImporter.cs` | GLB → FBX canônico ≤ budget + `_BaseMap`/`_MetallicGlossMap` + captura |
| 3 | **Governança, Direção Artística & Review** | **Fase 1 (Ativa)** | `studio-art-director`, `studio-strict-reviewer`, `art-bible.template.yaml`, `studio-git-workflow.md` | Todo asset/cena nasce sob `art-bible.yaml` e passa por checklist com poder de veto |
| 4 | **Vozes / Dublagem (TTS)** | **Fase 2 (Próxima)** | **Kokoro-82M** (~350 MB, PT-BR/EN) ou **F5-TTS / XTTS-v2** (~1.5–2 GB, clonagem por personagem) + `tools/audio/tts_bridge.py` | `dialogues.yaml` → `.wav`/`.ogg` normalizados em `Assets/Game/Audio/VO/` + manifesto de durações/legendas |
| 5 | **Cutscenes Declarativas** | **Fase 3** | Unity Timeline + Cinemachine + `CutsceneBuilder.cs` (batchmode) | Roteiro `cutscene_*.yaml` gera `.playable` + câmeras + VO/legendas + keyframes em `evidence/captures/` |
| 6 | **Conceito 2D / Texturas Tileáveis / UI** | **Fase 4** | ComfyUI em `C:\` + 1 checkpoint enxuto (~6–8 GB, ex.: FLUX.1-schnell Q4 ou SDXL Lightning) + `tools/art/ai_generator/concept_bridge.py` | Prompt + `art-bible.yaml` → conceito ortogonal (entrada do Fab3D), texturas tileáveis e ícones de UI |
| 7 | **Personagens Animados** | **Fase 5** | `Fab3D → tools/art/auto_rig.py` (Blender 5.0.1 WSL) + biblioteca de animações reutilizáveis (Humanoid/Generic) | Personagem gerado na Fábrica anda/age em cena sem deformação quebrada |
| 8 | **SFX & Música** | **Fase 6** | Modelo local compacto (ex.: Stable Audio Open Small ~1.5 GB) + acervo CC0 local + normalização LUFS | Eventos de gameplay e cutscenes com áudio mixado sem clipping |

---

## 2. Arquitetura: Studio Core vs. Projetos de Jogo

Para evitar misturar ferramentas do estúdio com dívida de um jogo específico (como
aconteceu com o NEON DISTRICT dentro do `gamedev-setup-ia`):

- **Studio Core (`gamedev-setup-ia`):** contém as ferramentas reutilizáveis (`tools/`),
  skills/agentes (`.agents/skills/`), templates (`docs/art/art-bible.template.yaml`),
  padrões (`docs/standards/`) e o projeto-base Unity com os importers/builders canônicos.
- **Cada Jogo Novo:** nasce em um repositório/workspace limpo derivado do Studio Core,
  com sua própria `docs/art/art-bible.yaml`, seu `PROGRESS.md`, seu `evidence/` e Git LFS
  configurado desde o commit 1. Detalhes em [studio-git-workflow.md](../standards/studio-git-workflow.md).

---

## 3. Fluxo Completo de Produção (Visão Alvo)

```text
Ideia / GDD (Game Designer)
  → Bíblia Visual: docs/art/art-bible.yaml (Art Director)
  → Conceito 2D / Texturas Tileáveis / UI (ComfyUI local, C:\)
  → Assets 3D (Fábrica 3D → fab3d_bridge.py → Fab3dImporter.cs)
  → Rigging/Animação (auto_rig.py) quando personagem/criatura
  → Vozes + SFX (tts_bridge.py / audio_bridge.py)
  → Montagem de Cenas + Gameplay + Cutscenes (CutsceneBuilder.cs / YAML)
  → Evidência objetiva (validate_asset + validate_scene + capturas in-game + playtest)
  → Auditoria com poder de veto (Strict Reviewer) → Aprovação do dono → Merge/Push
```
