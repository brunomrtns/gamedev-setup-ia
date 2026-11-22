# AGENTS.md — Contrato Canônico de Operação dos Agentes

Este documento é a autoridade central e vinculante para qualquer agente de IA (Claude Code, OpenCode, Antigravity, etc.) operando neste repositório.

---

## 1. Princípios Não-Negociáveis

1. **Agnóstico ao Agente:** Todos os agentes leem os mesmos padrões (`docs/standards/`), usam os mesmos comandos (`tools/`) e persistem o estado em `PROGRESS.md` e `.studio/`.
2. **Documentação Antes da Implementação:** Nenhuma funcionalidade de gameplay, arquitetura ou arte é codificada sem uma story e critérios de aceitação formalizados (via BMad em `_bmad-output/` ou contratos em `docs/`).
3. **Automação Verificável:** "Código que compila" **NÃO** é critério de conclusão. Toda tarefa exige testes automatizados (EditMode/PlayMode), validação determinística (`ValidateScene`, `ValidateAsset`) e evidências registradas em `evidence/`.
4. **Isolamento de Cena:** Nunca abra ou edite simultaneamente a mesma cena a partir de agentes concorrentes. Modificações de cena são serializadas.
5. **Autonomia com Responsabilidade e Validação de Assets:** Execute tarefas sequencialmente conforme dependências sem solicitar prompts ao usuário para passos rotineiros. Interrompa para intervenção humana **obrigatoriamente** para:
   - Incompatibilidade ou indisponibilidade de assets solicitados no prompt: se o usuário pediu assets específicos (ex: Unreal `.uasset` em projeto Unity) que não podem ser usados diretamente, **é terminantemente proibido criar substitutos primitivos/sem textura em silêncio**. O agente DEVE interromper e questionar o usuário via `ask_question`, explicando a limitação e propondo soluções viáveis.
   - Credenciais privadas e pagamentos.
   - Decisões de design de jogo ou aprovação de arte/game feel.
   - Comandos destrutivos ou `git push` não autorizado.
