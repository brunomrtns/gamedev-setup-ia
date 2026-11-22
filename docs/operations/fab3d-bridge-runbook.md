# Runbook — Ponte Fábrica 3D → Unity (assets originais 100% locais)

> **Contexto:** [ADR-005](../decisions/ADR-005-fab3d-asset-pipeline.md). Este runbook
> é a autoridade operacional do fluxo. Ele consome o GLB "print-grade" publicado pela
> pipeline da Fábrica 3D (repo `3dprinter-setup-ia`, `pipeline/run_asset.py`) e o
> converte em asset canônico de jogo sob o Quality Gate V2.
>
> **Regra permanente:** 100% local. Nenhuma etapa chama serviço externo de
> geração/texturização em nuvem (ver ADR-009 do repo `3dprinter-setup-ia`).

## 0. Pré-requisitos (nada novo para instalar)

- Blender 5.0.1 no WSL2 (já instalado): `wsl blender --version`
- Unity 6.3 LTS com o projeto `unity/` do repo (já operacional)
- Um GLB publicado pela Fábrica 3D (Modo 1) **ou** ambiente da Fábrica operacional
  (Modo 2)
- **Espaço em disco necessário: ~0 GB** — a ponte não baixa nada; ela só converte

## 1. Modo 1 — `from_glb` (teste da ponte; comece sempre por aqui)

Usa um GLB que a Fábrica **já publicou** (ex.: peça da vitrine/jubarte). Não roda
geração — prova a conversão, o import e a validação de ponta a ponta.

1. **Spec:** preencha um bloco em `docs/art/fab3d-asset-spec.yaml` (ou passe tudo por
   flags, como abaixo).

2. **Rode a ponte (Windows, python stdlib puro):**

   ```powershell
   python tools/art/ai_generator/fab3d_bridge.py --from-glb "C:\caminho\peca.glb" `
       --name caixa_neon --category small_prop_item
   ```

   Etapas internas e ordem canônica de gates (DOC 3 §3-B — nenhum asset pula etapa):

   1. **Gate 0 — `glTF-Validator` (GLB):** `python tools/validation/validate_gltf.py --input <glb> --name <nome>` → valida buffers, acessores, imagens e extensões do GLB antes do Blender e grava `evidence/validation/gltf_validator_<nome>.json`.
   2. **Staging + Forja Blender WSL headless (`fab3d_glb_to_fbx.py`):** importa o GLB, **decima** até o budget da categoria (`art-spec.yaml`: small_prop 600 tris), **extrai e reduz** as texturas (BaseColor 4K/8K → 512px; MetallicRoughness 2048 → 512px) e exporta FBX canônico via `fbx_canonical.canonical_finalize`. Grava `fab3d_manifest.json` e `evidence/art/fab3d_bridge_<nome>.json`.
   3. **Gate 1 — `validate_asset`:** `python tools/validation/validate_asset.py --asset unity/Assets/Game/Art/Generated/<nome>/<nome>.fbx` → verifica integridade binária, UV0, transform identidade e N-gons.
   4. **Gate 2 — `validate_visual`:** `python tools/validation/validate_visual.py --asset unity/Assets/Game/Art/Generated/<nome>/<nome>.fbx --manifest unity/Assets/Game/Art/Generated/<nome>/fab3d_manifest.json` → verifica **orientação Y-up** (`altura > largura × 1.6` para bípede, detectando modelos deitados/invertidos), **budget de triângulos**, **densidade UV/texel** (contra UV 1×1 esticado), **material** e **escala em metros**, gravando `evidence/validation/validate_visual_<nome>.json`.

   Saída esperada: `REPORT_JSON:{...}` com `identity_transform: true`, `validate_asset` exit 0 e `validate_visual` exit 0.

3. **Gate 3 & 4 — Import no Unity (`Fab3dImporter`) + Captura Automática Pós-Import:**

   ```powershell
   # Editor: menu  Studio > Fab3D > Import Generated Assets
   # Batchmode (ou via python tools/qa/run_visual_gates.py --asset-dir unity/Assets/Game/Art/Generated/<nome>):
   & "C:\Program Files\Unity\Hub\Editor\6000.3.26f1\Editor\Unity.exe" -batchmode -quit `
     -projectPath <repo>\unity -executeMethod Studio.Editor.Fab3dImporter.RunImportBatch `
     -logFile fab3d-import.log
   ```

   O `Fab3dImporter` cria: texturas com settings ASTC por categoria, material URP Lit `Mat_Fab3D_<nome>` com `_BaseMap` (BaseColor) + `_MetallicGlossMap`, prefab em `Assets/Game/Prefabs/Fab3D/<nome>.prefab`, relatório `evidence/art/fab3d-import-report.json` e **executa automaticamente a captura pós-import** (`CapturePreviewForPrefab`), salvando o PNG datado `evidence/captures/fab3d_<nome>_<YYYYMMDD_HHMMSS>.png` (e `evidence/captures/fab3d_<nome>.png`). **Exit 0 obrigatório.**

4. **Gate 5 & 6 — Validação de Cena + Testes Automatizados + Inspeção Visual MCP:**

   ```powershell
   python tools/validation/validate_scene.py --scene Assets/Game/Scenes/<Cena>.unity --strict
   python tools/qa/run_unity_tests.py
   ```

   O agente **DEVE** abrir a captura gerada em `evidence/captures/fab3d_<nome>_*.png`, descrever em 1 frase o que vê (orientação, textura, silhueta vs. `art-bible.yaml`) e pedir o OK humano antes do próximo asset.

5. **Handoff:** marque o item correspondente em `PROGRESS.md` (seção "Integração Fábrica 3D").

## 2. Modo 2 — `fab3d_run` (geração completa)

Roda a pipeline da Fábrica **de dentro da ponte** e segue idêntico ao Modo 1 a partir
da etapa 3. Requer o ambiente da Fábrica operacional (venvs `hunyuan`/`trellis2` no
WSL, GPU livre).

```powershell
python tools/art/ai_generator/fab3d_bridge.py `
    --asset-config "C:\...\3dprinter-setup-ia\assets\<id>.json" `
    --fab3d-repo "C:\...\3dprinter-setup-ia" `
    --name <asset_id> --category small_prop_item
```

