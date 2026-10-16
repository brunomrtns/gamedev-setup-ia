# Backlog de Stories e Critérios de Aceitação — NEON RUSH

**Projeto:** NEON RUSH Vertical Slice  
**Metodologia:** BMad Game Dev Studio  
**Status das Stories:** Planejadas -> Execução Automatizada  

---

## Épico 1: Fundação & Pipeline de Entrada (Input & Foundation)

### STORY-001: Adaptador Unificado de Entrada (Touch & Teclado)
- **Como:** Jogador mobile ou desenvolvedor no Unity Editor.
- **Quero:** Controlar o robô por gestos na tela (swipes) ou teclas do teclado (A/D/W/S, Setas, Espaço).
- **Critérios de Aceitação:**
  1. Swipe para esquerda ou tecla `A`/`LeftArrow` emite evento `OnSwipeLeft`.
  2. Swipe para direita ou tecla `D`/`RightArrow` emite evento `OnSwipeRight`.
  3. Swipe para cima ou tecla `W`/`Space`/`UpArrow` emite evento `OnSwipeUp`.
  4. Swipe para baixo ou tecla `S`/`DownArrow` emite evento `OnSwipeDown`.
  5. Threshold de swipe configurado para telas mobile com deadzone que impede ativações acidentais.

### STORY-002: Serviço de Áudio Procedural Sintetizado
- **Como:** Jogador.
- **Quero:** Ouvir feedback sonoro consistente para coleta de células, pulo, deslize, impacto e vitória sem requerer arquivos de áudio externos ausentes.
- **Critérios de Aceitação:**
  1. Geração determinística de `AudioClip` via `AudioClip.Create` na inicialização.
  2. Efeitos criados: `JumpSound` (sweep ascendente), `SlideSound` (ruído filtrado suave), `CollectSound` (acorde brilhante de sino sintetizado), `HitSound` (impacto grave amortecido), `VictorySound` (arpeggio comemorativo).
  3. Execução sem alocações adicionais na heap durante os disparos.

---

## Épico 2: Controlador do Personagem & Animação (Player & Game Feel)

### STORY-003: Controlador de Movimento em 3 Faixas (3-Lane Runner Controller)
- **Como:** Droide N3-0N.
- **Quero:** Me deslocar continuamente para frente e mudar suavemente entre as 3 faixas (-2.2m, 0.0m, +2.2m).
- **Critérios de Aceitação:**
  1. O robô avança no eixo Z a uma velocidade configurável (padrão: 12 m/s).
  2. Ao receber comando de troca de faixa, interpola no eixo X até a faixa de destino em ~0.15s.
  3. Comandos além das faixas extremas (X < -2.2 ou X > +2.2) são ignorados com segurança sem offsets cumulativos.
  4. Suporte a Pulo (arco parabólico atingindo ápice de 2.2m) e Deslize (redução temporária de altura do colisor).

### STORY-004: Animador e Game Feel Procedural do Droide
- **Como:** Jogador.
- **Quero:** Ver o robô reagir visualmente às ações (inclinação na troca de faixas, achatamento no pouso, encolhimento no deslize, oscilação de flutuação).
- **Critérios de Aceitação:**
  1. Flutuação suave (*idle hover bobbing*) com onda senoidal constante.
  2. Rotação sutil no eixo Z (*roll*) proporcional à velocidade lateral durante trocas de faixa.
  3. Efeito de achatamento/esticamento (*squash & stretch*) no pulo e pouso.
  4. Retração da carcaça e rotação de propulsores durante o deslize.

---

## Épico 3: Pista, Cenário Futurista & Obstáculos (Level & Hazards)

### STORY-005: Construtor Procedural da Pista e Cenário Urbano Neon
- **Como:** Jogador.
- **Quero:** Correr por uma autoestrada de dados cyberpunk com faixas iluminadas, prédios com silhuetas futuristas e um portal de extração impressionante.
- **Critérios de Aceitação:**
  1. Pista de 350 metros com 3 faixas visualmente destacadas por linhas neon emissivas.
  2. Guard rails holográficos magenta/violeta ao longo das margens.
  3. Skyline com torres monolíticas futuristas ao fundo exibindo faixas de janelas luminosas.
  4. Portal de Extração no final da pista (Z = 350m) com anéis neon concêntricos e núcleo luminoso.

### STORY-006: Obstáculos Diferenciados e Células de Energia
- **Como:** Jogador.
- **Quero:** Enfrentar perigos que exijam decisões rápidas e coletar células de energia para aumentar a pontuação.
- **Critérios de Aceitação:**
  1. **Laser Baixo:** Colisor entre 0 e 0.8m de altura. Se colidir sem estar pulando, causa derrota.
  2. **Viga Suspensa:** Colisor entre 1.2m e 2.5m de altura. Se colidir sem estar deslizando, causa derrota.
  3. **Barricada Total:** Colisor cobrindo toda a faixa do chão ao topo. Exige esquiva lateral.
  4. **Células de Energia:** Octaedro giratório ciano. Ao encostar, soma +100 pontos, toca som de coleta e desativa o item.

---

## Épico 4: Câmera, Estados de Jogo e Interface (UI & Flow)

### STORY-007: Câmera Suave em 3ª Pessoa (Cinemachine / Tracking)
- **Como:** Jogador.
- **Quero:** Ter uma visão clara em 3ª pessoa que acompanhe o robô sem tremores ou cortes visuais.
- **Critérios de Aceitação:**
  1. Câmera posicionada a (0, 3.2, -6.5) relativa ao robô com ângulo de visão descendente de 18 graus.
  2. Seguimento no eixo Z com amortecimento suave (*damping*) nos eixos X e Y para evitar solavancos em curvas/pulos.

### STORY-008: Ciclo de Vida da Partida & Telas de UI (Game Flow)
- **Como:** Jogador.
- **Quero:** Iniciar a partida através de uma tela inicial, acompanhar meu progresso no HUD e ver telas de vitória ou derrota com opção de reiniciar.
- **Critérios de Aceitação:**
  1. **Start Screen:** Jogo inicia pausado ou aguardando; botão "START" dá a largada.
  2. **HUD:** Atualiza células coletadas, pontos e barra de progresso em tempo real.
  3. **Vitória:** Ao atingir o portal de extração, ativa a tela de vitória com estatísticas e botão reiniciar.
  4. **Derrota:** Ao colidir com obstáculo, para o robô, ativa animação/partículas de colisão e mostra tela de derrota.
  5. **Reiniciar:** Restaura o estado da partida instantaneamente sem vazamentos de memória.
