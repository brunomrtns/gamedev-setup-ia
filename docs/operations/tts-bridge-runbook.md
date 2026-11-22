# Runbook — Ponte de Vozes / TTS Local (XTTS-v2 WSL → Unity VO)

> **Contexto:** Fase 2 do [Studio V2 Local Pipeline](../roadmap/studio-v2-local-pipeline.md).
> Reutiliza o modelo **XTTS-v2** já em cache no WSL (`/home/bruno/.local/share/tts`) e a
> venv existente em `/home/bruno/Desenvolvimento/brunointegrations/video-generate/.venv`
> (**0 MB de download**, 100% local, GPU RTX 3060 12 GB, somente `C:\`).

## 1. Definir o Elenco de Vozes (`voice-cast.json`)

Copie `docs/audio/voice-cast.template.json` para `docs/audio/voice-cast.json` no projeto
do jogo e associe cada personagem a uma amostra `.wav` limpa (6–15 s, sem música de
fundo, mono ou estéreo) — seja **gravada por você** ou de uma biblioteca de vozes pronta.

## 2. Escrever o Roteiro da Cena (`dialogues_<cena>.json`)

```json
{
  "scene": "intro",
  "lines": [
    {
      "id": "intro_001",
      "character": "narrator",
      "language": "pt",
      "text": "Há mil anos, as ruínas guardam um segredo que ninguém ousou despertar."
    }
  ]
}
```

## 3. Gerar as Vozes via Ponte (Windows → WSL XTTS-v2)

```powershell
python tools/audio/tts_bridge.py `
    --cast docs/audio/voice-cast.json `
    --dialogues docs/narrative/dialogues_intro.json `
    --scene intro
```

Saídas geradas automaticamente:
- `unity/Assets/Game/Audio/VO/<cena>/<id>.wav` — WAV PCM 16-bit normalizado em `-1 dBFS`
- `unity/Assets/Game/Audio/VO/<cena>/vo_manifest.json` — lista de falas com **duração exata em segundos** (usada diretamente pelo `CutsceneBuilder` e pelo sistema de legendas)
- `evidence/audio/tts_bridge_<cena>.json` — relatório de auditoria para o `studio-strict-reviewer`

## 4. Critérios de Aceitação (Gate Fase 2)

- [ ] `tts_bridge.py` exit 0 rodando em `cuda` (ou `cpu` se GPU ocupada)
- [ ] Zero chamadas a serviços externos de TTS
- [ ] Todos os WAVs com `duration_sec >= 0.15`, não silenciosos e normalizados (`peak_dbfs <= -0.5`)
- [ ] `vo_manifest.json` e `evidence/audio/tts_bridge_<cena>.json` gravados e válidos
