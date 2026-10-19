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

## Bugs históricos que esta troca elimina
- PED_FLOATING (esqueleto dos _baked elevado do root)
- A-pose residual (braços precisavam de correção manual em LateUpdate)
- FOOT_SLIDING por mismatch de stride (agora `anim.speed = _speed/1.35` casa
  o ciclo Walk_Loop com o deslocamento real)
