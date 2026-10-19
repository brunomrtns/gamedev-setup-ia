# GDD — NEON DISTRICT: FREE RIDE

> Versão 1.0 · Escopo travado pelo briefing · Unity 6.3 LTS + URP · Android (mobile-first, paisagem)

## 1. Visão e Fantasia

Um distrito neon ao entardecer, vivo e dirigível. O jogador é um motorista noturno:
avenidas molhadas, postes acesos, tráfego civil fluindo, synthwave procedural.
Sem missões, sem polícia, sem score — **o produto é o mundo vivo e dirigível**.

**Pilares:**
1. **Mundo vivo** — tráfego civil circulando em loop pelas vias, postes e faróis acesos, entardecer atmosférico.
2. **Direção arcade gostosa** — esterço suave, drift dedicado com marcas de pneu e derrapagem, câmera chase suave.
3. **Mobile-first** — toque + WASD, 30+ fps em mid-range, HUD minimalista.

## 2. Escopo (travado — não inventar além disto)

| Incluído | Excluído |
|---|---|
| Distrito ~5x5 quarteirões com ruas, praça central e avenida principal | Missões, objetivos, score |
| Direção arcade (acelerar/frear/esterço/drift) | Polícia, dano, destruição |
| Tráfego ambiente com spawn/despawn por distância (~80m), waypoints em loop, frenagem por raycast | NPCs a pé, mini-mapa |
| Colisão com feedback físico (bounce + som) | Ultrapassagem/mudança de faixa do tráfego |
| Atmosfera dusk: skybox, sol âmbar, emissivos, bloom URP, fog leve | Ciclo dia/noite, clima |
| Áudio procedural: motor (pitch por velocidade), skid, thud, synthwave loop | Áudio licenciado |

## 3. Assets Obrigatórios (já importados)

| Asset | Uso |
|---|---|
| `Assets/Versatile Studio Assets/Demo City By Versatile Studio/` | Prédios (office/mid_house/small_house/factory), postes (`lamp_pole_*`, `building_lamp_*`), árvores, bancos, hedges, blocos; texturas PBR `asphalt_*`/`sideway_tile` para as vias |
| `Assets/SportCar/Prefabs/SportCar_1.prefab` | Carro do jogador (o detalhado, com rodas FL/FR/RL/RR) |
| `Assets/Awbmecreations/Mobile Optimize-Free Low Poly Cars/` | Tráfego civil (10 variações low-poly, já URP Lit) |
| `ProceduralMeshLibrary` | Geometria utilitária (ruas, calçadas, zebras, rampas de praça) — **nunca `GameObject.CreatePrimitive`** |

**Nota URP:** materiais de Demo City e SportCar vêm Built-in RP → conversão in-loco para URP Lit no Scene Builder (`_MainTex`→`_BaseMap`, `_Color`→`_BaseColor`, emissão/normal/occlusion preservados).

## 4. Distrito (layout determinístico)

- Grade 6×6 vias em `(i-2.5)*52` → i∈[0,5] ⇒ 25 quarteirões de 40×40m (~272×272m).
- Ruas 12m (`asphalt_2_tracks`, mão dupla, faixas ±2.75m) e **Avenida Neon** 16m em z=+26 (`asphalt_4_tracks`, faixas ±4m).
- Malhas de via próprias com UV em escala-mundo (marcações corretas), cruzamentos em `asphalt_square` (+0.01), zebras na praça em `asphalt_extra` recortado (+0.02), calçadas em `sideway_tile` (plano-mãe y=-0.02).
- **Praça Central** (quarteirão 0,0): hedges, árvores, bancos, `lamp_pole_small`, piso `sideway_tile`.
- Prédios do pool Demo City por seed determinística, 2–4 por quarteirão, `ScaleToFit` por bounds; colliders box por bounds.
- Postes `lamp_pole_*` (Light incluída no prefab) a cada ~26m; na avenida, `lamp_pole_dual_*`.
- Barreiras invisíveis (`Bounds_Blocker_*`, isentas do validador) contendo o distrito.

## 5. Mecânicas

