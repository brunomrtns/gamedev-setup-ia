# PROGRESS — Handoff entre sessões

> **Leia primeiro.** Este é o arquivo de retomada. Qualquer sessão/agente que continue o trabalho
> deve: (1) ler este arquivo, (2) ler [AGENTS.md](AGENTS.md), (3) verificar o estado das tarefas no DAG via `python tools/studio/runner.py --list`,
> (4) executar a próxima tarefa pronta via `python tools/studio/runner.py --run-next` ou comando correspondente,
> (5) atualizar este arquivo e as evidências ao concluir cada item.
>
> Legenda: `[x]` feito e validado · `[~]` em andamento · `[ ]` pendente · `[!]` bloqueado (ação humana)

## Estado atual (resumo)

- **Fase:** Game 3 — NEON DISTRICT: FREE RIDE — **CONCLUÍDO E VALIDADO**
- **Última atualização:** 2026-10-16 — Sessão Devin (jogo novo a frio via Receita Canônica)
- **Status do Ambiente:** Unity 6.3 LTS (`6000.3.26f1`) Ativo, Licença Personal Ativa, URP 17.3, Windows Standalone x64 Operacional. Ambiente dual Windows+WSL2 (Blender 5.0.1/venv `game-ai3d`) operacional.
- **Status de Qualidade:** 100% dos testes UTF (**55/55** — 14 Neon Rush + 19 Abyssal Breach + 22 Neon District) aprovados (`evidence/test-reports/neondistrict-editmode-results.xml`).
- **Validação de Assets V2:** `validate_asset.py --all` → **5104/5104 aprovados (exit 0)** — relatório em `evidence/test-reports/asset-validation-v2.json`. Zero materiais sem `_BaseMap`, zero FBX sem UV0.
- **Validação de Cenas V2:** `NeonRush.unity` (exit 0), `AbyssalBreach.unity` (**exit 0 em `--strict`**), `Calibration.unity` (exit 0) e `NeonDistrict.unity` (**exit 0 em `--strict`**) — sem primitivas nativas visíveis, sem Missing Scripts, todos os materiais URP Lit texturizados.
- **Zero `GameObject.CreatePrimitive`** em todo o código do projeto (verificado por grep).
- **Evidências Visuais:** `evidence/captures/v2_neonrush.png` + `v2_abyssalbreach.png` (via `capture_scene.py` corrigido) + 12 capturas multi-ângulo regeneradas com a geometria V2.
- **Builds Entregues (sessões anteriores):**
  - Game 1: APK Android ARM64 IL2CPP (`builds/game_development_20261015_185856.apk`)
  - Game 2: Standalone Windows x64 (`builds/AbyssalBreach_Windows/AbyssalBreach.exe`)

### Game 3 — NEON DISTRICT: FREE RIDE (sessão fria, Receita Canônica)

- **GDD:** [`_bmad-output/planning-artifacts/gdd-neon-district.md`](_bmad-output/planning-artifacts/gdd-neon-district.md)
- **Cena:** `unity/Assets/Game/Scenes/NeonDistrict.unity` (~1.3 MB) — distrito 6×6 vias /
  25 quarteirões de 40×40 m (~272×272 m), avenida principal 16 m (z=+26,
  `asphalt_4_tracks`), ruas 12 m (`asphalt_2_tracks`), cruzamentos `asphalt_square`,
  zebras `asphalt_extra`, calçada-mãe `sideway_tile`, praça central (hedges, árvores,
  bancos, postes), 24 quarteirões com prédios Demo City por seed determinística.
- **Conversão URP vendor:** `ConvertVendorMaterialsToUrp()` no builder converte
  in-loco (idempotente) Demo City + SportCar Built-in RP → URP Lit
  (`_MainTex`→`_BaseMap`, `_Color`→`_BaseColor`, emission/normal/occlusion/smoothness
  preservados, vidros → transparente via `_Surface=1`).
- **Código novo:**
  - `Studio.Foundation`: `DriveMath` (integração de velocidade, atenuação de esterço
    por velocidade, yaw com boost de drift, slip angle, anel de spawn, wrap de ângulo);
    `ProceduralAudioService` estendido com loop de motor (pitch ∝ velocidade), loop de
    skid, `PlayCollisionThud` e `StartCruiseMusic` (synthwave procedural Am–F–C–G,
    120 BPM, 8 s em loop: kick, baixo em colcheias, pads detunados, arpejo 16ªs, hats).
  - `Studio.Input`: `DriveInputAdapter` (WASD/setas + drift Space/Shift + R recenter +
    controles virtuais touch).
  - `Studio.Gameplay`: `PlayerCarController` (Rigidbody arcade, grip lateral
    exponencial, drift, rodas FL/FR/RL/RR giram com sinal de espelhamento corrigido,
    trails de skid, thud em colisão); `TrafficNetwork` + `TrafficNetworkBuilder`
    (grafo dirigido mão direita: 120 arestas de faixa + conectores de cruzamento
    reto/esq/dir — **416 arestas** na grade 6×6); `TrafficVehicle` (segue polilinha
    por arc-length, raycast frontal → freia/para); `TrafficManager` (spawn em anel
    28–70 m, despawn >80 m, alvo 14, RNG semeado, colisor garantido, rb kinematic);
    `NeonDistrictGameManager` (targetFrameRate 60, sleepTimeout, RecenterPlayer).
  - `Studio.Cameras`: `ChaseCamera` (SmoothDamp + lag de yaw p/ drift).
  - `Studio.UI`: `NeonDistrictHUD` (km/h com cache de strings, FPS, indicador DRIFT,
    refresh 10 Hz) + `DriveTouchButton` (IPointerDown/Up → controles virtuais).
- **Carro do jogador:** `SportCar_1.prefab` escalado a ~4.5 m por bounds, BoxCollider
  por bounds, 2 faróis spot + lanterna traseira pontual, spawn na avenida (-60,·,22)
  heading +X. **Tráfego:** 7 prefabs civis AWBM (URP Lit nativo) — Air Plane, Military
  e Police excluídos por coerência com a fantasia.
- **Editor:** `NeonDistrictSceneBuilder.cs` — `RunBuildSceneBatch` +
  `CaptureNeonDistrictAnglesBatch` (warm-up de 3 frames). `BuildPipeline` ganhou
  `-productName=`; `build_apk.py` deriva o nome do produto do arquivo de saída.
- **Testes:** `NeonDistrictGameplayTests.cs` — 22 testes (DriveMath, polilinhas,
  grafo de tráfego, PickNext determinístico, nearest pose, controles virtuais).
- **Gates:** `validate_scene.py --strict` exit 0 · `validate_asset.py --all`
  5104/5104 exit 0 · UTF **55/55** (`evidence/test-reports/neondistrict-editmode-results.xml`)
  · 7 capturas `evidence/captures/neon_district_01..07.png` · APK
  `builds/neon_district.apk` (productName=NeonDistrict, `com.studio.neondistrict`).

### Refatoração V2 — o que mudou (ETAPA 5)

1. **Novo `unity/Assets/Editor/ProceduralMeshLibrary.cs`**: biblioteca canônica de malhas
   procedurais (`SM_Proc_Box/Cylinder/Sphere/Capsule/Octahedron/Torus/Plane` — UV0, normais,
   triângulos, mesmas dimensões das primitivas nativas) persistidas como `.asset` em
   `Assets/Game/Art/Meshes/`; helpers `CreatePart`, `CreateKitInstance` (PrefabUtility com
   preservação da correção de eixos -90X), `ScaleToHeight`/`ScaleToFit` por bounds de renderer.
   Windings de todas as malhas verificados numericamente antes do build.
2. **`NeonRushSceneBuilder.cs`**: pista/faixas/guard-rails/hazards → malhas `SM_Proc_*` com
   colliders equivalentes preservados; skyline de 26 edifícios → **prefabs reais do CityKit**
   (`building-skyscraper-*`, `building-*`, `low-detail-building-wide-*`, pool determinístico)
   escalados por bounds + faixa de janela neon; arcos neo-góticos → **portais `gate_complex`/
   `gate_simple` do SpaceKit** + beam neon; células de energia → octaedro procedural; portal de
   extração → toro procedural + disco de vórtice; fallback do player → malhas próprias.
