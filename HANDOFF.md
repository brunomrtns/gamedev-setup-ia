# HANDOFF — Sessão de Refatoração Arquitetural V2

> **Instrução para o agente que retomar:** leia este arquivo inteiro antes de qualquer ação.
> Ele contém o estado completo da sessão, o ambiente, as regras e os próximos passos.
> Quando terminar uma etapa, ATUALIZE este arquivo (mova itens de "Próximos Passos" para "Concluído").

## Objetivo

Executar a "REFATORAÇÃO ARQUITETURAL V2" do projeto (`Asset Pipeline`, `Texturas`,
`Eixos & Ecossistema Open Source`) em 5 etapas, **sem pedir ações manuais ao usuário**,
até validação final com evidências. Briefing original:

- Pipeline Blender headless canônico.
- Download/catalogação de kits CC0.
- Validadores determinísticos endurecidos.
- Atualização de governança.
- Reprocessar assets, reconstruir cenas, validar, testar UTF, capturar evidências e
  atualizar `PROGRESS.md`.

Regras de aceite do briefing:

- Falha `exit 1` se `_BaseMap` nulo ou cor chapada monocromática.
- Proibido primitivas nativas (Cube/Sphere/Cylinder/Capsule/Plane/Quad) visíveis nas cenas.
- Proibido rotação descompensada em X/Z.
- Texturas 1024 (principais) + 512 (variantes).
- 33/33 testes UTF devem continuar passando.
- Atualizar `PROGRESS.md` ao final, detalhadamente.

## Ambiente (OBRIGATÓRIO — ambiente dual)

- **Windows nativo:** Unity (`unity/`), builds e testes.
- **WSL2 Ubuntu:** Blender 5.0.1 (`/usr/bin/blender`) e venv
  `/home/bruno/.venvs/game-ai3d` (PIL 12.3.0, numpy 2.4.6, requests 2.34.2, trimesh 5.1.1).
- Invocação: `wsl blender -b -P <script> -- <args>` e
  `wsl /home/bruno/.venvs/game-ai3d/bin/python <script>`.
- Caminho `C:\foo` → `/mnt/c/foo` no WSL.
- Python Windows 3.13 **NÃO** tem PIL/numpy/requests/trimesh — não rodar scripts de
  arte no Python nativo.

## Peculiaridades conhecidas (não redescobrir)

- Blender 5.0.1: importador FBX legacy precisa de monkeypatch
  (`files` é runtime-only; `fbx_canonical.ensure_fbx_import()` já faz isso).
- `bpy.ops.object.origin_set(type='ORIGIN_CURSOR')` sem `mode`.
- Exportador FBX **nativo** do Blender só aceita `filepath`, `axis_forward`, `axis_up`
  (bake_space_transform/unit scale inerentes; exporta TODA a cena).
- FBX ASCII (Kenney, 153 arquivos do SpaceKit) não importa no Blender — mantidos
  vendor-canônicos e auditados por parser ASCII.
- `AbyssalBreachSceneBuilder.cs` extrai malhas do FBX por nome e posiciona com offsets
  locais — compensar deltas de pivo das armas ao reconstruir.
- `capture_scene.py` **ignora `--scene`** (bug: não passa `-scenePath=` que
  `StudioAutomation` já suporta) — corrigir na ETAPA 5.
- GUID canônico do shader URP Lit: `933532a4fcc9baf4fa0491de14d08ed7`.
- Malhas built-in Unity: fileID `10202` Cube, `10206` Cylinder, `10207` Sphere,
  `10208` Capsule, `10209` Plane, `10210` Quad, guid `0000000000000000e000000000000000`.
- Kenney: link ZIP extraído do anchor `#donate-text` da página
  `https://kenney.nl/assets/<slug>`.
- Poly Haven API: `https://api.polyhaven.com/files/<id>` → `{mapa: {res: {jpg: {url}}}}`;
  User-Agent obrigatório.
- Isenções dos validadores: materiais prefixo `Mat_UI_/Mat_FX_/Mat_Skybox_/Mat_Debug_/Mat_Lightcone_`;
  GO com nome contendo trigger/collider/invisible/ghost/bounds/culling/gizmo/system/sensor/blocker.
