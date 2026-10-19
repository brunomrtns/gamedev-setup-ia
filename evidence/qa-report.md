# QA Report — NEON DISTRICT (AutonomousPlaytester + Studio.QA)

Checklist vivo de achados do QA autônomo. Legenda: `[x]` corrigido/verificado, `[~]` parcial/suspeita, `[ ]` aberto.

## Arquitetura (sessão 4 — refatorada)

- **Núcleo universal** `Studio.QA` (`unity/Assets/Game/Scripts/QA/`): `QaResult`
  (Pass/Fail/Blocked/Invalid/Inconclusive), `QaFinding`, `QaScenario` com
  pré-condições (`PrepStatus` Ready/Working/Blocked), `QaSession` JSON,
  `CoverageGrid`, `PerfTracker` (warmup+spikes), `ExceptionWatchdog`.
- **Adapter** `AutonomousPlaytester` (`Studio.Gameplay`): cenários ND +
  monitores contínuos + driver de input REAL (`SetVirtualControl`/
  `SetQaFootMove`/`TriggerEnterExit` — mesmos canais dos botões touch).
- Runner `tools/qa/run_autonomous_playtest.py` dirige o editor vivo via
  `unity cmd` (open_scene → editor_play → eval_file → console → export).

## Sessão 1 — 62s (bot antigo, drive + walk)

- [ ] `FOOT_SLIDING` ×5 — `npc_csl_01f_01_baked` (detector antigo, estado
  compartilhado entre peds = falsos positivos prováveis).
- [x] Bot navegava pra calçada → `CityLayout.RoadPoint`.
- [x] Alternância carro↔a pé aos 60s/75s.

## Sessão 2 — 89s (detectores ampliados)

- [~] `PED_FLOATING` y=1.4m ×2 — fix v1 (spawn por ossos) insuficiente.
- [~] `STUCK` ×3 em (-9,105) — recuperação por teleport adicionada.
- [ ] `FPS_DROP` ×3 (0.34–0.40s).
- [x] `shots=0` → fallback `ScareAt` (depois: tiro real via FireHeld).
- [ ] `visited=7/64` — cobertura baixa.

## Sessão 3 — 210s (Studio.QA v1, cenários com pré-condições)

- [x] `PhysicsSimGuard` **Pass** — simulationMode=FixedUpdate (guarda da
  regressão histórica `m_SimulationMode=2`).
- [x] `ExitEnter` **Pass** — ciclo sair→andar→entrar pelo pipeline real.
- [x] `ShotPanic` **Pass** — AK real: `FireHeld` → ammo 30→29 → ped fugiu.
- [x] `ScarePanic` **Pass** — subsistema isolado `ScareAt` → pânico.
- [~] `DriveAccel` **Fail** — gas 4s → dist=0.0m. Hipótese confirmada por
  diag: canal de input OK (throttle=1 chega), rb saudável; carro estava
  **dentro de collider** (teleport levou a (-120,101), dentro de quarteirão).
  Prep agora faz reset p/ via livre + SphereCast → `Blocked` se obstruído.
- [~] `DriveSteer` **Fail** — yaw Δ=0°: consequência do mesmo aprisionamento
  (steer yaw ∝ velocidade).
- [x] `RunOver` **Pass** — reset a 28m do ped + drive real → atropelou e caiu.
- [x] `EXCEPTION` NRE `BreakableProp.Break:63` — confirmado: `_visual` sem
  Renderer → `GetComponentInChildren<Renderer>().bounds` null. **Fix:**
  guard de renderer (sessão 4+). Achado NOVO do watchdog.
- [x] `EXCEPTION` NRE `TrafficNetwork.PickNext:93` — confirmado: `_rng` não
  serializável zera em domain reload → `_rng ??= new(...)` + guard
  `rng==null` + teste `PickNext_NullRng_ReturnsDeadEnd`. **0 NREs pós-fix.**
- [ ] `PED_FLOATING` ×4 — causa raiz refinada: `SnapY` ancora o ROOT, mas a
  pose animada eleva o esqueleto (0.5–1.26m variável). Fix: correção
  iterativa na armature (50%/snap, ignora transient <0.3m) — **aguardando
  validação sessão 4**.
- [ ] `FOOT_SLIDING` confirmado (per-ped) em `01f_02`, `01m_02`, `01m_03`
  `_baked` — malha não segue esqueleto animado.
