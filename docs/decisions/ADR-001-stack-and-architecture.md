# ADR-001: Seleção da Stack Tecnológica, Versão da Engine e Plataforma Base

- **Status:** Aceito
- **Data:** 2026-10-15
- **Autor:** Sessão Antigravity (Arquiteto Principal)

## Contexto

O objetivo é transformar o repositório em um estúdio autônomo de desenvolvimento de jogos 3D mobile com IA, operado por múltiplos agentes especializados. É necessário congelar a versão da engine, render pipeline, plataforma prioritária e modelo de versionamento para garantir reprodutibilidade e evitar divergências de API.

## Decisão

1. **Engine:** **Unity 6.3 LTS (versão 6000.3.26f1)** instalada nativamente no Windows com módulos Android Build Support, OpenJDK 17 e Android NDK r27c.
2. **Render Pipeline:** **Universal Render Pipeline (URP)** com renderização Linear e Render Graph.
3. **Plataforma Prioritária:** **Android (ARM64)** como target de referência; compilações automatizadas via scripts locais e Unity CLI.
4. **Governança de Processo:** **BMad Game Dev Studio** como autoridade exclusiva para documentação (GDD, arquitetura, stories e épicos).
5. **Automação do Editor:** **Unity CLI oficial (`unity`)** como interface prioritária para comandos de linha, inspeção e testes, complementada por scripts C# de Editor (`-executeMethod`) para operações de cena determinísticas.

## Consequências

- **Positivas:**
  - Estabilidade de LTS de longo prazo.
  - Suporte completo a batchmode e testes headless no Windows.
  - Alinhamento 100% com as ferramentas e módulos oficiais instalados.
- **Negativas/Mitigações:**
  - Licença Unity Personal exige login via Unity Hub / browser uma única vez para ativação no ambiente do desenvolvedor.
