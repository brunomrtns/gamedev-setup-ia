---
name: studio-cutscene-director
description: 'Cria e compila cutscenes declarativas (JSON -> Unity Timeline + AudioTrack sincronizado ao XTTS-v2 + camera cinematica + legendas + capturas de keyframes) usando tools/studio/build_cutscene.py e CutsceneBuilder.cs. Use quando criar introducoes, dialogos cinematograficos ou transicoes de historia.'
---

# Studio Cutscene Director — Cutscenes como Código (Timeline + VO)

**Goal:** produzir cutscenes cinematográficas reproduzíveis e versionadas em Git,
integrando roteiro, vozes locais (`tts_bridge.py`), câmera e legendas com revisão
visual automática via keyframes.

## Fluxo

1. Leia `docs/operations/cutscene-builder-runbook.md`.
2. Se a cutscene tiver falas dubladas, gere o áudio primeiro via skill `studio-voice-tts`
   (`tts_bridge.py` → `vo_manifest.json`).
3. Escreva `docs/narrative/cutscene_<id>.json` seguindo `docs/narrative/cutscene.template.json`
   (cada shot com `camera_from`, `camera_to`, `look_at`, `fov`, `vo_line_id` ou `subtitle_text`).
4. Rode:
   ```powershell
   python tools/studio/build_cutscene.py --spec docs/narrative/cutscene_<id>.json --capture-keyframes
   ```
5. Inspecione os keyframes em `evidence/captures/cutscene_<id>_*.png` (enquadramento,
   leitura, iluminação) e submeta ao `studio-strict-reviewer`.
