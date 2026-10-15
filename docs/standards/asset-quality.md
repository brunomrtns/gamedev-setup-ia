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
