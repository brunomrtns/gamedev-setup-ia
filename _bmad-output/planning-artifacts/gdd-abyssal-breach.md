# Game Design Document (GDD) — ABYSSAL BREACH

**Projeto:** Setup A — Jogo 2  
**Título:** Abyssal Breach  
**Gênero:** Sub-Aquatic First-Person Survival / Extraction Shooter (`GameGenre.ActionShooter` / `RpgSurvival`)  
**Engine & Target:** Unity 6.3 LTS (6000.3.26f1) · Universal Render Pipeline (URP) · Windows Standalone (64-bit) & Mobile Ready  

---

## 1. Visão Geral e Premissa

Em **Abyssal Breach**, o jogador assume o papel de um mergulhador de operações especiais infiltrado na **Fenda Mariana-X**, uma trincheira oceânica alienígena bioluminescente onde pods científicos naufragaram. O objetivo tático é avançar pela fenda de corais abissais, recuperar **3 Núcleos de Energia (Data/Energy Cores)** protegidos por **Drones Sentinelas Subaquáticos**, gerenciar o suprimento crítico de **Oxigênio ($O_2$)** coletando cilindros pressurizados pelo caminho e alcançar a **Cápsula de Extração (Sub-Pod)** antes que a hipóxia seja fatal.

---

## 2. Pilares de Design

1. **Imersão Subaquática Tática (First-Person):** Visão de dentro do capacete pressurizado com HUD diegético, flutuabilidade hidrodinâmica em 6 graus moderados de liberdade (nado horizontal, impulsão vertical ascendente/descendente) e atmosfera densa com caustics e corais bioluminescentes.
2. **Combate Híbrido Submarino (Rifle de Plasma + Faca Tática):**
   - **Rifle Submarino de Cavitação/Plasma (`Assault_Rifle_B`):** Disparo de projéteis de alta energia com redução de dispersão em mira óptica (ADS), recuo amortecido pela água e recarga tática.
   - **Faca Tática de Mergulho (`Tactical_Knife`):** Arma secundária de resposta rápida para combate corpo a corpo contra drones próximos e corte de incrustações sem consumo de munição.
3. **Tensão de Sobrevivência e Extração:** O oxigênio decai continuamente (e acelera durante o uso dos propulsores de sprint subaquático), forçando decisões constantes entre explorar recifes em busca de cilindros de $O_2$ ou avançar diretamente para os núcleos e a cápsula de extração.

---

## 3. Mecânicas Principais

### 3.1 Movimentação Subaquática (`AbyssalFirstPersonMotor`)
- **Deslocamento Planar & Hidrodinâmico:** Controle em primeira pessoa (WASD) com aceleração suave, arrasto hidrodinâmico (`waterDrag = 4.5f`) e velocidade base de `5.5 m/s` (`8.5 m/s` em propulsão rápida / Sprint).
- **Controle Vertical & Flutuabilidade:**
  - **Subir (`Ascend` / Espaço):** Impulso vertical positivo (`+4.2 m/s`).
  - **Mergulhar (`Descend` / C ou Ctrl):** Impulso vertical negativo (`-4.2 m/s`).
  - **Flutuabilidade Passiva (`Buoyancy`):** Oscilação senoidal suave em repouso simulando suspensão hidrostática, com limites seguros de profundidade (`Y` entre `1.2m` acima do leito rochoso e `18.0m` no teto da fenda).
- **Orientação de Câmera:** Controle de Yaw e Pitch (`-75°` a `+75°`) com bobbing hidrodinâmico sincronizado ao nado.

### 3.2 Sistema de Armas (`AbyssalWeaponController`)
- **Arma Primária — Rifle de Plasma Submarino:**
  - Capacidade do carregador: `24` disparos por pente (`96` em reserva inicial).
  - Cadência de tiro: `0.16s` entre disparos (~375 RPM subaquático).
  - Dano por impacto: `35 HP`.
  - **Mira (ADS):** Reduz o FOV da câmera de `65°` para `46°`, alinha o rifle ao centro óptico da tela e reduz a dispersão balística de `2.2°` para `0.25°`.
  - **Recarga (`Reload`):** Duração de `1.5s`, restaurando o pente a partir da reserva.
- **Arma Secundária — Faca Tática Abissal:**
  - Acionada via troca de arma (`SwitchWeapon`) ou golpe rápido (`MeleeAttack`).
  - Alcance: `3.2m`, arco frontal, dano de `55 HP`, cooldown de `0.45s`.

### 3.3 Gestão de Oxigênio, Integridade e Coletáveis (`OxygenSurvivalSystem`)
- **Oxigênio ($O_2$):**
  - Máximo: `100.0` unidades.
  - Consumo padrão: `1.5 / s` (nado normal) | Consumo em Sprint: `3.2 / s`.
  - **Hipóxia:** Quando $O_2 = 0$, o jogador sofre `12.0 HP/s` de dano por asfixia.
- **Integridade do Traje ($HP$):**
  - Máximo: `100.0 HP`. Chegar a `0 HP` aciona o estado `GameOver`.
- **Coletáveis na Fenda:**
  - **Cilindro de Oxigênio (`OxygenTank`):** Restaura `+35.0` de $O_2$ e `+10 HP`.
  - **Núcleo de Energia (`EnergyCore`):** Item de missão (`+250` pontos). Coletar os núcleos habilita a extração prioritária.
  - **Caixa de Munição (`AmmoCrate`):** Restaura `+24` projéteis de reserva.

### 3.4 Ameaças e Extração
- **Drones Sentinelas Abissais (`AbyssalThreatDrone`):**
  - `100 HP`, patrulham em órbita ao redor dos pods naufragados e avançam contra o mergulhador quando dentro do raio de detecção (`18m`), causando dano elétrico/impacto (`18 HP/s` em contato próximo). Ao serem destruídos, concedem `+300` pontos.
- **Cápsula de Extração (`AbyssalExtractionPod`):**
  - Localizada no final da trincheira (`Z = 115m`). Entrar na zona de acoplamento da cápsula conclui a operação com estado `Victory` (`Extraction Successful`).

---

## 4. Direção de Arte e Atmosfera URP

- **Paleta Abissal:** Azul-profundo oceânico (`#041322`), ciano bioluminescente (`#00F0FF`), coral magenta/laranja vibrante (`#FF4D79`, `#FF8822`) e verde-tático no HUD (`#20FFB0`).
- **Iluminação e Fog:**
  - Fog exponencial subaquático denso (`#051A2E`, densidade `0.014`).
  - Luz direcional zenital azul-ciano simulando raios solares filtrados pela superfície oceânica, complementada por luzes pontuais bioluminescentes nos recifes de corais e pods naufragados.
  - Pós-processamento URP com **Bloom** nos corais/visores, **Tonemapping ACES** e **Vignette** simulando a curvatura do visor de mergulho.
