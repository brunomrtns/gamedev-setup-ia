# M1 — Mapa de dano entre revisões da cena

- PRE: `inv_pre.json` · POST: `inv_final.json`
- Objetos: PRE=3686 POST=2322 · adicionados(+nomes únicos)=1 (80 instâncias) · removidos=26 (670 instâncias) · pareados=2105
- Colisores ADICIONADOS no POST: **4** · removidos: 4

## Colisores adicionados — classificação por geometria

| Veredito | Qtd | Na via | Exemplos |
|---|---|---|---|
| OK | 4 | 0 | parking meter@(216,35); parking meter@(148,35); parking meter@(164,35); parking meter@(200,35) |

## Detalhe — colisores adicionados QUE TOCAM A VIA (bloqueio potencial)

| Objeto | Pos | Tipo | Bounds (cx,cy,cz / sx,sy,sz) | Veredito |
|---|---|---|---|---|

## Detalhe — colisores adicionados FORA da via (inventário completo)

4 itens (tabela completa no JSON)
- `Environment/OBD_ArtPass/parking meter prefab/parking meter` @(216.0,34.6) BoxCollider [0.2x1.4x0.1] → OK
- `Environment/OBD_ArtPass/parking meter prefab/parking meter` @(148.0,34.6) BoxCollider [0.2x1.4x0.1] → OK
- `Environment/OBD_ArtPass/parking meter prefab/parking meter` @(164.0,34.6) BoxCollider [0.2x1.4x0.1] → OK
- `Environment/OBD_ArtPass/parking meter prefab/parking meter` @(200.0,34.6) BoxCollider [0.2x1.4x0.1] → OK

## Materiais trocados em placas planas largas (via/calçada)

(nenhum)

## Nomes de objetos adicionados (agregado por nome)

- `LampPointLight` × 80