A ponte executa `python pipeline/run_asset.py --config <config> --out <tmp>` no repo
da Fábrica, localiza o maior `*.glb` publicado no diretório de saída e continua o
fluxo do Modo 1 (staging → Blender → manifest). Se a geração falhar, a ponte aborta
**sem** escrever manifest (nada entra no Unity pela metade).

## 3. Critérios de aceitação (gate da fase)

- [ ] `fab3d_bridge.py` exit 0, `REPORT_JSON` com `identity_transform: true`, tris
      ≤ budget da categoria
- [ ] `Fab3dImporter` exit 0; material URP Lit com `_BaseMap` + `_MetallicGlossMap`
- [ ] `validate_asset.py --asset ...` exit 0
- [ ] Captura in-game do prefab na cena alvo (`evidence/captures/`)
- [ ] Zero downloads externos e zero chamadas a serviço em nuvem durante o fluxo
- [ ] `PROGRESS.md` atualizado

## 4. Troubleshooting

| Sintoma | Causa provável | Ação |
| --- | --- | --- |
| Blender não acha o addon glTF | Blender headless sem addon core carregado | A ponte já chama `fbx_canonical._ensure_addon("io_scene_gltf2")`; se falhar, rode `wsl blender --version` e confirme 5.0.x |
| Texturas desalinhadas no Unity | Nome/RG canais trocados na extração | Confira no relatório quais imagens foram classificadas como BaseColor/MR; a heurística é área + nome (`metal`/`rough`) |
| `_MetallicGlossMap` escura demais | PNG lido como sRGB | O importer força sRGB off no MR e no metallicsmooth gerado; valide o `TextureImporter` |
| Tris acima do budget | Decimate não convergiu | Rode de novo com `--max-tris` menor; silhueta crítica? pare e consulte o dono (decisão de arte) |
| GLB com múltiplas variantes | Modo 2 pegou o maior GLB errado | Use `--glb-index` para escolher a variante explicitamente |

## 5. Fase futura (fora do escopo deste runbook)

Front-end de imagens de conceito (text-to-image local, ComfyUI + modelo destilado)
fica adiado até liberação de armazenamento no PC. Quando existir, o contrato de
entrada da ponte não muda: uma imagem → (geração 3D na Fábrica) → GLB → este runbook.
A geração de peças segue 100% local, sem exceção.
