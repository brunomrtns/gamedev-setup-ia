# CLAUDE.md — Adaptador de Comandos para Claude Code

Este arquivo fornece atalhos rápidos e regras operacionais para Claude Code. Para regras completas e vinculantes, consulte [AGENTS.md](AGENTS.md).

## Princípios Rápidos
- **Nunca presuma que compilar basta.** Sempre execute validações e testes (`tools/validation/` e `unity test`).
- **Nunca edite a mesma cena simultaneamente.** Se trabalhar com subagentes, serialize edições de `.unity` e `.prefab`.
- **Sempre registre progresso:** Ao iniciar e terminar uma tarefa, atualize [PROGRESS.md](PROGRESS.md).

## Comandos Mais Usados

```bash
# Validar status e ambiente Unity
unity doctor
unity status

# Executar testes automatizados (EditMode + PlayMode)
unity test unity/ --format json

# Validar cena e detectar dependências quebradas
python tools/validation/validate_scene.py --scene Assets/Game/Scenes/Main.unity

# Validar asset 3D e orçamento móvel
python tools/validation/validate_asset.py --asset Assets/Game/Models/Player.fbx

# Orquestrador local de tarefas autônomas
python tools/studio/runner.py --status
python tools/studio/runner.py --run-next
```

## Referências
- Padrões de código e C#: [docs/standards/unity.md](docs/standards/unity.md)
- Arquitetura de software: [docs/standards/architecture.md](docs/standards/architecture.md)
- Budgets mobile (FPS, tris, memória): [docs/standards/mobile-performance.md](docs/standards/mobile-performance.md)
- Art Bible e especificações visuais: [docs/art/art-bible.md](docs/art/art-bible.md)
