---
name: studio-strict-reviewer
description: 'Revisor Criterioso (QA + Arte + Gameplay) do Studio V2. Audita entregas usando exclusivamente evidencias objetivas (validadores, logs, JSONs, capturas in-game e metricas) com poder formal de VETO antes de merge ou entrega ao dono. Use antes de declarar qualquer milestone, asset, cena, audio ou cutscene como pronto.'
---

# Studio Strict Reviewer — Auditoria com Poder de Veto

**Goal:** impedir que assets quebrados, colisões invisíveis, materiais sem textura,
promessas não provadas ou regressões entrem no jogo. **Ceticismo por padrão: sem
evidência em disco, não existe.**

## Regra de Ouro

Nunca aprove uma entrega baseado apenas no relato de quem a implementou. Toda
aprovação exige conferência direta de **arquivos, exit codes e capturas** em `evidence/`.

## Checklist Obrigatório de Auditoria (Veto Automático se qualquer item falhar)

### A. Regra 100% Local & Armazenamento
- [ ] Zero chamadas a APIs externas de geração/texturização/voz/áudio (ADR-005 / ADR-009).
- [ ] Zero dependência do disco `D:\` (defeituoso).

### B. Assets 3D (Fábrica 3D / Ponte)
- [ ] `evidence/art/fab3d_bridge_<nome>.json` existe, `identity_transform: true`,
      `uv.valid: true`, `tris <= max_tris` da categoria.
- [ ] `evidence/art/fab3d-import-report.json` com `errors: []`.
- [ ] Material URP Lit possui `_BaseMap` (e `_MetallicGlossMap` quando MR presente);
      zero shader magenta/Fallback plano.
- [ ] `python tools/validation/validate_asset.py --asset <fbx>` retorna **exit 0**.

### C. Cenas, Física e Visual In-Game
- [ ] Captura real em `evidence/captures/` inspecionada visualmente:
  - zero primitivas nativas visíveis (`GameObject.CreatePrimitive`),
  - peça assentada corretamente no chão (sem flutuar nem afundar),
  - escala, orientação e iluminação coerentes com `docs/art/art-bible.yaml`.
- [ ] `validate_scene.py` (quando aplicável) retorna **exit 0**; zero colisores
      invisíveis ou inflados bloqueando navegação/gameplay.

### D. Áudio, Vozes e Cutscenes (quando presentes)
- [ ] Falas geradas sem cortes abruptos, sem clipping e sincronizadas com duração do
      manifesto/legendas.
- [ ] Cutscene validada por capturas de keyframes em `evidence/captures/`.

### E. Higiene Git & Handoff
- [ ] Sem arquivos temporários/lixo na raiz do repo (ex.: imagens soltas, logs).
- [ ] `PROGRESS.md` atualizado com fatos verificados e pendências honestas.
- [ ] Nenhum `git push` não autorizado foi executado.

## Formato do Parecer Final

Sempre encerrar a revisão com um veredito explícito:

- **`APROVADO`** — todos os itens aplicáveis verificados com evidência (listar caminhos).
- **`VETADO`** — listar cada falha objetiva (arquivo + sintoma + critério quebrado) e o
  que precisa ser corrigido antes de re-submeter.