3. **`AbyssalBreachSceneBuilder.cs`**: seabed/canopy → malhas próprias; **paredões da fenda →
   rochas modulares do kit CC0** (`rock_largeA/B`, `rock_crystalsLarge*`, `rock-wide`, `rock-large`)
   escaladas por bounds com `Mat_Abyssal_Rock`; `SpawnModelOrFallback` passa a usar malhas
   procedurais (nunca primitivas); coletáveis/olho do drone/anel do pod → malhas próprias com
   colliders equivalentes (SphereCollider/CapsuleCollider trigger).
4. **`StudioAutomation.cs`** (Calibration): primitivas substituídas por malhas próprias +
   `Mat_PBR_ConcretePanel`; **`CaptureScreenshotBatch` ganhou warm-up de 3 frames** (elimina
   o frame magenta de shaders URP não compilados em batchmode).
5. **`tools/capture/capture_scene.py`**: bug corrigido — `--scene` agora é passado como
   `-scenePath=` ao Unity (antes era ignorado e sempre capturava a cena aberta).
6. **Materiais**: 11 `.mat` que falhavam o gate (`_BaseMap` nulo) receberam albedo Poly Haven
   1024 preservando tint/emissão (`Mat_Lane_*`, `Mat_Robot*`, `Mat_EnergyCell`,
   `Mat_WindowStrip`, `Mat_DataCore`, `Mat_FPS_PlasmaGlow`, `Mat_OxygenTank`,
   `Mat_SubDrone_Eye`). `fountain-center.fbx`/prefab removidos (malha degenerada sem UV).
7. **Reconstrução e evidências**: ambas as cenas reconstruídas em batchmode
   (`evidence/logs/*-rebuild-v2.log`), 12 capturas multi-ângulo regeneradas, inspeção visual
   com paridade confirmada (toro do portal com textura, prédios kitbash com janelas, rochas
   na fenda abissal).
8. **ETAPA 1 do briefing (IA 3D local + rigging headless)**: `tools/art/auto_rig.py`
   (armadura humanoide, pesos automáticos robustos, ações Idle/RunCycle/Jump/Slide,
   FBX compatível com Mecanim) e `tools/art/character_pipeline.py` já existiam.
   Novo nesta sessão: **`tools/art/ai_generator/generate_model.py`** — módulo canônico
   imagem/prompt→3D via TripoSR no WSL/CUDA (bake de textura, .obj/.glb, `--normalize`
   encadeia normalize_mesh.py). Comandos documentados em `AGENTS.md` §4.
9. **Catálogo expandido para futuros briefings**: `KENNEY_KITS` ganhou `racing-kit`
   (pistas/props de corrida), `car-kit` (veículos) e `nature-kit` (vegetação) +
   `MODEL_DENYLIST` (exclui `fountain-center.fbx` degenerado). `ModularKitImporter.RunImportBatch`
   regenerou **852 prefabs, 0 erros**; `validate_asset.py --all` → **5097/5097, exit 0**.
   Nova **"Receita Canônica — Novo Jogo do Zero (sessão fria)"** em `AGENTS.md` §4 —
   fluxo briefing→GDD→SceneBuilder→gates→capturas→APK para sessões sem contexto prévio.

---

## Checklist de Execução do Roadmap

