# Biblioteca de Referências

Material de consulta para agentes e desenvolvedores. Conteúdo **somente leitura** — não editar os arquivos fonte; cite-os em decisões (ADRs) e implementações.

## Estrutura

```text
docs/references/
├── books/      # Livros e e-books (PDF)
├── unity/      # Documentação oficial de pacotes Unity
└── games/      # Referências de jogos (análises, postmortems, materiais)
```

## Índice

### books/

| Arquivo | Conteúdo | Uso sugerido |
|---|---|---|
| `Game Programming Patterns.pdf` | Robert Nystrom — padrões de arquitetura de jogos (Component, State, Command, Object Pool, Game Loop, Update Method, etc.) | Consultar ao projetar sistemas de gameplay; alinhar com `docs/standards/architecture.md` |
| `Level_up_your_code_with_design_patterns_and_SOLID_e-book.pdf` | Design patterns GoF + princípios SOLID aplicados | Base para refatorações e code review; complementa padrões Unity C# |

### unity/ai-navigation/

Documentação oficial do pacote `com.unity.ai.navigation` (NavMesh runtime/bake), espelhada de
<https://github.com/needle-mirror/com.unity.ai.navigation/tree/master/Documentation~>.

Destaques:
- `TableOfContents.md` / `index.md` — índice completo
- `NavMeshSurface.md`, `NavMeshAgent.md`, `NavMeshLink.md`, `NavMeshObstacle.md` — componentes de bake e runtime
- `NavMeshBuildingComponents.md` — pipeline de build do NavMesh
- `CreateNavMesh*.md` — how-tos (agentes, links, obstáculos)
- `CodeExamples/` — exemplos C# oficiais (asmdef incluído)

### games/

_(vazio — reservado para referências de jogos: análises de design, postmortems, GDDs públicos)_

## Convenções

- PDFs e documentação de terceiros ficam **fora de `unity/Assets/`** para não serem importados pelo Editor.
- Ao adicionar material novo, registre nesta tabela: arquivo, conteúdo e uso sugerido.
- Material com restrição de licença: anotar a licença na tabela.
