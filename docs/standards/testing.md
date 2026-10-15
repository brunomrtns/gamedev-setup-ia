# Padrões de Teste e Quality Gates

**Framework de Teste:** Unity Test Framework (UTF)  
**Níveis de Teste:** EditMode (unidade/lógica pura) e PlayMode (integração de cena, física e input)

---

## 1. Organização e Nomenclatura

- Todos os testes devem residir sob `Assets/Game/Tests/` ou assembly equivalente:
  - `Assets/Game/Tests/EditMode/` -> Assembly `Studio.Tests.EditMode`
  - `Assets/Game/Tests/PlayMode/` -> Assembly `Studio.Tests.PlayMode`
- **Padrão de Nome de Arquivo:** `<FeatureName>Tests.cs` (ex: `PlayerMovementTests.cs`, `HealthSystemTests.cs`).
- **Padrão de Nome de Método:** `MethodOrEvent_StateUnderTest_ExpectedBehavior`
  - Exemplo: `TakeDamage_WhenHealthDropsToZero_FiresDiedEvent()`
  - Exemplo: `Move_WhenTouchApplied_UpdatesVelocitySmoothly()`

---

## 2. Testes em EditMode (Rápidos)

- **Propósito:** Testar lógica de domínio C#, estruturas matemáticas, cálculos de dano, parseamento de dados, validação de regras de inventário, sem inicializar cenas ou física.
- **Execução:** Devem rodar em milissegundos e sem alocações não liberadas.
- **Estrutura:**
```csharp
[Test]
public void CalculateVelocity_GivenInputAndAcceleration_ReturnsClampedSpeed()
{
    // Arrange
    var calculator = new MovementCalculator(maxSpeed: 5f, accel: 10f);
    
    // Act
    float speed = calculator.ComputeSpeed(currentSpeed: 4f, inputMagnitude: 1f, deltaTime: 0.2f);
    
    // Assert
    Assert.AreEqual(5f, speed, 0.001f);
}
```

---

## 3. Testes em PlayMode (Integração de Engine)

- **Propósito:** Validar comportamentos dependentes de física (`Rigidbody`), colisores, `CinemachineCamera`, `InputSystem` e ciclo de vida de componentes MonoBehaviour.
- **Corrotinas:** Usar o atributo `[UnityTest]` com `IEnumerator` para aguardar frames (`yield return null;`) ou passos de física (`yield return new WaitForFixedUpdate();`).
- **Limpeza de Objetos:** Todo GameObject instanciado em PlayMode DEVE ser destruído no `[TearDown]` ou no final do teste com `Object.DestroyImmediate(go)` ou `Object.Destroy(go)`.

---

## 4. Quality Gates e Critérios de Falha Automática

O pipeline de teste falha se qualquer um dos seguintes critérios ocorrer:
1. Qualquer teste `[Test]` ou `[UnityTest]` falhar ou lançar exceção não capturada.
2. Qualquer mensagem de `Debug.LogError` ou `Debug.LogException` for emitida durante a execução.
3. Vazamento de memória de cena (objetos não liberados no unload).
4. Relatório estruturado gerado em formato JSON / NUnit XML sob `evidence/test-reports/`.