6. **Proibição Estrita de Jogos Sem Textura (No Flat Low-Poly Fallbacks):** É proibido entregar jogos compostos por cubos, esferas e polígonos com cores sólidas e sem textura. Todos os jogos gerados devem possuir modelos 3D proporcionados com mapeamento UV e texturas PBR (Albedo, Normal Map, Metallic/Smoothness) devidamente renderizadas.
7. **Quality Gate V2 (Ecossistema Open Source + Kitbash Modular):** Toda geração de ambiente ou entidade visual deve usar **estritamente** os pacotes modulares de `Assets/Game/Art/Environment/Modular/` (Kenney CC0), os materiais PBR de `Assets/Game/Materials/Modular/Mat_PBR_*` (Poly Haven CC0) ou modelos processados pelo script de normalização headless (`tools/art/normalize_fbx.py` / `fbx_canonical.py`) ou assets originais gerados 100% localmente pela ponte Fábrica 3D (`tools/art/ai_generator/fab3d_bridge.py` → FBX canônico + materiais via `Fab3dImporter`, ver ADR-005). Primitivas nativas visíveis (`GameObject.CreatePrimitive`) e materiais sem `_BaseMap` são **falha de build** detectada pelos validadores V2 (`validate_scene.py` / `validate_asset.py --all`).
8. **Física e Combate (`docs/standards/physics-and-combat.md`):** Proibido criar veículos de trânsito em modo puramente cinemático que não reajam a impactos do jogador (obrigatória máquina de estados Routing/PhysicsFree/Wrecked). Proibido implementar disparo de armas onde o tiro divirja do centro do retículo da câmera ativa (obrigatória convergência raycast-centro → muzzle). Veículos rígidos exigem `centerOfMass` abaixo do eixo, `ContinuousDynamic` e clamp de velocidade. Props de rua exigem limiar de quebra por impulso.
9. **Studio V2 — Pipeline 100% Local, Direção Artística & Revisão (`docs/roadmap/studio-v2-local-pipeline.md`):** Todo projeto novo nasce sob uma Bíblia Visual (`docs/art/art-bible.yaml`, governada por `.agents/skills/studio-art-director/`) e só fecha entregas após auditoria baseada em evidência objetiva (`.agents/skills/studio-strict-reviewer/`). Armazenamento de modelos/repos locais é **estritamente em `C:\`** (`D:\` é defeituoso e proibido). Seguir `docs/standards/studio-git-workflow.md` para separação Studio Core vs. jogo e Git LFS. **Política de seleção de ferramentas (decisão do dono):** licenciamento não é filtro de bloqueio — priorizar sempre a melhor qualidade técnica rodando 100% localmente, evitando APIs externas de terceiros.
10. **Regra Anti-Overclaim (Honestidade de Status):** Todo relatório de status no chat ou em documentos DEVE espelhar fielmente os checkboxes de `PROGRESS.md`. Um item ou fase **só pode ser declarado "concluído"** quando seu gate determinístico retornou exit 0 e a evidência correspondente está gravada em `evidence/`. Código escrito sem smoke test aprovado deve ser reportado explicitamente como **"código pronto, pendente gate/evidência"**.

---

## 1.1. Regras de Visibilidade do Agente (Unity MCP) & Institucionalização de Arte (BAN LIST) — Inegociáveis

### Regras de Visibilidade do Agente (Unity MCP) — inegociáveis
1. O agente DEVE usar a ferramenta de screenshot do Unity MCP após TODA operação que altere cena, transform, material ou prefab. Nunca confiar em exit 0 como prova de qualidade visual.
2. Antes de qualquer entrega ao dono: captura da cena + 1 frase do agente descrevendo o que vê e comparando com o art-bible. Se não reconhecer o objeto na captura, reprovar sozinho e corrigir (máx. 3 tentativas, depois PARE).
3. Na fase de mecânica: proibido gerar arte IA (malha/textura). Placeholder cinza no eixo correto (capsule Y-up, +Z para frente, escala em metros). Arte final entra somente após mecânica aprovada, via fab3d_bridge canônico, 1 asset por vez.
4. Toda captura vai para `evidence/captures/` com nome datado — histórico auditável.

### Cláusulas Permanentes de Institucionalização & BAN LIST
1. **Agente de código NUNCA gera arte final:** Durante a fase de mecânica, gameplay e greybox, o agente de código jamais gera malhas ou texturas finais por IA. A mecânica é validada primeiro sobre placeholders padronizados.
2. **TripoAI / TripoSR banido PERMANENTEMENTE em qualquer jogo do `gamedev-setup-ia`:** É terminantemente proibido utilizar TripoSR ou TripoAI (seja local ou via API) para qualquer personagem, criatura ou prop de jogo em todo o ecossistema do Studio Core.
3. **Arte final de herói e props autorais só via Fábrica 3D:** Todo modelo 3D original de produção deve vir exclusivamente da Fábrica 3D (`TRELLIS.2-4B + Hunyuan3D-Paint + pack_glb modo game`), consumido pela ponte canônica `tools/art/ai_generator/fab3d_bridge.py` → forja Blender (`fab3d_glb_to_fbx.py`) → `Fab3dImporter`, respeitando a ordem inegociável de gates (`glTF-Validator` → `validate_asset` → `validate_visual` → `Fab3dImporter` → captura datada MCP) e a tabela de licenças/carrimbos em `LICENSES-ARTE.md`.
4. **Placeholders só de fonte padronizada CC0/Kenney:** Todo greybox e blocagem modular utiliza exclusivamente `Kenney Prototype Textures` (grid 1 tile = 1 m), kits modulares CC0 (`Kenney City Kit (Industrial)`, `Quaternius UAL`) e materiais PBR CC0 (`Poly Haven` / `AmbientCG`) registrados em `LICENSES-ARTE.md`.
5. **Regra Anti-Improviso:** Proibido criar script paralelo de conversão/eixo enquanto existir ponte canônica (`fab3d_bridge.py` / `fbx_canonical.py`); se a ponte não atender, abrir ADR em `docs/decisions/`, não gambiarra.
6. **Protocolo de Sync Studio Core → Jogos Derivados (`game-concerto`):** Qualquer atualização futura da BAN LIST, dos validadores (`tools/validation/`, `tools/qa/`) ou das skills (`.agents/skills/`) no Studio Core tem como parte obrigatória do DoD a sincronização imediata para o `game-concerto` no mesmo ciclo, com a nota de proveniência atualizada para o novo hash.

### Leis de Higiene & Pasta Canônica de Decisões (Vigentes Permanentemente)
- **Lei 6a (Captura Limpa de Gate):** Captura de ferramenta/gate usa **SEMPRE `Calibration.unity`** (ou cena neutra isolada). **Zero** cenário de outros jogos (`NeonDistrict.unity` proibido em previews de gate). Capturas antigas com fundo Neon ficam arquivadas em `evidence/captures/arquivo_neon/`.
- **Lei 6b (Isolamento de Outros Jogos):** Assets do Neon District só entram item a item, com OK explícito do Bruno e linha registrada em `LICENSES-ARTE.md` — nunca em lote. Código, cenas e design do Neon District: **NUNCA**.
- **Lei 6c (Linha AMBIENTE na Fábrica 3D):** A Fábrica 3D ganha linha **AMBIENTE** (chão/tiles, módulos arquitetônicos, props estruturais grandes, horizonte/skybox) com meta final de mundo 100% IA local. `CC0/Kenney` é andaime temporário substituível sem retrabalho (mesmos pivôs, escalas e snap).
- **Lei 6d (Decisão vs. Engenharia):** **PROIBIDO** enviar fixtures ou capturas puramente de engenharia ao Bruno para aprovação visual — enviar apenas artefatos de decisão de diretor.
- **Lei 7 (Pasta Canônica `director_review`):** Toda imagem que exige veredito do Bruno vai para `<jogo>\director_review\<YYYY-MM-DD>_<nome>\` acompanhada de `LEIA-ME.txt` de 3 linhas (`O QUE É` / `O QUE VOCÊ DECIDE` / `O QUE ACONTECE DEPOIS`), e o checkpoint no chat **SEMPRE** inclui o caminho completo da pasta.

---

## 2. Estrutura Canônica de Diretórios

```text
gamedev-setup-ia/
├── AGENTS.md                         # Este contrato
├── CLAUDE.md                         # Ponte concisa para Claude Code
├── PROGRESS.md                       # Checklist vivo e handoff entre sessões
├── docs/
│   ├── roadmap/                      # Roadmap original de referência
│   ├── plan/                         # Plano de implementação e dependências
│   ├── standards/                    # Normas técnicas invioláveis
│   │   ├── unity.md                  # Padrões C#, URP, convenções de código
│   │   ├── architecture.md           # Padrões de arquitetura (Assembly Defs, desacoplamento)
│   │   ├── testing.md                # UTF, EditMode, PlayMode, cobertura
│   │   ├── mobile-performance.md     # Budgets de draw calls, tris, memória e thermal
│   │   └── asset-quality.md          # Especificação de malha, texturas e LODs
│   ├── art/                          # Art Bible, art-spec.yaml e referências
│   ├── decisions/                    # Architecture Decision Records (ADRs)
│   └── operations/                   # Guias de ambiente, auditoria e troubleshooting
├── tools/                            # Ferramentas determinísticas do estúdio
│   ├── studio/                       # Orquestrador local autônomo (DAG runner)
│   ├── validation/                   # Validadores de cena, assets e budgets
│   ├── capture/                      # Captura automatizada de screenshots/vídeos
│   └── android/                      # Scripts de build, deploy e profiling via ADB
├── unity/                            # Projeto Unity 6 URP canônico
│   ├── Assets/Game/                  # Código e assets da aplicação
│   ├── Assets/Editor/                # Ferramentas e hooks de Editor C#
│   ├── Packages/                     # Manifesto de pacotes
│   └── ProjectSettings/              # Configurações do projeto
├── evidence/                         # Evidências geradas (testes, capturas, logs)
└── .studio/                          # Estado persistente da orquestração autônoma
```

---

## 3. Ambiente de Execução Dual — Windows (Unity) + WSL (Toolchain de Arte)

> **LEIA ANTES DE EXECUTAR QUALQUER FERRAMENTAL.** Este estúdio opera em DOIS ambientes coexistentes.
> Não procure Blender ou libs científicas no Windows — eles **não existem** lá.

| Responsabilidade | Ambiente | Invocação Canônica |
|---|---|---|
| Unity Editor/CLI, testes UTF, builds Android/Windows, validadores | **Windows nativo** | `unity ...`, `python tools/validation/...` |
| Blender headless (bpy), processamento de texturas (PIL/numpy), trimesh, downloads de assets | **WSL2 (distro Ubuntu)** | `wsl blender -b -P <script.py> -- <args>` |
| Python com dependências científicas | **WSL2 venv `game-ai3d`** | `wsl /home/bruno/.venvs/game-ai3d/bin/python <script.py> <args>` |

Fatos vinculantes do ambiente:
- **Blender 5.0.1** está instalado apenas no WSL (`/usr/bin/blender`). Todo script `bpy` roda via `wsl blender -b -P ...`.
- O **Python 3.13 do Windows NÃO possui** `PIL`, `numpy`, `trimesh` nem `requests`. O venv `/home/bruno/.venvs/game-ai3d` do WSL possui todos (PIL 12.3, numpy 2.4.6, requests 2.34.2, trimesh 5.1.1).
- Scripts devem **delegar automaticamente** para o WSL quando as libs faltarem no Windows — padrão de referência: `run_in_wsl_venv()` em `tools/art/convert_abyssal_assets.py`.
- Conversão de caminho Windows→WSL: `C:\foo\bar` → `/mnt/c/foo/bar` (helper `convert_to_wsl_path()` em `tools/art/`).
- GPU (RTX 3060) é acessível ao WSL para cargas CUDA (TRELLIS.2-4B, SDXL-Turbo, XTTS-v2, UniRig, MoMask).

---

## 4. Comandos Canônicos do Estúdio

Todos os agentes devem executar comandos através das interfaces padronizadas:

### Ambiente e Toolchain de Arte (WSL + Ponte Fábrica 3D)
```bash
# Blender headless no WSL — geração/normalização de malhas FBX
wsl blender -b -P tools/art/<script_bpy>.py -- <output_dir>