- `robot_character.fbx` (8 MB, rigado) **NÃO** passar pelo canonical_finalize (perderia o rig);
  `RobotAnimator` anima transforms proceduralmente — não substituir por FBX skinned.

## Estado do projeto

### Concluído

**Ambiente e governança**
- Auditoria do projeto + ambiente WSL confirmado (sonda `probe_wsl.sh`).
- `AGENTS.md`: novo §3 Dual Environment, §4 comandos WSL, renumerado DoD §5,
  Quality Gate V2 como princípio 7, comandos `download_modular_kits.py`/`normalize_fbx.py`.
- `CLAUDE.md`: bullet WSL + comandos. `README.md`: pré-requisitos WSL2.
- `docs/operations/environment-audit.md`: linha WSL corrigida + adendo V2 com inventário.
- Quality Gate V2 em AGENTS.md/CLAUDE.md; nova seção 8 em
  `docs/standards/asset-quality.md` (proibições rotação descompensada/sem UV0/n-gons,
  comandos pipeline, políticas de pivô).

**ETAPA 1 — Pipeline canônico**
- Criados `tools/art/fbx_canonical.py` (transform_apply, triangulate, shade_smooth,
  ensure_uv/smart_project 66°/0.02, orient_weapon PCA+muzzle, pivots character/weapon/prop/kit,
  export FBX nativo axis_forward='-Z'/axis_up='Y', `ensure_fbx_import()` monkeypatch,
  purga de não-mesh) e `tools/art/normalize_fbx.py` (CLI).
- `generate_abyssal_meshes.py` delega ao canônico (`ASSET_PRESETS`, hints `-Y`/`+Z`).
- Testado: drone identity ✓, rifle grip `(0, 0.0336, -0.158)` 1.35 m ✓, round-trip identidade ✓.

**ETAPA 2 — Kits CC0**
- Criados `tools/art/download_modular_kits.py` (delegação WSL automática, scraping Kenney,
  Poly Haven API, MS map URP, variantes 512, manifesto SHA-256, catálogo idempotente) e
  `tools/art/batch_normalize.py`.
- Executado: **361 modelos FBX** baixados, **208 normalizados (0 falhas)**, 7 famílias PBR
  (1024+512).
- Criado `unity/Assets/Editor/ModularKitImporter.cs`; executado no Unity batchmode →
  **exit 0: 7 materiais PBR + 361 prefabs, 0 erros**
  (`evidence/art/modular-import-report.json`, `evidence/logs/modular-import-report.json`/`modular-import.log`).
- Kits: SpaceKit (153 FBX ASCII), CityKitCommercial (41), FantasyTownKit (167);
  7 famílias Poly Haven (MetalPlate, MetalProfile, MetalRust, ConcretePanel, ConcreteWorn,
  Asphalt, RockAbyssal).

**ETAPA 3 — Validadores**
- Criados `tools/validation/studio_validators.py` (índice GUID `meta.with_suffix("")`,
  `is_builtin_mesh_guid`, check_material/check_prefab/check_scene, parser ASCII FBX,
  `inspect_binary_fbxs` Blender em lote) e `tools/art/inspect_fbx.py`.
- Reescritos `validate_asset.py` (`--asset`/`--all`, relatório
  `evidence/test-reports/asset-validation-v2.json`) e `validate_scene.py` (`--scene`/`--strict`).
- Bugs corrigidos: guid index apontava pasta; primitivas com guid mágico `e000...`;
  limiar UV `0.01`→`1e-5` (paleta Kenney); artefato `sub_warnings if False`.
- Baseline: NeonRush **105 issues**, AbyssalBreach **43 issues**.

**ETAPA 5 (iniciada)**
- 4 texturas >10 MB reduzidas a 1024: `T_Abyssal_Rock_N`, `T_Abyssal_Seabed_N`,
  `T_Coral_01_N`, `T_Neon_Track_N` (~2.4–2.7 MB).
