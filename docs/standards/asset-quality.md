# Padrões de Qualidade de Assets 3D e Áudio

Diretrizes técnicas para importação, normalização e validação de assets no pipeline.

---

## 1. Geometria 3D e Modelos

- **Formato Canônico:** `.fbx` ou `.blend` (quando o exportador for automatizado). Para prototipagem rápida e UPM, `.fbx` com eixos congelados é o padrão recomendado.
- **Sistemas de Coordenadas:**
  - Y-up, Z-forward (padrão Unity).
  - Transformações zeradas na raiz: Posição `(0,0,0)`, Rotação `(0,0,0)`, Escala `(1,1,1)`.
- **Pivô:**
  - Personagens/criaturas: Na base dos pés (y = 0), centralizado em X e Z.
  - Props estáticos/móveis: Na base de apoio ou centro de gravidade lógico.
  - Projéteis/armas: No ponto de empunhadura ou ponto de disparo.
- **Topologia:**
  - Somente quads e tris limpos; proibido n-gons (> 4 lados).
  - Normais devidamente unificadas e suavizadas (Smooth Shading com ângulos apropriados, ex: 30–60°).
  - Escala real em metros (1 unidade Unity = 1 metro). Altura de humanoide médio: 1.70m a 1.85m.

---

## 2. Texturas e Materiais

- **Pipeline de Materiais:** URP Lit Shader padrão.
- **Canal de Textura (Packing):**
  - Albedo (RGB) + Alpha / Transparência (A).
  - Mask Map / Metallic-Gloss: Metallic (R), Occlusion (G), Detail Mask (B), Smoothness (A).
  - Normal Map (formato Tangent Space).
- **Sem Texturas Desnecessárias:** Não importar texturas 4K para objetos de fundo. Usar o limite de `maxTextureSize: 1024` ou `512` nos presets de importação.

---

## 3. Rigs e Animações

- **Rig de Personagens:** Compatível com Unity Mecanim **Humanoid** quando aplicável, permitindo retargeting de animações universais.
- **Otimização de Curvas:** Compressão de animação ativada (`Keyframe Reduction` com taxa padrão do Unity).
- **Looping:** Animações de caminhada, corrida e idle com loop perfeitamente alinhado sem saltos perceptíveis.

---

## 4. Áudio

- **Formatos:**
  - Efeitos sonoros curtos (SFX < 3 segundos): `.wav`, carregados descompactados em memória (`Decompress On Load`).
  - Músicas e ambientes longos (> 5 segundos): `.ogg` ou `.mp3`, com streaming contínuo (`Streaming` ou `Compressed In Memory`).
- **Mixers:** Todos os AudioSources devem direcionar para o `AudioMixer` do jogo (`Master -> SFX`, `Master -> Music`, `Master -> UI`).

---

## 5. Protocolo Obrigatório de Validação de Assets do Prompt (Anti-Divergência Silenciosa)

- **Validação Prévia Mandatória:**
  Quando o usuário solicitar no prompt o uso de assets específicos (seja em `assets_vault/`, pastas de downloads ou repositórios externos), o agente **DEVE inspecionar previamente o formato e a compatibilidade nativa** antes de construir a cena ou gerar prefabs.
- **Proibição de Divergência Silenciosa:**
  Se os assets indicados possuírem incompatibilidade técnica estrutural (exemplo: arquivos `.uasset` de Unreal Engine não-compilados em um projeto Unity):
  1. **É TERMINANTEMENTE PROIBIDO** criar uma solução paliativa em silêncio (como blocos geométricos primitivos, cones/cubos ou modelos sem textura).
  2. **O agente DEVE INTERROMPER imediatamente o fluxo** e questionar o usuário via ferramenta de questionamento (`ask_question`), informando:
     - O diagnóstico técnico da limitação.
     - O porquê da impossibilidade de uso direto.
     - As opções reais para o usuário escolher o rumo (ex: fornecer o asset nativo em `.unitypackage`/`.fbx`, exportar via engine de origem ou definir uma biblioteca alternativa).

---

## 6. Proibição de Jogos Sem Textura / Blocos Primitivos Planos

- **Nenhum jogo criado pelo estúdio deve ser composto por cubos, esferas ou superfícies monocromáticas lisas sem textura UV.**
- Todo modelo 3D em cena (armas em 1ª pessoa, cenários, inimigos, coletáveis e props) deve conter:
  1. Coordenadas UV desdobradas e contínuas (`Smart UV` ou unwrap profissional).
  2. Mapeamento de textura PBR real no shader URP Lit:
     - Mapa de Albedo/Difuso (`_BaseMap`) com variação de cor e desgaste.
     - Mapa de Normais (`_BumpMap`) para relevo e microdetalhes.
     - Ajuste adequado de Metallic e Smoothness (sem estourar emissão que lave os detalhes).
  3. Escala métrica rigorosa conferida via `Renderer.bounds.size` antes de renderizar capturas de evidência.

