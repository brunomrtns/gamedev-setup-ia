---
name: studio-voice-tts
description: 'Gera falas e dublagens 100% locais para personagens de jogos usando XTTS-v2 no WSL (tools/audio/tts_bridge.py) a partir de voice-cast.json e roteiros de dialogo. Use quando criar dublagem, vozes de NPCs ou audio de cutscenes.'
---

# Studio Voice TTS — Dublagem 100% Local por Personagem

**Goal:** transformar roteiros versionados em JSON (`dialogues_*.json`) + elenco de
vozes (`voice-cast.json`) em arquivos `.wav` normalizados e `vo_manifest.json` prontos
para o Unity e para o Cutscene Builder — 100% local via XTTS-v2 no WSL.

## Regras não negociáveis

1. **100% local:** nunca usar APIs externas de TTS (ElevenLabs, OpenAI TTS etc.).
2. **Elenco explícito:** toda voz de personagem precisa estar cadastrada em
   `docs/audio/voice-cast.json` apontando para uma amostra `.wav` limpa (gravada pelo
   dono ou amostra de voz pronta).
3. **VRAM:** não rodar XTTS-v2 na GPU simultaneamente com TRELLIS.2 / Hunyuan3D / ComfyUI.

## Fluxo

1. Consulte `docs/operations/tts-bridge-runbook.md`.
2. Garanta que `voice-cast.json` tem o personagem e o `speaker_wav` válido.
3. Escreva ou atualize o roteiro `dialogues_<cena>.json`.
4. Rode:
   ```powershell
   python tools/audio/tts_bridge.py --cast <cast.json> --dialogues <dialogues.json> --scene <cena>
   ```
5. Valide `evidence/audio/tts_bridge_<cena>.json` (`errors: []`, durações coerentes,
   `peak_dbfs` normalizado) e atualize `PROGRESS.md`.
