---
name: fab3d-asset
description: 'Gera e importa assets 3D ORIGINAIS, 100% locais, para os jogos do studio usando a ponte Fábrica 3D (GLB print-grade -> FBX canonico -> materiais URP -> prefab validado). Use quando o jogo precisar de props/personagens proprios, quando os packs CC0 nao cobrirem o item, ou quando o usuario pedir "peca propria/gerada por IA". Nao usar para mudancas em packs existentes.'
---

# Fab3D Asset — peças originais 100% locais para os jogos

**Goal:** produzir um asset de jogo original via ponte Fábrica 3D, aprovado no
Quality Gate V2, com evidência completa — sem nunca chamar serviço externo.

## Regras não negociáveis (leia antes de agir)

1. **100% local:** proibido qualquer serviço externo de geração/texturização em
   nuvem. A fonte é o repo `3dprinter-setup-ia` (Fábrica 3D) ou um GLB já publicado
   por ele. Ver ADR-005 (este repo) e ADR-009 (repo da Fábrica).
2. **Budgets são gate:** `docs/art/art-spec.yaml` manda (small_prop 600 tris / 512px,
   large_prop 3000 / 512px, player 4500 / 1024px, enemy 2500 / 1024px, ASTC). A ponte
   aplica decimate + downscale; nunca importe GLB print-grade direto no Unity.
3. **Zero primitivas:** material URP Lit com `_BaseMap` obrigatório; o importer
   `Fab3dImporter` é o único caminho de entrada (nada de mão na massa manual).

## Fluxo (siga nesta ordem)

1. Leia `docs/operations/fab3d-bridge-runbook.md` por inteiro — ele é a autoridade
   do fluxo, incluindo critérios de aceitação e troubleshooting.
2. Preencha um bloco em `docs/art/fab3d-asset-spec.yaml` com categoria, preset e
   origem (`from_glb` ou `fab3d_run`).
3. Rode **Modo 1 (`from_glb`)** primeiro — prova a ponte com um GLB já publicado.
   Só use **Modo 2 (`fab3d_run`)** com o ambiente da Fábrica confirmado
   (venvs no WSL, GPU livre) e depois de pelo menos um Modo 1 bem-sucedido.
4. Import no Unity: menu `Studio > Fab3D > Import Generated Assets` (ou batchmode
   `Studio.Editor.Fab3dImporter.RunImportBatch`). Exit 0 obrigatório.
5. Valide: `python tools/validation/validate_asset.py --asset <fbx>` — exit 0.
6. Evidência: `evidence/art/fab3d_bridge_<nome>.json` +
   `evidence/art/fab3d-import-report.json` + captura in-game em `evidence/captures/`.
7. Atualize `PROGRESS.md` (seção "Integração Fábrica 3D") com o resultado honesto —
   pendência registrada é melhor que pendência escondida.

## Quando PARAR e consultar o dono

- O decimate para cumprir o budget destruiria a silhueta/leitura da peça
  (decisão de arte, não técnica).
- Texturas com artefatos graves após o downscale de 4K/8K → 512px.
- Não existe GLB disponível **e** o ambiente da Fábrica está indisponível
  (não invente substituto primitivo — proibido pelo AGENTS.md §5/§6).
