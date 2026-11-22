# Análise de Lacunas Unity MCP — `docs/mcp-gap-analysis.md` (P4 — Somente Catálogo)

> **Escopo (Turno 3 — P4):** Levantamento comparativo técnico entre nossa ponte ativa (**CoderGamester/mcp-unity**, Rota B, MIT) e duas alternativas open-source maduras (**IvanMurzak/Unity-MCP**, Apache-2.0, e **AnkleBreaker/unity-mcp-server**, MIT).
> **Decisão vigente:** **NÃO trocar a ponte atual (`CoderGamester/mcp-unity`) agora.** Este documento propõe apenas quais capacidades isoladas valem absorver no futuro sobre o Studio Core.

---

## 1. Matriz Comparativa de Capacidades

| Capacidade | **CoderGamester/mcp-unity** *(Ativa — Rota B, MIT)* | **IvanMurzak/Unity-MCP** *(Apache-2.0 + PolyForm Shield)* | **AnkleBreaker/unity-mcp-server** *(MIT)* | Situação Atual no Studio Core |
| --- | --- | --- | --- | --- |
| **Licença Comercial p/ Jogo** | ✅ MIT (100% livre) | ✅ Apache-2.0 (PolyForm restringe apenas concorrentes AI) | ✅ MIT (100% livre) | Resolvido (`LICENSES-ARTE.md`) |
| **Execução de Menu Items / Console Logs** | ✅ Sim (`execute_menu_item`, `get_console_logs`) | ✅ Sim | ✅ Sim | Atendido pela ponte ativa |
| **Manipulação de Cena / GameObjects / Materiais** | ✅ Sim (`update_gameobject`, `set_transform`, `create_material`, `batch_execute`) | ✅ Sim (`rql-query` / `rql-mutate` declarativo) | ✅ Sim | Atendido pela ponte ativa |
| **Captura Nativa de Screenshot (Scene View / Game View / Isolated Prefab)** | ⚠️ Limitada / depende de `StudioAutomation.CaptureScreenshotBatch` em batchmode | ✅ **Sim (`screenshot-isolated`, Scene/Game view em tempo real no editor aberto)** | ✅ **Sim (captura direta de câmera/SceneView sem reiniciar batchmode)** | **LACUNA #1:** Hoje dependemos de `Unity -batchmode` (~20–30s por captura) |
| **Execução de Testes EditMode/PlayMode via MCP** | ✅ Sim (`run_tests` básico) | ✅ **Sim (`tests-run` com filtro por assembly/classe + JSON estruturado)** | ✅ Sim (streaming de resultado NUnit) | **LACUNA #2:** Hoje rodamos `-runTests` via CLI batchmode |
| **Execução Dinâmica de C# (Roslyn `script-execute`)** | ❌ Não | ✅ **Sim (avaliação C# instantânea sem recompilar projeto inteiro)** | ⚠️ Parcial | **LACUNA #3:** Útil para inspeção rápida de bounds/colisores em cena viva |
| **Query Declarativa de Cena (RQL / Filtros em Lote)** | ⚠️ Somente hierarquia simples (`get_scenes_hierarchy`) | ✅ **Sim (`rql-query` para auditar todos os lights/materials/renderers em 1 chamada)** | ⚠️ Busca por nome/tag | **LACUNA #4:** Complementaria `validate_scene.py` em tempo real no Editor |

---

## 2. O Que Vale Absorver (Sem Trocar a Ponte Atual)

1. **Absorção Prioritária #1 — Ferramenta `capture_isolated_prefab` / `capture_active_scene` no Editor (inspirada em `IvanMurzak/Unity-MCP` e `AnkleBreaker`):**
   - Adicionar um endpoint C# leve em `Studio.Editor` acionável via `execute_menu_item` do `CoderGamester/mcp-unity` para tirar screenshots instantâneas (`< 1 s`) diretamente do Editor aberto (sem abrir/fechar `-batchmode` a cada ajuste de layout ou iluminação).
2. **Absorção Prioritária #2 — Query de Auditoria de Cena em Tempo Real (`rql`-like JSON dump):**
   - Expor um método `Studio.Editor.SceneAuditTool.DumpAndroidUrpMetricsJson()` invocável pela ponte atual, permitindo que o agente consulte `lights`, `draw_calls` e `static_batching` em tempo real sem depender apenas do parser YAML offline.
3. **NÃO Absorver Agora — `script-execute` via Roslyn:**
   - Risco de quebrar a disciplina de código versionado e rastreável em git (permite mutações ad-hoc não commitadas na cena).

---

## 3. Análise de Risco de Troca da Ponte

| Risco | Impacto | Recomendação |
| --- | --- | --- |
| **Retrabalho de configuração MCP + pacote UPM nos 2 repos** | Médio (quebra o fluxo estável validado na Etapa 0) | **Manter `CoderGamester/mcp-unity`** até fechar o Vertical Slice M1–M2 do CONCERTO. |
| **Cláusula PolyForm Shield 1.0.0 (`IvanMurzak`)** | Baixo para jogos comerciais, mas exige atenção se o Studio Core virar produto comercial | Preferir absorver conceitos pontuais sob MIT/código próprio em `Studio.Editor` em vez de adotar dependência integral. |
| **Maturidade de `AnkleBreaker`** | Comunidade menor que `CoderGamester` e `IvanMurzak` | Usar apenas como referência de API de screenshot. |

**Conclusão:** Manter **CoderGamester/mcp-unity (MIT)** como ponte oficial do Studio Core e, em um ciclo futuro (pós-M2), implementar as 2 extensões leves acima em `Studio.Editor` mantendo 100% de compatibilidade com os validadores e a Lei 6a (`Calibration.unity`).
