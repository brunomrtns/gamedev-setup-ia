# Runbook — Ponte de SFX & Música Local (EBU R128 → Unity Audio)

> **Contexto:** Fase 6 do [Studio V2 Local Pipeline](../roadmap/studio-v2-local-pipeline.md).
> Normaliza e empacota SFX e trilhas locais (acervo CC0 ou foley gravado) sem APIs
> externas e sem download de modelos (**0 MB**). Usa `ffmpeg` (nativo ou WSL).

## 1. Uso

### SFX (exporta `.wav` 44.1 kHz normalizado em `-16 LUFS / -1.0 dBTP`)
```powershell
python tools/audio/audio_bridge.py `
    --input-dir C:\caminho\raw_sfx `
    --pack caldeiral_sfx --kind sfx
```

### Música / Loops (exporta `.ogg` 44.1 kHz normalizado em `-18 LUFS / -1.5 dBTP`)
```powershell
python tools/audio/audio_bridge.py `
    --input-dir C:\caminho\raw_music `
    --pack tema_oficina --kind music
```

## 2. Saídas & Gate

- `unity/Assets/Game/Audio/<SFX|Music>/<pack>/audio_manifest.json` + arquivos normalizados.
- `evidence/audio/audio_bridge_<pack>.json` (`errors: []`, zero clipping, zero silêncio).