- Purga de não-mesh adicionada ao `canonical_finalize` em `fbx_canonical.py`.
- FBX renormalizados com sucesso (todos com REPORT_JSON ok):
  - `BarramundiFish.fbx` (prop): identity ✓, UV ✓.
  - `SM_SciFi_Repeater_Rifle.fbx` (weapon): identity ✓, UV ✓ (PCA ambíguo — front/rear
    spread quase iguais; aceito para catálogo, ver risco abaixo).
  - `SM_Sentinel_Drone.fbx` (prop): identity ✓, UV ✓.
  - `SM_Assault_Rifle_B.fbx` (weapon, `--forward -Y --up +Z`): grip `(0, 0.033, -0.1557)`,
    dims 0.062×1.35×0.4832 ✓.
  - `SM_Tactical_Knife.fbx` (weapon, `--forward -Y --up +Z`): grip `(0, 0.0243, -0.0176)`,
    dims 0.0311×0.5505×0.088 ✓.
  - `fountain-center.fbx` (`--force-uv`): UV ainda `area 0.0`, 4 triângulos, quad flat
    degenerado → **removido do catálogo** (model + prefab apagados; 360 prefabs restantes).
- Catálogo de peças mapeado: `building-a..n`, `building-skyscraper-a..e`,
  `low-detail-building-*`, `wall-*`, `stairs*`, `pillar-*`, `corridor_*`, `pipe_*`,
  `rocket_*`, `machine_*`, `barrel*`, `hangar_*`, `monorail_*`, `craft_*`, `rock*`,
  `crater*`, `fence*`, `detail-*`, `gate_*`.
- **Revalidação `--all` (item 1)**: primeira execução reprovou 12/1850 —
  `fountain-center.fbx`/prefab ainda existiam (agora de fato removidos do disco,
  sem referências em cenas) e 11 materiais com `_BaseMap` nulo. Correção: `_BaseMap`
  atribuído com albedo Poly Haven 1024 preservando tint/emissão —
  `Mat_Lane_Cyan/Magenta`/`Mat_RobotVisor`/`Mat_OxygenTank`→MetalProfile,
  `Mat_WindowStrip`→ConcretePanel, demais (`Mat_EnergyCell`, `Mat_RobotBody`,
  `Mat_RobotThruster`, `Mat_DataCore`, `Mat_FPS_PlasmaGlow`, `Mat_SubDrone_Eye`)
  →MetalPlate. Resultado final: **1848/1848 aprovados, exit 0**
  (warnings apenas: PIL ausente no Windows + texturas >4MB).
- **Refatoração dos builders (item 2)**: novo `unity/Assets/Editor/ProceduralMeshLibrary.cs`
  — malhas procedurais com UV0/normais persistidas como `.asset` em
  `Assets/Game/Art/Meshes/` (`SM_Proc_Box/Cylinder/Sphere/Capsule/Octahedron/Torus/Plane`,
  dimensões equivalentes às primitivas nativas), `CreatePart`, `CreateKitInstance`
  (PrefabUtility + preserva correção -90X do prefab), `ScaleToHeight`/`ScaleToFit`
  por bounds. Windings verificados numericamente (esfera/toro/cápsula/octaedro/box
  corrigidos). Zero `GameObject.CreatePrimitive` no projeto:
  - NeonRush: pista/listras/guard-rails → `SM_Proc_Box`; edifícios → pool
    determinístico de prefabs CityKit (`BUILDING_POOL`, altura por bounds) + faixa
    de janela neon; arcos → `gate_complex`/`gate_simple` (SpaceKit) + beam neon;
    laser/viga/bloco → malhas próprias com colliders preservados; células →
    `SM_Proc_Octahedron`; portal → `SM_Proc_Torus` + disco fino; fallback do player
    → malhas próprias.
  - AbyssalBreach: seabed/canopy → `SM_Proc_Box`; paredões → pool de rochas do kit
    (`ROCK_POOL`, ScaleToFit, matRockWall); `SpawnModelOrFallback` → fallback via
    `ForPrimitive`; coletáveis → malhas próprias + colliders equivalentes; olho do
    drone → esfera própria; anel do pod → toro.
  - StudioAutomation (Calibration): primitivas substituídas + material
    `Mat_PBR_ConcretePanel` + warm-up de shaders em `CaptureScreenshotBatch`
    (3 frames dummy — elimina frame magenta em batchmode).
- **`capture_scene.py` corrigido (item 3)**: `-scenePath=` passado ao Unity
  (antes ignorado).
