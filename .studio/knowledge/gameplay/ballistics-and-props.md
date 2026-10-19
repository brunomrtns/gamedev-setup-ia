# Ballistics & Props — regras de parentesco e VFX

## Decals de tiro
- Decal em corpo móvel (Rigidbody) DEVE ser parentado:
  `decal.transform.SetParent(hit.collider.transform, true)` — o buraco de
  bala acompanha a física do veículo/prop, nunca fica solto no espaço.
- Em pedestres é PROIBIDO decal — só partícula efêmera (VfxPool).
- Pool de decals tolera slots destruídos (carro despawnado leva o decal):
  checar `d == null` antes de usar.

## Explosões
- Batida E tiros invocam o MESMO prefab: `WFXMR_Explosion` (War FX, URP).
- O variant Mirza Beig "Explosion FREE" gera quadrados pretos — não usar.
- PROIBIDO emissor procedural de fumaça/fogo por script (shader incompleto
  = quadrado preto). Se o prefab VFX faltar, skip — nunca fallback procedural.

## Props quebráveis
- `BreakableProp` + `Rigidbody` vivem na RAIZ VISUAL; o rb fica kinematic
  até o impulso > threshold → dinâmico → o prop INTEIRO (colisor+renderer)
  tomba. Colisor via `ColliderFitter.FitProp(go, name, go.transform)` — filho
  do prop, cai junto.
- Props com física precisam `StaticEditorFlags = 0` (static batching prende
  a malha no lugar = textura fantasma).
- Lixeiras/bancos/cones leves: rb DINÂMICO 15–30 kg desde o spawn —
  empurráveis pelo para-choque (não BreakableProp).

## Veículos
- HP: player 350, tráfego 140 (`VehicleDamageController.ConfigureHealth`).
- Cooldown 0.35s entre registros de dano por contato (colliders compostos
  do mesmo impacto não somam dano instantâneo).
