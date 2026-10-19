# Arquitetura Técnica — ABYSSAL BREACH (Jogo 2)

**Projeto:** Setup A — Jogo 2 (`abyssal-breach`)  
**Engine:** Unity 6.3 LTS (`6000.3.26f1`) · URP 17.3  
**Padrão Arquitetural:** Modular por Assembly Definitions (`Studio.*`), orientado a contratos em `Studio.Foundation`.

---

## 1. Mapa de Assemblies e Módulos

O Jogo 2 compartilha e estende a arquitetura limpa do estúdio sem quebrar nenhuma funcionalidade do Jogo 1 (`Neon Rush`):

| Assembly | Scripts de Abyssal Breach | Responsabilidades |
|---|---|---|
| `Studio.Foundation` | `AbyssalMath.cs`, `GameGenreManifest.cs`, `ProceduralAudioService.cs` | Cálculos puros (flutuabilidade, consumo de O2, dispersão balística, recuo, pontuação), síntese procedural de áudio subaquático (disparo de cavitação, faca, coleta de O2, sonar). |
| `Studio.Input` | `AbyssalInputAdapter.cs` | Implementa `IGameInputAdapter`. Lê teclado/mouse via Novo Input System (com fallback seguro) e expõe eventos e métodos determinísticos (`TriggerFire`, `TriggerToggleAim`, `TriggerReload`, `TriggerMelee`, `TriggerSwitchWeapon`, `SetMoveInput`, `SetVerticalInput`, `SetLookDelta`, `SetSprint`). |
| `Studio.Gameplay` | `AbyssalFirstPersonMotor.cs`, `AbyssalWeaponController.cs`, `AbyssalThreatDrone.cs`, `AbyssalCollectible.cs`, `AbyssalExtractionPod.cs`, `OxygenSurvivalSystem.cs`, `AbyssalGameManager.cs` | Motor FPS 3D subaquático (`ICharacterMotor`), máquina de estados de armas (Rifle ADS/Fire/Reload + Faca Melee), IA de drones hostis, sobrevivência de O2/HP, coletáveis e gerenciador de partida/extração. |
| `Studio.Cameras` | `FirstPersonCameraRig.cs` | Controle de pitch/yaw em primeira pessoa, FOV zoom suave em ADS, head-bobbing hidrodinâmico e trauma de câmera (recoil/dano). |
| `Studio.UI` | `AbyssalHUDManager.cs` | HUD diegético do capacete de mergulho: barras de O2 e HP, munição e arma ativa, bússola/profundidade, contador de núcleos, retículo dinâmico (crosshair + hitmarker) e telas de Start/Victory/GameOver. |
| `Studio.Editor` | `AbyssalBreachSceneBuilder.cs`, `BuildPipeline.cs` | Construtor determinístico da cena `AbyssalBreach.unity`, geração de materiais URP com texturas extraídas do `assets_vault/`, captura automatizada de 6 ângulos de evidência e build Windows Standalone (`AbyssalBreach.exe`). |
| `Studio.Tests.EditMode` | `AbyssalBreachGameplayTests.cs` | Suíte completa de testes unitários e de integração cobrindo matemática subaquática, controles sem condição de corrida, combate FPS, oxigênio e extração. |

---

## 2. Pipeline de Conversão do `assets_vault`

Os pacotes do `assets_vault` (`underwater_coral`, `fps_rifle_system`, `medieval_knives_daggers`, `scifi_skyboxes`) são processados pelo script determinístico `tools/art/convert_abyssal_assets.py`:
1. **Extração Direta de Texturas PBR Reais:** Extrai os blocos PNG de alta resolução embutidos nos arquivos `.uasset` de `T_Coral_*`, `T_Assault_Rifle_B_*`, `T_KNIFE_01_4K_*` e `T_ScifiSkies_*`, normalizando-os para `unity/Assets/Game/Art/AbyssalBreach/Textures/` (1024x1024 / 2048x2048 otimizados para URP).
2. **Geração de Malhas 3D (.FBX) via Blender Headless:** Gera as malhas 3D UV-mapeadas em `.fbx` dentro de `unity/Assets/Game/Art/AbyssalBreach/Models/`:
   - `SM_Assault_Rifle_B.fbx` (Rifle tático submarino detalhado com corpo, cano, mira óptica, carregador e empunhadura).
   - `SM_Tactical_Knife.fbx` (Faca tática de combate e sobrevivência).
   - `SM_Coral_Reef_Cluster.fbx`, `SM_Coral_Brain.fbx`, `SM_Coral_Tube.fbx`, `SM_Abyssal_RockPillar.fbx` (Formações de corais e rochas abissais).
   - `SM_Sunken_Pod.fbx`, `SM_Extraction_SubPod.fbx`, `SM_SubDrone_Hostile.fbx` (Elementos mecânicos da missão).
