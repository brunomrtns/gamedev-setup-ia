# Padrão de Organização Git & Projetos — Studio V2

> **Autoridade:** define como o estúdio separa ferramentas reutilizáveis dos jogos,
> como versiona assets binários e como controla commits, branches e revisões.

## 1. Separação Studio Core vs. Jogo Novo

- **`gamedev-setup-ia` (Studio Core):**
  - Mantém as pontes (`fab3d_bridge.py`, `tts_bridge.py`, `concept_bridge.py`),
    validadores (`tools/validation/`, `tools/qa/`), scripts de Editor (`Fab3dImporter.cs`,
    `CutsceneBuilder.cs`), skills (`.agents/skills/`) e templates (`docs/art/art-bible.template.yaml`).
  - **Proibido** acumular scripts descartáveis/experimentos soltos na raiz de `tools/art/`
    ou assets de teste temporários em `unity/Assets/Game/Art/Generated/`. Experimentos
    temporários ficam fora do repo ou em pastas ignoradas pelo `.gitignore`.
  - **Checklist de Higiene de Geradores e Repositórios de Terceiros (Inegociável — DOC 3 §3-J):**
    - [ ] Repositórios, servidores e pesos de terceiros (`mcp-unity`, `UniRig`, `MoMask`, etc.) vivem **obrigatoriamente fora do Studio Core**, em `~/tools/<ferramenta>` (no Windows: `C:\Users\Bruno\tools\<ferramenta>`; no WSL: `/home/bruno/tools/<ferramenta>`).
    - [ ] **Nunca** clonar ou manter pastas de geradores externos dentro de `gamedev-setup-ia/tools/art/` (`TripoSR` removido para fora do Studio Core e permanentemente banido pelo `AGENTS.md` §1.1).
    - [ ] `tools/art/` e `tools/art/ai_generator/` contêm **exclusivamente** código canônico versionado do Studio Core.
    - [ ] Antes de qualquer commit no Studio Core, `git status` não pode listar scripts experimentais avulsos ou pastas de output temporárias dentro de `tools/art/`.
- **Repositório de Jogo Novo (ex.: `game-<slug>`):**
  - Nasce limpo (sem packs de terceiros misturados), copiando apenas o Studio Core
    necessário (importers, validadores, skills, contratos `AGENTS.md`).
  - Contém sua própria `docs/art/art-bible.yaml`, `docs/art/fab3d-asset-spec.yaml`,
    `docs/narrative/dialogues.yaml`, `docs/narrative/cutscenes/`, `PROGRESS.md` e `evidence/`.

## 2. Git LFS (obrigatório em todo jogo novo)

Configure `.gitattributes` no commit 1 para não inflar o histórico Git com binários:

```gitattributes
*.fbx filter=lfs diff=lfs merge=lfs -text
*.glb filter=lfs diff=lfs merge=lfs -text
*.png filter=lfs diff=lfs merge=lfs -text
*.wav filter=lfs diff=lfs merge=lfs -text
*.ogg filter=lfs diff=lfs merge=lfs -text
```

> **Regra de staging da ponte Fab3D:** o GLB print-grade original (15–50 MB) **não**
> precisa ser versionado no repo do jogo se já estiver guardado no acervo da Fábrica 3D;
> versione o `.fbx` canônico + texturas 512/1024 px + `fab3d_manifest.json` + material + prefab.

## 3. Branches e Commits

- **Branch principal:** `main` sempre compilável e com gates verdes.
- **Trabalho de feature/asset:** preferir branches curtas (`feat/<slug>`, `art/<asset_id>`,
  `fix/<slug>`) ou commits atômicos em `main` quando trabalhando solo, sempre com
  Conventional Commits:
  - `docs(...)`, `feat(...)`, `fix(...)`, `art(...)`, `audio(...)`, `chore(...)`.
- **Parada obrigatória:** `git push` e comandos destrutivos (`reset --hard`, `push --force`)
  **nunca** são executados sem autorização explícita do dono (AGENTS.md §5).

## 4. Ciclo de Entrega com Evidência (Definition of Done)

Nenhuma entrega (código, asset 3D, áudio ou cutscene) é considerada concluída sem:

1. **Artefato versionado** no caminho canônico do projeto.
2. **Evidência objetiva em `evidence/`** (relatório JSON do validador + captura PNG
   quando houver impacto visual).
3. **Aprovação do Strict Reviewer** (`.agents/skills/studio-strict-reviewer/SKILL.md`).
4. **Atualização de `PROGRESS.md`** (pendências honestas sempre visíveis).
