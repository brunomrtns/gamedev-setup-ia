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
