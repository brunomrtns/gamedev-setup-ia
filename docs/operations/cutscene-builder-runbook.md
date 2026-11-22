# Runbook — Cutscene Builder Declarativo (JSON → Unity Timeline + Keyframes)

> **Contexto:** Fase 3 do [Studio V2 Local Pipeline](../roadmap/studio-v2-local-pipeline.md).
> Permite que a IA escreva cutscenes inteiras como JSON versionável no Git, conectando
> automaticamente os áudios e durações do `vo_manifest.json` (Fase 2 — `tts_bridge.py`)
> a um `TimelineAsset` real no Unity + prefab com câmera cinematográfica, letterbox e
> legendas + capturas automáticas de keyframes para o `studio-strict-reviewer`.

## 1. Fluxo de Produção

1. (Opcional, quando houver voz) Gere as falas da cena via `tts_bridge.py`:
   ```powershell
   python tools/audio/tts_bridge.py --cast docs/audio/voice-cast.json `
       --dialogues docs/narrative/dialogues_intro.json --scene intro
   ```
2. Crie `docs/narrative/cutscene_<id>.json` baseado em `docs/narrative/cutscene.template.json`.
   - Se `vo_line_id` corresponder a uma linha de `vo_manifest.json`, o builder herda
     automaticamente o `AudioClip`, a duração exata (`duration_sec + pad_after_sec`),
     o nome do personagem e o texto da legenda.
3. Compile a cutscene e capture os keyframes em batchmode:
   ```powershell
   python tools/studio/build_cutscene.py --spec docs/narrative/cutscene_intro.json --capture-keyframes
   ```

## 2. Saídas Geradas

- `unity/Assets/Game/Cutscenes/<id>/<id>.playable` — Unity `TimelineAsset` com `AudioTrack` sincronizada.
- `unity/Assets/Game/Cutscenes/<id>/<id>.prefab` — `PlayableDirector` + `AudioSource` + `CutscenePlayer` (câmera rail suave + letterbox + legendas).
- `evidence/captures/cutscene_<id>_<shot_id>.png` — keyframe central de cada plano (sem alterar a cena em disco).
- `evidence/cutscenes/cutscene_<id>.json` — relatório estruturado para auditoria.

## 3. Critérios de Aceitação (Gate Fase 3)

- [ ] `build_cutscene.py` exit 0, `errors: []`
- [ ] Keyframes capturados e inspecionados visualmente (enquadramento legível, sem clipping de câmera em parede, sem magenta)
- [ ] Áudio sincronizado quando `vo_manifest_path` estiver presente