- **Cenas reconstruídas (item 4)**: batchmode `RunBuildSceneBatch` NeonRush +
  AbyssalBreach + `RunSceneCreationBatch` Calibration — logs em
  `evidence/logs/*-rebuild-v2.log`.
- **Validação de cenas (item 5)**: NeonRush exit 0, AbyssalBreach `--strict`
  exit 0 (zero warnings), Calibration exit 0.
- **UTF (item 6)**: `tools/qa/run_unity_tests.py EditMode editmode-results-v2` →
  **33/33 passed**, rc=0 (`evidence/test-reports/editmode-results-v2.xml`).
- **Capturas (item 7)**: `v2_neonrush.png` + `v2_abyssalbreach.png` via
  capture_scene.py + 12 capturas multi-ângulo regeneradas (`neon_rush_01..06`,
  `abyssal_breach_01..06`). Inspeção visual: prédios CityKit com janelas,
  portais gate do SpaceKit, toro do portal, octaedros, rochas da fenda, HUD —
  paridade visual confirmada vs. capturas anteriores.

### Conhecido / risco aceito

- `SM_SciFi_Repeater_Rifle.fbx` (vault): PCA de orientação sem sinal claro
  (front_spread 0.0958 ≈ rear_spread 0.0957) — facing pode estar invertido; o asset não é
  usado nas cenas, só precisa passar na validação.
- `robot_character.fbx` fica fora do pipeline canônico (rig).

## Próximos passos (ETAPA 5 restante)

**REFATORAÇÃO V2 COMPLETA (ETAPAs 1–6 do briefing master)** — ver "ETAPA 5" em
Concluído. Resumo: assets 1848/1848, zero CreatePrimitive, cenas validadas
inclusive `--strict`, 33/33 UTF, 14 capturas PNG.

Adendo final (ETAPA 1 do briefing — IA 3D local): `tools/art/auto_rig.py`
(armadura humanoide + skinning robusto + ações Idle/RunCycle/Jump/Slide + FBX
Mecanim) e `tools/art/character_pipeline.py` (TripoSR→normalização→texturas→rig→
quality gate) **já existiam** de trabalho anterior. Gap fechado nesta sessão:
novo módulo canônico `tools/art/ai_generator/generate_model.py` — front-end
genérico imagem/prompt→3D (TripoSR via WSL/CUDA, bake de textura 512–2048,
saída .obj/.glb via trimesh, `--normalize` encadeia normalize_mesh.py), com
`__init__.py` do pacote. Comandos documentados em AGENTS.md §4.

**Expansão do catálogo (para futuros briefings — corrida/aventura)**: adicionados
`racing-kit` (pistas/props de corrida), `car-kit` (veículos) e `nature-kit`
(vegetação) ao `KENNEY_KITS` em `download_modular_kits.py` + `MODEL_DENYLIST`
(exclui `fountain-center.fbx` degenerado em todo re-download). Executado:
**852 prefabs gerados, 0 erros** (`evidence/logs/modular-import-v2b.log` via
`ModularKitImporter.RunImportBatch` — o método canônico; não é `RunBatch`).
Nova seção **"Receita Canônica — Novo Jogo do Zero (sessão fria)"** em
AGENTS.md §4: fluxo briefing→GDD→builder→gates→capturas→APK para que uma
sessão sem contexto prévio entregue um jogo novo de forma autônoma.

**Assets pagos/grátis importados do cache da Asset Store** (`-importPackage`
batchmode, 0 erros): `Assets/Versatile Studio Assets` (Demo City — 38 FBX +
51 prefabs, cidade mobile-friendly), `Assets/SportCar` (Sport Car Free), 
`Assets/Awbmecreations` (Low Poly Cars). Logs: `evidence/logs/import-*.log`.
Nota: materiais desses packs podem ser Built-in RP — se a validação de cena
acusar shader incompatível, reatribuir para URP Lit (`933532a4fcc9baf4fa0491de14d08ed7`).
Importados depois: **FPS AKM** (ChamferZone) e **Fantasy Forest Free Demo**
(TriForge). Novo: **`tools/assetstore/import_new_packages.py`** — varre o cache
da Asset Store e importa só pacotes novos/alterados (estado em
`.studio/imported_packages.json`, idempotente; `--all` reimporta, `--dry-run`
lista). Uso: `python tools/assetstore/import_new_packages.py`.