# Python do venv do WSL (PIL/numpy/trimesh/requests)
wsl /home/bruno/.venvs/game-ai3d/bin/python tools/art/<script>.py <args>

# Download determinístico de kits modulares CC0 + texturas PBR Poly Haven (ETAPA V2)
python tools/art/download_modular_kits.py

# Normalização canônica de um asset existente (eixos, pivo, UV, triangulação)
wsl blender -b -P tools/art/normalize_fbx.py -- --input <fbx> --output <fbx> --preset weapon|character|prop|kit

# Ponte Fábrica 3D (TRELLIS.2-4B + Hunyuan3D-Paint + pack_glb modo game -> FBX canônico + gates pré-import)
python tools/art/ai_generator/fab3d_bridge.py --from-glb <peca.glb> --name <asset_id> --category small_prop_item|large_prop_building|player_character|enemy_character [--auto-rig]

# Pipeline completo de gates visuais (glTF-Validator -> validate_asset -> validate_visual -> Fab3dImporter + captura datada)
python tools/qa/run_visual_gates.py --asset-dir unity/Assets/Game/Art/Generated/<asset_id>
```

### Receita Canônica — Novo Jogo do Zero (sessão fria)

Para um briefing de jogo novo, execute nesta ordem (referência: `.agents/skills/studio-scene-director/SKILL.md`, `NeonRushSceneBuilder.cs` e `AbyssalBreachSceneBuilder.cs`):

0. **Etapa 0 — Olhos do Agente (Unity MCP + Smoke Test de Captura):** instale/verifique o Unity MCP (Rota A oficial `com.unity.ai.assistant` → Rota B `CoderGamester/mcp-unity` MIT em `C:\Users\Bruno\tools\mcp-unity` → Rota C `IvanMurzak/Unity-MCP` Apache-2.0) e execute um smoke test de captura sobre `Assets/Game/Scenes/Calibration.unity`, abrindo o PNG em `evidence/captures/` e descrevendo em 1 frase o que vê. **Sem olhos, sem jogo.**
1. **Documento de design & Diagrama Top-Down (C1)**: crie GDD/arquitetura em `_bmad-output/planning-artifacts/` ou `docs/gdd/`, `LICENSES-ARTE.md` e produza o diagrama top-down do cenário (markdown + PNG: zonas, dimensões em metros, altura de teto, focal point único, lista de props com budget). **PARE e peça OK humano antes de criar qualquer `GameObject` no Unity.**
2. **Greybox Fechado (C2) & Catálogo de assets (C3)**: bloqueie a cena fechada (piso com `Kenney Prototype Textures` grid 1 tile = 1 m, paredes + teto fechando o espaço, jamais fundo void, cápsulas/cubos placeholder Y-up +Z-frente); inventário em `docs/art/asset-catalog.md`; prefabs modulares CC0 (`Kenney City Kit (Industrial)`) em `Assets/Game/Prefabs/Modular/`; malhas utilitárias via `ProceduralMeshLibrary` (editor) — **nunca `GameObject.CreatePrimitive`**.
3. **Código gameplay**: assemblies em `Assets/Game/Scripts/` respeitando `docs/standards/architecture.md`; testes em `Assets/Game/Tests/EditMode/`. **Na fase de mecânica é proibido gerar arte IA.**
4. **Scene Builder**: crie `Assets/Editor/<Jogo>SceneBuilder.cs` com `RunBuildSceneBatch` (build+save) e método de captura multi-ângulo datada com warm-up de shaders (padrão `RenderAndSave` + warmup de `StudioAutomation`).
5. **Build da cena** (Windows):
   `"C:\Program Files\Unity\Hub\Editor\6000.3.26f1\Editor\Unity.exe" -batchmode -quit -projectPath unity -executeMethod Studio.Editor.<Jogo>SceneBuilder.RunBuildSceneBatch -logFile evidence/logs/<jogo>-build.log`
6. **Arte Final (C5, após mecânica aprovada) & Ordem Canônica de Gates** (todos exit 0, 1 asset por vez):
   `glTF-Validator` (`validate_gltf.py`) → `validate_asset.py` → `validate_visual.py` → `Fab3dImporter` (com captura datada automática em `evidence/captures/`) → `validate_scene.py --scene <cena> --strict` → `python tools/qa/run_unity_tests.py`.
7. **Evidências & Revisão Multimodal**: abra cada captura gerada em `evidence/captures/`, descreva em 1 frase o que vê comparando com o `art-bible.yaml` e passe pelo `studio-strict-reviewer`.
8. **Build Android**: `python tools/android/build_apk.py --target development --out builds/<jogo>.apk`.
9. **Atualize `PROGRESS.md`** com checklist, evidências e ponteiro para logs.

### Inspeção e Status
```bash
# Verificar integridade glTF/GLB (Gate 0)
python tools/validation/validate_gltf.py --input <arquivo.glb> --name <asset_id>

