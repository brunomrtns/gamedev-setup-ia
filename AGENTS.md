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
7. **Quality Gate V2 (Ecossistema Open Source + Kitbash Modular):** Toda geração de ambiente ou entidade visual deve usar **estritamente** os pacotes modulares de `Assets/Game/Art/Environment/Modular/` (Kenney CC0), os materiais PBR de `Assets/Game/Materials/Modular/Mat_PBR_*` (Poly Haven CC0) ou modelos processados pelo script de normalização headless (`tools/art/normalize_fbx.py` / `fbx_canonical.py`). Primitivas nativas visíveis (`GameObject.CreatePrimitive`) e materiais sem `_BaseMap` são **falha de build** detectada pelos validadores V2 (`validate_scene.py` / `validate_asset.py --all`).
8. **Física e Combate (`docs/standards/physics-and-combat.md`):** Proibido criar veículos de trânsito em modo puramente cinemático que não reajam a impactos do jogador (obrigatória máquina de estados Routing/PhysicsFree/Wrecked). Proibido implementar disparo de armas onde o tiro divirja do centro do retículo da câmera ativa (obrigatória convergência raycast-centro → muzzle). Veículos rígidos exigem `centerOfMass` abaixo do eixo, `ContinuousDynamic` e clamp de velocidade. Props de rua exigem limiar de quebra por impulso.

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
- GPU (RTX 3060) é acessível ao WSL para cargas CUDA (ex: TripoSR).

---

## 4. Comandos Canônicos do Estúdio

Todos os agentes devem executar comandos através das interfaces padronizadas:

### Ambiente e Toolchain de Arte (WSL)
```bash
# Blender headless no WSL — geração/normalização de malhas FBX
wsl blender -b -P tools/art/<script_bpy>.py -- <output_dir>

# Python do venv do WSL (PIL/numpy/trimesh/requests)
wsl /home/bruno/.venvs/game-ai3d/bin/python tools/art/<script>.py <args>

# Download determinístico de kits modulares CC0 + texturas PBR Poly Haven (ETAPA V2)
python tools/art/download_modular_kits.py

# Normalização canônica de um asset existente (eixos, pivo, UV, triangulação)
wsl blender -b -P tools/art/normalize_fbx.py -- --input <fbx> --output <fbx> --preset weapon|character|prop|kit

# Geração IA 3D local (TripoSR/CUDA via WSL): imagem ou prompt -> obj/glb texturizado
python tools/art/ai_generator/generate_model.py --image <img.png> --output <out.obj|.glb> [--normalize --target-height <m>]

# Auto-rigging headless + ações canônicas (Idle/RunCycle/Jump/Slide) -> FBX Mecanim
wsl blender -b -P tools/art/auto_rig.py -- <mesh.obj> <out.fbx> [altura_m]

# Pipeline completo de personagem (TripoSR -> normalização -> texturas -> rig -> quality gate)
python tools/art/character_pipeline.py --image <concept.png> --output-dir unity/Assets/Game/Art/Characters/<Nome> --rig-type humanoid
```

### Receita Canônica — Novo Jogo do Zero (sessão fria)

Para um briefing de jogo novo, execute nesta ordem (referência: `NeonRushSceneBuilder.cs`
e `AbyssalBreachSceneBuilder.cs` são os templates de Scene Builder):

1. **Documento de design**: crie GDD/arquitetura em `_bmad-output/planning-artifacts/`
   (ver exemplos `gdd-neon-rush.md`, `gdd-abyssal-breach.md`).
2. **Catálogo de assets**: inventário em `docs/art/asset-catalog.md`; prefabs prontos em
   `Assets/Game/Prefabs/Modular/`; malhas utilitárias via `ProceduralMeshLibrary`
   (editor) — **nunca `GameObject.CreatePrimitive`**.
3. **Código gameplay**: assemblies em `Assets/Game/Scripts/` respeitando
   `docs/standards/architecture.md`; testes em `Assets/Game/Tests/EditMode/`.
4. **Scene Builder**: crie `Assets/Editor/<Jogo>SceneBuilder.cs` com
   `RunBuildSceneBatch` (build+save) e método de captura multi-ângulo com warm-up
   de shaders (copie o padrão de `RenderAndSave` + warmup de `StudioAutomation`).
5. **Build da cena** (Windows):
   `"C:\Program Files\Unity\Hub\Editor\6000.3.26f1\Editor\Unity.exe" -batchmode -quit -projectPath unity -executeMethod Studio.Editor.<Jogo>SceneBuilder.RunBuildSceneBatch -logFile evidence/logs/<jogo>-build.log`
6. **Gates obrigatórios** (todos exit 0): `validate_scene.py --scene <cena> --strict`,
   `validate_asset.py --all`, `python tools/qa/run_unity_tests.py` (33+ testes).
7. **Evidências**: capturas via método do builder ou
   `python tools/capture/capture_scene.py --scene <cena> --out evidence/captures/<jogo>.png`.
8. **Build Android**: `python tools/android/build_apk.py --target development --out builds/<jogo>.apk`.
9. **Atualize `PROGRESS.md`** com checklist, evidências e ponteiro para logs.

### Inspeção e Status
```bash
# Verificar estado dos editores e CLI
unity doctor
unity status

# Verificar integridade da cena atual
python tools/validation/validate_scene.py --scene Assets/Game/Scenes/Main.unity

# Verificar integridade de um asset
python tools/validation/validate_asset.py --asset Assets/Game/Models/Player.fbx
```

### Testes e Validação
```bash
# Executar todos os testes em batchmode
unity test unity/ --test-platform EditMode --test-platform PlayMode --format json

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
4. `ValidateScene` e `ValidateAsset` retornam código 0 (sem componentes órfãos ou leaks).
5. As evidências (logs de teste, screenshots em `evidence/`) foram salvas em disco.
6. `PROGRESS.md` foi atualizado com o status e o ponteiro para a evidência.
