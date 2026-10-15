# Padrões Técnicos de Unity e C#

**Versão da Engine:** Unity 6 LTS (6000.3.x)  
**Render Pipeline:** Universal Render Pipeline (URP)  
**Linguagem:** C# 12 (.NET Standard 2.1 / .NET 8 runtime)  

---

## 1. Convenções de Código C#

- **Nomenclatura:**
  - `PascalCase`: Classes, Structs, Enums, Interfaces (`IPascalCase`), Métodos, Propriedades públicas.
  - `camelCase`: Variáveis locais, parâmetros de métodos.
  - `_camelCase`: Campos privados e protegidos (ex: `private float _moveSpeed;`).
  - `UPPER_SNAKE_CASE`: Constantes (`const float MAX_VELOCITY = 10f;`).
- **Campos Serializados:**
  - Usar sempre `[SerializeField] private Type _fieldName;` em vez de campos públicos expostos.
  - Adicionar atributos de validação onde aplicável: `[Header("...")], [Tooltip("...")], [Range(min, max)]`.
- **Organização de Classes (Ordem de leitura):**
  1. Constantes e Enums estáticos
  2. Campos serializados (`[SerializeField]`)
  3. Campos privados / de estado interno
  4. Propriedades públicas
  5. Ciclo de vida do Unity (`Awake`, `OnEnable`, `Start`, `Update`, `FixedUpdate`, `OnDisable`, `OnDestroy`)
  6. Métodos públicos da API do componente
  7. Métodos privados de lógica interna

---

## 2. Universal Render Pipeline (URP)

- **Render Graph:** Utilizar a API URP Render Graph para qualquer custom pass ou feature de renderização. Evitar métodos legados de ScriptableRendererFeature.
- **Shaders:**
  - Priorizar **Shader Graph** com target URP Lit ou Unlit.
  - Shaders customizados em código devem usar sintaxe HLSL moderna compatível com SRP Batcher (`CBUFFER_START(UnityPerMaterial)`).
- **Post-Processing:**
  - Configurado via Volumes locais/globais.
  - No mobile, manter efeitos pesados (Bloom com alta resolução, Motion Blur, Depth of Field) desligados ou restritos ao perfil High-End.

---

## 3. Gestão de Memória e Garbage Collection (GC)

- **Zero Alocação no Loop Principal:** `Update()`, `FixedUpdate()` e `LateUpdate()` **NÃO DEVEM** alocar na heap gerenciada.
  - Proibido uso de LINQ dentro de loops de frames.
  - Proibido concatenação de strings (`"score: " + score`) em updates; usar `StringBuilder` ou formatação sem alocação.
  - Proibido `FindObjectsByType`, `GetComponent`, ou `GameObject.Find` dentro de loops de frames (cachear no `Awake` ou injetar).
  - Usar `NonAlloc` para consultas de física: `Physics.RaycastNonAlloc`, `Physics.OverlapSphereNonAlloc`.
- **Object Pooling:**
  - Usar a API nativa `UnityEngine.Pool.ObjectPool<T>` para projéteis, partículas temporárias, floating text e inimigos instanciados com frequência.

---

## 4. Input System e Cinemachine

- **Input System:**
  - Utilizar o novo **Unity Input System** (`com.unity.inputsystem`). O legacy `Input.GetKey` é proibido.
  - Ações devem ser mapeadas em um `InputActionAsset` (.inputactions) compartilhado.
  - Suporte nativo e testado para toques móveis (virtual sticks, touch gestures) e gamepads.
- **Cinemachine:**
  - Versão: Cinemachine 3.x.
  - Câmeras virtuais (`CinemachineCamera`) controladas via estados de gameplay, com damping suave adequado a telas touch.
