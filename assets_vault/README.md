# 🏛️ Studio Asset Vault

Banco de assets de alta qualidade do estúdio autônomo (**Projeto Setup A**), preparado para abastecer múltiplos gêneros de jogos gerados por agentes de inteligência artificial.

---

## 📁 Estrutura de Diretórios

```text
assets_vault/
├── catalog.json                                # Catálogo de inventário estruturado e indexado
├── weapons/
│   ├── firearms/
│   │   └── fps_rifle_system/                   # Sistema FPS completo (Rifle, Animações, Blueprints)
│   └── melee/
│       ├── knives_daggers/                     # Facas e adagas medievais de combate e sobrevivência
│       └── swords/                             # 13 espadas medievais detalhadas com PBR 2K/4K
└── environments/
    ├── sci_fi_skyboxes/                        # Panoramas cósmicos, nebulosas e céus sci-fi
    └── underwater_coral/                       # Recifes de corais, flora submarina e bioma marinho
```

---

## 📊 Pacotes Disponíveis no Vault

| ID | Pacote | Categoria | Gêneros de Jogos | Inventário |
|---|---|---|---|---|
| `fps_rifle_system` | First-Person Shooter Rifle System | `weapons/firearms` | `ActionShooter`, `RpgSurvival`, `AdventureExploration` | 90 .uassets (Meshes, Animações, Blueprints, Texturas) |
| `medieval_swords` | Medieval Swords Pack | `weapons/melee` | `Fighting`, `ActionShooter`, `RpgSurvival` | 151 .uassets (13 espadas, Static & Skeletal, Texturas 2K/4K) |
| `medieval_knives_daggers` | Medieval Knives and Daggers | `weapons/melee` | `Fighting`, `RpgSurvival`, `AdventureExploration` | 96 .uassets (8+ facas/adagas, Static & Skeletal, PBR) |
| `scifi_skyboxes` | Matte Painting Sci-Fi Skies | `environments/skies` | `RunnerPlatformer`, `VehicleRacing`, `ActionShooter` | 63 .uassets (Panoramas HDRI de alta resolução, Skydomes) |
| `underwater_coral` | Underwater Coral Pack 01 | `environments/underwater`| `AdventureExploration`, `RpgSurvival` | 694 .uassets (Corais, rochas, fundos oceânicos, materiais) |

---

## 🛠️ Como Consultar via Linha de Comando (CLI)

Qualquer agente de IA ou desenvolvedor pode utilizar a ferramenta oficial [`tools/art/asset_vault_manager.py`](../tools/art/asset_vault_manager.py):

```powershell
# Listar todos os pacotes e estatísticas
python tools/art/asset_vault_manager.py --list

# Buscar assets por gênero ou palavra-chave
python tools/art/asset_vault_manager.py --query melee
python tools/art/asset_vault_manager.py --query shooter
python tools/art/asset_vault_manager.py --query underwater

# Ver inventário detalhado de um pacote
python tools/art/asset_vault_manager.py --info fps_rifle_system
python tools/art/asset_vault_manager.py --info medieval_swords

# Reindexar o catálogo após adicionar novos pacotes
python tools/art/asset_vault_manager.py --rebuild
```

---

## 🔄 Pipeline de Conversão (.uasset -> Unity URP / FBX / PNG)

Os arquivos brutos estão no formato canônico da Unreal Engine 5.5/5.6 (`.uasset`):
- Para jogos nativos em Unreal Engine, basta copiar a pasta `extracted/<Pacote>/Content/` diretamente para o diretório `Content/` do projeto.
- Para integração no Unity (URP), malhas estáticas e texturas podem ser exportadas para `.fbx` e `.png`/`.tga` via **UModel / UE Viewer** ou exportador de assets do estúdio.