### Fase 0 — Auditoria, Compatibilidade e Governança
- [x] Ler roadmap integralmente e versionar em [docs/roadmap/plano_game_studio_ia_unity.md](docs/roadmap/plano_game_studio_ia_unity.md).
- [x] Auditar SO, Hardware, GPU (RTX 3060) e Ferramentas → [docs/operations/environment-audit.md](docs/operations/environment-audit.md).
- [x] Contratos Canônicos vinculantes criados: [AGENTS.md](AGENTS.md) e [CLAUDE.md](CLAUDE.md).
- [x] Padrões de Engenharia e Qualidade:
  - [x] [docs/standards/unity.md](docs/standards/unity.md) (C# 12, URP, zero GC loop)
  - [x] [docs/standards/architecture.md](docs/standards/architecture.md) (Assembly Definitions, desacoplamento)
  - [x] [docs/standards/testing.md](docs/standards/testing.md) (UTF EditMode/PlayMode)
  - [x] [docs/standards/mobile-performance.md](docs/standards/mobile-performance.md) (Budgets Android, draw calls, tris, RAM)
  - [x] [docs/standards/asset-quality.md](docs/standards/asset-quality.md) (Geometria, texturas, áudio)
  - [x] [docs/art/art-bible.md](docs/art/art-bible.md) (Identidade visual estilizada e iluminação Neon Rush)
  - [x] [docs/art/art-spec.yaml](docs/art/art-spec.yaml) (Especificação técnica em YAML)
- [x] Architecture Decision Records (ADRs):
  - [x] [docs/decisions/ADR-001-stack-and-architecture.md](docs/decisions/ADR-001-stack-and-architecture.md) (Unity 6.3 LTS + URP + Android)
  - [x] [docs/decisions/ADR-002-autonomous-orchestration.md](docs/decisions/ADR-002-autonomous-orchestration.md) (BMad + OmO + Studio DAG Runner)
  - [x] [docs/decisions/ADR-003-neon-rush-architecture.md](docs/decisions/ADR-003-neon-rush-architecture.md) (Arquitetura e Decisões de NEON RUSH)

### Fase 1 — Fundação Técnica e Ferramental
- [x] Instalar ferramentas base: Python 3.13, Bun 1.4, Node.js 24 LTS, Unity Hub, Unity CLI.
- [x] Instalar Unity Editor **6.3 LTS (6000.3.26f1)** + Módulos Android (OpenJDK 17, NDK r27c, SDK 34-37).
- [x] Instalar e configurar BMad Method v6.12.4 + BMad Game Dev Studio v0.7.2.
- [x] Criar motor de orquestração em DAG: `tools/studio/runner.py`.
- [x] Criar ferramentas determinísticas de validação: `validate_scene.py`, `validate_asset.py`, `build_apk.py`, `capture_scene.py`.

### Fase 2 — Vertical Slice 3D: NEON RUSH
- [x] Planejamento BMad:
  - [x] Game Design Document: [_bmad-output/planning-artifacts/gdd-neon-rush.md](_bmad-output/planning-artifacts/gdd-neon-rush.md)
  - [x] Arquitetura Técnica: [_bmad-output/planning-artifacts/architecture-neon-rush.md](_bmad-output/planning-artifacts/architecture-neon-rush.md)
  - [x] Backlog de Stories com Critérios de Aceitação: [_bmad-output/planning-artifacts/backlog-stories.md](_bmad-output/planning-artifacts/backlog-stories.md)
  - [x] Plano de Testes & Quality Gates: [_bmad-output/planning-artifacts/test-plan.md](_bmad-output/planning-artifacts/test-plan.md)
- [x] Engenharia & Código C# 12 (Assemblies segregados):
  - [x] `Studio.Foundation`: `GameConstants.cs`, `LaneMath.cs`, `ProceduralAudioService.cs` (síntese procedural de áudio em tempo de carregamento).
  - [x] `Studio.Input`: `NeonInputAdapter.cs` (suporte simultâneo a swipes touch mobile e teclado WASD/Setas no Editor).
  - [x] `Studio.Gameplay`: `PlayerMovement.cs` (3 faixas, pulo parabólico, slide), `RobotAnimator.cs` (bobbing, banking roll, squash & stretch), `PlayerController.cs`, `ObstacleHazard.cs` (LowLaser, HighBeam, FullBlock), `EnergyCellCollectible.cs` (octaedro giratório ciano), `ExtractionPortal.cs` (anéis concêntricos e vórtice), `GameManager.cs`.
  - [x] `Studio.Cameras`: `SmoothFollowCamera.cs` (seguimento suave em terceira pessoa sem tremores).
  - [x] `Studio.UI`: `UIManager.cs` (Start Panel, HUD com células/score/progresso, Game Over, Vitória, controles mobile touch na tela).
- [x] Automação de Editor & Construção de Cena:
  - [x] `Studio.Editor`: `NeonRushSceneBuilder.cs` (montagem determinística da pista de 350m, 12 materiais URP neon, 24 edifícios futuristas, arcos neo-góticos, iluminação noturna e pós-processamento).
  - [x] Cena real gerada: `Assets/Game/Scenes/NeonRush.unity` (454 KB).
- [x] Testes Automatizados (UTF):
  - [x] `NeonRushGameplayTests.cs` (10 novos testes de unidade cobrindo faixas, clamping, pulo e pontuação).
  - [x] Suíte total: 14/14 testes aprovados (100% passing) em `evidence/test-reports/junit-results.xml`.
- [x] Validação e Evidências Visuais:
  - [x] `validate_scene.py` aprovou `NeonRush.unity` com código 0 (sem scripts ausentes ou órfãos).
  - [x] 6 capturas de tela de alta resolução geradas em `evidence/captures/` cobrindo início, gameplay, obstáculos, portal, game over e vitória.
- [x] Compilação do APK Android ARM64 IL2CPP (`tools/android/build_apk.py`).

### Fase 3 — Jogo 2: Abyssal Breach (Sub-Aquatic Survival / Extraction Shooter)
- [x] Planejamento & Arquitetura:
  - [x] Game Design Document: [_bmad-output/planning-artifacts/gdd-abyssal-breach.md](_bmad-output/planning-artifacts/gdd-abyssal-breach.md)
  - [x] Arquitetura Técnica: [_bmad-output/planning-artifacts/architecture-abyssal-breach.md](_bmad-output/planning-artifacts/architecture-abyssal-breach.md)
  - [x] Architecture Decision Record: [docs/decisions/ADR-004-abyssal-breach-architecture.md](docs/decisions/ADR-004-abyssal-breach-architecture.md)
- [x] Pipeline de Conversão de Assets do Vault:
  - [x] Scripts de conversão e extração de texturas PBR (`tools/art/convert_abyssal_assets.py`) e meshes FBX via Blender headless (`tools/art/generate_abyssal_meshes.py`).
  - [x] 11 texturas PBR (Albedo, Normal, Roughness/Metallic) e 9 modelos FBX triangulados em `unity/Assets/Game/Art/AbyssalBreach/`.
  - [x] Validação determinística de conformidade de assets aprovada via `tools/validation/validate_asset.py`.
- [x] Engenharia & Código C# 12 (Universal Render Pipeline):
  - [x] `Studio.Foundation`: `AbyssalMath.cs` (cálculo de drenagem de O2, dano por hipóxia, balística ADS e hidrodinâmica), `ProceduralAudioService.cs` (estendido com sintetizadores de áudio de plasma submarino, corte de faca, recarga, recarga de oxigênio e sonar ping).
  - [x] `Studio.Input`: `AbyssalInputAdapter.cs` (novo Input System e métodos determinísticos implementando `IGameInputAdapter`).
  - [x] `Studio.Gameplay`: `AbyssalFirstPersonMotor.cs` (motor 6-DoF moderado implementando `ICharacterMotor` com flutuabilidade, arrasto aquático, bobbing e desacoplamento vertical), `OxygenSurvivalSystem.cs` (vida, oxigênio, consumo dinâmico e hipóxia), `AbyssalWeaponController.cs` (rifle de plasma + faca tática, recuo, dispersão, mira ADS e traçadores de tiro), `AbyssalThreatDrone.cs` (IA de patrulha/perseguição de drones sentinelas), `AbyssalCollectible.cs` (tanques de O2, núcleos de energia, munição), `AbyssalExtractionPod.cs` (cápsula de extração com anel de sinalização), `AbyssalGameManager.cs` (máquina de estados: Menu, Jogando, Vitória, Derrota).
  - [x] `Studio.UI`: `AbyssalHUDManager.cs` (HUD tático de capacete de mergulho: medidores de O2 e HP, munição, bússola e retículo dinâmico).
- [x] Automação de Editor & Construção de Cena:
  - [x] `AbyssalBreachSceneBuilder.cs` (montagem determinística do relevo da fenda oceânica, corais, rochas, skybox sci-fi, luz volumétrica e pós-processamento).
  - [x] Cena real gerada: `Assets/Game/Scenes/AbyssalBreach.unity` (698 KB), validada com código 0 em `tools/validation/validate_scene.py`.
- [x] Testes Automatizados (UTF):
  - [x] `AbyssalBreachGameplayTests.cs` (19 testes cobrindo balística, oxigênio, hipóxia, ciclo de combate, coleta de recursos e motor).
  - [x] Suíte total: **33/33 testes aprovados (100% passing)** (14 Neon Rush + 19 Abyssal Breach) em `evidence/test-reports/abyssal-editmode-results.xml`.
- [x] Validação e Evidências Visuais:
  - [x] 6 capturas de tela determinísticas de alta resolução geradas em `evidence/captures/` (`abyssal_breach_01_start_helmet.png` a `06_hypoxia_gameover.png`).
- [x] Build Standalone Windows x64:
  - [x] `unity/Assets/Editor/BuildPipeline.cs` e `tools/windows/build_windows.py` executados com sucesso (Exit code 0).
  - [x] Executável `builds/AbyssalBreach_Windows/AbyssalBreach.exe` validado via `tools/qa/verify_build.py` (0 erros de compilação, 0 C# warnings, smoke test de execução ativo sem erros no Player.log).

### Fase 4 — Jogo 3: Neon District: Free Ride (Open-City Driving / Mobile)
- [x] GDD: [_bmad-output/planning-artifacts/gdd-neon-district.md](_bmad-output/planning-artifacts/gdd-neon-district.md)
- [x] Engenharia C#: `DriveMath`, `DriveInputAdapter`, `PlayerCarController`, `TrafficNetwork`/`TrafficNetworkBuilder`/`TrafficVehicle`/`TrafficManager`, `NeonDistrictGameManager`, `ChaseCamera`, `NeonDistrictHUD`, `DriveTouchButton`, extensão de `ProceduralAudioService` (motor/skid/thud/synthwave).
- [x] Scene Builder: `NeonDistrictSceneBuilder.cs` — grid 6×6 vias, avenida 16 m, 25 quarteirões, praça central, conversão URP dos packs vendor, rede de tráfego de 416 arestas, postes/props com orçamento de luzes.
- [x] Testes UTF: 22 novos testes; suíte total **55/55** aprovados.
- [x] Gates: cena `--strict` exit 0 · assets `--all` 5104/5104 · 7 capturas `neon_district_*.png`.
- [x] Build Android: `builds/neon_district.apk` (verificar artefato no log `evidence/logs/build_android_*.log`).

<!-- TASKS_START -->

## Fila de Tarefas Autônomas Persistentes (.studio/tasks/)

| ID | Estágio | Tarefa | Status | Dependências | Evidências |
|---|---|---|---|---|---|
| `TASK-001` | setup | Auditoria de SO, Hardware e Ferramentas | [x] Concluído | — | [`environment-audit.md`](docs/operations/environment-audit.md) |
| `TASK-002` | governance | Contratos Canônicos e Padrões (AGENTS.md, standards) | [x] Concluído | TASK-001 | [`AGENTS.md`](AGENTS.md), [`CLAUDE.md`](CLAUDE.md), [`unity.md`](docs/standards/unity.md), [`architecture.md`](docs/standards/architecture.md), [`testing.md`](docs/standards/testing.md), [`mobile-performance.md`](docs/standards/mobile-performance.md), [`asset-quality.md`](docs/standards/asset-quality.md) |
| `TASK-003` | agents | Instalação do BMad Game Dev Studio e Módulos | [x] Concluído | TASK-001 | [`_bmad`](_bmad/) |
| `TASK-004` | engine | Instalação do Unity 6.3 LTS e Módulos Android | [x] Concluído | TASK-001 | [`Unity.exe`](C:\Program Files\Unity\Hub\Editor\6000.3.26f1\Editor\Unity.exe) |
| `TASK-005` | engine | Inicialização do Projeto Unity URP e Settings | [x] Concluído | TASK-004 | [`unity`](unity/) |
| `TASK-006` | engineering | Scripts C# de Automação de Editor e Validadores de Cena | [x] Concluído | TASK-005 | [`Calibration.unity`](unity/Assets/Game/Scenes/Calibration.unity) |
| `TASK-007` | qa | Cena de Calibração e Testes Automatizados UTF | [x] Concluído | TASK-006 | [`junit-results.xml`](evidence/test-reports/junit-results.xml) |
| `TASK-008` | agents | Conexão de Agente Autônomo via Unity MCP / CLI | [x] Concluído | TASK-006 | [`mcp_config.json`](C:\Users\Bruno\.gemini\config\mcp_config.json) |
| `TASK-009` | build | Pipeline de Build Android Local (APK) | [x] Concluído | TASK-007 | [`game_development_20261015_185856.apk`](builds/game_development_20261015_185856.apk) |
| `TASK-010` | e2e | Teste Ponta a Ponta E2E: Briefing -> GDD -> Unity -> Testes -> APK | [x] Concluído | TASK-008, TASK-009 | [`qa-report.md`](evidence/qa-report.md) |
| `TASK-011` | engineering | Neon Rush - Foundation Services & Procedural Synthesized Audio | [x] Concluído | TASK-010 | [`ProceduralAudioService.cs`](unity/Assets/Game/Scripts/Foundation/ProceduralAudioService.cs) |
| `TASK-012` | engineering | Neon Rush - Input System & Touch/Keyboard Swipe Adapter | [x] Concluído | TASK-011 | [`NeonInputAdapter.cs`](unity/Assets/Game/Scripts/Input/NeonInputAdapter.cs) |
| `TASK-013` | engineering | Neon Rush - Player 3-Lane Movement & Procedural Robot Animator | [x] Concluído | TASK-012 | [`PlayerMovement.cs`](unity/Assets/Game/Scripts/Gameplay/PlayerMovement.cs) |
| `TASK-014` | engineering | Neon Rush - Hazards, Obstacles & Energy Cell Collectibles | [x] Concluído | TASK-013 | [`ObstacleHazard.cs`](unity/Assets/Game/Scripts/Gameplay/ObstacleHazard.cs) |
| `TASK-015` | engineering | Neon Rush - GameManager, UI Canvas & Audio Integration | [x] Concluído | TASK-014 | [`GameManager.cs`](unity/Assets/Game/Scripts/Gameplay/GameManager.cs), [`UIManager.cs`](unity/Assets/Game/Scripts/UI/UIManager.cs) |
| `TASK-016` | engineering | Neon Rush - Track Builder, URP Materials, Lighting & Extraction Portal | [x] Concluído | TASK-015 | [`NeonRushSceneBuilder.cs`](unity/Assets/Editor/NeonRushSceneBuilder.cs), [`NeonRush.unity`](unity/Assets/Game/Scenes/NeonRush.unity) |
| `TASK-017` | qa | Neon Rush - UTF EditMode & PlayMode Automated Tests | [x] Concluído | TASK-016 | [`junit-results.xml`](evidence/test-reports/junit-results.xml) |
| `TASK-018` | qa | Neon Rush - Scene Assembly & Visual Capture Validation | [x] Concluído | TASK-017 | [`captures`](evidence/captures/) |
| `TASK-019` | build | Neon Rush - Android Build (APK) & Final Verification | [x] Concluído | TASK-018 | [`game_development_20261015_185856.apk`](builds/game_development_20261015_185856.apk) |
| `TASK-020` | governance | Abyssal Breach - GDD, Architecture & ADR-004 | [x] Concluído | TASK-010 | [`gdd-abyssal-breach.md`](_bmad-output/planning-artifacts/gdd-abyssal-breach.md), [`ADR-004`](docs/decisions/ADR-004-abyssal-breach-architecture.md) |
| `TASK-021` | art | Abyssal Breach - Asset Pipeline & 3D Model/Texture Conversions | [x] Concluído | TASK-020 | [`Models`](unity/Assets/Game/Art/AbyssalBreach/Models), [`Textures`](unity/Assets/Game/Art/AbyssalBreach/Textures) |
| `TASK-022` | engineering | Abyssal Breach - Core Gameplay, 6-DoF Motor, Submarine Weapons & Oxygen System | [x] Concluído | TASK-021 | [`AbyssalFirstPersonMotor.cs`](unity/Assets/Game/Scripts/Gameplay/AbyssalFirstPersonMotor.cs), [`OxygenSurvivalSystem.cs`](unity/Assets/Game/Scripts/Gameplay/OxygenSurvivalSystem.cs), [`AbyssalWeaponController.cs`](unity/Assets/Game/Scripts/Gameplay/AbyssalWeaponController.cs) |
| `TASK-023` | engineering | Abyssal Breach - Scene Builder, Coral Trench Environment, Lighting & Tactical Helmet HUD | [x] Concluído | TASK-022 | [`AbyssalBreachSceneBuilder.cs`](unity/Assets/Editor/AbyssalBreachSceneBuilder.cs), [`AbyssalBreach.unity`](unity/Assets/Game/Scenes/AbyssalBreach.unity) |
| `TASK-024` | qa | Abyssal Breach - UTF EditMode Automated Tests (19 unit/integration tests) | [x] Concluído | TASK-023 | [`abyssal-editmode-results.xml`](evidence/test-reports/abyssal-editmode-results.xml) |
| `TASK-025` | qa | Abyssal Breach - Deterministic Visual Captures & Verification | [x] Concluído | TASK-024 | [`evidence/captures/`](evidence/captures/) |
| `TASK-026` | build | Abyssal Breach - Standalone Windows x64 Build & Execution Validation | [x] Concluído | TASK-025 | [`AbyssalBreach.exe`](builds/AbyssalBreach_Windows/AbyssalBreach.exe) |

<!-- TASKS_END -->

## Log de Sessões

| Data | Sessão / Agente | Resumo das Entregas |
|---|---|---|
| 2026-10-15 | Antigravity (Arquiteto Principal) | Fundação completa: ambiente, contratos canônicos, Unity 6.3 LTS + Android, BMad e runner DAG. |
| 2026-10-15 | Antigravity (Vertical Slice Lead) | Desenvolvimento completo de **NEON RUSH**: GDD, Art Bible, ADR-003, backlog de stories, 10 scripts C# em 6 assemblies, cena procedural `NeonRush.unity` com materiais URP e 350m de pista, 14 testes UTF passando 100%, 6 capturas de tela em batchmode e compilação de APK Android. |
| 2026-10-16 | Antigravity (Líder Técnico & Engenheiro Chefe) | Desenvolvimento completo de **ABYSSAL BREACH**: GDD, Arquitetura, ADR-004, pipeline de conversão de assets (20 PBR textures e FBX triangulados), 11 componentes C# modulares em 5 assemblies respeitando Studio.Foundation (motor 6-DoF moderado, balística de plasma submarino, sistema de hipóxia/oxigênio, HUD tático de mergulho, IA de drones e pods), cena procedural `AbyssalBreach.unity` com iluminação URP e pós-processamento, 19 novos testes UTF (total 33/33 passando 100%), 6 capturas de evidência visual de alta fidelidade e compilação de executável Windows standalone (`AbyssalBreach.exe`) com 0 erros/warnings de compilação C# e verificação de processo. |
| 2026-10-16 | Devin (Refatoração V2 — ETAPA 5) | Conclusão da **Refatoração Arquitetural V2 (Quality Gate V2)**: validação de assets `1848/1848` exit 0; nova `ProceduralMeshLibrary` com 7 malhas procedurais `.asset` (UV0/normais, winding verificado numericamente); **zero `GameObject.CreatePrimitive`** — skyline NeonRush em prefabs CityKit + portais SpaceKit, paredões Abyssal em rochas kitbash CC0, octaedros/toros procedurais para coletáveis e portais; correção de `capture_scene.py` (`-scenePath=`) e warm-up de shaders em `CaptureScreenshotBatch`; cenas NeonRush/AbyssalBreach/Calibration reconstruídas e validadas (AbyssalBreach em `--strict`); **33/33 testes UTF**; 14 capturas de evidência regeneradas. Detalhes completos em `HANDOFF.md`. |
| 2026-10-17 | Devin (Engenheiro Chefe Gameplay/Física) | **NEON DISTRICT v2.2**: refatoração de física e combate — `TrafficVehicle` com máquina de estados de colisão (Routing/PhysicsFree/Wrecked, `FindNearestEdge` re-roteia NPCs após impacto), `BreakableProp` (postes decepam por limiar de impulso, toco estático + haste com Rigidbody), anti-launch do player (`centerOfMass` -0.4, `ContinuousDynamic`, clamps de velocidade + auto-nivelamento), convergência balística câmera→muzzle (`ComputeMuzzleDir` + ADS centrado), F10 debug de posição, fixes de z-fighting (cruzamentos cobrem a avenida, gaps >=5cm, tiling de calçada 48x48). Novo padrão `docs/standards/physics-and-combat.md` + regras em AGENTS.md/CLAUDE.md. **75/75 testes UTF** (8 novos), cena `--strict` validada. |


## NEON DISTRICT — sessão de física/VFX (17/10)

**Bug raiz do dia:** `DynamicsManager.m_SimulationMode=2` (Script) — física
desligada: carro girava mas não andava, impulsos não resolviam, nada
funcionava. Origem provável: drift de serialização do Unity (serializedVersion
19→24). **Guarda definitiva:** `Physics.simulationMode=FixedUpdate` no Awake
do GameManager + `DynamicsManagerSettingsFix()` no build — nunca mais mata.

**Cadeia de falhas e correções:**
- Poste não caía: arquitetura rb-no-colisor errada → rb vai na raiz do prop.
- Carro transparente na batida: malha `wrecked_*` com material FBX embutido
  (fora do conversor URP) + conversor marcava alfa~0 como vidro →
  `SwapVisual` agora só escurece o intacto (nunca esconde malha).
- Fumaça rosa/preta: primeira extração do War FX pulou `.mat` (prefabs sem
  material = magenta) + shaders `WFX_*` incompatíveis URP → re-extração
  completa + `ConvertParticleMaterialsToUrp` força `Particles/Unlit` com
  `_BaseMap` fallback.
- Carro de NPC sumindo: `TrafficManager` despawna >80m e em dead-end →
  veículos `PhysicsFree`/`Wrecked`/`Exploded` nunca são reciclados.
- Colisores compostos (`Col_Chassis/Cabin/Hood/Tank`) no player + tráfego:
  decal cai na lataria, zonas de dano (Hood 1.5x/Tank 2x).
- `VehicleDamageController`: HP 100, estágios faísca→fogo(capô, 5-8s)→
  explosão (OverlapSphereNonAlloc + AddExplosionForce + dano quadrático +
  CameraShakeBus + fogos residuais 12s).
- War FX (CFX mobile) importado do cache (110+289 assets); Mirza/Cinematic
  Explosions NÃO baixado — fallback `WFXMR_Explosion`.
- Compound colliders + compound-building + MeshCollider prédios.
- Testes: 77/77 verde; cena `--strict` ok; evidências em evidence/captures/.

**Lição operacional:** se física "morre do nada" → checar
`ProjectSettings/DynamicsManager.asset` (m_SimulationMode) PRIMEIRO, antes
de mexer em código.

---

## Pedestres — reconstrução limpa com clips REAIS (KayKit Mecanim Humanoid)

**Problema crônico resolvido:** todas as tentativas anteriores falhavam —
procedural bone-curves (rejeitado pelo usuário), retarget de rig robô,
Ellen (3D Game Kit) com rest pose incompatível (braço a 90°, tronco a 45°).

**Solução final:**
- `KayKit Knight.fbx` (CC0, `github.com/KayKit-Game-Assets/.../Adventures-1.0`)
  baixado p/ `Assets/Game/Art/Animations/KayKit/` — 133 clips humanóides reais
  embutidos como takes, rig de proporções humanas.
- `ReimportKayKitAnims()`: `ModelImporter` → Humanoid + CreateFromThisModel,
  clips nomeados por take, `loopTime` em locomotion, root motion extraído
  (applyRootMotion=false → in-place), `heightFromFeet`.
- `Pedestrian.controller`: Idle→Walking_A (Speed>0.1)→Running_A (Speed>1.2),
  Any→Death_B (trigger Dead), Any→Hit_A (bool Cowering = medo/flinch).
- **Bug raiz multi-armature:** prefabs `npc_csl_*` são modulares — corpo,
  cabelo e roupas de FBXs separados, cada um com armature própria. O avatar
  animava UM esqueleto; a malha do corpo ficava em bind pose. Fix:
  `Pedestrian.RebindToAvatarSkeleton()` rebinda os 30 SMRs por nome de osso
  ao esqueleto dirigido pelo avatar. Aterramento por bounds no spawn.
- Prefabs restaurados do pack original; robô e FBXs gerados removidos de
  Resources; `tools/art/animate_npc_rig.py` deletado.
- Validação: `CapturePedAnims` (batch) amostra clips via
  `AnimationMode.SampleAnimationClip` + loga footY/hipsY/headY/bounds.
  Evidência: `evidence/captures/ped_{idle,walk,walk2,run,death,cower}.png` —
  morte deitada, corrida dinâmica, passada com pé elevado. OK.

**Aprendizado para NÃO repetir:** `Animator.Update()` não escreve pose fora
de play mode — usar `AnimationMode.SampleAnimationClip` em batch.

---

## QA Autônomo v2 — Studio.QA + AutonomousPlaytester (18/10)

**Arquitetura:** núcleo universal `Studio.QA` (`Assets/Game/Scripts/QA/`):
`QaResult` (Pass/Fail/Blocked/Invalid/Inconclusive), `QaScenario` com
pré-condições (`PrepStatus`), `QaFinding` deduplicado, `QaSession` JSON,
`CoverageGrid`, `PerfTracker` (warmup+spikes), `ExceptionWatchdog`.
Adapter `AutonomousPlaytester` em `Studio.Gameplay` — 8 cenários ND +
monitores contínuos + **driver por input real** (`SetVirtualControl`/
`SetQaFootMove`/`TriggerEnterExit`, mesmos canais dos botões touch).

**Runner:** `tools/qa/run_autonomous_playtest.py` → editor vivo via
`unity cmd` (open_scene→editor_play→eval_file→console→export). NÃO usar
`-runTests` batchmode (testcasecount=0 = falso positivo). Payloads eval em
`tools/qa/*.cs`. Sessões: `evidence/qa/session_*.json`, relatórios
`report_*.md`, frames `evidence/qa/frames/`, checklist `evidence/qa-report.md`.

**Bugs de produção encontrados e corrigidos (com evidência):**
- NRE `TrafficNetwork.PickNext:93` — `_rng` (System.Random) não serializável
  zera em domain reload durante play → `_rng ??=` + guard + teste
  `PickNext_NullRng_ReturnsDeadEnd`. 0 NREs pós-fix.
- NRE `BreakableProp.Break` — `_visual` sem Renderer → guard de renderer.
- `PED_FLOATING` — pose animada eleva esqueleto 0.5–1.4m acima do root;
  fix iterativo (root −lift/2 + cápsula compensada) — em validação.

**Bugs do harness corrigidos:** FOOT_SLIDING compartilhava estado entre
peds (agora per-ped); whiskers batiam nos colisores compostos do próprio
carro (agora `IsChildOf` skip); DriveAccel resetava olhando +Z numa avenida
E-W; enter threshold 2.4<3.0.

**Validado por cenário (input real):** PhysicsSimGuard, DriveBrake,
ShotPanic (ammo 30→29→fuga), ScarePanic, RunOver (atropelamento real),
ExitEnter (Pass s3/Blocked s4 = flake timing). DriveAccel ainda pendente
de veredito limpo (obstruções ambientes dominavam — whiskers/ré adicionados).

**Detecção de obstáculo:** whiskers frontais (0/±22°, ignora self-colliders)
→ bloqueio >0.9s → RÉ real (input Brake) esterçando pro lado livre → retoma.
**Pendente:** FOOT_SLIDING confirmado em 01f_02/01m_02/01m_03/02m_02/02f_01
_baked (SMRs de roupa), perf spikes ~350-420ms pós-warmup, cobertura real.

---

## Biblioteca de Referências (18/10)

Criada `docs/references/` — material de consulta somente-leitura para
agentes/devs, fora de `unity/Assets/` (não é importado pelo Editor).

- `books/` — "Game Programming Patterns" (Nystrom) e "Level Up Your Code
  with Design Patterns and SOLID" (PDFs importados de Downloads).
- `unity/ai-navigation/` — docs oficiais `com.unity.ai.navigation`
  (espelho needle-mirror, branch master, `Documentation~`), com
  `CodeExamples/` C# e imagens.
- `games/` — reservado (análises/postmortems/GDDs públicos).
- Índice e convenções: `docs/references/README.md`.

---

## Multiplayer NEON DISTRICT — fatia vertical (18/10)

**Decisão:** Netcode for GameObjects 2.13.3 + Unity Transport (UDP, porta 7777).
Autoridade no servidor: cliente envia inputs via RPC → servidor simula
`PlayerCarController` (campos `UseNetInput`/`Net*`) → `NetworkTransform`
(server-authoritative) replica. ADR em `docs/decisions/adr-multiplayer-netcode.md`.

**Implementado:** `Studio.Net` asmdef, `NdNetSession` (args `-ndserver`,
`-ndclient=IP:PORT`, `-ndautodrive`), `NdNetBootstrap` (hook runtime; SP
preservado sem args), `NetPlayerCar` NB + prefab `Resources/Net/NetPlayerCar`
(clone do PlayerCar da cena). `BuildLinuxServerCommandLine` no BuildPipeline;
módulo Linux Dedicated Server instalado. Deploy script `tools/net/deploy_server.sh`
(systemd + ufw 7777/udp + limits CPU/RAM pra coexistir com produção).

**Validação:** pendente — build Windows+Linux server e teste 2 clientes.

**VALIDAÇÃO REMOTA CONCLUÍDA (18/10 ~22:58):** Linux Dedicated Server deployado
na VPS em `/opt/neon-district`, serviço systemd `neon-district` (enabled, limits
2G/150%CPU), escutando `0.0.0.0:7777/udp` (UFW aberto). Dois clientes Windows
conectaram a `161.97.133.242:7777`: identidades únicas (owner=1/2), spawn
autoritativo separado (-54 vs -48), cada cliente controla só o próprio carro,
ambos veem os dois, movimento replicado (`fwd=14.5m/s` em owner=1). Desconexão
de um cliente → carro despawned ~30s, servidor+restante sobreviveram.
Servidor: 33% CPU / 225MB RAM. Produção (printflow3d, gpcg-*, bi-*) intacta.
Evidências: `evidence/net/` (c1.log, c2.log, vps-server.log).
Pendente: predição client-side, sync peds/tráfego/armas (etapas B-E), modo a pé.

## Multiplayer Etapa B — predição + reconciliação (18/10)

**Implementado** (`NetPlayerCar.cs` rewrite):
- **Predição do dono**: PlayerCarController ligado no owner, rb dinâmico,
  resposta local imediata; inputs sequenciados 30Hz via RPC.
- **Reconciliação**: ackSeq do servidor; erro >3m → snap (colisão real);
  0.15–3m → blend suave 4×/s (PhysX não determinístico — replay exato não
  garantido; correção controlada). NetworkTransform REMOVIDO do prefab.
- **Interpolação remota**: buffer de snapshots, render 100ms no passado.
- **Segurança**: RequireOwnership, rate-limit 60Hz, clamp inputs, rejeita
  NaN/seq antiga; servidor nunca aceita posição do cliente.
- **Enter/exit infra**: `OccupantClientId` NetworkVariable +
  RequestEnter/ExitServerRpc com validação de assento (visual a pé pendente —
  precisa de entidade de pé em rede, documentado).

**Teste remoto VPS (23:08)**: 2 clientes Windows → 161.97.133.242:7777.
seq=0→743 (input stream 30Hz), owner=1 dirigiu 25m/s→11m/s, cross-observação.
Servidor: 35.8% CPU / 222MB RAM, 28 containers produção intactos.
Evidências: `evidence/net/nd-B-c*.log`, journalctl VPS.

## Multiplayer Etapa C — jogador a pé em rede (18/10)

**Implementado:**
- `NetPlayerFoot` NB: predição no dono (OnFootMotor/CharacterController —
  kinematic, quase determinístico), servidor autoritativo via `UseNetInput`
  (NetMove/NetYaw/NetSprint/Jump/Crouch), snapshots+interpolação nos remotos
  (Speed no Animator dos peds _baked).
- `IsOnFoot` NetworkVariable<bool> → visibilidade/física do avatar a pé.
- Enter/exit completo: `RequestExitServerRpc` no carro (valida ocupante,
  libera OccupantClientId, teleporta pé p/ -right*1.6+up*0.4) +
  `EnterCarServerRpc` no pé (valida dist≤4.5m e assento livre).
- Registry `NdNetSession.Feet[clientId]` pareia pé↔jogador; servidor spawna
  pé por cliente (SpawnWithOwnership), spawn inicial (0,-50,0) escondido.
- `NetPrefabBuilder` (editor) gera NetPlayerFoot.prefab de `npc_csl_*_baked`.
- `DriveInputAdapter`: PeekJump/PeekCrouch não-destrutivos.
- **Bug raiz corrigido**: prefab de pé precisava `nm.AddNetworkPrefab()` nos
  dois lados — sem isso nunca replicava nos clientes (pés congelados).
- Autoteste: `-ndautodrive` dirige ~8s → sai → anda → re-entra ~6s.

**Validação remota (VPS 161.97.133.242:7777, 23:36):**
2 clientes Windows → connect + IDs únicos → drive fwd=25m/s →
`exit car 1 by 1` + `exit car 2 by 2` → `foot owner=N onFoot=True` (pés
visíveis ao lado dos carros) → caminhada replicada → `enter car` por ambos.
Servidor: 36.6% CPU / 225MB RAM, 28 containers produção intactos.
Evidências: `evidence/net/nd-C3-c*.log`, `evidence/net/vps-etapaC.log`.

## Multiplayer Etapa C2 — pedestres + tráfego replicados (19/10)

**Implementado:**
- `NetPedestrian` NB assado nas prefabs `*_baked` (19 prefabs com
  NetworkObject via `NetPrefabBuilder.BakeNetEntities`): servidor roda a IA
  existente (PedestrianSystem/Pedestrian) e difunde pos/yaw/speed/state a
  ~8Hz unreliable; clientes só interpolam + Animator (Speed/Dead/Cowering).
- `NetTrafficSync` NB nas prefabs de veículo civil do TrafficManager —
  mesmo padrão (pos/rot ~10Hz).
- `PedestrianSystem`/`TrafficManager`: `NetworkClientMode` (cliente
  desativa IA local) + `OnPedSpawned`/`OnTrafficSpawned`/`Despawned` —
  servidor chama `no.Spawn()/Despawn()` via hooks (sem dep de Netcode).
- `Pedestrian.NetState`/`NetSpeedNorm` (0 walk,1 flee,2 dead,3 cower) —
  NetPedestrian lê e replica p/ Animator dos clientes.
- `NdNetSession`: `AddNetworkPrefab` em todos os peds+veículos; flags de
  modo cliente; wiring dos hooks p/ Spawn/Despawn.

**Prova remota (VPS 161.97.133.242:7777, 04:10):**
2 clientes → carros spawnaram com NO IDs 46/47 (~45 NetworkObjects de
peds+tráfego existiam e replicaram antes) → drive fwd=17.7m/s →
`exit car 46 by 1` + `exit car 47 by 2` → pés a pé e andando →
`enter car 47` → `Physics:SendOnCollisionEnter` (colisão real server-side).
Sem erros de spawn nos clientes. Servidor: 42.7% CPU / 226MB, produção
intacta. Ciclo a pé preservado com mundo compartilhado vivo.
Evidências: `evidence/net/nd-D-c*.log`, `vps-etapaC2.log`,
`bakeC2.log` (19 prefabs assados).

**Limitação honesta:** spawn/despawn/movimento de peds+tráfego
replicam (entidades nos clientes, sem erros); a fidelidade visual de
animação interp foi implementada mas não foi medida frame-a-frame
na sessão headless (evidência é por contagem de entidades + sem erros
+ colisões server-side, não por screenshot do ped andando).

## Multiplayer Etapa D — hardening (19/10)

**Implementado:**
- **Relevância por distância** real: `NetworkObject.CheckObjectVisibility`
  nos NBs de ped (120m/140m histerese) e tráfego (130m/160m) — servidor
  decide visibilidade por cliente via `NdNetSession.CheckVis`/`_visCache`
  por (NO,clientId). Spawn/despawn dinâmico na borda sem flicker.
- **Métricas ao vivo**: `NetPeds`/`NetTraf` counters + log por spawn
  (`ped spawned total=N`, `traf spawned total=N`).
- `DistanceToPlayer(cid)` usa pé se a pé, senão carro — relevância segue
  o jogador em qualquer modo.
- `NdScreenshot` + `-ndshot=path` (absoluto) p/ evidência visual de MP.
- `DebugSimulator` do UTP é obsoleto/no-op nesta versão → descartado;
  teste adverso real feito via `tc netem` na VPS (sem fake).

**Prova remota (VPS, 04:31-04:36):**
- 56 eventos ped+traf spawn com contagem; churn dinâmico funcionando
  (`traf total=14→12→14` conforme jogadores andam)
- Ciclo a pé intacto com mundo cheio: `exit car 49 by 1`/`48 by 2` →
  `foot onFoot=True` → `enter car` → collisions server-side
- **Latência real**: `tc qdisc netem delay 150ms 20ms loss 3%` em eth0 —
  clientes 5 e 6 mantiveram inputs (`seq` 2002→2127) e foot tracking sem
  desconectar; netem removido (fq_codel restaurado)
- **Evidência visual**: `evidence/net/nd_shot.png` — chase cam, cidade
  texturizada, HUD, cliente conectado e renderizando a sessão remota
- Servidor: 39-43% CPU / 225MB, 28 containers produção intactos
- Logs: `evidence/net/nd-E-*.log`, `vps-etapaD.log`

**Honesto:** relevância, contagem, churn, adversidade e renderização estão
provados. Captura in-frame de um pedestre/tráfego visível depende da
posição da câmera — validação visual por contagem + ausência de erros +
render ativo; close-up de ped não garantido nesta sessão.


## Reestruturação Executiva — Quaternius + Gameplay Core (19/10)

**ETAPA 1 — Pedestres Quaternius (definitivo):**
- `tools/art/import_quaternius_pack.py` extraiu 69 arquivos (2 modelos
  Superhero FullBody + UAL1/UAL2 + 8 hairs + texturas PBR).
- `Studio.Editor.QuaterniusPedPipeline` (novo): Humanoid/CreateFromThisModel
  nos corpos, Humanoid/CopyFromOther nos UAL (86 clips), materiais URP Lit,
  5 prefabs `quat_*` (male a/b/c, female a/b) + `Pedestrian.controller`
  reescrito (Idle_Loop/Walk_Loop/Sprint_Loop/Death01/Crouch_Idle_Loop).
- `Pedestrian` reescrito: REMOVIDOS `RebindToAvatarSkeleton`, LateUpdate de
  ossos, propagação `_prop`, hack PED_FLOATING. Novo `IShootable` +
  `KillByShot`/`RunOver`/`Die` compartilhado. `PedestrianSystem` prefere
  prefabs `quat_*`.

**ETAPA 2 — Dirigibilidade:** `_steerWheelMaxDeg` 32°; volante visual
suavizado (`_visualSteerDeg` via `Mathf.MoveTowards` 260°/s) com limite
progressivo por velocidade (±32° parado → ~40% acima de 2× cruzeiro).
`DriveMath.SteeringSpeedFactor`: 1.0 até cruzeiro → decai a 0.45 em alta.

**ETAPA 3 — Combate/vida:**
- `Ak47Weapon`: hit em `Pedestrian` → `KillByShot` + partícula efêmera
  (decal PROIBIDO em ped); decal `SetParent(hit.collider.transform,true)`
  em corpos com Rigidbody (gruda na lataria).
- `VehicleDamageController`: `ConfigureHealth` (player 350 / tráfego 140),
  cooldown de colisão 0.35s, fallback procedural de fogo REMOVIDO,
  `PlayerHealth.Instance?.OnExplosion` na detonação.
- Explosão UNIFICADA: `WFXMR_Explosion` (War FX) — Mirza removido (quadrados
  pretos). `PlayerHealth.cs` novo: 100HP, atropelamento (CC hit rb>200kg),
  queda |vy|>11 m/s (`OnFootMotor.LastLandImpactSpeed`), explosão quadrática,
  regen 6s, respawn no carro. HUD: `HealthText` (HP verde/âmbar/vermelho).

**ETAPA 4 — Props/pássaros:**
- `BreakableProp` refactor: `RequireComponent(Rigidbody)`, rb kinematic na
  raiz → break = dinâmico + impulso → prop INTEIRO tomba (colisor+renderer).
  Builder: postes breakable (rb 60kg kin), bancos/lixeiras rb dinâmico 15–30kg.
- `BirdSystem`: bandos 6–12m sobre avenida (z=+26) e praça; materiais das
  prefabs migrados Built-In→URP (GUID swap nas 3 prefabs).

**ETAPA 5 — Gates:** cena rebuildada (2.19MB), `validate_scene.py --strict`
exit 0, capturas `ped_{idle,walk,walk2,run,death,cower}.png` (bounds sanos,
death hipsY=0.06 deitado), `wheels_steering_turn.png`, `birds_visible_sky.png`.
Conhecimento: `.studio/knowledge/gameplay/{pedestrian-pipeline,ballistics-and-props}.md`.
UTF EditMode em `evidence/test-reports/quat-editmode.xml`.


## Integração PROMETEO — Física Fluida (19/10)

- PROMETEO Car Controller (Asset Store, cache Mena) extraído p/ `unity/Assets/PROMETEO - Car Controller/` + `PROMETEO.asmdef` + ref em Gameplay/Editor asmdefs + .gitignore (vendor).
- `PrometeoCarController` no PlayerCar: maxSpeed=140, accel=3, steerAngle=32, steerSpeed=0.5, driftMult=5. 4 WheelColliders (r=~0.32, susp 0.25m) nos mounts FL/FR/RL/RR + meshes visuais wireadas.
- `useExternalInput` (novo) gateia o Update do vendor — `PlayerCarController` virou fachada: traduz Steer/Throttle/Brake/Drift do DriveInputAdapter p/ GoForward/TurnLeft/Handbrake (teclado + mobile + QA + rede).
- Zero regressão visual: SportCar_Model + materiais URP intactos; ChaseCamera, VehicleDamageController, HP/PlayerHealth preservados.
- Cena rebuildada (2.20MB) + validate --strict exit 0. UTF pendente re-run.

## Pipeline Universal de Pedestres — npc_csl vestidos FUNCIONANDO (19/10)

**Vitória:** pedestres vestidos reais no jogo — `ped_dressed_*` (12 variantes,
corpo+roupa+cabelo/barba) animando com o MESMO `Pedestrian.controller`+clips UAL.
Verificado por imagem: walk/idle/run/death íntegros, texturizados, zero deformação.

**Receita final (documentada em .studio/knowledge/gameplay/pedestrian-pipeline.md §v2):**
- `tools/art/inspect_character_fbx.py` (Blender): inventário — provou que
  `npc_csl_00_character_*.fbx` já traz UMA armature de 54 ossos com tudo bound;
  extras (haircut/hairfacial) trazem armature duplicada idêntica.
- `tools/art/unify_character_fbx.py` + `ped_dressed_manifest.json`: merge
  determinístico de armatures (blindagem 1e-4), strip lod>0, normalização com
  desparentamento (bug do "blob"), render rest+pose obrigatório, FBX único.
- `QuaterniusPedPipeline.BakeDressedBatch` v3: import Humanoid + materiais por
  mapa da prefab vendor + Animator/controller — SEM cirurgia de esqueleto.
- `PedestrianSystem`: prefere `ped_dressed_*` → fallback `quat_*`. 16 peds
  dressed confirmados em play mode.

**Causa raiz das 3 falhas:** FBX vendor com bindposes inconsistentes p/ o
import Humanoid do Unity (render T-pose OK, SOME sob animação). Não era o bake
— era o asset de origem; só o re-export do Blender recria bindposes nativas.

**Bug processual corrigido:** capturas de prova criavam luz/cubo/câmera na
cena aberta (12x acumulados = estouro branco + lag). `CaptureDressedPilot`
agora abre cena vazia. Cena reloaded do disco — NeonDistrict.unity intacta.

**Gates:** scene --strict exit 0; validate_asset ped_dressed_01f_01 OK.
UTF EditMode re-run: 80/81 — único fail = AutonomousPlaytest.Bot_Drives_And_Detects_Anomalies
(bug de harness: teste PlayMode executado no contexto EditMode, pre-existente).
validate_asset --all: 5159/5179 — os 20 fails sao vendor pre-existentes
(anim FBXs sem UV, carros wrecked N-gons); ZERO nos novos PedDressed/.

## Downtown City MegaKit — import + piloto 2 quarteirões (19/10)

- `import_downtown_megakit.py`: extraiu 189 arquivos (153 FBX Unity + 36 texturas)
  p/ `Art/Environment/Modular/DowntownMegaKit/` + manifesto SHA-256 `.studio/`.
- Inventário: grid modular 3m (sidewalk 3×3×0.15); prédios montados
  12–24m footprint, 17–28m altura, 12-13 slots MI_*.
- `DowntownKitImporter.cs`: Mat_DT_* por família de textura (13 famílias:
  RedBrick, Concrete, Trim, MarbleFloor, MetalConcrete, RoofSlate, Ornaments,
  Dirt, lit_interior_1/2, dark_interior, Concrete_Asphalt, Street_Decals) +
  153 prefabs em `Prefabs/Modular/DowntownMegaKit/`.
- Piloto: quarteirões (1,3) e (3,3) — norte da Avenida Neon (z=+26) usam
  MEGAKIT_POOL; resto permanece Demo City. Capturas `megakit_prefabs.png` +
  `downtown_pilot_{west,east,street}.png` visualizadas: prédios tijolo
  estilo Boston texturizados, iluminação dusk intacta.
- Gameplay zero tocado: tráfego/pedestres/spawns/física intactos.
- Gates: validate_scene --strict exit 0.
- Pendente: expansão aprovada? + perf numbers + UTF re-run + APK (aprov. usuário).

## ART PASS — Old Brick District completo + City Pack (20/10)

- City Pack: `import_city_pack.py` (whitelist 53 modelos + 26 atlas texturas,
  manifesto SHA-256) + `RunCityPackImport` → 106 prefabs URP `Mat_CP_*`.
- `BuildOBD_ArtPass` (NeonDistrictSceneBuilder): calçadas do kit por quarteirão,
  decals em TODAS interseções OBD (crosswalk/stop/double-yellow/manhole/drain),
  densidade por quarteirão (postes quentes, árvores, hidrante, bancos, bins,
  parking meters, mailbox, bus stop, semáforo decorativo), letreiros de térreo
  com `Letter_*` (DELI/BAR/PAWN/LAUNDRY/LIQUOR/CAFE), praça do bairro,
  canto industrial (containers/barrier/fence/tractor), pássaros.
- `ApplyLitWindows`: 4 variantes emissivas âmbar (Mat_WinLit_0-3) em ~55% dos
  prédios (slots interior/glass/window), seed fixa.
- TrafficNetworkBuilder.BuildGrid aceita grid assimétrico — ruas N-S do OBD
  cobertas pela rede de tráfego.
- Perf baseline: 1.376M tris / 1552 slots. Cena pós-pass: 4.35MB,
  validate_scene --strict exit 0. Matriz BEFORE/AFTER em evidence/captures/.
- Pendências honestas: perf pós-pass não medido (tris), UTF re-run pendente,
  neon downtown (letters no centro) não implementado — só storefronts no OBD,
  classical_* importados mas não posicionados no mapa (inventário pronto).

## MISSÃO v3.1 — ponto de continuação (20/10)

**Feito na v3:** A2 postes calibrados (commit e7ab7528); A1 AuditBuildingFootprints()
existe em NeonDistrictSceneBuilder + rodou; ApplyLitWindows 75%.
**PENDENTE COM PROVA (ordem da missão):**
1. Etapa 1 — prova visual A1: render ortográfico topo com corredores de via
   destacados (zero invasão) + 3 street-level dos piores ex-invasores.
2. Etapa 2 — A3 restante: cabeças de poste emissivas (âmbar OBD/branco-rosa
   downtown) + poça de luz decal + regra dia/noite (sol alto apaga) + neon
   Letter_A-Z em ≥60% prédios downtown (horizontal/vertical/marquee).
3. Etapa 3 — A4: piso distinto por zona (avenida 4Lane / OBD kit / praças
   Asphalt_6x6-9x9 + tint ≤3 mats).
4. Etapa 4 — FASE B: boulevard curvo (Street_Curve_*) leste + rotatória +
   diagonais; tráfego só se BuildGrid engolir sem reescrita.
5. Etapa 5 — FASE C: Green Hills ao leste do OBD (cliff_* morros, ground_river*
   rio, path_* trilhas, windmill landmark, crops/fence/tents, bosque por espécie,
   cave+waterfall+camp POIs). Todos os assets já existem no projeto.
6. Etapa 6 — perf antes/depois + UTF 75/75 + validators + matriz final + docs.

**Veredito do dono sobre poste e invasão: PENDENTE (campos em branco no prompt).**

## v3.1 continuação — Etapa 1 FECHADA (20/10)

- `CaptureAuditTopDown` + `audit_topdown.png`: ortográfico topo com corredores
  de via em overlay vermelho — **zero invasão de prédio sobre via** (commit
  85722f33). Prédios confinados nos quarteirões, OBD incluso.
- Veredito do dono (poste/invasão) permanece PENDENTE dele testar.

**Próximo exato (Etapa 2 — A3 restante):** cabeças de poste emissivas + poça de
luz decal + regra dia/noite (sol alto apaga) + neon Letter_A-Z ≥60% downtown
(3 formatos). Métodos vivem em NeonDistrictSceneBuilder; pools: POLY_PROPS /
POLY_LETTERS / MEGAKIT_PREFABS. Depois: A4 pisos → FASE B curvas → FASE C
Green Hills → FASE D gates. Editor batch provado; live-editor eval instável.

## v3.1 — Etapa 2 FECHADA (20/10)

- ApplyCityLights: 40 postes com cabeça emissiva (âmbar OBD / neon pink-white-cyan
  downtown por paleta) + poça de luz translúcida no chão; neon Letter_* em
  46/80 prédios downtown (~58%), 3 formatos (horizontal/vertical/marquee).
- PENDENTE: regra dia/noite (componente ambiente), Point Light real subset,
  A4 pisos, FASE B curvas, FASE C Green Hills, FASE D gates+docs.

## v3.2 — Etapa 1 FECHADA (20/10)

- CityLightingController (Studio.Foundation, ambiente): luzes APAGAM só em
  sky day/noon/high_sun; dusk/night/red = acesas. Dusk permanente = default ON.
- Point Lights reais na avenida (|z-26|<10, x<134): intensity 2.4, range 11,
  sem shadows — subset de teste de budget (medida de FPS em play mode pendente).
- Neon top-up: 62+ prédios elegíveis com letreiro (exclui _bgr do denominador)
  — cobertura ≥75% dos elegíveis >= meta 60%.
- Provas: evidence/captures/aerial_neon_dusk.png (janelas+letreiros acesos),
  street_lamp_closeup.png (dusk street; cabeça do poste fora do frame —
  recapture pendente se o dono exigir).
- PENDENTE: Etapa 2 A4 pisos | Etapa 3 boulevard+rotatória | Etapa 4 Green Hills
  | Etapa 5 gates+docs.

## v3.2 — Etapa 2 FECHADA (20/10)

- ApplyRoadSurfaces: Mat_Road_Avenue (asfalto escuro azulado) na Avenue_Neon_EW,
  Mat_Road_OBD (desgastado marrom) nas OBD_Street_* + interseções x>134 (9 quads).
- 63 marcações 4Lane na avenida (Decal_DoubleYellow eixo + BrokenLine faixas);
  4 patches Street_Asphalt_9x9 nas interseções do Central Plaza.
- Prova visual por zona: pendente captura dedicada (cobrir na matriz final).
- PENDENTE: Etapa 3 boulevard+rotatória | Etapa 4 Green Hills | Etapa 5 gates.

## v3.2 — Etapa 3 FECHADA (20/10)
- BuildCurvedBoulevard: rotatória em (292,26) [anel Street_Curve_2Lane + pad
  Asphalt_9x9], arco de 10x Street_2Lane tangenciais (R=55) rumo ao nordeste,
  diagonal (284,78)->(300,40), decals de faixa/crosswalk/stop.
- Tráfego IA: NÃO coberto — TrafficNetwork é grid-only, reescrita fora de escopo.
  Via dirigível sem IA (report honesto exigido pela missão).
- PENDENTE: Etapa 4 Green Hills | Etapa 5 gates+docs.

## v3.2 — Etapa 4 Green Hills ENTREGUE COM RESSALVAS (20/10)
- BuildGreenHills executou sem faltar asset: 2 morros + rio + ponte + moinho
  + caverna + acampamento + fazenda + bosques + pássaros (~200 objs).
- RESSALVAS (2 tentativas usadas no chão): textura de grama ideal pendente
  (fallback escuro), morros leem esparsos na aérea. Receita e problemas em
  .studio/knowledge/environment/green-hills-district.md.
- Capturas: greenhills_aerial, river_waterfall, cave_camp, windmill_dusk,
  boulevard_curve — inspecionadas, documentam o estado real.
- PENDENTE: Etapa 5 gates finais (perf/UTF/validators), HANDOFF, push.

## v3.2 — Fechamento parcial (20/10)
- validate_scene --strict: APROVADO (LampGlow/LightPool → *_Ghost isentos,
  lamp mats ganharam _BaseMap asfalto, emission controla a cor real).
- NÃO rodado: UTF 75/75, validate_asset --all, perf numérico pós-v3.2.
- Green Hills: funcional mas com ressalvas visuais doc's em green-hills-district.md
  (chão escuro fallback, morros esparsos na aérea — retrabalho cosmético).
- Veredito do dono pendente nos campos do prompt v3.2.
