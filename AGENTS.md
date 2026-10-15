# AGENTS.md — Contrato Canônico de Operação dos Agentes

Este documento é a autoridade central e vinculante para qualquer agente de IA (Claude Code, OpenCode, Antigravity, etc.) operando neste repositório.

---

## 1. Princípios Não-Negociáveis

1. **Agnóstico ao Agente:** Todos os agentes leem os mesmos padrões (`docs/standards/`), usam os mesmos comandos (`tools/`) e persistem o estado em `PROGRESS.md` e `.studio/`.
2. **Documentação Antes da Implementação:** Nenhuma funcionalidade de gameplay, arquitetura ou arte é codificada sem uma story e critérios de aceitação formalizados (via BMad em `_bmad-output/` ou contratos em `docs/`).
3. **Automação Verificável:** "Código que compila" **NÃO** é critério de conclusão. Toda tarefa exige testes automatizados (EditMode/PlayMode), validação determinística (`ValidateScene`, `ValidateAsset`) e evidências registradas em `evidence/`.
4. **Isolamento de Cena:** Nunca abra ou edite simultaneamente a mesma cena a partir de agentes concorrentes. Modificações de cena são serializadas.
5. **Autonomia com Responsabilidade:** Execute tarefas sequencialmente conforme dependências sem solicitar prompts ao usuário para passos rotineiros. Interrompa para intervenção humana **apenas** para:
   - Credenciais privadas e pagamentos.
   - Decisões de design de jogo ou aprovação de arte/game feel.
   - Comandos destrutivos ou `git push` não autorizado.

---

## 2. Estrutura Canônica de Diretórios

```text
gamedev-setup-ia/
├── AGENTS.md                         # Este contrato
├── CLAUDE.md                         # Ponte concisa para Claude Code
├── PROGRESS.md                       # Checklist vivo e handoff entre sessões
├── docs/
│   ├── roadmap/                      # Roadmap original de referência
│   ├── plan/                         # Plano de implementação e dependências
│   ├── standards/                    # Normas técnicas invioláveis
│   │   ├── unity.md                  # Padrões C#, URP, convenções de código
│   │   ├── architecture.md           # Padrões de arquitetura (Assembly Defs, desacoplamento)
│   │   ├── testing.md                # UTF, EditMode, PlayMode, cobertura
│   │   ├── mobile-performance.md     # Budgets de draw calls, tris, memória e thermal
│   │   └── asset-quality.md          # Especificação de malha, texturas e LODs
│   ├── art/                          # Art Bible, art-spec.yaml e referências
│   ├── decisions/                    # Architecture Decision Records (ADRs)
│   └── operations/                   # Guias de ambiente, auditoria e troubleshooting
├── tools/                            # Ferramentas determinísticas do estúdio
│   ├── studio/                       # Orquestrador local autônomo (DAG runner)
│   ├── validation/                   # Validadores de cena, assets e budgets
│   ├── capture/                      # Captura automatizada de screenshots/vídeos
│   └── android/                      # Scripts de build, deploy e profiling via ADB
├── unity/                            # Projeto Unity 6 URP canônico
│   ├── Assets/Game/                  # Código e assets da aplicação
│   ├── Assets/Editor/                # Ferramentas e hooks de Editor C#
│   ├── Packages/                     # Manifesto de pacotes
│   └── ProjectSettings/              # Configurações do projeto
├── evidence/                         # Evidências geradas (testes, capturas, logs)
└── .studio/                          # Estado persistente da orquestração autônoma
```

---

## 3. Comandos Canônicos do Estúdio

Todos os agentes devem executar comandos através das interfaces padronizadas:

### Inspeção e Status
```bash
# Verificar estado dos editores e CLI
unity doctor
unity status

# Verificar integridade da cena atual
python tools/validation/validate_scene.py --scene Assets/Game/Scenes/Main.unity

# Verificar integridade de um asset
python tools/validation/validate_asset.py --asset Assets/Game/Models/Player.fbx
```

### Testes e Validação
```bash
# Executar todos os testes em batchmode
unity test unity/ --test-platform EditMode --test-platform PlayMode --format json

# Capturar cena padronizada para evidência visual
python tools/capture/capture_scene.py --scene Assets/Game/Scenes/Calibration.unity --out evidence/captures/calibration.png
```

### Build Android
```bash
# Gerar build Android local
python tools/android/build_apk.py --target development --out builds/game-dev.apk
```

---

## 4. Definition of Done (DoD)

Uma story ou tarefa só é considerada `[x]` (concluída) quando:
1. Todos os critérios de aceitação da story foram atendidos.
2. A compilação no Unity passa sem nenhum warning marcado como erro e sem exceptions.
3. Testes EditMode e PlayMode passam 100%.
4. `ValidateScene` e `ValidateAsset` retornam código 0 (sem componentes órfãos ou leaks).
5. As evidências (logs de teste, screenshots em `evidence/`) foram salvas em disco.
6. `PROGRESS.md` foi atualizado com o status e o ponteiro para a evidência.
