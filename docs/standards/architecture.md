# Padrões de Arquitetura de Software

## 1. Modularização via Assembly Definitions (asmdef)

Para garantir tempos rápidos de compilação, isolamento e prevenir dependências circulares, todo código deve pertencer a um Assembly Definition explícito:

| Assembly | Papel | Dependências Permitidas |
|---|---|---|
| `Studio.Foundation` | Tipos fundamentais, extensões, pooling, eventos | Nenhuma dependência de features |
| `Studio.Input` | Mapeamento de ações, adaptadores touch | `Studio.Foundation`, `InputSystem` |
| `Studio.Camera` | Controle de Cinemachine, oclusão, transições | `Studio.Foundation`, `Cinemachine` |
| `Studio.Gameplay` | Lógica de regras, movimento, física, interação | `Studio.Foundation`, `Studio.Input` |
| `Studio.UI` | Menus, HUD, feedback em tela | `Studio.Foundation`, `Studio.Gameplay` |
| `Studio.Editor` | Ferramentas de validação, builders, exportadores | UnityEditor, assemblies runtime |
| `Studio.Tests.EditMode` | Testes de unidade e EditMode | `Studio.*`, `UnityEngine.TestRunner` |
| `Studio.Tests.PlayMode` | Testes de integração e PlayMode | `Studio.*`, `UnityEngine.TestRunner` |

---

## 2. Padrões de Comunicação e Desacoplamento

- **ScriptableObject-based Architecture:**
  - Compartilhamento de dados estáticos e configurações balanceáveis via `ScriptableObject`.
  - Eventos de desacoplamento global (Event Channels) para evitar acoplamento direto entre sistemas distantes (ex: `PlayerHealthChangedEventChannelSO`).
- **Injeção de Dependências Leve:**
  - Prefira injeção explícita de referências via inicialização ou compositores de cena a Singletons globais estáticos.
  - Se um serviço global for indispensável (ex: áudio persistente, analytics), expor via Interface (`IAudioService`) com localização centralizada no bootstrap da cena.

---

## 3. Gestão de Cenas e Ciclo de Vida

- **Bootstrap Pattern:**
  - A cena `Bootstrap.unity` inicializa os serviços fundamentais da engine, configurações gráficas e carrega a cena principal de menu ou gameplay aditivamente (`LoadSceneMode.Additive`).
- **Prefab First:**
  - Elementos de gameplay dinâmicos nunca são colocados soltos na cena; sempre existem como Prefabs versionados em `Assets/Game/Prefabs/`.
  - Mudanças em prefabs devem ser salvas no asset do prefab, evitando overrides não intencionais na cena.
