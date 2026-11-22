# LICENSES-ARTE.md — Registro Canônico de Licenças de Arte e Ferramentas do Studio Core

> **Regra permanente (DOC 3 §2 e §3-H):** Nenhum asset ou ferramenta entra no pipeline do Studio Core (`gamedev-setup-ia`) ou em qualquer jogo derivado sem linha registrada nesta tabela com data e fonte oficial da verificação.
> - **Ferramenta GPL / Não-comercial:** Uso permitido **exclusivamente como ferramenta externa** (CLI/Python fora do binário do jogo, sem embutir código no runtime) + carrimbo explícito do dono.
> - **Asset Não-comercial ou Licença Comunitária Condicional:** Exige **CARRIMBO** explícito do dono antes do uso (decisão soberana do dono).

---

## 1. Ferramentas do Pipeline 3D, Validação e Visibilidade (Verificadas em 22/11/2026)

| Ferramenta / Pacote | Licença (Verificada) | Fonte Oficial | Papel no Pipeline | Status / Restrições |
| --- | --- | --- | --- | --- |
| **glTF-Transform** (`@gltf-transform/cli`) | **MIT** | `github.com/donmccurdy/glTF-Transform` (`LICENSE.md`) | Otimização pós-geração (`webp`/`uastc`/`etc1s`, `resize`, `optimize`, `simplify`, `weld`, `prune`, `validate`) | ✅ Uso comercial livre (CLI global) |
| **glTF-Validator** (`KhronosGroup/glTF-Validator`) | **Apache-2.0** | `github.com/KhronosGroup/glTF-Validator` (`LICENSE`) | Gate 0 oficial de validação glTF/GLB (`tools/validation/validate_gltf.py`) antes da conversão/import no Unity | ✅ Uso comercial livre |
| **glTFast** (`com.unity.cloud.gltfast`) | **Apache-2.0** | `docs.unity3d.com/Packages/com.unity.cloud.gltfast` | Importador glTF/GLB nativo para Unity (editor/runtime) quando aplicável | ✅ Uso comercial livre |
| **TRELLIS.2** (`microsoft/trellis.2` + `TRELLIS.2-4B`) | **MIT** | `github.com/microsoft/TRELLIS` (`LICENSE`) | Gerador imagem→3D canônico do acervo da Fábrica 3D (`TRELLIS.2-4B + Hunyuan3D-Paint + pack_glb modo game` → `fab3d_bridge.py`) | ✅ Padrão oficial aprovado (código + modelo MIT) |
| **Instant Meshes** (`wjakob/instant-meshes`) | **BSD 3-clause** | `github.com/wjakob/instant-meshes` (`LICENSE.txt`) | Retopologia automática (alternativa GUI; caminho headless canônico segue via Blender `bpy`) | ✅ Uso comercial livre |
| **PyMeshLab** (`cnr-isti-vclab/PyMeshLab`) | **GPL-3.0** | `github.com/cnr-isti-vclab/PyMeshLab` (`LICENSE.txt`) | Decimação/simplificação de malha exclusivamente como ferramenta externa CLI/Python | ⚠️ **GPL Externa:** proibido embutir no jogo; Blender `bpy` cobre o mesmo uso |
| **TextureLab** (`njbrown/texturelab`) | **GPL-3.0** | `github.com/njbrown/texturelab` (`LICENSE`) | Geração procedural de texturas PBR (opcional) | ⚠️ **GPL Externa:** usar apenas fora do runtime |
| **Unity Project Auditor** (`com.unity.project-auditor`) | **Unity Package License** | Pacote nativo Unity (`com.unity.project-auditor`, Unity 2021.1+) | Auditoria estática de projeto, assets e import settings | ✅ Uso comercial conforme licença do Unity Editor |
| **Unity MCP Server Oficial** (`com.unity.ai.assistant`) | **Termos Unity (Beta)** | `docs.unity3d.com/Packages/com.unity.ai.assistant@2.0` | Rota A de visibilidade do agente no Editor Unity | ⚠️ Indisponível no cache local sem entitlement beta; usar Rota B |
| **mcp-unity** (`CoderGamester/mcp-unity`) | **MIT** | `github.com/CoderGamester/mcp-unity` (`LICENSE.md`) | Rota B (ativa) de visibilidade do agente (MCP Server Node em `C:\Users\Bruno\tools\mcp-unity`) | ✅ Uso comercial livre |
| **Unity-MCP** (`IvanMurzak/Unity-MCP`) | **Apache-2.0** (+ PolyForm Shield 1.0.0) | `github.com/IvanMurzak/Unity-MCP` (`LICENSE`) | Rota C de visibilidade do agente (PolyForm Shield restringe apenas concorrentes de ferramentas AI) | ✅ Livre para desenvolvimento comercial de jogos |
| **Hunyuan3D-2** (`Tencent-Hunyuan/Hunyuan3D-2`) | ⚠️ **CARRIMBO: Licença Comunitária Tencent (Não confirmada nesta pesquisa)** | `github.com/Tencent-Hunyuan/Hunyuan3D-2` (`LICENSE`) | Gerador alternativo (shape + paint) na Fábrica 3D | 🛑 **CARRIMBADO:** Não integrar no Studio Core sem conferência/autorização explícita do dono; preferir **TRELLIS.2 (MIT)** |

