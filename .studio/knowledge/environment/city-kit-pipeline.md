# City Kit Pipeline — como plugar QUALQUER kit modular de cidade

Provado com Downtown City MegaKit (CC0, 153 FBX Unity + 36 texturas compartilhadas).

## Receita
1. Extração idempotente: `tools/art/import_<kit>.py` →
   `Assets/Game/Art/Environment/Modular/<Kit>/{Models,Textures}` +
   manifesto SHA-256 em `.studio/`. Kits CC0 frequentemente exportam por
   engine (`Exports/FBX (Unity)` etc.) — pegar SÓ a variante Unity.
2. Inventário: descobrir o grid modular por bounds das peças
   (`probe_dims.py` ou `inspect_character_fbx.py`) — MegaKit = **3m**.
   NÃO renormalizar em massa: pivôs de kit modular são calibrados pelo
   autor p/ snap; só tocar se houver desvio medido.
3. Materiais: o kit usa slots `MI_*` com famílias de textura
   `T_<fam>_BaseColor/Normal/ORM` compartilhadas. Criar `Mat_DT_<fam>`
   (URP Lit, `_BaseMap`+`_BumpMap`, ORM ignorado) e remapear por NOME do
   slot importado — UM material por família = SRP Batcher friendly.
   `DowntownKitImporter.RunImportBatch` faz isso + prefab por peça.
4. Pool determinístico no Scene Builder: `MEGAKIT_POOL` + `prefabDir`
   param em `SpawnBuilding` — piloto = subconjunto de quarteirões
   (`IsDowntownPilot`), Demo City permanece como fallback no resto.
5. Capturas obrigatórias ANTES de expandir (prefabs isolados + piloto
   in-scene multi-ângulo) — olhar as imagens.

## Pegadinhas do MegaKit
- Grid 3m; pivôs já corretos (sidewalk pivot na superfície, origin_z=-0.15).
- Vertex colors (wear) — URP Lit ignora; aceitável, só logar.
- Prédios pré-montados: 13–31k verts, 12-13 slots MI_* cada — pesados mas
  OK em ~4-6 instâncias por quarteirão piloto.
- Peças de rua (calçada/poste/sinal) NÃO integradas — nossas ruas têm
  colisores/quebra/z-fighting calibrados. Follow-up opcional.
- `unity cmd` pipeline do editor VIVO pode rodar código velho se o domain
  reload não completou — após editar C#, sempre `recompile` + confirmar
  `isCompiling=false` ANTES de eval de build.