Opcional (não bloqueante): revalidar após futuras mudanças com os comandos
canônicos abaixo.

## Comandos úteis (atuais)

```powershell
# Validar todos os assets (Windows)
cd C:\Users\Bruno\workspace\gamedev-setup-ia
python tools\validation\validate_asset.py --all

# Renormalizar um FBX (WSL/Blender)
wsl blender -b -P /mnt/c/Users/Bruno/workspace/gamedev-setup-ia/tools/art/normalize_fbx.py -- \
  --input "<path/fbx>" --output "<path/fbx>" --preset weapon --forward -Y --up +Z

# Validar cenas
python tools\validation\validate_scene.py --scene NeonRush
python tools\validation\validate_scene.py --scene AbyssalBreach --strict
```

## Como esta sessão foi retomável (para o usuário)

- O estado não vive na conversa: vive em arquivos do repo (`HANDOFF.md`, `PROGRESS.md`,
  relatórios em `evidence/`). Outro agente (Devin, Claude Code etc.) só precisa de acesso
  ao diretório do projeto e da instrução: "leia HANDOFF.md e continue os Próximos Passos".

---

## Adendo — NEON DISTRICT: FREE RIDE (sessão Devin, 2026-10-16)

**Estado:** CONCLUÍDO — todos os gates verdes. Detalhes completos em `PROGRESS.md` § "Game 3".

- Cena `NeonDistrict.unity` construída por `NeonDistrictSceneBuilder.RunBuildSceneBatch`
  (distrito 6×6 vias/25 quarteirões, avenida 16m, praça, ~90 prédios Demo City,
  rede de tráfego dirigida de 416 arestas com mão direita, ~14 carros AWBM).
- Conversão URP vendor in-loco: `ConvertVendorMaterialsToUrp()` (Demo City + SportCar).
- Gates: scene `--strict` exit 0 · assets `--all` 5104/5104 · UTF **55/55** ·
  7 capturas `neon_district_*.png` · APK `builds/neon_district.apk` (150 MB) ·
  Windows `builds/NeonDistrict_Windows/NeonDistrict.exe`.
- Ferramentas novas: `BuildWindows` aceita heurística de produto por nome
  (Abyssal/NeonDistrict/NeonRush); `build_windows.py --scene`; `build_apk.py`
  deriva `-productName` do arquivo; `PlayNeonDistrict()` entra em Play Mode.
- Controles: WASD/setas, Space/Shift=drift, R=recenter; touch via botões HUD.
- Editor GUI está aberto em Play Mode nesta sessão (log `evidence/logs/neon-district-editor.log`).

