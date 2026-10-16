# Art Bible — Direção Artística e Identidade Visual

**Versão:** 1.0  
**Estilo-Chave:** Stylized Low-Poly / Clean PBR Minimalista  
**Gênero-Alvo:** 3D Action / Exploration Mobile  

---

## 1. Visão Estética — NEON RUSH

A estética de **NEON RUSH** adota o estilo **Stylized Cyberpunk / High-Contrast Sci-Fi**, combinando silhuetas limpas e marcantes com uma iluminação noturna expressiva para garantir máxima legibilidade em telas pequenas de smartphones Android.

### Pilares Visuais
1. **Silhuetas Legíveis:** Obstáculos e coletáveis possuem formatos geométricos distintos e bordas destacadas para leitura instantânea em alta velocidade.
   - *Triângulos / Feixes pontiagudos:* Perigo, exigem pulo.
   - *Vigas suspensas / Scanners horizontais:* Exigem deslize.
   - *Monólitos retangulares pesados:* Bloqueio de faixa, exigem esquiva lateral.
   - *Octaedros / Cristais giratórios:* Células de energia coletáveis.
2. **Paleta de Cores Tridimensional Neon:**
   - **Pista & Asfalto:** Grafite escuro / Carbono (`#0d0d14`) com faixas de rolamento delimitadas por linhas neon ciano e magenta.
   - **Cenário & Skyline:** Azul-marinho profundo e violeta escuro (`#080612` a `#180d2b`) com prédios exibindo janelas e faixas horizontais de neon.
   - **Droide N3-0N (Jogador):** Chassi metálico polido com detalhes em azul cobalto, visor único com emissão ciano vibrante (`#00e5ff`) e propulsores inferiores com chama de plasma azulada.
   - **Células de Energia:** Ciano brilhante e amarelo elétrico pulsante (`#00ffff` / `#ffe600`).
   - **Portal de Extração:** Grande portal circular/hexagonal com anéis concêntricos giratórios em magenta vibrante (`#ff007f`) e núcleo de vórtice luminoso.
3. **Iluminação e Pós-Processamento URP:**
   - Universal Render Pipeline com luz ambiente azul-violeta, luz direcional sutil para volume e materiais com *Emission* compatíveis com SRP Batcher.
   - Efeito Bloom sutil para realçar a intensidade dos néons e hologramas sem ofuscar os obstáculos.

---

## 3. Especificação de Cena de Calibração (`CalibrationScene`)

Antes de validar qualquer nova fase ou asset, a cena `CalibrationScene.unity` deve conter:
1. Uma esfera cromada 100% refletiva (para testar reflexos e iluminação global).
2. Uma esfera cinza 50% difusa (para checar intensidade da luz direcional e sombras).
3. O modelo de referência do jogador na escala 1:1.
4. Um prop de ambiente aprovado (árvore estilizada, rocha).
5. A câmera Cinemachine com a distância e ângulo padrão de gameplay (ex: 45° de inclinação, 8 metros de distância).
