# ADR-003: Arquitetura de Sistemas e Engine do Jogo NEON RUSH

**Data:** 15 de Outubro de 2026  
**Status:** Aceito  
**Contexto:** Implementação da Vertical Slice de NEON RUSH no estúdio autônomo.

---

## 1. Decisões Tomadas

### 1.1 Modelo de Movimento por Faixas Físico-Cinemático
- **Decisão:** O robô utiliza avanço cinemático no eixo Z com interpolação de faixa baseada em curva matemática suave (*smooth damping* ou *sine easing*) no eixo X, e física de arco gravitacional para o eixo Y (Pulo).
- **Justificativa:** Proporciona precisão milimétrica nas 3 faixas (-2.2, 0.0, +2.2), eliminando desvios acidentais típicos de física baseada puramente em forças de atrito, garantindo resposta imediata ao toque do jogador em telas mobile.

### 1.2 Adaptador Híbrido de Entrada (Touch + Editor Keyboard)
- **Decisão:** Centralizar a leitura de comandos em um único componente `NeonInputAdapter`.
- **Justificativa:** Permite que desenvolvedores e agentes testem e depurem instantaneamente a cena no Unity Editor com teclas de teclado (WASD, Setas, Espaço) enquanto mantém suporte total a gestos na tela (swipes direcionais) para execução no dispositivo Android.

### 1.3 Procedural Audio Service com Geração em Tempo de Carregamento
- **Decisão:** Em vez de depender de arquivos externos WAV/MP3 que não existem no repositório, gerar formas de onda senoidais/quadradas moduladas diretamente na memória via `AudioClip.Create` durante a inicialização.
- **Justificativa:** Garante 100% de autonomia e reprodutibilidade sem ativos externos proprietários, entregando feedback audiovisual completo para saltos, deslizamentos, coleta de energia e impactos.

### 1.4 Geração Determinística da Pista via Editor Builder
- **Decisão:** Construir a pista e o cenário de 350m através de um script de automação C# do Editor (`NeonRushSceneBuilder.cs`), gerando a cena completa `NeonRush.unity`, os materiais URP neon, a iluminação e os prefabs na estrutura canônica do projeto.
- **Justificativa:** Cria uma cena de jogo real, durável e inspecionável no Unity Editor, sem necessidade de GUI aberta ou processos manuais.

---

## 2. Consequências

- **Positivas:**
  - Código desacoplado e compilável em frações de segundo via Assembly Definitions.
  - Zero GC no loop de frames (`Update`).
  - Totalmente testável via Unity Test Framework (UTF).
  - Experiência visual rica com iluminação e pós-processamento URP.
- **Limitações:**
  - Percurso fixo de 350m para a vertical slice (ideal para teste de desempenho e conclusão garantida sem loop infinito).
