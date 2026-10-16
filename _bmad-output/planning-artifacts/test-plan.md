# Plano de Testes e Critérios de Conclusão (Quality Gates) — NEON RUSH

**Projeto:** NEON RUSH  
**Framework:** Unity Test Framework (UTF)  
**Relatórios:** JUnit XML em `evidence/test-reports/junit-results.xml`  

---

## 1. Suíte de Testes EditMode (Unidade e Validação Lógica)

Residência: `unity/Assets/Game/Tests/EditMode/`  
Assembly: `Studio.Tests.EditMode`

| ID Teste | Descrição | Comportamento Esperado |
|---|---|---|
| `TEST-EDIT-001` | `LaneCalculator_ChangeLeftFromCenter_ReturnsLeftLanePosition` | Troca para faixa esquerda calcula X = -2.2f |
| `TEST-EDIT-002` | `LaneCalculator_ChangeLeftFromLeftmost_ClampsToLeftLane` | Troca além do limite esquerdo permanece em X = -2.2f |
| `TEST-EDIT-003` | `LaneCalculator_ChangeRightFromCenter_ReturnsRightLanePosition` | Troca para faixa direita calcula X = +2.2f |
| `TEST-EDIT-004` | `ScoreCalculator_AddEnergyCell_IncreasesScoreByHundred` | Coleta de célula adiciona exatamente 100 pontos e 1 ao contador |
| `TEST-EDIT-005` | `ScoreCalculator_DistanceTraveled_ComputesTenPointsPerMeter` | Distância de 50 metros calcula 500 pontos |
| `TEST-EDIT-006` | `JumpTrajectory_AtHalfDuration_ReachesApexHeight` | Cálculo parabólico de salto atinge ápice previsto |
| `TEST-EDIT-007` | `GameManager_InitialState_IsWaitingToStart` | Estado inicial é Idle/WaitingToStart |
| `TEST-EDIT-008` | `GameManager_TriggerVictory_SwitchesStateAndLocksMovement` | Ativação de vitória congela movimento e define estado Victory |

---

## 2. Quality Gates Determinísticos

Para considerar a vertical slice aprovada:
1. **Compilação C#:** 0 erros de compilação em todos os assemblies runtime e editor.
2. **UTF Tests:** 100% de aprovação na suíte de testes EditMode executada via batchmode.
3. **Validação de Cena:** Execução de `tools/validation/validate_scene.py` com código de retorno 0 (sem scripts faltantes, componentes corrompidos ou referências nulas).
4. **Inspeção Visual:** Screenshots gerados em `evidence/captures/` cobrindo início, pista com obstáculos, coleta de células e portal de extração.
5. **Build Android:** Script `tools/android/build_apk.py` compila com sucesso o APK ARM64 IL2CPP em `builds/`.
