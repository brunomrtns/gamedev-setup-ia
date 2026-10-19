# ADR-004: Arquitetura e Pipeline de Assets para "Abyssal Breach" (Jogo 2)

- **Status:** Aceito
- **Data:** 2026-10-16
- **Contexto:** Criação do segundo jogo do estúdio autônomo (**Abyssal Breach**, FPS Tático de Sobrevivência e Extração Subaquática) compartilhando o projeto Unity 6.3 LTS URP com o primeiro jogo (**Neon Rush**).

## Decisões

1. **Coexistência Multi-Jogo sem Regressão:**
   - Todos os contratos em `Studio.Foundation` (`ICharacterMotor`, `IGameInputAdapter`, `GameGenre.ActionShooter`) são implementados pelos novos sistemas (`AbyssalFirstPersonMotor`, `AbyssalInputAdapter`) mantendo 100% de compatibilidade com os testes e cenas de `Neon Rush`.
2. **Motor FPS Subaquático Determinístico (`StepSimulation`):**
   - Assim como em `PlayerMovement`, o `AbyssalFirstPersonMotor`, o `AbyssalWeaponController` e o `OxygenSurvivalSystem` expõem métodos explícitos de avanço de simulação (`StepSimulation(float dt)`) além do loop `Update()` padrão do Unity, permitindo testes automatizados determinísticos em milissegundos no UTF sem depender de espera real de tempo.
3. **Pipeline de Conversão de Assets do Vault (`convert_abyssal_assets.py`):**
   - Extrai as texturas PBR reais diretamente dos pacotes `.uasset` do `assets_vault` (`underwater_coral`, `fps_rifle_system`, `medieval_knives_daggers`, `scifi_skyboxes`) e gera as malhas `.fbx` otimizadas via Blender headless para `Assets/Game/Art/AbyssalBreach/`, respeitando os orçamentos de performance do estúdio.
4. **Eliminação de Condições de Corrida no Ciclo de Vida (`Awake`/`OnEnable`):**
   - O `AbyssalInputAdapter` e o `AbyssalGameManager` utilizam lazy-lookup seguro em `Instance` (`FindAnyObjectByType`) e inscrição resiliente (`EnsureInputSubscribed()`), garantindo funcionamento idêntico independentemente da ordem de instanciação na cena.
