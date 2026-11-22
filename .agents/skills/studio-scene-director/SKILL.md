---
name: studio-scene-director
description: 'Diretor de Cenario e Level Design do Studio V2. Governa a construcao de qualquer cena 3D pelo metodo anti-lixo C1-C5 (C1 Diagrama Top-Down -> C2 Greybox Fechado Jogavel -> C3 Kit CC0 + PBR World-Scale -> C4 Composicao & Camera -> C5 Substituicao in-place por Props da Fabrica 3D), exigindo captura MCP e aprovacao humana em cada fase.'
---

# Studio Scene Director — Método C1–C5 para Construção de Cenários 3D

**Goal:** eliminar os 5 sintomas clássicos de falha de cenário por IA:
1. Espalhamento de objetos sem layout nem level design;
2. Piso/parede com UV `1×1` esticado (escala de textura absurda);
3. Primitivas cruas (`GameObject.CreatePrimitive`) usadas como prop final;
4. Fundo *void* sem paredes, teto ou fechamento espacial;
5. Câmeras de gameplay ou cutscene filmando geometria quebrada ou atravessando paredes.

**Regra de Ouro:** Toda cena de qualquer jogo novo passa obrigatoriamente pelas 5 fases sequenciais **C1 → C2 → C3 → C4 → C5**. Cada fase termina em **captura MCP datada em `evidence/captures/` + 1 frase do agente descrevendo o que vê + OK humano**. É proibido avançar para a próxima fase sem aprovação da fase anterior.

---

## As 5 Fases Obrigatórias (C1–C5)

### C1 — Diagrama Top-Down de Layout (Zero GameObjects no Unity)
- **Entrada:** `docs/gdd/05-level-design.md` (ou GDD equivalente) + `docs/art/art-bible.yaml` + `docs/art/fab3d-asset-spec.yaml`.
- **Entregável:**
  1. Documento Markdown (`docs/level-design/<cena>-layout.md`) + Diagrama visual PNG (`evidence/concepts/<cena>_topdown_layout.png`).
  2. Zonas demarcadas com **dimensões exatas em metros** (largura × comprimento) e **altura de teto**.
  3. **Focal point único** da cena claramente identificado.
  4. **Fluxo do jogador** passo a passo (spawn → interação/diálogo → mecânica central → combate/saída).
  5. **Tabela de props por zona** com coordenadas previstas, função e budget de triângulos (`max_tris`).
- **Gate C1:** **PARE e mostre o diagrama ao dono.** Sem OK humano, não existe nenhum `GameObject` no Unity.

### C2 — Greybox Fechado Jogável (Mecânica Primeiro, Proibido Arte IA)
- **Entregável:**
  1. Espaço arquitetônico **100% fechado** (piso + paredes + teto cinza em 2–3 níveis de altura; **jamais fundo void**).
  2. Piso com material de **grid de debug (`Kenney Prototype Textures`, densidade 1 tile = 1 metro)**.
  3. Personagens e props representados por **placeholders padronizados no eixo correto** (`Y-up`, `+Z para frente`, escala real em metros, tags/nomes definitivos).
  4. Iluminação base (1 direcional/zenital + 2–3 luzes pontuais nos focal points).
  5. **Toda a mecânica do slice funcionando** sobre o greybox (`InteractionSystem`, `DialoguePresenter`, combate/puzzles).
- **Gate C2:** Captura MCP multi-ângulo datada em `evidence/captures/` + checklist visual (nada deitado, nada dentro de parede, zero void, grid 1 m visível) + 1 frase descritiva + OK humano.

### C3 — Vestir com Kit Modular CC0 Real + PBR em Escala de Mundo
- **Entregável:**
  1. Volumes estruturais vestidos com **Kenney City Kit (Industrial)** (40 peças CC0) ou kits modulares CC0 registrados em `LICENSES-ARTE.md`.
  2. Materiais PBR CC0 (**Poly Haven / AmbientCG**) com **tiling em escala de mundo: 1 tile = 1 a 2 metros e densidade de texel IDÊNTICA em toda superfície** (proibido UV `1×1` esticado).
  3. Tubulações e detalhes utilitários gerados via `ProceduralMeshLibrary` do Studio (**proibido `GameObject.CreatePrimitive`**).
  4. Skybox industrial ou fog volumétrico denso eliminando qualquer horizonte vazio.
- **Gate C3:** `validate_scene.py --strict` exit 0 + captura MCP multi-ângulo datada + 1 frase descritiva + OK humano.

### C4 — Composição Visual & Auditoria de Câmeras (Gameplay + Cutscenes)
- **Entregável:**
  1. **1 focal point principal** + 1 ponto de interesse visual a cada **5–8 metros** do caminho do jogador.
  2. Todos os rails/shots de cutscenes (`CutsceneBuilder`) auditados zona por zona via screenshots MCP (nenhuma câmera pode cortar parede, filmar de dentro da geometria ou enquadrar vazio).
- **Gate C4:** Mostrar ao dono **as 3 piores imagens** das câmeras (não apenas as melhores) + 1 frase descritiva por imagem + OK humano.

### C5 — Substituição In-Place pelos Props e Personagens da Fábrica 3D
- **Entregável:**
  1. Apenas após a mecânica e o cenário C1–C4 estarem aprovados, os assets autorais da Fábrica 3D (`TRELLIS.2-4B + Hunyuan3D-Paint + pack_glb modo game` via `fab3d_bridge.py`) entram **um por vez**.
  2. Cada asset passa pela ordem canônica de gates (`glTF-Validator` → `validate_asset` → `validate_visual` → `Fab3dImporter` + captura datada).
  3. O modelo final **substitui o placeholder no MESMO transform** (posição, rotação e escala herdadas) — a cena jamais é reorganizada na fase de arte.
- **Gate C5:** Captura MCP após cada substituição comparada ao conceito 2D aprovado + `validate_visual` exit 0 + OK humano.