- [ ] `CORPSE_FLOATING` ×1 — `01m_03` pós-atropelamento; bounds do cadáver
  também defasados.
- [ ] `STUCK` ×16 / `collisions=130` — driver era cego a obstáculos.
  **Fix:** whiskers frontais (0/±22°) + manobra de RÉ real (input Brake)
  + esterço pro lado livre — **aguardando validação sessão 4**.
- [~] `ExitEnter` **Blocked** na sessão 4a — flakiness de timing (foi Pass
  na 3a). Monitorar.

## Infra

- [x] `unity cmd` no editor vivo — caminho funcional comprovado.
- [ ] `-runTests` batchmode → `testcasecount=0` (falso positivo). Não usar.
- [x] Frames em `unity/Temp/qa_frames` → copiados p/ `evidence/qa/frames/`.
- [x] `clear_console` no runner (evita capturar markers de sessões antigas).

## Pendências registradas

- Validar PED_FLOATING/whiskers na sessão 4 (em execução).
- FOOT_SLIDING: investigar SMRs de roupa nos `_baked` (bindposes/armatures).
- Perf spikes ~350–380ms post-warmup: correlacionar spawn/death/GC.
- Cobertura real por navegação (teleports medidos à parte).

## Sessão 20261018_193406_4b5e — 2026-10-18 16:37 (editor vivo, input real)
- [x] - Pass         PhysicsSimGuard: simulationMode=FixedUpdate (0,3s, prep 0,3s)
- [~] - Blocked      DriveAccel:  (0,0s, prep 0,0s)
- [ ] - Fail         DriveSteer: steer+gas 2.5s â†’ yaw Î”=7Â° (esperado >15Â°) (2,5s, prep 0,0s)
- [x] - Pass         DriveBrake: parou: fwd=0,3m/s (3,0s, prep 0,0s)
- [ ] - Fail         ExitEnter: nÃ£o alcanÃ§ou o carro (d=2,6m) (12,1s, prep 15,4s)
- [x] - Pass         ShotPanic: 1/1 peds fugiram apÃ³s tiro real (ammo 30â†’29) (0,5s, prep 11,5s)
- [x] - Pass         ScarePanic: 1/1 peds fugiram apÃ³s ScareAt (0,3s, prep 10,1s)
- [x] - Pass         RunOver: ped atingido e caiu (npc_csl_00_character_01f_02_baked(Clone)) (4,4s, prep 5,8s)
- evidências: `evidence\qa\report_20261018_193406_4b5e.md`, `evidence\qa\session_20261018_193406_4b5e.json`, `evidence\qa\frames\20261018_193406_4b5e`

## Sessão 20261018_193406_4b5e — 2026-10-18 16:42 (editor vivo, input real)
- evidências: `evidence\qa\report_20261018_193406_4b5e.md`, `evidence\qa\session_20261018_193406_4b5e.json`, `evidence\qa\frames\20261018_193406_4b5e`

## Sessão 20261018_194303_b888 — 2026-10-18 16:45 (editor vivo, input real)
- [x] - Pass         PhysicsSimGuard: simulationMode=FixedUpdate (0,3s, prep 0,3s)
- [~] - Blocked      DriveAccel:  (0,0s, prep 0,0s)
- [x] - Pass         DriveSteer: yaw Î”=22Â° em 2.5s (2,5s, prep 0,0s)
- [x] - Pass         DriveBrake: parou: fwd=0,0m/s (3,0s, prep 0,0s)
- [ ] - Fail         ExitEnter: a pÃ© nÃ£o move (Î”=0,56m em 2s) (2,4s, prep 0,3s)
- [x] - Pass         ShotPanic: 1/1 peds fugiram apÃ³s tiro real (ammo 30â†’29) (0,5s, prep 2,8s)
- [x] - Pass         ScarePanic: 1/1 peds fugiram apÃ³s ScareAt (0,3s, prep 1,7s)
- [x] - Pass         RunOver: ped atingido e caiu (npc_csl_00_character_02f_01_baked(Clone)) (1,9s, prep 0,0s)
- evidências: `evidence\qa\report_20261018_194303_b888.md`, `evidence\qa\session_20261018_194303_b888.json`, `evidence\qa\frames\20261018_194303_b888`

## Análise consolidada (sessões 3-4, bot v2)

