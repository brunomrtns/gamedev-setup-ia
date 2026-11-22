---
name: studio-concept-2d
description: 'Gera conceitos 2D (entrada da Fabrica 3D), texturas 100% tileaveis (padding circular) e icones de UI localmente via tools/art/ai_generator/concept_bridge.py seguindo docs/art/art-bible.yaml. Use antes de gerar novos modelos 3D ou quando precisar de texturas de chao/parede ou UI.'
---

# Studio Concept 2D — Conceitos, Texturas Tileáveis e UI 100% Locais

**Goal:** fechar o ciclo `texto → imagem 2D (estilo da Art Bible) → Fábrica 3D → Unity`,
além de gerar texturas tileáveis e ícones de UI sem serviços em nuvem.

## Fluxo

1. Consulte `docs/operations/concept-2d-runbook.md` e `docs/art/art-bible.yaml`.
2. Rode `concept_bridge.py` no modo desejado (`concept`, `tileable` ou `ui`).
3. Inspecione visualmente o PNG gerado (silhueta limpa, fundo neutro no modo `concept`,
   sem emenda no modo `tileable`, aderente à paleta da Bíblia Visual).
4. Para peças 3D, envie o conceito aprovado para a Fábrica 3D (`3dprinter-setup-ia`) e
   siga o runbook `fab3d-bridge-runbook.md`.
