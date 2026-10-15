# Orçamento de Performance Mobile (Mobile Performance Budgets)

Definição de limites técnicos obrigatórios para jogos 3D mobile no Android (dispositivo de referência: chipset mid-range ARM Mali / Adreno equivalente a Snapdragon 7xx / Dimensity 8000).

---

## 1. Perfis de Aparelhos-Alvo

| Métrica | Perfil Low-End (Ex: Snapdragon 680) | Perfil Mid-Range (Alvo Primário) | Perfil High-End (Flagship) |
|---|---|---|---|
| **Taxa de Quadros Alvo** | 30 FPS estável (33.3ms) | **60 FPS estável (16.6ms)** | 60–120 FPS |
| **Draw Calls (SetPass Calls)** | < 80 | **< 150** | < 250 |
| **Triângulos na Cena (Visíveis)** | < 150.000 | **< 350.000** | < 800.000 |
| **Vértices na Cena (Visíveis)** | < 120.000 | **< 300.000** | < 700.000 |
| **Uso de RAM Total do App** | < 600 MB | **< 1.0 GB** | < 2.0 GB |
| **Alocação GC por Frame (Update)**| **0 bytes** | **0 bytes** | **0 bytes** |
| **Tamanho do APK Inicial** | < 100 MB | **< 150 MB** | < 250 MB |
| **Tempo de Carregamento (Cena)** | < 3.0s | **< 2.0s** | < 1.0s |

---

## 2. Diretrizes Gráficas Obrigatórias

- **SRP Batcher:** 100% dos materiais devem ser compatíveis com o SRP Batcher (`CBUFFER` padrão).
- **GPU Instancing:** Habilitar GPU Instancing em materiais de folhagens, pedras, itens coletáveis repetidos.
- **Dynamic Occlusion Culling:** Cenas com oclusão volumosa devem ter Occlusion Culling pré-calculado (Baked).
- **LOD (Level of Detail):** Qualquer modelo complexo (> 2.000 polígonos) visível em distâncias variáveis deve ter grupo de LOD (LOD0, LOD1, LOD2 e Culling).
- **Compressão de Texturas:**
  - Android: Formato **ASTC** obrigatório (ASTC 4x4 ou 6x6 para assets principais; ASTC 8x8 para props menores/UI de fundo).
  - Resoluções: Personagens principais até 1024x1024; props de ambiente 512x512; partículas 256x256.

---

## 3. Gestão Térmica e de Bateria

- **Target Frame Rate Clamping:** Forçar `Application.targetFrameRate = 60` no arranque para evitar renderização destravada a centenas de frames que aquecem a bateria.
- **Sleep Timeout:** Manter tela acesa durante gameplay com `Screen.sleepTimeout = SleepTimeout.NeverSleep`.
- **Física Fixed Timestep:** Configurar `Time.fixedDeltaTime = 0.02f` (50 Hz) para manter estabilidade física com baixo overhead de CPU.
