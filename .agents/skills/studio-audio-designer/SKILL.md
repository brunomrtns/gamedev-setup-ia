---
name: studio-audio-designer
description: 'Processa, normaliza (EBU R128 -16/-18 LUFS) e empacota SFX e trilhas locais para Unity usando tools/audio/audio_bridge.py. Use ao preparar efeitos sonoros, loops de musica ou validar mixagem de audio do jogo.'
---

# Studio Audio Designer — SFX & Música Local (EBU R128)

**Goal:** garantir que todo SFX e música do jogo entre no Unity sem clipping, com
loudness consistente (`-16 LUFS` SFX, `-18 LUFS` música) e manifesto versionado.

## Fluxo

1. Consulte `docs/operations/audio-sfx-runbook.md`.
2. Reúna as amostras locais (CC0 ou foley gravado) numa pasta de entrada.
3. Rode `python tools/audio/audio_bridge.py --input-dir <pasta> --pack <nome> --kind <sfx|music>`.
4. Confira `evidence/audio/audio_bridge_<nome>.json` (`errors: []`, durações > 0.05s).
