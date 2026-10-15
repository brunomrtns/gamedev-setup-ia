# ADR-002: Orquestração Autônoma Persistente e Integração Multiagente

- **Status:** Aceito
- **Data:** 2026-10-15
- **Autor:** Sessão Antigravity (Arquiteto Principal)

## Contexto

O usuário requisitou uma plataforma capaz de receber um briefing e conduzir autonomamente todo o ciclo de produção: planejamento, decomposição com BMad, execução em DAG com dependências sem novos prompts manuais, controle do Unity, testes, autocorreção, playtesting e persistência entre sessões. O roadmap sugeriu avaliar Symphony e OmO (oh-my-opencode).

## Avaliação das Alternativas

1. **Symphony (openai/symphony):**
   - *Prós:* Modelo declarativo de issues e workflows assíncronos.
   - *Contras:* Projetado primariamente para Linear + nuvem; adiciona dependência de conta externa SaaS e infraestrutura não local; não oferece integração nativa com o Unity Editor no Windows local.
2. **OmO (oh-my-opencode):**
   - *Prós:* Execução local via OpenCode (já instalado nativamente no Windows com credencial ativa), suporta orquestrador Sisyphus e roteamento de modelos.
   - *Decisão:* Adotado como harness de execução de modelos para tarefas que exigem OpenCode local.
3. **BMad Game Dev Studio:**
   - *Decisão:* Adotado como autoridade central de planejamento, geração de GDD, arquitetura e histórias de usuário (stories).
4. **Studio Task Runner (`tools/studio/runner.py`):**
   - *Decisão:* Criar um executor leve, persistente e determinístico baseado em DAG (Grafo Acíclico Dirigido) com arquivos JSON em `.studio/tasks/`. Ele garante que qualquer agente (OpenCode, Claude Code, Antigravity) ou processo em segundo plano possa:
     - Enfileirar tarefas com dependências (`dependencies: ["TASK-001"]`).
     - Executar o próximo passo elegível sem intervenção humana.
     - Persistir logs e evidências em `evidence/`.
     - Permitir a retomada imediata em qualquer nova sessão através do `PROGRESS.md`.

## Consequências

- O ciclo de vida de desenvolvimento é totalmente desacoplado da sessão de chat: o estado reside em disco (`.studio/` e `PROGRESS.md`).
- Se uma sessão for interrompida, a próxima sessão simplesmente chama `python tools/studio/runner.py --run-next` e continua exatamente do ponto de parada com integridade total.