**Peculiaridades novas:**
- Packs vendor (Demo City, SportCar, AWBM) NÃO são varridos por `validate_asset --all`
  (escopo = Assets/Game/*), mas a cena é — por isso a conversão in-loco é obrigatória.
- `Rigidbody.velocity/drag` obsoletos no Unity 6.3 → usar `linearVelocity/linearDamping`.
- Tráfego: raycast frontal com offset de origem além do próprio colisor (1.9m),
  layer ~0, QueryTriggerInteraction.Ignore.

### Adendo 2 — Polimento Neon District (sessão Devin, 2026-10-16, final)

- **CORRUPÇÃO CRÍTICA pelo import do 3D Game Kit (Asset Store):** sobrescreveu
  `GraphicsSettings.asset` (m_CustomRenderPipeline → GlobalSettings em vez de PC_RPAsset
  = tela toda magenta) e `DynamicsManager.asset` (collision matrix zerada → carro caía
  no limbo). Ambos restaurados ao estado git. **Lição: auditar ProjectSettings após
  qualquer import de unitypackage.** 3DGamekit removido do projeto (reimportável).
- **ColliderFitter.cs** (`Assets/Editor/`): perfil canônico de colisores —
  poste/sinal = haste ≤0.55m, árvore = tronco ≤0.6m, barreiras = AABB, veículo = 80%XZ.
  Regra em docs/standards/unity.md §5. Teto absoluto em METROS (fração de bounds falha
  quando braço/copa domina o bounds). Colisor sempre em root world-aligned.
- **Carro:** colisor criado DEPOIS das rodas (bounds incluem fundo das rodas → base
  assenta nelas); PhysicsMaterial zero-fricção ("travado" = box raspando no chão);
  rodas instanciadas nos mounts FL/FR/RL/RR (~14% do comprimento); lift automático;
  tração por RotateTowards (grip 4.2 / drift 0.55); maxSpeed 33 m/s.
- **Wrap Mode:** EnsureRepeatWrap força Repeat nas texturas asphalt/sideway (Clamp
  causava streaks no chão — era o "chão sem textura").
- **ColliderDebugOverlay.cs**: F9 = wireframe de todos os colisores na Game view.
- Builds: APK `builds/neon_district.apk` (150MB) + EXE `builds/NeonDistrict_Windows/`
  gerados ANTES dessa rodada de polimento — rebuildar quando aprovar o feel final.

## 2026-10-17 — Neon District v2.2: Refatoração de Física & Combate

### Novas classes
- `TrafficVehicle` (rewrite): máquina de estados `CrashState {Routing, PhysicsFree, Wrecked}`.
  Rigidbody cinemático em rota; impacto leve/médio (`ClassifyImpulse`, 4k–20k N·s) libera física
  com `AddForceAtPosition` no contato, assenta e re-roteia via `TrafficNetwork.FindNearestEdge`;
  impacto severo (>=20k N·s) deixa o carro acidentado permanentemente na via.
- `BreakableProp`: quebra por limiar de impulso (`ShouldBreak`, 9k N·s). Toco estático
  (malha comprimida, `StumpScaleY` clamped) + peça solta com Rigidbody e impulso no contato.
  Vive no GO `_BoundsCollider` (ColliderFitter.FitProp agora retorna o GO do colisor) e
  recebe o visual via `SetVisual`.
- `Ak47Weapon.ComputeMuzzleDir` + `UpdateAimConvergence`: raycast do centro da câmera
  define o targetPoint; o muzzle converge geometricamente (~8Hz refresh, clamp [5,range]).

### Estabilização do player (anti-launch)
`PlayerCarController`: `centerOfMass` explícito (0,-0.4,0), `ContinuousDynamic`,
`maxAngularVelocity` 7, `angularDamping` 1.5, clamp |v|<=maxSpeed*1.35 e vy<=9,
torque auto-nivelante quando inclinado >1.5°.

### Governança
Novo `docs/standards/physics-and-combat.md` (veículos, estados de colisão, props
destrutíveis, convergência balística, camadas de raycast). Regras vinculantes
adicionadas em `AGENTS.md` §1.8 e `CLAUDE.md` princípios rápidos.

### Gates
- Cena `--strict`: aprovada. UTF: **75/75** (8 novos em `NeonDistrictPhysicsTests`).
- Z-fighting: cruzamentos cobrem a largura da avenida (eram 13.2 vs 16m), gaps
  de camada >=5cm, calçada com tiling 48x48 (antes esticada em 300m).


## HANDOFF — Sessão 19/10 (Quaternius + Gameplay Core)

**Mudou de vez:**
- Pedestres = Quaternius (`quat_*.prefab`, `Resources/Art/Pedestrians`).
  Pipeline: `tools/art/import_quaternius_pack.py` +
  `-executeMethod Studio.Editor.QuaterniusPedPipeline.RunPipelineBatch`.
  NUNCA recriar RebindToAvatarSkeleton / ossos manuais — ver
  `.studio/knowledge/gameplay/pedestrian-pipeline.md`.
- BreakableProp agora TOMBA o prop inteiro (rb kinematic→dinâmico na raiz,
  SetVisual removido). Decals parentados a rigidbodies. Explosão = só
  `WFXMR_Explosion` (Mirza tinha quadrados pretos).
- PlayerHealth (100HP) no objeto foot; HUD `HealthText`.
  `OnFootMotor.LastLandImpactSpeed` é consumido por PlayerHealth.
- Rodas: `_steerWheelMaxDeg` 32 + `_visualSteerDeg` suavizado.

**Pendente:** relatório UTF em `evidence/test-reports/quat-editmode.xml`.

