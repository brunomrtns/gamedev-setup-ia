# M1 — Mapa de dano entre revisões da cena

- PRE: `inv_pre.json` · POST: `inv_head.json`
- Objetos: PRE=3686 POST=2683 · adicionados(+nomes únicos)=7 (441 instâncias) · removidos=26 (670 instâncias) · pareados=2241
- Colisores ADICIONADOS no POST: **361** · removidos: 0

## Colisores adicionados — classificação por geometria

| Veredito | Qtd | Na via | Exemplos |
|---|---|---|---|
| OK | 336 | 336 | Decal_Crosswalk_Wide_BoundsCollider@(174,-130); Decal_Crosswalk_Wide_BoundsCollider@(226,130); Decal_Crosswalk_Wide_BoundsCollider@(226,-78); Decal_Crosswalk_Wide_BoundsCollider@(226,78) |
| LIXO-sem-visual | 25 | 25 | AsphaltPatch_Plaza_BoundsCollider@(-22,22); AsphaltPatch_Plaza_BoundsCollider@(30,22); AsphaltPatch_Plaza_BoundsCollider@(-22,-30); AsphaltPatch_Plaza_BoundsCollider@(30,-30) |

## Detalhe — colisores adicionados QUE TOCAM A VIA (bloqueio potencial)