- [x] Driver de ré validado: 26 recuperações, **0 teleports** — whiskers
  detectam obstáculos reais (`Plaza_Hedge`, `Bldg_*`, `Hydrant`, `Col_Tank`
  de tráfego); navegação real `visited=13/64`.
- [x] `DriveSteer` Pass (yaw Δ=22°); `DriveBrake`, `ShotPanic`, `ScarePanic`,
  `RunOver` (atropelamento real) — todos Pass por input real.
- [ ] `DriveAccel` Blocked — `Avenue_Dual_-91_BoundsCollider` a ~9m da via
  pós-reset. Suspeita de bug de cena (collider atravessando a avenida).
- [ ] `ExitEnter` Fail — a pé moveu 0.56m em 2s (obstáculo na calçada;
  driver a pé ainda sem whiskers).
- [ ] `PED_FLOATING` persiste (0.53–1.38m) — lift vem da pose animada
  (hips-local-Y do clip vs armature do _baked); fix v1/v2 insuficientes.
- [ ] `CORPSE_FLOATING`, `FOOT_SLIDING` (_baked) abertos.
- [ ] `EXCEPTION @17s` — NRE `Studio.Foundation.V*` (provável VfxPool)
  acessando transform de objeto destruído (~19:46). Bug novo, aberto.
- [~] Perf spikes ~350-420ms pós-warmup (GC/VfxPool?).

## Sessão 20261018_194923_6786 — 2026-10-18 16:52 (editor vivo, input real)
- [x] - Pass         PhysicsSimGuard: simulationMode=FixedUpdate (0,3s, prep 0,3s)
- [~] - Blocked      DriveAccel:  (0,0s, prep 0,0s)
- [ ] - Fail         DriveSteer: steer+gas 2.5s â†’ yaw Î”=0Â° (esperado >15Â°) (2,5s, prep 0,0s)
- [x] - Pass         DriveBrake: parou: fwd=0,0m/s (3,0s, prep 0,0s)
- [~] - Blocked      ExitEnter:  (0,0s, prep 0,0s)
- [~] - Blocked      ShotPanic:  (0,0s, prep 35,0s)
- [~] - Blocked      ScarePanic:  (0,0s, prep 0,0s)
- [~] - Inconclusive RunOver: nÃ£o alcanÃ§ou o pedestre em 28s (28,0s, prep 0,0s)
- evidências: `evidence\qa\report_20261018_194923_6786.md`, `evidence\qa\session_20261018_194923_6786.json`, `evidence\qa\frames\20261018_194923_6786`

## Missão 2 — correções de causa raiz (18/10 ~20:00)

**Causas raiz identificadas nesta rodada:**
- `Avenue_Dual_*_BoundsCollider` = postes duplos do canteiro central da
  avenida (legítimo, scene builder l.1391). O bot navegava pelo CENTRO da
  rua → DriveAccel Blocked + 175 colisões. **Fix:** navegação por poses de
  FAIXA do `TrafficNetwork.TryGetNearestPose` (`LaneTarget`), incluindo
  reset do DriveAccel e un-stick.
- `VfxPool.Spawn` NRE: fila `_free` estática retém GameObjects destruídos
  (scene/domain reload) → `go.transform` num objeto morto. **Fix:** loop de
  dequeue que pula instâncias destruídas (== null Unity).
- `CORPSE_FLOATING`/`PED_FLOATING`: mesma causa — `SnapCorpseToGround` e
  `SnapY` usavam renderer.bounds/root defasados após pose animada.
  **Fix:** aterramento pelo menor osso (vivo: correção iterativa root +
  cápsula compensada; cadáver: min-bone no snap 2.7s).
- Diag ao vivo provou: todos os peds vivos `lift≈0` (fix convergiu);
  cadáver `02f_01` a 0.41m = caso real corrigido agora.
- `TRAFFIC_FLIPPED` flaggava wrecked capotado = aftermath legítimo →
  agora só Routing/PhysicsFree.
- `_rig` resolvido 1× no Bootstrap → refs spawnadas depois ficavam null
  ("PedestrianSystem ausente" etc.) → `RefetchRig()` lazy por frame.

**Aguardando validação:** sessão 6 com todos os fixes (lane nav +
VfxPool + corpse + rig lazy) — rodar `run_autonomous_playtest.py`.
