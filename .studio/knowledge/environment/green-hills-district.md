# Green Hills District — receita e status (v3.2)

## Layout implementado (BuildGreenHills, seed 731)
- Região: x>284 (leste do OBD), raiz `GreenHills`, ~200 objetos.
- C1 transição: fence_a em x=288, árvores esparsas, trilha crops_dirtRow.
- C2 morros: 2 Hill() em anéis de cliff_block/slope/top (raios 42/30/18,
  degraus y=0/4.2/8.4) + platform_grass no topo (y=12.6).
- C3 rio: 12 peças ground_riverStraight/Bend serpenteando x≈352±14,
  bridge_stone em (352,26), platform_beach, waterfall+Top no morro N.
- C4 rural: windmill no topo do morro N (POI), cliff_cave no morro S (POI),
  acampamento campfire+tent, fazenda crops+fence+stall, trilhas path_stone/wood.
- Bosques: pines encosta N, oaks vale, fall/dark acento S; stumps/logs/
  mushrooms/bushes/flores no chão; 3 bandos de birds.
- Colisão: ColliderFitter.FitProp nos collide=true (árvores/cercas/cliffs/
  moinho/ponte/tendas). PROIBIDO MeshCollider em render — respeitado.
- Estrada: Street_2Lane x=300→344 em z=26 liga a rotatória à ponte.

## Problemas conhecidos (HONESTO — retrabalho necessário)
1. CHÃO: Mat_Kit_NatureKit (atlas) NÃO tileia em quad grande — vira listras.
   Grass 2.mat tileado vira grade. Fallback atual: Mat_ND_GroundFar (sólido
   escuro). Correção futura: tiles ground_grass instanciados OU material
   URP com textura de grama tileável dedicada.
2. Morros leem ESPARSOS de cima — anéis de cliff têm gaps; considerar
   anel interno de preenchimento ou platform_* como núcleo sólido.
3. Windmill/árvores: escala presumida — verificar in-game se leem bem.
4. Sem IA de tráfego nas vias novas (TrafficNetwork é grid-only).

## v3.4 — REPARO (colisor fantasma + chão + fill)

**LIÇÃO PERMANENTE:** verificação de mundo = teste IN-GAME dirigindo.
Render de editor escondeu 3 falhas de fundação (blocker, chão, densidade).

- COLISOR FANTASMA identificado: `Bounds_Blocker_E` — parede invisível em
  x=292 (OBD_ROAD_END+8), 10m altura × 294m, layer IgnoreRaycast. Era o
  bloqueio original da borda leste do grid 5×5, nunca movido na expansão.
  → movido p/ x=522, z-span 420; novos Bounds_Blocker_GH_N/S (z=±165).
- CHÃO: 3ª tentativa funcionou — T_Concrete (albedo claro) × tint verde.
  grass.png lia preto no dusk; atlas NatureKit virava listras; Grass 2 virava
  grade; asfalto escuro virou navy. Regra: quad grande de terreno exige
  textura CLARA e tileável + tint.
- FILL: peças elevadas (cliff/platform) ganharam pilares cliff_block abaixo —
  relevo sólido, nada paira. Fill roda POR ÚLTIMO, depois da densificação.
- Prova in-game: tools/qa/ingame_proof.py teleporta o carro na rota
  avenida→OBD→rotatória→ponte→moinho e renderiza Camera.main p/ PNG.
  Frames: evidence/ingame/INGAME_0*.png — carro atravessa tudo.

## Ressalvas que ficam (honesto)
- Morros ainda leem como aglomerado de rochas, não colina orgânica — o kit
  de cliff é de blocos ~8m; relevo convincente precisaria de muito mais
  densidade ou um terreno de verdade.
- Aérea: rota de POIs parcialmente legível; trilhas curtas.
- Tráfego IA fora das ruas novas (grid-only), PlayMode test discovery em
  batch não detecta testes (prova via pipeline do editor vivo).
