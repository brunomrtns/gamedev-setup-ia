# physics-and-combat.md — Padrões de Física Veicular e Combate

Padrão **inviolável** para física de veículos, props destrutíveis e alinhamento
balístico em todos os jogos do estúdio. Implementação de referência: Neon
District (`PlayerCarController`, `TrafficVehicle`, `BreakableProp`, `Ak47Weapon`).

---

## 1. Veículos Rígidos — Parâmetros Obrigatórios

| Parâmetro | Valor obrigatório | Motivo |
|---|---|---|
| `centerOfMass` | `Vector3(0, -0.4f, 0)` (abaixo do eixo geométrico), definido explicitamente em `Awake/Start` | Anti-capotamento instantâneo |
| `collisionDetectionMode` | `ContinuousDynamic` (veículo do jogador), `Continuous` (NPCs) | Sem tunneling em alta velocidade |
| `interpolation` | `Interpolate` | Visual suave com FixedUpdate |
| `maxAngularVelocity` | `<= 8 rad/s` | Impulsos não geram giros absurdos |
| `angularDamping` | `>= 1` | Estabilidade pós-impacto |
| Clamp de velocidade linear | `|v| <= maxSpeed * 1.35`, `v.y <= ~9 m/s` | Anti-launch: impulsos não arremessam o carro |
| Auto-nivelamento | Torque `Cross(up, Vector3.up)` quando `dot(up, Vector3.up) < 0.985` | Carro retorna à posição ereta |

**Proibido:** colisores interpenetrantes entre carroceria e rodas; `AddForce`
sem clamping; `centerOfMass` não configurado.

## 2. Veículos de Tráfego — Máquina de Estados de Colisão

Obrigatória a máquina de três estados (`TrafficVehicle.CrashState`):

- **Routing** — rota normal (arc-length ou equivalente); rigidbody cinemático.
- **PhysicsFree** — impulso leve/médio (`impulse >= softThreshold`): rb vira
  dinâmico, `AddForceAtPosition(impulse)` no ponto de contato, assenta
  (`|v| < settleSpeed` por `settleTime`) e **re-roteia** para a aresta mais
  próxima (`TrafficNetwork.FindNearestEdge`).
- **Wrecked** — impulso severo (`impulse >= severeThreshold`): sai do tráfego
  permanentemente, permanece físico e acidentado na via.

Limiares de referência (massa do player ~1350kg): `soft = 4000 N·s` (~3 m/s),
`severe = 20000 N·s` (~15 m/s). Classificação via função pura
`ClassifyImpulse(impulse, soft, severe)` — testável em EditMode.

**Proibido:** veículos de tráfego puramente cinemáticos que não reajam a
impactos do jogador.

## 3. Props Urbanos Destrutíveis (`BreakableProp`)

- Todo poste/sinal de rua deve ter `breakingForceThreshold` em **impulso**
  (`collision.impulse.magnitude`, N·s), não velocidade bruta.
- Abaixo do limiar: som metálico apenas (`PlayCollisionThud`), sem dano.
- Acima do limiar: divisão intacto/destruído —
  - **Toco estático**: mesma malha comprimida até `stumpHeight`
    (`StumpScaleY` clamado em [0.05, 0.6]), colisor próprio.
  - **Peça solta**: rigidbody dinâmico que recebe `AddForceAtPosition` no
    ponto de contato; `MeshCollider` côncavo → `convex = true`; tempo de vida
    limitado (`debrisLifetime ≈ 25s`) para orçamento mobile.

## 4. Alinhamento Balístico — Convergência Câmera→Muzzle

Padrão de duas etapas, obrigatório em qualquer arma hitscan/projetada:

1. `Physics.Raycast` a partir do **centro da lente da câmera**
   (`cam.ViewportPointToRay(0.5, 0.5)`) até o alcance máximo → `targetPoint`.
2. O `MuzzlePoint` é rotacionado para convergir geometricamente no
   `targetPoint` (`ComputeMuzzleDir`, função pura testável), independente da
   pose do personagem.

- O retículo desenhado no ecrã **tem de coincidir 100%** com o ponto de
  impacto — em hip fire (crosshair central) e em ADS.
- ADS: pose alinha a mira da arma ao centro do ecrã, FOV reduz ~15°,
  transição `lerp` com resposta `dt * >= 10` (sem atraso perceptível).

**Proibido:** implementar disparo em que a trajetória divirja do centro do
retículo da câmera ativa.

## 5. Camadas de Raycast

- `Ignore Raycast` (layer 2): blockers de limite de mapa e objetos que não
  devem receber impactos/decals de bala mas mantêm colisão física.
- `Weapon` (layer 3): rig da arma em câmera overlay URP dedicada.


## Veículos: colisores compostos e dano

- Colisor veicular é composto (`Col_Chassis` + `Col_Cabin`) — proibido AABB único que faça mira/decal flutuar.
- Dano via `VehicleDamageController`: Intact→Light→Severe→Burning(fogo)→Exploded (força radial + carcaça).
- Malhas danificadas geradas por `tools/art/damage_vehicle.py` (Blender headless, WSL) e instaladas como filhos `__wrecked_*` inativos pelo Scene Builder.


## VFX e destruição veicular (pipeline padrão)

- Zonas de dano: Col_Hood 1.5x, Col_Tank 2.0x, demais 1.0x.
- Estágios: >50% faíscas pooled; 1-35% fogo no capô + auto-ignição 5-8s; 0% explosão.
- VFX SEMPRE via `VfxPool` (recicla + ParticleSystem.Clear) — proibido Instantiate em gameplay.
- Explosão: OverlapSphereNonAlloc + AddExplosionForce + dano quadrático + CameraShakeBus + fogos residuais 12s.
- Veículos fora de rota (PhysicsFree/Wrecked/Exploded) nunca são despawnados.
