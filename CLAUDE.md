# CLAUDE.md — Adaptador de Comandos para Claude Code

Este arquivo fornece atalhos rápidos e regras operacionais para Claude Code. Para regras completas e vinculantes, consulte [AGENTS.md](AGENTS.md).

## Princípios Rápidos
- **Ambiente dual Windows + WSL (LEIA PRIMEIRO):** Unity, builds e validadores rodam no **Windows nativo**. Blender headless e libs de processamento (PIL/numpy/trimesh/requests) ficam **apenas no WSL2 Ubuntu** — não existem no Windows. Invoque via `wsl blender -b -P <script> -- <args>` e `wsl /home/bruno/.venvs/game-ai3d/bin/python <script>`. Detalhes em [AGENTS.md §3](AGENTS.md).
- **Nunca presuma que compilar basta.** Sempre execute validações e testes (`tools/validation/` e `unity test`).
- **Nunca edite a mesma cena simultaneamente.** Se trabalhar com subagentes, serialize edições de `.unity` e `.prefab`.
- **Validação de Assets Obrigatória (Sem Gambiarras):** Se os assets pedidos no prompt forem incompatíveis (ex: `.uasset` de Unreal em projeto Unity), É PROIBIDO criar cubos/blocos sem textura em silêncio. Pare e pergunte ao usuário via `ask_question`.
- **Zero Tolerância para Jogos Sem Textura:** Todo jogo DEVE usar modelos com texturas PBR reais (Albedo, Normal, Smoothness) e UV mapping.
- **Quality Gate V2:** Ambientes usam estritamente os kits `Assets/Game/Art/Environment/Modular/` (CC0), materiais `Mat_PBR_*` (Poly Haven) ou modelos normalizados via `tools/art/normalize_fbx.py`. Primitivas nativas visíveis e materiais sem `_BaseMap` falham nos validadores V2. Reposição do ecossistema: `python tools/art/download_modular_kits.py`.
- **Física e Combate:** Proibido tráfego puramente cinemático sem reação a impactos (Routing/PhysicsFree/Wrecked obrigatório). Proibido tiro que divirja do centro do retículo da câmera (raycast-centro → muzzle obrigatório). Detalhes: [docs/standards/physics-and-combat.md](docs/standards/physics-and-combat.md).
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

# Blender headless (WSL) e Python científico (venv game-ai3d no WSL)
wsl blender -b -P tools/art/<script_bpy>.py -- <output_dir>
wsl /home/bruno/.venvs/game-ai3d/bin/python tools/art/<script>.py <args>
```

## Referências
- Padrões de código e C#: [docs/standards/unity.md](docs/standards/unity.md)
- Arquitetura de software: [docs/standards/architecture.md](docs/standards/architecture.md)
- Budgets mobile (FPS, tris, memória): [docs/standards/mobile-performance.md](docs/standards/mobile-performance.md)
- Art Bible e especificações visuais: [docs/art/art-bible.md](docs/art/art-bible.md)
