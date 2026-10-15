# Art Bible — Direção Artística e Identidade Visual

**Versão:** 1.0  
**Estilo-Chave:** Stylized Low-Poly / Clean PBR Minimalista  
**Gênero-Alvo:** 3D Action / Exploration Mobile  

---

## 1. Visão Estética

Para manter alta legibilidade em telas pequenas de smartphones sob luz solar e desempenho estável a 60 FPS, a direção estética adota **geometria estilizada limpa**, silhuetas claras e iluminação contrastante com gradientes ricos, evitando microdetalhes fotorrealistas que geram ruído visual e aliasing.

### Pilares Visuais
1. **Silhuetas Legíveis:** Cada personagem, inimigo e obstáculo deve ser identificável instantaneamente pela sua forma básica (círculo = amigável/rolante; triângulo = perigoso/cortante; retângulo = estável/pesado).
2. **Paleta de Cores Coesa:** Harmonia cromática baseada em cores análogas para o ambiente e cores complementares com alta saturação para pontos de interesse e perigo.
3. **Iluminação Cênica:** URP com luz direcional quente (sol), sombras suaves e ambient lighting com céu azul suave ou pôr-do-sol.
4. **Pós-Processamento Cuidadoso:** Bloom sutil nos emissores, Color Grading com contraste médio e ACES Tonemapping.

---

## 2. Paleta de Cores e Hierarquia Visual

- **Ambiente Base:** Tons terrosos, verdes musgo, pedras cinza-azuladas.
- **Jogador / Protagonista:** Cores primárias vibrantes (Azul Cobalto + Branco/Dourado) para contraste imediato contra qualquer fundo.
- **Inimigos e Ameaças:** Laranja-avermelhado e Vermelho carmim.
- **Interativos / Coletáveis:** Amarelo brilhante / Dourado com emissor pulsante.

---

## 3. Especificação de Cena de Calibração (`CalibrationScene`)

Antes de validar qualquer nova fase ou asset, a cena `CalibrationScene.unity` deve conter:
1. Uma esfera cromada 100% refletiva (para testar reflexos e iluminação global).
2. Uma esfera cinza 50% difusa (para checar intensidade da luz direcional e sombras).
3. O modelo de referência do jogador na escala 1:1.
4. Um prop de ambiente aprovado (árvore estilizada, rocha).
5. A câmera Cinemachine com a distância e ângulo padrão de gameplay (ex: 45° de inclinação, 8 metros de distância).