---

## 2. Kits Modulares, Texturas PBR, Personagens e Fixtures de QA (Verificados em 22/11/2026)

| Asset / Pack | Caminho no Repositório | Licença (Verificada) | Fonte / Proveniência | Notas |
| --- | --- | --- | --- | --- |
| **Kenney Prototype Textures** | `unity/Assets/Game/Art/Textures/Prototype/` | **CC0 1.0 (Domínio Público)** | `kenney.nl/assets/prototype-textures` | 75 texturas de grid (1 tile = 1 m) para greybox sem UV esticado |
| **Kenney City Kit (Industrial)** | `unity/Assets/Game/Art/Environment/Modular/` | **CC0 1.0 (Domínio Público)** | `kenney.nl/assets/city-kit-industrial` | 40 peças modulares industriais (substitui o antigo Industrial Kit removido/404) |
| **Kenney Modular Kits (City / Roads / Props)** | `unity/Assets/Game/Art/Environment/Modular/` | **CC0 1.0 (Domínio Público)** | `kenney.nl/assets` (via `tools/art/download_modular_kits.py`) | Kits modulares low-poly usados nas cenas do Studio Core |
| **Poly Haven / AmbientCG PBR (`Mat_PBR_ConcretePanel` e demais `Mat_PBR_*`)** | `unity/Assets/Game/Materials/Modular/Mat_PBR_ConcretePanel.mat` e `unity/Assets/Game/Art/Textures/PBR/` | **CC0 1.0 (Domínio Público)** | `polyhaven.com` / `ambientcg.com` (via `tools/art/download_modular_kits.py` e `generate_studio_textures.py`) | Material PBR usado na fixture de QA `unity/Assets/Game/Scenes/Calibration.unity`; texturas binárias `.png` rastreadas via Git LFS (`.gitattributes`) |
| **Quaternius Universal Animation Library (UAL1 / UAL2)** | `unity/Assets/Game/Art/Characters/Quaternius/` | **CC0 1.0 (Domínio Público)** | `quaternius.com` (`UAL1_Standard.fbx`, `UAL2_Standard.fbx`) | Animações Humanoid padrão vinculadas pelo `Fab3dImporter.cs` |
| **Fixture QA Cena (`Calibration.unity`)** | `unity/Assets/Game/Scenes/Calibration.unity` | **MIT / Interno Studio Core** | Gerado deterministicamente por `Studio.Editor.StudioAutomation.CreateCalibrationScene` | Cena canônica de smoke test de câmera/captura com `Mat_PBR_ConcretePanel` |
| **Fixture QA Regressão Visual (`fixture_biped_deitado`)** | `tests/fixtures/visual_gate/fixture_biped_deitado/` | **MIT / Interno Studio Core** | Gerado via Blender headless (22/11/2026) para teste de regressão de orientação do `validate_visual.py` | Prova determinística de `exit 1` (`ORIENTATION_FAIL`) para personagem bípede deitado; `.fbx` e `.png` sob Git LFS |