### 5.1 Direção arcade (`PlayerCarController`, Rigidbody)
- Throttle frente/ré, freio, drag; esterço com atenuação por velocidade; `centerOfMass` baixo.
- **Drift** (botão dedicado/Space): reduz grip lateral, boost de yaw, TrailRenderer nas rodas traseiras + skid audio.
- Colisão: bounce físico natural + `PlayCollisionThud()` via `OnCollisionEnter`.
- Rodas FL/FR/RL/RR giram visualmente (rotação por velocidade + esterço nas dianteiras).

### 5.2 Tráfego (`TrafficNetwork` + `TrafficManager` + `TrafficVehicle`)
- Rede dirigida: arestas de faixa (segmento entre cruzamentos) + conectores de cruzamento (reto/esq/dir, sem retorno).
- Veículo segue polilinha por arco; raycast frontal ~7m → desacelera até parar; ao fim da aresta escolhe próxima (RNG semeado).
- Spawn/despawn por raio (entra < ~70m, sai > ~80m, alvo ~14 veículos ativos); sem mudança de faixa.

### 5.3 Câmera (`ChaseCamera`)
- Posição SmoothDamp atrás do carro, yaw com lag (realça drift), lookAt levemente acima.

### 5.4 Input (`DriveInputAdapter`)
- Teclado (novo Input System): W/↑ gas, S/↓ freio/ré, A/D ou ←/→ esterço suavizado, Space/Shift drift.
- Touch: botões grandes (gas/freio/←/→/drift) via `DriveTouchButton` (IPointerDown/Up).

## 6. Atmosfera e Áudio

- Skybox procedural dusk (âmbar→violeta), sol direcional baixo âmbar, fog exponencial leve, ambient flat arroxeado.
- Volume global URP: Bloom moderado (threshold ~1.0, intensity ~0.9), ACES, vinheta leve.
- Luzes reais: 1 direcional + spots dos postes (prefab) + 2 faróis do player. Emissivos: `*_light` do pack, faróis/lanternas do SportCar.
- `ProceduralAudioService` estendido: loop de motor (pitch/volume ∝ velocidade), loop de skid, thud de colisão, **trilha synthwave procedural em loop** (baixo + pad + arpejo + hats, ~8s, volume ambiente).

## 7. HUD

- Velocímetro (km/h) + FPS counter (debug), botões touch grandes em paisagem, indicador DRIFT.
- Sem menus nesta versão: HUD direto ao abrir (free ride). Tecla R / botão "RECENTER" recoloca o carro na via mais próxima.

## 8. Arquitetura (assemblies)

| Assembly | Conteúdo novo |
|---|---|
| `Studio.Foundation` | `DriveMath` (física arcade pura, testável), extensão de `ProceduralAudioService` |
| `Studio.Input` | `DriveInputAdapter` |
| `Studio.Gameplay` | `PlayerCarController`, `TrafficNetwork`, `TrafficManager`, `TrafficVehicle`, `NeonDistrictGameManager` |
| `Studio.Cameras` | `ChaseCamera` |
| `Studio.UI` | `NeonDistrictHUD`, `DriveTouchButton` |
| `Studio.Editor` | `NeonDistrictSceneBuilder` (`RunBuildSceneBatch`, `CaptureNeonDistrictAnglesBatch`, conversão URP de vendors) |
| `Studio.Tests.EditMode` | `NeonDistrictGameplayTests` |

## 9. Critérios de Aceitação / Gates

1. `validate_scene.py --scene NeonDistrict --strict` → exit 0.
2. `validate_asset.py --all` → exit 0 (materiais novos com `_BaseMap`, zero primitivas visíveis).
3. UTF: 33 existentes + ≥12 novos → 100%.
4. ≥6 capturas `evidence/captures/neon_district_*.png` com warm-up de shaders.
5. `builds/neon_district.apk` gerado via `tools/android/build_apk.py`.
6. Tráfego: ≥12 veículos ativos perto do player; param em obstáculo frontal.
7. Orçamento mobile: draw calls < 150, tris visíveis < 350k, zero GC/frame em Update.

## 10. Riscos Aceitos

- Física arcade (sem WheelColliders) — trade-off deliberado mobile/diversão.
- Conectores de cruzamento são polilinhas aproximadas (sem curvas reais) — suficiente para tráfego ambiente.
- Luzes de postes em prefabs vendor — contagem moderada (~40 spots) com range curto.