| Objeto | Pos | Tipo | Bounds (cx,cy,cz / sx,sy,sz) | Veredito |
|---|---|---|---|---|
| `AsphaltPatch_Plaza/AsphaltPatch_Plaza_BoundsCollider` | (-21.5,21.5) | BoxCollider | (-21.5,0.9,21.5 / 8.3,2.0,8.3) | LIXO-sem-visual |
| `AsphaltPatch_Plaza/AsphaltPatch_Plaza_BoundsCollider` | (30.5,21.5) | BoxCollider | (30.5,0.9,21.5 / 8.3,2.0,8.3) | LIXO-sem-visual |
| `AsphaltPatch_Plaza/AsphaltPatch_Plaza_BoundsCollider` | (-21.5,-30.5) | BoxCollider | (-21.5,0.9,-30.5 / 8.3,2.0,8.3) | LIXO-sem-visual |
| `AsphaltPatch_Plaza/AsphaltPatch_Plaza_BoundsCollider` | (30.5,-30.5) | BoxCollider | (30.5,0.9,-30.5 / 8.3,2.0,8.3) | LIXO-sem-visual |
| `Decal_DoubleYellow_Straight/Decal_DoubleYellow_Straight_BoundsCollider` | (-72.0,26.0) | BoxCollider | (-72.0,0.9,26.0 / 5.5,2.0,0.4) | LIXO-sem-visual |
| `Decal_DoubleYellow_Straight/Decal_DoubleYellow_Straight_BoundsCollider` | (-12.0,26.0) | BoxCollider | (-12.0,0.9,26.0 / 5.5,2.0,0.4) | LIXO-sem-visual |
| `Decal_DoubleYellow_Straight/Decal_DoubleYellow_Straight_BoundsCollider` | (-84.0,26.0) | BoxCollider | (-84.0,0.9,26.0 / 5.5,2.0,0.4) | LIXO-sem-visual |
| `Decal_DoubleYellow_Straight/Decal_DoubleYellow_Straight_BoundsCollider` | (36.0,26.0) | BoxCollider | (36.0,0.9,26.0 / 5.5,2.0,0.4) | LIXO-sem-visual |
| `Decal_DoubleYellow_Straight/Decal_DoubleYellow_Straight_BoundsCollider` | (-24.0,26.0) | BoxCollider | (-24.0,0.9,26.0 / 5.5,2.0,0.4) | LIXO-sem-visual |
| `Decal_DoubleYellow_Straight/Decal_DoubleYellow_Straight_BoundsCollider` | (-108.0,26.0) | BoxCollider | (-108.0,0.9,26.0 / 5.5,2.0,0.4) | LIXO-sem-visual |
| `Decal_DoubleYellow_Straight/Decal_DoubleYellow_Straight_BoundsCollider` | (-120.0,26.0) | BoxCollider | (-120.0,0.9,26.0 / 5.5,2.0,0.4) | LIXO-sem-visual |
| `Decal_DoubleYellow_Straight/Decal_DoubleYellow_Straight_BoundsCollider` | (60.0,26.0) | BoxCollider | (60.0,0.9,26.0 / 5.5,2.0,0.4) | LIXO-sem-visual |
| `Decal_DoubleYellow_Straight/Decal_DoubleYellow_Straight_BoundsCollider` | (-60.0,26.0) | BoxCollider | (-60.0,0.9,26.0 / 5.5,2.0,0.4) | LIXO-sem-visual |
| `Decal_DoubleYellow_Straight/Decal_DoubleYellow_Straight_BoundsCollider` | (48.0,26.0) | BoxCollider | (48.0,0.9,26.0 / 5.5,2.0,0.4) | LIXO-sem-visual |
| `Decal_DoubleYellow_Straight/Decal_DoubleYellow_Straight_BoundsCollider` | (96.0,26.0) | BoxCollider | (96.0,0.9,26.0 / 5.5,2.0,0.4) | LIXO-sem-visual |
| `Decal_DoubleYellow_Straight/Decal_DoubleYellow_Straight_BoundsCollider` | (24.0,26.0) | BoxCollider | (24.0,0.9,26.0 / 5.5,2.0,0.4) | LIXO-sem-visual |
| `Decal_DoubleYellow_Straight/Decal_DoubleYellow_Straight_BoundsCollider` | (72.0,26.0) | BoxCollider | (72.0,0.9,26.0 / 5.5,2.0,0.4) | LIXO-sem-visual |
| `Decal_DoubleYellow_Straight/Decal_DoubleYellow_Straight_BoundsCollider` | (84.0,26.0) | BoxCollider | (84.0,0.9,26.0 / 5.5,2.0,0.4) | LIXO-sem-visual |
| `Decal_DoubleYellow_Straight/Decal_DoubleYellow_Straight_BoundsCollider` | (-96.0,26.0) | BoxCollider | (-96.0,0.9,26.0 / 5.5,2.0,0.4) | LIXO-sem-visual |
| `Decal_DoubleYellow_Straight/Decal_DoubleYellow_Straight_BoundsCollider` | (-48.0,26.0) | BoxCollider | (-48.0,0.9,26.0 / 5.5,2.0,0.4) | LIXO-sem-visual |
| `Decal_DoubleYellow_Straight/Decal_DoubleYellow_Straight_BoundsCollider` | (0.0,26.0) | BoxCollider | (0.0,0.9,26.0 / 5.5,2.0,0.4) | LIXO-sem-visual |
| `Decal_DoubleYellow_Straight/Decal_DoubleYellow_Straight_BoundsCollider` | (-36.0,26.0) | BoxCollider | (-36.0,0.9,26.0 / 5.5,2.0,0.4) | LIXO-sem-visual |
| `Decal_DoubleYellow_Straight/Decal_DoubleYellow_Straight_BoundsCollider` | (12.0,26.0) | BoxCollider | (12.0,0.9,26.0 / 5.5,2.0,0.4) | LIXO-sem-visual |
| `Decal_DoubleYellow_Straight/Decal_DoubleYellow_Straight_BoundsCollider` | (120.0,26.0) | BoxCollider | (120.0,0.9,26.0 / 5.5,2.0,0.4) | LIXO-sem-visual |
| `Decal_DoubleYellow_Straight/Decal_DoubleYellow_Straight_BoundsCollider` | (108.0,26.0) | BoxCollider | (108.0,0.9,26.0 / 5.5,2.0,0.4) | LIXO-sem-visual |
| `Environment/OBD_ArtPass/Decal_Crosswalk_Wide_BoundsCollider` | (174.0,-130.0) | BoxCollider | (174.0,0.9,-130.0 / 10.5,2.0,4.3) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_Wide_BoundsCollider` | (226.0,130.0) | BoxCollider | (226.0,0.9,130.0 / 10.5,2.0,4.3) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_Wide_BoundsCollider` | (226.0,-78.0) | BoxCollider | (226.0,0.9,-78.0 / 10.5,2.0,4.3) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_Wide_BoundsCollider` | (226.0,78.0) | BoxCollider | (226.0,0.9,78.0 / 10.5,2.0,4.3) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_Wide_BoundsCollider` | (174.0,78.0) | BoxCollider | (174.0,0.9,78.0 / 10.5,2.0,4.3) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_Wide_BoundsCollider` | (226.0,-130.0) | BoxCollider | (226.0,0.9,-130.0 / 10.5,2.0,4.3) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_Wide_BoundsCollider` | (174.0,-130.0) | BoxCollider | (174.0,0.9,-130.0 / 10.5,2.0,4.3) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_Wide_BoundsCollider` | (174.0,26.0) | BoxCollider | (174.0,0.9,26.0 / 10.5,2.0,4.3) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_Wide_BoundsCollider` | (226.0,130.0) | BoxCollider | (226.0,0.9,130.0 / 10.5,2.0,4.3) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_Wide_BoundsCollider` | (174.0,-26.0) | BoxCollider | (174.0,0.9,-26.0 / 10.5,2.0,4.3) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_Wide_BoundsCollider` | (174.0,130.0) | BoxCollider | (174.0,0.9,130.0 / 10.5,2.0,4.3) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_Wide_BoundsCollider` | (174.0,130.0) | BoxCollider | (174.0,0.9,130.0 / 10.5,2.0,4.3) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_Wide_BoundsCollider` | (174.0,-26.0) | BoxCollider | (174.0,0.9,-26.0 / 10.5,2.0,4.3) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_Wide_BoundsCollider` | (226.0,130.0) | BoxCollider | (226.0,0.9,130.0 / 10.5,2.0,4.3) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_Wide_BoundsCollider` | (174.0,-78.0) | BoxCollider | (174.0,0.9,-78.0 / 10.5,2.0,4.3) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_Wide_BoundsCollider` | (174.0,26.0) | BoxCollider | (174.0,0.9,26.0 / 10.5,2.0,4.3) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_Wide_BoundsCollider` | (226.0,-78.0) | BoxCollider | (226.0,0.9,-78.0 / 10.5,2.0,4.3) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_Wide_BoundsCollider` | (174.0,-130.0) | BoxCollider | (174.0,0.9,-130.0 / 10.5,2.0,4.3) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_Wide_BoundsCollider` | (226.0,78.0) | BoxCollider | (226.0,0.9,78.0 / 10.5,2.0,4.3) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_Wide_BoundsCollider` | (174.0,130.0) | BoxCollider | (174.0,0.9,130.0 / 10.5,2.0,4.3) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_Wide_BoundsCollider` | (226.0,-130.0) | BoxCollider | (226.0,0.9,-130.0 / 10.5,2.0,4.3) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_Wide_BoundsCollider` | (226.0,-26.0) | BoxCollider | (226.0,0.9,-26.0 / 10.5,2.0,4.3) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_Wide_BoundsCollider` | (174.0,-78.0) | BoxCollider | (174.0,0.9,-78.0 / 10.5,2.0,4.3) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_Wide_BoundsCollider` | (174.0,-78.0) | BoxCollider | (174.0,0.9,-78.0 / 10.5,2.0,4.3) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_Wide_BoundsCollider` | (226.0,-26.0) | BoxCollider | (226.0,0.9,-26.0 / 10.5,2.0,4.3) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_Wide_BoundsCollider` | (226.0,-130.0) | BoxCollider | (226.0,0.9,-130.0 / 10.5,2.0,4.3) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_Wide_BoundsCollider` | (226.0,-78.0) | BoxCollider | (226.0,0.9,-78.0 / 10.5,2.0,4.3) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_Wide_BoundsCollider` | (226.0,26.0) | BoxCollider | (226.0,0.9,26.0 / 10.5,2.0,4.3) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_Wide_BoundsCollider` | (174.0,26.0) | BoxCollider | (174.0,0.9,26.0 / 10.5,2.0,4.3) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_Wide_BoundsCollider` | (226.0,-26.0) | BoxCollider | (226.0,0.9,-26.0 / 10.5,2.0,4.3) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_Wide_BoundsCollider` | (174.0,78.0) | BoxCollider | (174.0,0.9,78.0 / 10.5,2.0,4.3) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_Wide_BoundsCollider` | (226.0,78.0) | BoxCollider | (226.0,0.9,78.0 / 10.5,2.0,4.3) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_Wide_BoundsCollider` | (226.0,26.0) | BoxCollider | (226.0,0.9,26.0 / 10.5,2.0,4.3) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_Wide_BoundsCollider` | (174.0,-26.0) | BoxCollider | (174.0,0.9,-26.0 / 10.5,2.0,4.3) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_Wide_BoundsCollider` | (226.0,26.0) | BoxCollider | (226.0,0.9,26.0 / 10.5,2.0,4.3) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_Wide_BoundsCollider` | (174.0,78.0) | BoxCollider | (174.0,0.9,78.0 / 10.5,2.0,4.3) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_BoundsCollider` | (182.0,-137.5) | BoxCollider | (182.0,0.9,-137.5 / 4.2,2.0,5.0) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_BoundsCollider` | (182.0,-33.5) | BoxCollider | (182.0,0.9,-33.5 / 4.2,2.0,5.0) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_BoundsCollider` | (234.0,70.5) | BoxCollider | (234.0,0.9,70.5 / 4.2,2.0,5.0) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_BoundsCollider` | (182.0,33.5) | BoxCollider | (182.0,0.9,33.5 / 4.2,2.0,5.0) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_BoundsCollider` | (234.0,-70.5) | BoxCollider | (234.0,0.9,-70.5 / 4.2,2.0,5.0) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_BoundsCollider` | (182.0,-18.5) | BoxCollider | (182.0,0.9,-18.5 / 4.2,2.0,5.0) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_BoundsCollider` | (182.0,137.5) | BoxCollider | (182.0,0.9,137.5 / 4.2,2.0,5.0) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_BoundsCollider` | (234.0,-18.5) | BoxCollider | (234.0,0.9,-18.5 / 4.2,2.0,5.0) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_BoundsCollider` | (234.0,85.5) | BoxCollider | (234.0,0.9,85.5 / 4.2,2.0,5.0) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_BoundsCollider` | (234.0,-18.5) | BoxCollider | (234.0,0.9,-18.5 / 4.2,2.0,5.0) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_BoundsCollider` | (182.0,-70.5) | BoxCollider | (182.0,0.9,-70.5 / 4.2,2.0,5.0) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_BoundsCollider` | (234.0,85.5) | BoxCollider | (234.0,0.9,85.5 / 4.2,2.0,5.0) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_BoundsCollider` | (234.0,-85.5) | BoxCollider | (234.0,0.9,-85.5 / 4.2,2.0,5.0) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_BoundsCollider` | (234.0,-33.5) | BoxCollider | (234.0,0.9,-33.5 / 4.2,2.0,5.0) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_BoundsCollider` | (182.0,-70.5) | BoxCollider | (182.0,0.9,-70.5 / 4.2,2.0,5.0) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_BoundsCollider` | (182.0,-18.5) | BoxCollider | (182.0,0.9,-18.5 / 4.2,2.0,5.0) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_BoundsCollider` | (182.0,137.5) | BoxCollider | (182.0,0.9,137.5 / 4.2,2.0,5.0) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_BoundsCollider` | (234.0,-70.5) | BoxCollider | (234.0,0.9,-70.5 / 4.2,2.0,5.0) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_BoundsCollider` | (234.0,70.5) | BoxCollider | (234.0,0.9,70.5 / 4.2,2.0,5.0) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_BoundsCollider` | (234.0,-137.5) | BoxCollider | (234.0,0.9,-137.5 / 4.2,2.0,5.0) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_BoundsCollider` | (234.0,33.5) | BoxCollider | (234.0,0.9,33.5 / 4.2,2.0,5.0) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_BoundsCollider` | (234.0,18.5) | BoxCollider | (234.0,0.9,18.5 / 4.2,2.0,5.0) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_BoundsCollider` | (234.0,-122.5) | BoxCollider | (234.0,0.9,-122.5 / 4.2,2.0,5.0) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_BoundsCollider` | (234.0,33.5) | BoxCollider | (234.0,0.9,33.5 / 4.2,2.0,5.0) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_BoundsCollider` | (234.0,18.5) | BoxCollider | (234.0,0.9,18.5 / 4.2,2.0,5.0) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_BoundsCollider` | (234.0,122.5) | BoxCollider | (234.0,0.9,122.5 / 4.2,2.0,5.0) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_BoundsCollider` | (182.0,122.5) | BoxCollider | (182.0,0.9,122.5 / 4.2,2.0,5.0) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_BoundsCollider` | (182.0,-85.5) | BoxCollider | (182.0,0.9,-85.5 / 4.2,2.0,5.0) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_BoundsCollider` | (182.0,18.5) | BoxCollider | (182.0,0.9,18.5 / 4.2,2.0,5.0) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_BoundsCollider` | (182.0,122.5) | BoxCollider | (182.0,0.9,122.5 / 4.2,2.0,5.0) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_BoundsCollider` | (182.0,-85.5) | BoxCollider | (182.0,0.9,-85.5 / 4.2,2.0,5.0) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_BoundsCollider` | (182.0,70.5) | BoxCollider | (182.0,0.9,70.5 / 4.2,2.0,5.0) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_BoundsCollider` | (234.0,-85.5) | BoxCollider | (234.0,0.9,-85.5 / 4.2,2.0,5.0) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_BoundsCollider` | (234.0,85.5) | BoxCollider | (234.0,0.9,85.5 / 4.2,2.0,5.0) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_BoundsCollider` | (182.0,-122.5) | BoxCollider | (182.0,0.9,-122.5 / 4.2,2.0,5.0) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_BoundsCollider` | (182.0,137.5) | BoxCollider | (182.0,0.9,137.5 / 4.2,2.0,5.0) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_BoundsCollider` | (182.0,-85.5) | BoxCollider | (182.0,0.9,-85.5 / 4.2,2.0,5.0) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_BoundsCollider` | (234.0,-85.5) | BoxCollider | (234.0,0.9,-85.5 / 4.2,2.0,5.0) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_BoundsCollider` | (182.0,-122.5) | BoxCollider | (182.0,0.9,-122.5 / 4.2,2.0,5.0) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_BoundsCollider` | (182.0,18.5) | BoxCollider | (182.0,0.9,18.5 / 4.2,2.0,5.0) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_BoundsCollider` | (234.0,-122.5) | BoxCollider | (234.0,0.9,-122.5 / 4.2,2.0,5.0) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_BoundsCollider` | (182.0,70.5) | BoxCollider | (182.0,0.9,70.5 / 4.2,2.0,5.0) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_BoundsCollider` | (234.0,122.5) | BoxCollider | (234.0,0.9,122.5 / 4.2,2.0,5.0) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_BoundsCollider` | (182.0,70.5) | BoxCollider | (182.0,0.9,70.5 / 4.2,2.0,5.0) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_BoundsCollider` | (182.0,-18.5) | BoxCollider | (182.0,0.9,-18.5 / 4.2,2.0,5.0) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_BoundsCollider` | (234.0,137.5) | BoxCollider | (234.0,0.9,137.5 / 4.2,2.0,5.0) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_BoundsCollider` | (182.0,33.5) | BoxCollider | (182.0,0.9,33.5 / 4.2,2.0,5.0) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_BoundsCollider` | (234.0,-70.5) | BoxCollider | (234.0,0.9,-70.5 / 4.2,2.0,5.0) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_BoundsCollider` | (234.0,-33.5) | BoxCollider | (234.0,0.9,-33.5 / 4.2,2.0,5.0) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_BoundsCollider` | (234.0,-137.5) | BoxCollider | (234.0,0.9,-137.5 / 4.2,2.0,5.0) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_BoundsCollider` | (182.0,-33.5) | BoxCollider | (182.0,0.9,-33.5 / 4.2,2.0,5.0) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_BoundsCollider` | (234.0,137.5) | BoxCollider | (234.0,0.9,137.5 / 4.2,2.0,5.0) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_BoundsCollider` | (234.0,-122.5) | BoxCollider | (234.0,0.9,-122.5 / 4.2,2.0,5.0) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_BoundsCollider` | (234.0,137.5) | BoxCollider | (234.0,0.9,137.5 / 4.2,2.0,5.0) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_BoundsCollider` | (234.0,70.5) | BoxCollider | (234.0,0.9,70.5 / 4.2,2.0,5.0) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_BoundsCollider` | (182.0,-70.5) | BoxCollider | (182.0,0.9,-70.5 / 4.2,2.0,5.0) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_BoundsCollider` | (182.0,85.5) | BoxCollider | (182.0,0.9,85.5 / 4.2,2.0,5.0) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_BoundsCollider` | (234.0,-137.5) | BoxCollider | (234.0,0.9,-137.5 / 4.2,2.0,5.0) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_BoundsCollider` | (182.0,-33.5) | BoxCollider | (182.0,0.9,-33.5 / 4.2,2.0,5.0) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_BoundsCollider` | (182.0,18.5) | BoxCollider | (182.0,0.9,18.5 / 4.2,2.0,5.0) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_BoundsCollider` | (182.0,122.5) | BoxCollider | (182.0,0.9,122.5 / 4.2,2.0,5.0) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_BoundsCollider` | (234.0,33.5) | BoxCollider | (234.0,0.9,33.5 / 4.2,2.0,5.0) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_BoundsCollider` | (234.0,-33.5) | BoxCollider | (234.0,0.9,-33.5 / 4.2,2.0,5.0) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_BoundsCollider` | (182.0,-137.5) | BoxCollider | (182.0,0.9,-137.5 / 4.2,2.0,5.0) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_BoundsCollider` | (234.0,-18.5) | BoxCollider | (234.0,0.9,-18.5 / 4.2,2.0,5.0) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_BoundsCollider` | (234.0,122.5) | BoxCollider | (234.0,0.9,122.5 / 4.2,2.0,5.0) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_BoundsCollider` | (182.0,-137.5) | BoxCollider | (182.0,0.9,-137.5 / 4.2,2.0,5.0) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_BoundsCollider` | (182.0,85.5) | BoxCollider | (182.0,0.9,85.5 / 4.2,2.0,5.0) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_BoundsCollider` | (182.0,-122.5) | BoxCollider | (182.0,0.9,-122.5 / 4.2,2.0,5.0) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_BoundsCollider` | (234.0,18.5) | BoxCollider | (234.0,0.9,18.5 / 4.2,2.0,5.0) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_BoundsCollider` | (182.0,85.5) | BoxCollider | (182.0,0.9,85.5 / 4.2,2.0,5.0) | OK |
| `Environment/OBD_ArtPass/Decal_Crosswalk_BoundsCollider` | (182.0,33.5) | BoxCollider | (182.0,0.9,33.5 / 4.2,2.0,5.0) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (211.0,32.0) | BoxCollider | (211.0,1.0,32.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (174.0,32.0) | BoxCollider | (174.0,1.0,32.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (190.0,20.0) | BoxCollider | (190.0,1.0,20.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (214.0,32.0) | BoxCollider | (214.0,1.0,32.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (214.0,20.0) | BoxCollider | (214.0,1.0,20.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (150.0,20.0) | BoxCollider | (150.0,1.0,20.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (138.0,20.0) | BoxCollider | (138.0,1.0,20.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (144.0,20.0) | BoxCollider | (144.0,1.0,20.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (190.0,32.0) | BoxCollider | (190.0,1.0,32.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (199.0,20.0) | BoxCollider | (199.0,1.0,20.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (168.0,32.0) | BoxCollider | (168.0,1.0,32.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (196.0,20.0) | BoxCollider | (196.0,1.0,20.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (147.0,20.0) | BoxCollider | (147.0,1.0,20.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (153.0,20.0) | BoxCollider | (153.0,1.0,20.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (168.0,20.0) | BoxCollider | (168.0,1.0,20.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (141.0,20.0) | BoxCollider | (141.0,1.0,20.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (156.0,20.0) | BoxCollider | (156.0,1.0,20.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (159.0,32.0) | BoxCollider | (159.0,1.0,32.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (171.0,32.0) | BoxCollider | (171.0,1.0,32.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (141.0,32.0) | BoxCollider | (141.0,1.0,32.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (168.0,32.0) | BoxCollider | (168.0,1.0,32.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (196.0,20.0) | BoxCollider | (196.0,1.0,20.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (144.0,20.0) | BoxCollider | (144.0,1.0,20.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (211.0,20.0) | BoxCollider | (211.0,1.0,20.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (141.0,32.0) | BoxCollider | (141.0,1.0,32.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (190.0,20.0) | BoxCollider | (190.0,1.0,20.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (208.0,20.0) | BoxCollider | (208.0,1.0,20.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (156.0,32.0) | BoxCollider | (156.0,1.0,32.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (147.0,20.0) | BoxCollider | (147.0,1.0,20.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (223.0,20.0) | BoxCollider | (223.0,1.0,20.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (171.0,32.0) | BoxCollider | (171.0,1.0,32.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (226.0,20.0) | BoxCollider | (226.0,1.0,20.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (199.0,32.0) | BoxCollider | (199.0,1.0,32.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (202.0,20.0) | BoxCollider | (202.0,1.0,20.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (174.0,32.0) | BoxCollider | (174.0,1.0,32.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (138.0,20.0) | BoxCollider | (138.0,1.0,20.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (162.0,20.0) | BoxCollider | (162.0,1.0,20.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (156.0,20.0) | BoxCollider | (156.0,1.0,20.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (208.0,20.0) | BoxCollider | (208.0,1.0,20.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (162.0,32.0) | BoxCollider | (162.0,1.0,32.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (226.0,32.0) | BoxCollider | (226.0,1.0,32.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (165.0,20.0) | BoxCollider | (165.0,1.0,20.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (193.0,20.0) | BoxCollider | (193.0,1.0,20.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (141.0,32.0) | BoxCollider | (141.0,1.0,32.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (147.0,32.0) | BoxCollider | (147.0,1.0,32.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (193.0,32.0) | BoxCollider | (193.0,1.0,32.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (141.0,20.0) | BoxCollider | (141.0,1.0,20.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (214.0,32.0) | BoxCollider | (214.0,1.0,32.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (208.0,32.0) | BoxCollider | (208.0,1.0,32.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (153.0,20.0) | BoxCollider | (153.0,1.0,20.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (171.0,20.0) | BoxCollider | (171.0,1.0,20.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (190.0,32.0) | BoxCollider | (190.0,1.0,32.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (138.0,32.0) | BoxCollider | (138.0,1.0,32.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (159.0,32.0) | BoxCollider | (159.0,1.0,32.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (153.0,32.0) | BoxCollider | (153.0,1.0,32.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (193.0,20.0) | BoxCollider | (193.0,1.0,20.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (196.0,32.0) | BoxCollider | (196.0,1.0,32.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (202.0,20.0) | BoxCollider | (202.0,1.0,20.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (168.0,20.0) | BoxCollider | (168.0,1.0,20.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (150.0,20.0) | BoxCollider | (150.0,1.0,20.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (217.0,32.0) | BoxCollider | (217.0,1.0,32.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (223.0,32.0) | BoxCollider | (223.0,1.0,32.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (223.0,32.0) | BoxCollider | (223.0,1.0,32.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (205.0,20.0) | BoxCollider | (205.0,1.0,20.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (205.0,32.0) | BoxCollider | (205.0,1.0,32.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (153.0,32.0) | BoxCollider | (153.0,1.0,32.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (220.0,32.0) | BoxCollider | (220.0,1.0,32.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (153.0,20.0) | BoxCollider | (153.0,1.0,20.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (214.0,20.0) | BoxCollider | (214.0,1.0,20.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (159.0,20.0) | BoxCollider | (159.0,1.0,20.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (202.0,32.0) | BoxCollider | (202.0,1.0,32.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (196.0,32.0) | BoxCollider | (196.0,1.0,32.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (208.0,32.0) | BoxCollider | (208.0,1.0,32.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (220.0,32.0) | BoxCollider | (220.0,1.0,32.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (193.0,20.0) | BoxCollider | (193.0,1.0,20.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (174.0,32.0) | BoxCollider | (174.0,1.0,32.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (171.0,20.0) | BoxCollider | (171.0,1.0,20.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (226.0,32.0) | BoxCollider | (226.0,1.0,32.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (171.0,32.0) | BoxCollider | (171.0,1.0,32.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (193.0,32.0) | BoxCollider | (193.0,1.0,32.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (205.0,32.0) | BoxCollider | (205.0,1.0,32.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (165.0,32.0) | BoxCollider | (165.0,1.0,32.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (165.0,32.0) | BoxCollider | (165.0,1.0,32.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (226.0,20.0) | BoxCollider | (226.0,1.0,20.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (168.0,32.0) | BoxCollider | (168.0,1.0,32.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (199.0,20.0) | BoxCollider | (199.0,1.0,20.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (162.0,20.0) | BoxCollider | (162.0,1.0,20.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (217.0,20.0) | BoxCollider | (217.0,1.0,20.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (162.0,32.0) | BoxCollider | (162.0,1.0,32.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (138.0,32.0) | BoxCollider | (138.0,1.0,32.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (153.0,32.0) | BoxCollider | (153.0,1.0,32.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (220.0,32.0) | BoxCollider | (220.0,1.0,32.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (159.0,32.0) | BoxCollider | (159.0,1.0,32.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (217.0,20.0) | BoxCollider | (217.0,1.0,20.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (196.0,20.0) | BoxCollider | (196.0,1.0,20.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (202.0,32.0) | BoxCollider | (202.0,1.0,32.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (193.0,32.0) | BoxCollider | (193.0,1.0,32.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (174.0,20.0) | BoxCollider | (174.0,1.0,20.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (138.0,20.0) | BoxCollider | (138.0,1.0,20.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (162.0,32.0) | BoxCollider | (162.0,1.0,32.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (214.0,20.0) | BoxCollider | (214.0,1.0,20.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (199.0,32.0) | BoxCollider | (199.0,1.0,32.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (156.0,20.0) | BoxCollider | (156.0,1.0,20.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (162.0,20.0) | BoxCollider | (162.0,1.0,20.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (165.0,20.0) | BoxCollider | (165.0,1.0,20.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (150.0,32.0) | BoxCollider | (150.0,1.0,32.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (214.0,32.0) | BoxCollider | (214.0,1.0,32.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (211.0,32.0) | BoxCollider | (211.0,1.0,32.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (223.0,20.0) | BoxCollider | (223.0,1.0,20.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (217.0,20.0) | BoxCollider | (217.0,1.0,20.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (226.0,20.0) | BoxCollider | (226.0,1.0,20.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (211.0,20.0) | BoxCollider | (211.0,1.0,20.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (205.0,20.0) | BoxCollider | (205.0,1.0,20.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (223.0,32.0) | BoxCollider | (223.0,1.0,32.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (138.0,32.0) | BoxCollider | (138.0,1.0,32.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (199.0,32.0) | BoxCollider | (199.0,1.0,32.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (147.0,32.0) | BoxCollider | (147.0,1.0,32.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (168.0,20.0) | BoxCollider | (168.0,1.0,20.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (202.0,32.0) | BoxCollider | (202.0,1.0,32.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (208.0,32.0) | BoxCollider | (208.0,1.0,32.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (217.0,32.0) | BoxCollider | (217.0,1.0,32.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (165.0,32.0) | BoxCollider | (165.0,1.0,32.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (199.0,20.0) | BoxCollider | (199.0,1.0,20.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (150.0,20.0) | BoxCollider | (150.0,1.0,20.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (159.0,20.0) | BoxCollider | (159.0,1.0,20.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (211.0,32.0) | BoxCollider | (211.0,1.0,32.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (171.0,20.0) | BoxCollider | (171.0,1.0,20.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (190.0,32.0) | BoxCollider | (190.0,1.0,32.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (156.0,32.0) | BoxCollider | (156.0,1.0,32.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (141.0,20.0) | BoxCollider | (141.0,1.0,20.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (144.0,32.0) | BoxCollider | (144.0,1.0,32.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (196.0,32.0) | BoxCollider | (196.0,1.0,32.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (147.0,20.0) | BoxCollider | (147.0,1.0,20.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (190.0,20.0) | BoxCollider | (190.0,1.0,20.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (150.0,32.0) | BoxCollider | (150.0,1.0,32.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (165.0,20.0) | BoxCollider | (165.0,1.0,20.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (226.0,32.0) | BoxCollider | (226.0,1.0,32.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (208.0,20.0) | BoxCollider | (208.0,1.0,20.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (159.0,20.0) | BoxCollider | (159.0,1.0,20.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (174.0,20.0) | BoxCollider | (174.0,1.0,20.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (217.0,32.0) | BoxCollider | (217.0,1.0,32.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (156.0,32.0) | BoxCollider | (156.0,1.0,32.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (220.0,20.0) | BoxCollider | (220.0,1.0,20.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (144.0,32.0) | BoxCollider | (144.0,1.0,32.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (144.0,32.0) | BoxCollider | (144.0,1.0,32.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (202.0,20.0) | BoxCollider | (202.0,1.0,20.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (220.0,20.0) | BoxCollider | (220.0,1.0,20.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (205.0,32.0) | BoxCollider | (205.0,1.0,32.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (150.0,32.0) | BoxCollider | (150.0,1.0,32.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (205.0,20.0) | BoxCollider | (205.0,1.0,20.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (220.0,20.0) | BoxCollider | (220.0,1.0,20.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (147.0,32.0) | BoxCollider | (147.0,1.0,32.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (211.0,20.0) | BoxCollider | (211.0,1.0,20.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (144.0,20.0) | BoxCollider | (144.0,1.0,20.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (223.0,20.0) | BoxCollider | (223.0,1.0,20.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Sidewalk_Straight_3m_BoundsCollider` | (174.0,20.0) | BoxCollider | (174.0,1.0,20.0 / 2.8,2.1,2.8) | OK |
| `Environment/OBD_ArtPass/Decal_Stop_BoundsCollider` | (174.0,-81.0) | BoxCollider | (174.0,0.9,-81.0 / 1.4,2.0,2.7) | OK |
| `Environment/OBD_ArtPass/Decal_Stop_BoundsCollider` | (174.0,-29.0) | BoxCollider | (174.0,0.9,-29.0 / 1.4,2.0,2.7) | OK |
| `Environment/OBD_ArtPass/Decal_Stop_BoundsCollider` | (226.0,75.0) | BoxCollider | (226.0,0.9,75.0 / 1.4,2.0,2.7) | OK |
| `Environment/OBD_ArtPass/Decal_Stop_BoundsCollider` | (226.0,23.0) | BoxCollider | (226.0,0.9,23.0 / 1.4,2.0,2.7) | OK |
| `Environment/OBD_ArtPass/Decal_Stop_BoundsCollider` | (226.0,-29.0) | BoxCollider | (226.0,0.9,-29.0 / 1.4,2.0,2.7) | OK |
| `Environment/OBD_ArtPass/Decal_Stop_BoundsCollider` | (226.0,-29.0) | BoxCollider | (226.0,0.9,-29.0 / 1.4,2.0,2.7) | OK |
| `Environment/OBD_ArtPass/Decal_Stop_BoundsCollider` | (226.0,23.0) | BoxCollider | (226.0,0.9,23.0 / 1.4,2.0,2.7) | OK |
| `Environment/OBD_ArtPass/Decal_Stop_BoundsCollider` | (174.0,-29.0) | BoxCollider | (174.0,0.9,-29.0 / 1.4,2.0,2.7) | OK |
| `Environment/OBD_ArtPass/Decal_Stop_BoundsCollider` | (174.0,75.0) | BoxCollider | (174.0,0.9,75.0 / 1.4,2.0,2.7) | OK |
| `Environment/OBD_ArtPass/Decal_Stop_BoundsCollider` | (174.0,-81.0) | BoxCollider | (174.0,0.9,-81.0 / 1.4,2.0,2.7) | OK |
| `Environment/OBD_ArtPass/Decal_Stop_BoundsCollider` | (174.0,127.0) | BoxCollider | (174.0,0.9,127.0 / 1.4,2.0,2.7) | OK |
| `Environment/OBD_ArtPass/Decal_Stop_BoundsCollider` | (174.0,-133.0) | BoxCollider | (174.0,0.9,-133.0 / 1.4,2.0,2.7) | OK |
| `Environment/OBD_ArtPass/Decal_Stop_BoundsCollider` | (226.0,-81.0) | BoxCollider | (226.0,0.9,-81.0 / 1.4,2.0,2.7) | OK |
| `Environment/OBD_ArtPass/Decal_Stop_BoundsCollider` | (226.0,75.0) | BoxCollider | (226.0,0.9,75.0 / 1.4,2.0,2.7) | OK |
| `Environment/OBD_ArtPass/Decal_Stop_BoundsCollider` | (174.0,23.0) | BoxCollider | (174.0,0.9,23.0 / 1.4,2.0,2.7) | OK |
| `Environment/OBD_ArtPass/Decal_Stop_BoundsCollider` | (226.0,-133.0) | BoxCollider | (226.0,0.9,-133.0 / 1.4,2.0,2.7) | OK |
| `Environment/OBD_ArtPass/Decal_Stop_BoundsCollider` | (174.0,-133.0) | BoxCollider | (174.0,0.9,-133.0 / 1.4,2.0,2.7) | OK |
| `Environment/OBD_ArtPass/Decal_Stop_BoundsCollider` | (174.0,-133.0) | BoxCollider | (174.0,0.9,-133.0 / 1.4,2.0,2.7) | OK |
| `Environment/OBD_ArtPass/Decal_Stop_BoundsCollider` | (226.0,23.0) | BoxCollider | (226.0,0.9,23.0 / 1.4,2.0,2.7) | OK |
| `Environment/OBD_ArtPass/Decal_Stop_BoundsCollider` | (174.0,127.0) | BoxCollider | (174.0,0.9,127.0 / 1.4,2.0,2.7) | OK |
| `Environment/OBD_ArtPass/Decal_Stop_BoundsCollider` | (226.0,127.0) | BoxCollider | (226.0,0.9,127.0 / 1.4,2.0,2.7) | OK |
| `Environment/OBD_ArtPass/Decal_Stop_BoundsCollider` | (226.0,-29.0) | BoxCollider | (226.0,0.9,-29.0 / 1.4,2.0,2.7) | OK |
| `Environment/OBD_ArtPass/Decal_Stop_BoundsCollider` | (174.0,75.0) | BoxCollider | (174.0,0.9,75.0 / 1.4,2.0,2.7) | OK |
| `Environment/OBD_ArtPass/Decal_Stop_BoundsCollider` | (174.0,23.0) | BoxCollider | (174.0,0.9,23.0 / 1.4,2.0,2.7) | OK |
| `Environment/OBD_ArtPass/Decal_Stop_BoundsCollider` | (226.0,-81.0) | BoxCollider | (226.0,0.9,-81.0 / 1.4,2.0,2.7) | OK |
| `Environment/OBD_ArtPass/Decal_Stop_BoundsCollider` | (226.0,75.0) | BoxCollider | (226.0,0.9,75.0 / 1.4,2.0,2.7) | OK |
| `Environment/OBD_ArtPass/Decal_Stop_BoundsCollider` | (226.0,127.0) | BoxCollider | (226.0,0.9,127.0 / 1.4,2.0,2.7) | OK |
| `Environment/OBD_ArtPass/Decal_Stop_BoundsCollider` | (174.0,75.0) | BoxCollider | (174.0,0.9,75.0 / 1.4,2.0,2.7) | OK |
| `Environment/OBD_ArtPass/Decal_Stop_BoundsCollider` | (174.0,127.0) | BoxCollider | (174.0,0.9,127.0 / 1.4,2.0,2.7) | OK |
| `Environment/OBD_ArtPass/Decal_Stop_BoundsCollider` | (226.0,-133.0) | BoxCollider | (226.0,0.9,-133.0 / 1.4,2.0,2.7) | OK |
| `Environment/OBD_ArtPass/Decal_Stop_BoundsCollider` | (174.0,-29.0) | BoxCollider | (174.0,0.9,-29.0 / 1.4,2.0,2.7) | OK |
| `Environment/OBD_ArtPass/Decal_Stop_BoundsCollider` | (174.0,-81.0) | BoxCollider | (174.0,0.9,-81.0 / 1.4,2.0,2.7) | OK |
| `Environment/OBD_ArtPass/Decal_Stop_BoundsCollider` | (226.0,-81.0) | BoxCollider | (226.0,0.9,-81.0 / 1.4,2.0,2.7) | OK |
| `Environment/OBD_ArtPass/Decal_Stop_BoundsCollider` | (174.0,23.0) | BoxCollider | (174.0,0.9,23.0 / 1.4,2.0,2.7) | OK |
| `Environment/OBD_ArtPass/Decal_Stop_BoundsCollider` | (226.0,-133.0) | BoxCollider | (226.0,0.9,-133.0 / 1.4,2.0,2.7) | OK |
| `Environment/OBD_ArtPass/Decal_Stop_BoundsCollider` | (226.0,127.0) | BoxCollider | (226.0,0.9,127.0 / 1.4,2.0,2.7) | OK |
| `Environment/OBD_ArtPass/Decal_DoubleYellow_Straight_BoundsCollider` | (234.0,117.0) | BoxCollider | (234.0,0.9,117.0 / 5.5,2.0,0.4) | OK |
| `Environment/OBD_ArtPass/Decal_DoubleYellow_Straight_BoundsCollider` | (182.0,-117.0) | BoxCollider | (182.0,0.9,-117.0 / 5.5,2.0,0.4) | OK |
| `Environment/OBD_ArtPass/Decal_DoubleYellow_Straight_BoundsCollider` | (182.0,-117.0) | BoxCollider | (182.0,0.9,-117.0 / 5.5,2.0,0.4) | OK |
| `Environment/OBD_ArtPass/Decal_DoubleYellow_Straight_BoundsCollider` | (182.0,91.0) | BoxCollider | (182.0,0.9,91.0 / 5.5,2.0,0.4) | OK |
| `Environment/OBD_ArtPass/Decal_DoubleYellow_Straight_BoundsCollider` | (234.0,117.0) | BoxCollider | (234.0,0.9,117.0 / 5.5,2.0,0.4) | OK |
| `Environment/OBD_ArtPass/Decal_DoubleYellow_Straight_BoundsCollider` | (182.0,13.0) | BoxCollider | (182.0,0.9,13.0 / 5.5,2.0,0.4) | OK |
| `Environment/OBD_ArtPass/Decal_DoubleYellow_Straight_BoundsCollider` | (234.0,-13.0) | BoxCollider | (234.0,0.9,-13.0 / 5.5,2.0,0.4) | OK |
| `Environment/OBD_ArtPass/Decal_DoubleYellow_Straight_BoundsCollider` | (182.0,91.0) | BoxCollider | (182.0,0.9,91.0 / 5.5,2.0,0.4) | OK |
| `Environment/OBD_ArtPass/Decal_DoubleYellow_Straight_BoundsCollider` | (182.0,-13.0) | BoxCollider | (182.0,0.9,-13.0 / 5.5,2.0,0.4) | OK |
| `Environment/OBD_ArtPass/Decal_DoubleYellow_Straight_BoundsCollider` | (234.0,-13.0) | BoxCollider | (234.0,0.9,-13.0 / 5.5,2.0,0.4) | OK |
| `Environment/OBD_ArtPass/Decal_DoubleYellow_Straight_BoundsCollider` | (234.0,-117.0) | BoxCollider | (234.0,0.9,-117.0 / 5.5,2.0,0.4) | OK |
| `Environment/OBD_ArtPass/Decal_DoubleYellow_Straight_BoundsCollider` | (182.0,13.0) | BoxCollider | (182.0,0.9,13.0 / 5.5,2.0,0.4) | OK |
| `Environment/OBD_ArtPass/Decal_DoubleYellow_Straight_BoundsCollider` | (234.0,-117.0) | BoxCollider | (234.0,0.9,-117.0 / 5.5,2.0,0.4) | OK |
| `Environment/OBD_ArtPass/Decal_DoubleYellow_Straight_BoundsCollider` | (234.0,117.0) | BoxCollider | (234.0,0.9,117.0 / 5.5,2.0,0.4) | OK |
| `Environment/OBD_ArtPass/Decal_DoubleYellow_Straight_BoundsCollider` | (234.0,-91.0) | BoxCollider | (234.0,0.9,-91.0 / 5.5,2.0,0.4) | OK |
| `Environment/OBD_ArtPass/Decal_DoubleYellow_Straight_BoundsCollider` | (234.0,91.0) | BoxCollider | (234.0,0.9,91.0 / 5.5,2.0,0.4) | OK |
| `Environment/OBD_ArtPass/Decal_DoubleYellow_Straight_BoundsCollider` | (182.0,-91.0) | BoxCollider | (182.0,0.9,-91.0 / 5.5,2.0,0.4) | OK |
| `Environment/OBD_ArtPass/Decal_DoubleYellow_Straight_BoundsCollider` | (234.0,-91.0) | BoxCollider | (234.0,0.9,-91.0 / 5.5,2.0,0.4) | OK |
| `Environment/OBD_ArtPass/Decal_DoubleYellow_Straight_BoundsCollider` | (234.0,91.0) | BoxCollider | (234.0,0.9,91.0 / 5.5,2.0,0.4) | OK |
| `Environment/OBD_ArtPass/Decal_DoubleYellow_Straight_BoundsCollider` | (234.0,13.0) | BoxCollider | (234.0,0.9,13.0 / 5.5,2.0,0.4) | OK |
| `Environment/OBD_ArtPass/Decal_DoubleYellow_Straight_BoundsCollider` | (182.0,-117.0) | BoxCollider | (182.0,0.9,-117.0 / 5.5,2.0,0.4) | OK |
| `Environment/OBD_ArtPass/Decal_DoubleYellow_Straight_BoundsCollider` | (182.0,13.0) | BoxCollider | (182.0,0.9,13.0 / 5.5,2.0,0.4) | OK |
| `Environment/OBD_ArtPass/Decal_DoubleYellow_Straight_BoundsCollider` | (182.0,-91.0) | BoxCollider | (182.0,0.9,-91.0 / 5.5,2.0,0.4) | OK |
| `Environment/OBD_ArtPass/Decal_DoubleYellow_Straight_BoundsCollider` | (182.0,-13.0) | BoxCollider | (182.0,0.9,-13.0 / 5.5,2.0,0.4) | OK |
| `Environment/OBD_ArtPass/Decal_DoubleYellow_Straight_BoundsCollider` | (182.0,117.0) | BoxCollider | (182.0,0.9,117.0 / 5.5,2.0,0.4) | OK |
| `Environment/OBD_ArtPass/Decal_DoubleYellow_Straight_BoundsCollider` | (234.0,-13.0) | BoxCollider | (234.0,0.9,-13.0 / 5.5,2.0,0.4) | OK |
| `Environment/OBD_ArtPass/Decal_DoubleYellow_Straight_BoundsCollider` | (234.0,-91.0) | BoxCollider | (234.0,0.9,-91.0 / 5.5,2.0,0.4) | OK |
| `Environment/OBD_ArtPass/Decal_DoubleYellow_Straight_BoundsCollider` | (234.0,-117.0) | BoxCollider | (234.0,0.9,-117.0 / 5.5,2.0,0.4) | OK |
| `Environment/OBD_ArtPass/Decal_DoubleYellow_Straight_BoundsCollider` | (182.0,91.0) | BoxCollider | (182.0,0.9,91.0 / 5.5,2.0,0.4) | OK |
| `Environment/OBD_ArtPass/Decal_DoubleYellow_Straight_BoundsCollider` | (234.0,13.0) | BoxCollider | (234.0,0.9,13.0 / 5.5,2.0,0.4) | OK |
| `Environment/OBD_ArtPass/Decal_DoubleYellow_Straight_BoundsCollider` | (234.0,13.0) | BoxCollider | (234.0,0.9,13.0 / 5.5,2.0,0.4) | OK |
| `Environment/OBD_ArtPass/Decal_DoubleYellow_Straight_BoundsCollider` | (182.0,-13.0) | BoxCollider | (182.0,0.9,-13.0 / 5.5,2.0,0.4) | OK |
| `Environment/OBD_ArtPass/Decal_DoubleYellow_Straight_BoundsCollider` | (234.0,91.0) | BoxCollider | (234.0,0.9,91.0 / 5.5,2.0,0.4) | OK |
| `Environment/OBD_ArtPass/Decal_DoubleYellow_Straight_BoundsCollider` | (182.0,117.0) | BoxCollider | (182.0,0.9,117.0 / 5.5,2.0,0.4) | OK |
| `Environment/OBD_ArtPass/Decal_DoubleYellow_Straight_BoundsCollider` | (182.0,-91.0) | BoxCollider | (182.0,0.9,-91.0 / 5.5,2.0,0.4) | OK |
| `Environment/OBD_ArtPass/Decal_DoubleYellow_Straight_BoundsCollider` | (182.0,117.0) | BoxCollider | (182.0,0.9,117.0 / 5.5,2.0,0.4) | OK |

## Detalhe — colisores adicionados FORA da via (inventário completo)

0 itens (tabela completa no JSON)

## Materiais trocados em placas planas largas (via/calçada)

| Objeto | Pos | Antes | Depois |
|---|---|---|---|
| `Environment/Roads/Crosswalk_Street_-1` | (26.0,17.8) | Mat_ND_Crosswalk | Mat_Kit_NatureKit |
| `Environment/Roads/Street_EW_0` | (0.0,-130.0) | Mat_ND_Road_Street | Mat_Kit_NatureKit |
| `Environment/Roads/Street_NS_1` | (-78.0,0.0) | Mat_ND_Road_Street | Mat_Kit_NatureKit |
| `Environment/Roads/Street_NS_0` | (-130.0,0.0) | Mat_ND_Road_Street | Mat_Kit_NatureKit |
| `Environment/Roads/Street_NS_4` | (78.0,0.0) | Mat_ND_Road_Street | Mat_Kit_NatureKit |
| `Environment/Roads/Street_NS_2` | (-26.0,0.0) | Mat_ND_Road_Street | Mat_Kit_NatureKit |
| `Environment/Roads/Street_NS_3` | (26.0,0.0) | Mat_ND_Road_Street | Mat_Kit_NatureKit |
| `Environment/Roads/Street_EW_1` | (0.0,-78.0) | Mat_ND_Road_Street | Mat_Kit_NatureKit |
| `Environment/Roads/Street_NS_5` | (130.0,0.0) | Mat_ND_Road_Street | Mat_Kit_NatureKit |
| `Environment/Roads/Crosswalk_Street_1` | (26.0,34.2) | Mat_ND_Crosswalk | Mat_Kit_NatureKit |
| `Environment/Roads/Street_EW_4` | (0.0,78.0) | Mat_ND_Road_Street | Mat_Kit_NatureKit |
| `Environment/Roads/Street_EW_2` | (0.0,-26.0) | Mat_ND_Road_Street | Mat_Kit_NatureKit |
| `Environment/Roads/Street_EW_5` | (0.0,130.0) | Mat_ND_Road_Street | Mat_Kit_NatureKit |

## Nomes de objetos adicionados (agregado por nome)

- `Sidewalk_Straight_3m_BoundsCollider` × 156
- `LampPointLight` × 80
- `Decal_Crosswalk_BoundsCollider` × 72
- `Decal_DoubleYellow_Straight_BoundsCollider` × 57
- `Decal_Crosswalk_Wide_BoundsCollider` × 36
- `Decal_Stop_BoundsCollider` × 36
- `AsphaltPatch_Plaza_BoundsCollider` × 4
---

# ANÁLISE FINAL M1 (24/10) — veredito por classe de dano

## O que a sessão v3.6+"realçar" (commit 50354cdc) fez à cena

### DANO 1 — 361 colisores invisíveis (M2 remove TODOS)
Todos criados por `ColliderFitter.FitProp` com perfil `Full()` (`extraTop=2m`):
| Classe | Qtd | Problema |
|---|---|---|
| `Decal_DoubleYellow_Straight_BoundsCollider` | 57 | caixa 5,5x2,0x0,4m sobre o eixo amarelo (avenida/OBD) |
| `Decal_Crosswalk_BoundsCollider` | 72 | caixa 4,2x2,0x5,0m sobre faixa de pedestre |
| `Decal_Crosswalk_Wide_BoundsCollider` | 36 | caixa 10,5x2,0x4,3m sobre faixa larga |
| `Decal_Stop_BoundsCollider` | 36 | caixa 1,4x2,0x2,7m sobre linha de stop |
| `Sidewalk_Straight_3m_BoundsCollider` | 156 | calçada de 15cm virou muro de 2,1x2,8m |
| `AsphaltPatch_Plaza_BoundsCollider` | 4 | caixa 8,3x2,0x8,3m no meio de cruzamentos da praça |
**Veredito: REVERTER (remover os 361).** Decal é tinta no chão (regra: nunca colisor);
calçada/patch são pisos planos dirigíveis. Causa raiz: FIX-5 `FixRoadFragments` aplicou
FitProp em qualquer objeto >1m2 sem colisor e com nome fora da whitelist de ruas.

### DANO 2 — 13 pisos de via com material de VEGETAÇÃO (M3 corrige)
`FixWhiteTrees` (FIX-3) viu `mainTexture==null` em materiais de rua e os trocou por
`Mat_Kit_NatureKit` (verde natureza). Afetou: Street_EW_{0,1,2,4,5}, Street_NS_{0..5},
Crosswalk_Street_{-1,1}. **Veredito: REVERTER para asfalto correto** (escolha do dono em M3).

### DANO 3 — ~270 letras neon + 74 NeonSign destruídos (MAIS que o documentado)
PROGRESS v3.6 dizia "18 removidos"; inventário real: praticamente TODOS os letreiros
do art pass A3 (Letter_A-Z, NeonSign, Cube/Text filhos) sumiram. `FixFlyingSigns`
destruía qualquer letreiro sem fachada a <=2m em raycast horizontal (marquees,
fachadas recuadas, letreiros internos). A3 (zona sagrada: neon Letter_A-Z) foi
approved pelo dono. **Veredito: REVERTER (restaurar do pre-scene cdf35da9).**

### DANO 4 — 42 `Decal_BrokenLine_Straight` destruídos
Nome contém "Broken" (linha tracejada!) -> filtro de "fragmentos broken" do FIX-5
deletou as marcações de faixa da avenida. **Veredito: REVERTER.**

### MANTIDO (bom, não reverter)
- 80 `LampPointLight` (luzes reais de poste — dívida: medir FPS em M2/M4)
- 85 decals re-tiered (z-fight fix) — sem dano
- 297 árvores rematerializadas — ok (UVs ainda por conferir)
- Remoção de junk/debris perto de (219,-102.9) — intencional (campo parcialmente limpo)
