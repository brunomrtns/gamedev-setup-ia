# Pedestrian Pipeline — Quaternius CC0 (definitivo)

## Decisão
Pedestres usam **Quaternius Universal Base Characters + Universal Animation
Library (UAL1/UAL2)** — esqueleto humanóide limpo, UM armature + SMRs nativos.
Pipeline: `tools/art/import_quaternius_pack.py` (extração) →
`Studio.Editor.QuaterniusPedPipeline.RunPipelineBatch` (import Humanoid +
materiais URP + prefabs `quat_*` + `Pedestrian.controller`).

## Fatos que NÃO podem regredir
- **PROIBIDO** scripts de rebind de armadura em runtime
  (`RebindToAvatarSkeleton`) ou rotação manual de ossos (LateUpdate corretivo).
  A pipeline antiga `npc_csl_*_baked` precisava disso porque os FBX modulares
  tinham armatures duplicadas — o Quaternius não tem esse problema.
- Modelos base: `animationType=Human`, `avatarSetup=CreateFromThisModel`.
- Clips UAL: `animationType=Human`, `avatarSetup=CopyFromOther` +
  `sourceAvatar` = avatar do `Superhero_Male_FullBody.fbx`.
- Takes UAL nomeados `Armature|X_Loop` → clip renomeado pra `X_Loop`;
  `loopTime=true` em `*_Loop`; `lockRootHeightY`/`lockRootRotation`=true.
- Clips usados no controller: `Idle_Loop`, `Walk_Loop`, `Sprint_Loop`,
  `Death01`, `Crouch_Idle_Loop` (cower).
- Cabelos `Hair_*` são "rigged to head bone" — instanciar sob o osso `Head`.
- Textura masculina clara chama `T_Superhero_Male_Ligh.png` (typo no zip oficial).

## Pipeline Universal de Pedestres v2 — trocar o VISUAL mantendo a ANIMAÇÃO

**Princípio:** esqueleto + `Pedestrian.controller` + clips UAL = **CHASSI** (sagrado,
nunca mexer). O pack visual é a **CASCA** — trocável via FBX unificado.

**Causa raiz das 3 falhas anteriores (nunca repetir):**
1. Prefabs `*_baked` com avatar **Generic** → clips Humanoid deformam.
2. Remap de `SMR.bones` por nome em editor → bindposes antigas não casam com
   o novo espaço de ossos → malha deformada.
3. Recompute de bindposes (`bone.worldToLocal × mesh.localToWorld`) em
   runtime-bake → assume pose atual = bind pose (falso) → malha some/degenera.
   `SMR.bounds`/`localBounds` NÃO refletem skinning — telemetria "bounds sãos"
   com render vazio é o sintoma clássico de bindpose quebrada.
4. Diagnóstico da raiz: prefab renderiza T-pose mas SOME sob
   `AnimationMode.SampleAnimationClip` = skinning inconsistente NO ASSET de
   origem (não foi o bake que quebrou — foi o FBX vendor mal importado).

**Receita (provada em npc_casual_set_00 → 12 variantes ped_dressed_*):**
1. Inventário: `blender -b -P tools/art/inspect_character_fbx.py -- <fbx...>
   --json out.json` — conta armatures, ossos, SMRs, vgroups por arquivo.
2. Manifest: `tools/art/ped_dressed_manifest.json` — por variante: parts[]
   (corpo + cabelo/barba = FBXs com armatures duplicadas) + out.
3. Unificar: `wsl blender -b -P tools/art/unify_character_fbx.py -- manifest.json
   [--only <nome>]` — blindagem osso-a-osso (head/tail world, tol 1e-4;
   divergiu = aborta e reporta), re-aponta Armature modifier, apaga armature
   duplicada, strip `_lodN>0`, normaliza transforms (DESPARENTA meshes antes de
   apply — apply no pai com filho anexado multiplica escala residual),
   render Workbench rest+pose OBRIGATÓRIO em `evidence/art/unify_*`, exporta
   FBX único (exportador nativo Blender 5 só aceita filepath/axis_forward/axis_up
   — cena deve conter só o que exporta).
4. Unity: `QuaterniusPedPipeline.BakeDressedBatch` — reimporta
   `PedDressed/*.fbx` Humanoid+CreateFromThisModel+materialImportMode=None,
   instancia, aplica materiais por mapa SMR.nome→mats lido da **prefab vendor
   correspondente**, Animator=controller existente+AlwaysAnimate.
5. Verificação: `CaptureDressedBatch`/`CaptureDressedAllBatch` renderizam
   idle/walk/run/death p/ `evidence/captures/ped_dressed_*_{tag}.png` —
   **olhar TODAS as imagens**; "compilou/salvou" não é pronto.
6. Spawn: `PedestrianSystem` prefere `ped_dressed_*` → fallback `quat_*`.

**Para plugar um pack novo:** repetir passos 1–3 apontando o manifest para os
FBX do pack. Pack com 1 armature por FBX + esqueleto idêntico entre partes =
merge determinístico garantido.

**Armadilhas do harness de captura (lição dolorosa):** capturas DEVEM abrir
`EditorSceneManager.NewScene(EmptyScene)` — objetos de prova (luz/cubo/câmera)
criados na cena aberta vazam e estouram a iluminação/travam o jogo (12 câmeras
+luzes acumuladas). Telemetria de ossos não substitui olhar o PNG.

**Multiplayer:** prefabs novas NÃO passaram por `NetPrefabBuilder.BakeNetEntities`
— MP usa as antigas; rebake = follow-up quando MP voltar a ser ativo.

## Bugs históricos que esta troca elimina
- PED_FLOATING (esqueleto dos _baked elevado do root)
- A-pose residual (braços precisavam de correção manual em LateUpdate)
- FOOT_SLIDING por mismatch de stride (agora `anim.speed = _speed/1.35` casa
  o ciclo Walk_Loop com o deslocamento real)