# Verificar integridade estrutural de um asset (Gate 1)
python tools/validation/validate_asset.py --asset Assets/Game/Models/Player.fbx

# Verificar sanidade visual determinística: orientação Y-up, budget, texel density, material, escala (Gate 2)
python tools/validation/validate_visual.py --asset Assets/Game/Models/Player.fbx --class player_character

# Verificar integridade da cena atual
python tools/validation/validate_scene.py --scene Assets/Game/Scenes/Main.unity --strict
```

### Testes e Validação
```bash
# Executar todos os testes em batchmode
python tools/qa/run_unity_tests.py

# Capturar cena padronizada para evidência visual
python tools/capture/capture_scene.py --scene Assets/Game/Scenes/Calibration.unity --out evidence/captures/calibration.png
```

### Build Android
```bash
# Gerar build Android local
python tools/android/build_apk.py --target development --out builds/game-dev.apk
```

---

## 5. Definition of Done (DoD)

Uma story ou tarefa só é considerada `[x]` (concluída) quando:
1. Todos os critérios de aceitação da story foram atendidos.
2. A compilação no Unity passa sem nenhum warning marcado como erro e sem exceptions.
3. Testes EditMode e PlayMode passam 100%.
4. Todos os gates na ordem canônica (`validate_gltf`, `validate_asset`, `validate_visual`, `validate_scene`) retornam código 0.
5. As evidências (JSONs dos validadores, logs de teste e screenshots datados em `evidence/captures/`) foram salvas em disco e **inspecionadas visualmente** pelo agente (com 1 frase descritiva do que vê).
6. Todo asset ou ferramenta utilizada possui registro verificado em `LICENSES-ARTE.md`.
7. `PROGRESS.md` foi atualizado com o status e o ponteiro para a evidência.
