# ADR-005: Assets 3D Originais via Ponte Fábrica 3D (100% Local)

- **Status:** Aceito
- **Data:** 2026-11-21
- **Autor:** Sessão z.ai (planejamento da integração) — revisão do dono pendente

## Contexto

O studio já entrega jogos completos (Neon Rush, Abyssal Breach, NEON DISTRICT) sob o
Quality Gate V2: assets de packs CC0 (Kenney, Poly Haven), kitbash modular e
normalização headless via `tools/art/normalize_fbx.py` / `fbx_canonical.py`. Para peças
de rascunho existe um gerador local open-source (TripoSR) em
`tools/art/ai_generator/generate_model.py`, mas sua qualidade de malha/UV não alcança
peças finais.

Em paralelo, o dono mantém um segundo setup local — o repo `3dprinter-setup-ia`
("Fábrica 3D") — com uma pipeline local madura de assets 3D texturizados de alta
qualidade: `pipeline/run_asset.py` (00 preflight UV → 01 conditioning → 02
Hunyuan3D-Paint 2048 → 04 patch repair → 05 TRELLIS.2-4B MetallicRoughness → 06 pack
GLB 4K/8K JPEG q95 + MR 2048 PNG → 07 QA gates → 08 publish), com venvs `hunyuan` e
`trellis2` no WSL2. A proibição definitiva de serviços externos de geração em nuvem
está registrada no ADR-009 daquele repo (regra permanente: tudo 100% local).

O objetivo desta decisão é consumir peças 3D **originais** geradas localmente pela
Fábrica 3D dentro dos jogos do studio, sem violar o Quality Gate V2 (budgets de
`docs/art/art-spec.yaml`, FBX canônico, materiais URP Lit com `_BaseMap`, zero
primitivas nativas).

## Avaliação das Alternativas

1. **glTFast (`com.unity.cloud.gltfast`) importando o GLB diretamente:**
   - *Prós:* importação rápida; mapeia metallicRoughness nativamente para URP.
   - *Contras:* cria uma segunda classe de assets fora da política V2 — o validador
     (`validate_asset.py`) espera FBX com UV0 e materiais URP Lit `_BaseMap`; os
     settings de textura (ASTC por categoria) e os pivôs/eixos canônicos precisariam
     de um caminho paralelo; adiciona dependência de pacote ao projeto.
2. **Ponte Blender WSL → `fbx_canonical` → FBX canônico + importer URP no Editor (adotada):**
   - *Decisão:* reusar `normalize_fbx.py`/`fbx_canonical.py` (pivôs
     prop/weapon/character, triangulação, UV, política anti-rotação-residual), o
     padrão dos importers Editor C# (`ModularKitImporter` — materiais URP Lit +
     prefab + relatório JSON em `evidence/`) e o gate `validate_asset.py`. A ponte
     decima a malha até o budget da categoria e reduz as texturas do GLB
     print-grade (4K/8K) para os tamanhos do `art-spec.yaml` (512/1024), garantindo
     os budgets mobile. Zero pacotes e zero downloads novos: usa o Blender 5.0.1
     que já roda no WSL2.
3. **Manter TripoSR (local, open-source) como gerador principal:**
   - *Decisão:* mantido apenas como camada de rascunho (`generate_model.py`
     inalterado). Qualidade de malha/UV inferior à pipeline da Fábrica; não serve
     para peças finais de jogo.

## Consequências

- O Quality Gate V2 passa a permitir uma fonte adicional de assets: saída da ponte
  Fábrica 3D, sempre via `tools/art/ai_generator/fab3d_bridge.py` + menu
  `Studio > Fab3D > Import Generated Assets` (importer `Fab3dImporter`).
- GLB print-grade nunca entra direto no Unity: toda peça é decimada ao budget da
  categoria e re-texturizada nos tamanhos do `art-spec.yaml` (compressão ASTC no
  import), dentro da ponte.
- Custo de disco/rede zero para adotar a ponte (Blender já instalado). O front-end
  de geração de imagens de conceito (text-to-image local) fica **explicitamente
  adiado** até liberação de armazenamento no PC e **não bloqueia** a ponte: o
  contrato de entrada é e continua sendo um GLB (ou uma imagem, quando essa fase
  existir).
- Toda peça de jogo gera evidência rastreável: `evidence/art/fab3d_bridge_<nome>.json`
  (relatório Blender/decimate/texturas), `evidence/art/fab3d-import-report.json`
  (materiais/prefabs) e captura in-game em `evidence/captures/`.
- Regra permanente reafirmada: nenhuma etapa desta ponte chama serviço externo de
  geração/texturização; ver ADR-009 do repo `3dprinter-setup-ia`.
