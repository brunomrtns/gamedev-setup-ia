# ADR-0012 — Multiplayer NEON DISTRICT: Netcode for GameObjects + servidor dedicado

## Status
Aceito (primeira fatia vertical implementada).

## Contexto
NEON DISTRICT precisa virar online com servidor dedicado na VPS (Ubuntu 24.04,
6 vCPU, 11GB, Docker). Requisitos: Linux headless, UDP, autoridade do servidor,
clientes Unity/Android futuros, baixo custo, sem depender de UGS pago.

## Decisão
**Unity Netcode for GameObjects 2.13.3 + Unity Transport (UTP/UDP)**.
Oficial, gratuito, suporta Linux Dedicated Server build, sem dependência de
Relay/Lobby para conexão direta por IP:porta.

## Arquitetura (autoridade no servidor)
- `Studio.Net` asmdef — `NdNetSession` (args `-ndserver`/`-ndclient=IP:PORTA`/
  `-ndautodrive`), `NdNetBootstrap` (runtime hook), `NetPlayerCar` (NB).
- Cliente envia inputs 30Hz via `Rpc(SendTo.Server, Unreliable)` → servidor
  aplica no `PlayerCarController` (campos `Net*`/`UseNetInput`) → simula física
  → `NetworkTransform` (autoridade servidor) replica pos/rot interpolado.
- Cliente: `PlayerCarController.enabled=false` + `rb.isKinematic` — sem
  predição v1 (lag de ~1 RTT aceitável; predição é próximo passo).
- Spawn: `NetworkManager` com `PlayerPrefab=NetPlayerCar` — servidor decide.
- Câmera: `ChaseCamera.SetTarget` só no carro do dono (IsOwner).
- SP preservado: sem args de rede o bootstrap não faz nada.

## Consequências
- Sem predição client-side no v1 — input lag ~1 RTT no próprio carro.
- Peds/tráfego/armas ainda locais ao servidor (não replicados — Etapas C-E).
- Build: `BuildLinuxServerCommandLine` (StandaloneLinux64 + Server subtarget).

## Alternativas avaliadas
- Photon Fusion/PUN: SaaS pago, vendor lock-in.
- Mirror: maduro mas menos alinhado com Unity 6/UTP.
- UGS Relay/Lobby: custo recorrente, desnecessário pra IP direto.

## Etapa B — predição + reconciliação (implementado)
- Dono do carro simula localmente (PlayerCarController ativo, rb dinâmico);
  inputs sequenciados 30Hz → servidor aplica e devolve estado a 20Hz via
  StateClientRpc. Reconciliação: >3m snap / 0.15–3m blend suave. PhysX não é
  determinístico cross-machine — correção controlada, não replay exato.
- NetworkTransform removido do prefab; interpolação própria por buffer de
  snapshots (render 100ms no passado) nos clientes remotos.
- Segurança: RequireOwnership, rate-limit 60Hz, clamps, rejeita NaN/seq velha.
  Servidor nunca aceita posição do cliente.

## Etapa C — jogador a pé em rede (implementado)
- `NetPlayerFoot` NB: mesmo modelo do carro (predição no dono via
  OnFootMotor — CharacterController kinematic, quase determinístico;
  reconciliação snap/blend; interpolação de snapshots nos remotos com
  `Speed` no Animator).
- `IsOnFoot` NetworkVariable<bool> controla visibilidade/física do avatar a pé.
- Enter/exit: `RequestExitServerRpc` no carro (valida ocupante, libera
  `OccupantClientId`, teleporta pé pro lado do carro) + `EnterCarServerRpc`
  no pé (valida distância ≤3m+tolerância e assento livre). Registro
  `NdNetSession.Feet[clientId]` pareia pé↔jogador no servidor.
- Prefab `NetPlayerFoot.prefab` gerado de `*_baked` (humanoide real) via
  `Studio.Editor.NetPrefabBuilder`.
- `OnFootMotor`/`PlayerCarController`/`DriveInputAdapter`: canal `Net*`
  aditivo + `PeekJump`/`PeekCrouch` — SP intocado.

## Próximos passos
Sync peds/tráfego (LOD por distância), hit-reg de armas no servidor,
predição com replay de inputs (se PhysX permitir), lobby simples.
