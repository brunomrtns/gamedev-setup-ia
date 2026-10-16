# Catálogo de Assets do Estúdio (Asset Vault)

Este documento registra o inventário de assets externos organizados e disponíveis no **Projeto Setup A** para alimentar o estúdio autônomo na criação de jogos em múltiplos gêneros.

O banco físico de arquivos reside em `assets_vault/` (com arquivos binários pesados preservados localmente e ignorados pelo Git), indexado pelo arquivo canônico [`assets_vault/catalog.json`](../../assets_vault/catalog.json) e gerenciado pela ferramenta [`tools/art/asset_vault_manager.py`](../../tools/art/asset_vault_manager.py).

---

## 1. Mapeamento por Gêneros de Jogos

Conforme a arquitetura definida em [`docs/standards/architecture.md`](../standards/architecture.md) e [`GameGenreManifest.cs`](../../unity/Assets/Game/Scripts/Foundation/GameGenreManifest.cs):

### A. Jogos de Tiro e Ação (`ActionShooter` / FPS / TPS)
- **Pacotes Principais:**
  - `fps_rifle_system` (`assets_vault/weapons/firearms/fps_rifle_system`):
    - Mesh de rifle de assalto detalhado (`Assault_Rifle_B`), PhysicsAsset e Skeleton.
    - Animações completas em primeira pessoa com rig Manny/Quinn (Idle, Aim, Walk, Run, Fire, Reload).
    - Blueprints de controle de arma (`BP_Weapone`, `BP_ThirdPersonCharacter`), miras e mapeamentos de input (`IA_Move`, `IA_Look`, `IA_Jump`).
  - `medieval_knives_daggers` (Armas secundárias/corpo a corpo).
  - `scifi_skyboxes` (Ambientes e céus de combate sci-fi).

### B. Jogos de Luta e Ação Melee (`Fighting` / Hack & Slash)
- **Pacotes Principais:**
  - `medieval_swords` (`assets_vault/weapons/melee/swords`):
    - 13 modelos de espadas medievais (espadas curtas, longas, montantes).
    - StaticMeshes (`SM_Sword_01` a `SM_Sword_13`) e SkeletalMeshes com ossos de empunhadura (`SKM_Sword_01` a `SKM_Sword_13`).
    - Mapas PBR em 2K e 4K (Albedo/Diffuse, Metallic/Roughness, Normal Map).
  - `medieval_knives_daggers` (`assets_vault/weapons/melee/knives_daggers`):
    - 8+ modelos de adagas e facas com malhas estáticas e esqueléticas.

### C. Jogos de Corrida, Plataforma e Espaciais (`RunnerPlatformer` / `VehicleRacing`)
- **Pacotes Principais:**
  - `scifi_skyboxes` (`assets_vault/environments/sci_fi_skyboxes`):
    - Panoramas cósmicos de alta resolução, nebulosas, planetas e paisagens interestelares.
    - Mapas e materiais para skybox / skydome de pistas futuristas e corredores neon.

### D. Aventura, Sobrevivência e Exploração Submarina (`AdventureExploration` / `RpgSurvival`)
- **Pacotes Principais:**
  - `underwater_coral` (`assets_vault/environments/underwater_coral`):
    - 77 meshes de corais, plantas submarinas, formações rochosas e bioma oceânico.
    - 463 texturas de alta definição e materiais mestre.
    - Cenário completo para exploração subaquática, mergulho ou sobrevivência em alto-mar.

---

## 2. Comandos do Gerenciador de Vault

```powershell
# Listar todos os pacotes disponíveis e seu status
python tools/art/asset_vault_manager.py --list

# Buscar recursos para um gênero específico
python tools/art/asset_vault_manager.py --query shooter
python tools/art/asset_vault_manager.py --query melee
python tools/art/asset_vault_manager.py --query underwater

# Consultar detalhes e estatísticas de um pacote
python tools/art/asset_vault_manager.py --info medieval_swords

# Reindexar o catálogo após extração de novos pacotes
python tools/art/asset_vault_manager.py --rebuild
```