---

## 7. Normalização de Eixos e Validação de Orientação de Malhas (Anti-Inversão Blender/Unity)

- **A Armadilha de Coordenadas Blender -> Unity:**
  - O Blender utiliza nativamente **+Z como CIMA** e **-Y/+Y como FRENTE**.
  - A Unity utiliza nativamente **+Y como CIMA** e **+Z como FRENTE**.
  - Quando modelos são gerados no Blender ou importados de pacotes externos, o eixo longitudinal (comprimento) comumente fica mapeado no eixo **Y**. Se instanciado com rotação identidade `(0,0,0)`, o objeto apontará diretamente para o teto (vertical).
- **Diretrizes de Alinhamento Mandatório:**
  1. **Armas em 1ª Pessoa:**
     - O cano/traçador deve apontar estritamente para **+Z (Frente/Mira)**.
     - A mira holográfica/trilho Picatinny deve apontar para **+Y (Cima)**.
     - O carregador/gatilho deve apontar para **-Y (Baixo)**.
     - Rotação de correção padrão para malhas com cano em -Y: `Quaternion.Euler(-90f, 0f, 0f)`.
  2. **Fauna e Criaturas Vivas (Peixes, Animais):**
     - O focinho/cabeça deve estar orientado para o vetor de nado/deslocamento (**+Z**).
     - A barbatana dorsal/coluna deve estar virada para **+Y (Cima)**.
     - A barriga deve estar virada para **-Y (Baixo)**.
     - Rotação de correção para malhas com cabeça em +Y e dorso em +Z: `Quaternion.Euler(0f, heading, 0f) * Quaternion.Euler(-90f, 180f, 0f)`.
  3. **Veículos, Drones e Props:**
     - O sensor frontal/face deve apontar para a frente lógica do movimento ou em direção ao jogador em caso de inimigos patrulheiros (`Quaternion.Euler(0f, 180f, 0f)`).
  4. **Inspeção Prévia Automatizada:**
     - Todo `SceneBuilder` deve validar as dimensões de `bounds.size` para certificar-se de que o eixo maior do objeto corresponde à direção correta antes de salvar a cena ou gerar evidências.

---

## 8. Pipeline Canônico V2 de Normalização Headless (OBRIGATÓRIO)

**Proibições explícitas (falha de build nos validadores V2):**
1. **Modelos exportados com rotação descompensada nos eixos X/Z:** é proibido entregar `.fbx` cujo nó raiz contenha rotação residual (ex: `-90°` em X não compensado pela conversão Z-up→Y-up). O transform da raiz deve ser **identidade** (location `0,0,0`, rotation `0,0,0`, scale `1,1,1`). FBX binários são inspecionados via Blender headless (`tools/art/inspect_fbx.py`); FBX ASCII via parser determinístico.
2. **Malhas sem canal UV0:** todo modelo renderizável deve possuir UV mapping válido (presente, dentro do espaço 0-1 e não-degenerado). Malhas importadas sem UVs úteis devem ser reprocessadas com `bpy.ops.uv.smart_project(angle_limit=66.0, island_margin=0.02)` no pipeline headless.
3. **N-gons:** geometria exportada deve ser triangulada (`quads_convert_to_tris` BEAUTY).

**Comandos canônicos do pipeline (executados no WSL — ver AGENTS.md §3):**
```bash
# Normalização individual (presets: weapon | character | prop | kit | raw)
wsl blender -b -P tools/art/normalize_fbx.py -- --input <fbx> --output <fbx> --preset weapon

# Normalização em lote (manifesto JSON de entradas)
wsl blender -b -P tools/art/batch_normalize.py -- <manifest.json>
```

**Políticas de pivô por preset (fbx_canonical.py):**
- `character`: origem na base dos pés `(0,0,0)`, centralizado em X/Z.
- `weapon`: origem no ponto de empunhadura, eixo longitudinal alinhado a **+Z frontal** (+Y topo) — detecção PCA de muzzle (extremidade de menor seção transversal) com hints manuais (`--forward`, `--up`) quando a orientação de origem é conhecida.
- `prop`/`character`: base de apoio no chão.
- `kit`: **preserva o pivô do autor** (essencial para kitbashing/snapping de peças modulares Kenney).

**Exportação FBX canônica (Blender 5.0+):** `axis_forward='-Z'`, `axis_up='Y'` — o exportador nativo aplica unit scale e bake space transform inerentemente (verificado por round-trip determinístico com transform identidade).
