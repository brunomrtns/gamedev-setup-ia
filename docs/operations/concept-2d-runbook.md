# Runbook — Ponte 2D Local (Conceitos, Texturas Tileáveis & UI)

> **Contexto:** Fase 4 do [Studio V2 Local Pipeline](../roadmap/studio-v2-local-pipeline.md).
> Reutiliza a venv `~/.venvs/hunyuan` já existente no WSL (`diffusers 0.33.1` + CUDA),
> sem instalar ComfyUI pesado. Modelo padrão: `stabilityai/sdxl-turbo` (~6,5 GB em fp16
> no cache HF em `C:\`, 4 steps ≈ 1,5 s por imagem na RTX 3060 12 GB, sem token).

## 1. Modos de Uso

### A. Conceito 2D (entrada direta para a Fábrica 3D)
Combina automaticamente `style_prefix`, `style_suffix` e `negative_prompt` da Bíblia Visual:

```powershell
python tools/art/ai_generator/concept_bridge.py `
    --name altar_antigo --mode concept `
    --prompt "ancient stone shrine with glowing golden rune" `
    --art-bible docs/art/art-bible.yaml
```
Saída: `evidence/concepts/altar_antigo_concept.png` → alimenta `3dprinter-setup-ia`.

### B. Textura 100% Tileável (chão, pedra, areia, parede)
Ativa `padding_mode='circular'` em todas as camadas `Conv2d` da UNet + VAE para gerar
texturas sem emenda:

```powershell
python tools/art/ai_generator/concept_bridge.py `
    --name piso_ruinas --mode tileable `
    --prompt "weathered mossy cobblestone tiles, stylized PBR"
```
Saída: `unity/Assets/Game/Art/Textures/Generated/piso_ruinas_albedo.png`.

### C. Ícones & Arte de UI
```powershell
python tools/art/ai_generator/concept_bridge.py `
    --name icone_reliquia --mode ui `
    --prompt "glowing golden compass relic"
```
Saída: `unity/Assets/Game/Art/UI/Generated/icone_reliquia.png`.

## 2. Regras de Hardware

- Armazenamento estritamente em `C:\` (`D:\` proibido).
- Não rodar simultaneamente com TRELLIS.2 / Hunyuan3D / XTTS-v2 na RTX 3060 12 GB.
