# Auditoria do ambiente — 2026-10-15

Executada pela sessão Antigravity #1. Reexecutar com `tools/setup/audit.ps1` (gera `evidence/audit/*.json`).

## Hardware / SO

| Item | Valor |
|---|---|
| SO | Windows 11 Pro, build 10.0.26200 |
| CPU | AMD Ryzen 5 3600 (6C/12T) |
| RAM | 64 GB |
| GPU | NVIDIA GeForce RTX 3060 (driver 32.0.16.1742) |
| Disco C: | ~489 GB livres |
| Shell | Windows PowerShell 5.1 (não elevado). ExecutionPolicy CurrentUser → `RemoteSigned` (ajustado) |
| WSL | WSL2, distro `Ubuntu` (parada). **Não usada** no caminho crítico: Unity/Android são nativos no Windows |

## Ferramentas encontradas antes da intervenção

| Ferramenta | Estado |
|---|---|
| git 2.55 + git-lfs 3.7.1 | presente |
| opencode 1.18.34 | presente, credencial **Z.AI Coding Plan** (modelos GLM 4.7/5.x) + modelos gratuitos `opencode/*` |
| adb | presente (empacotado com scrcpy 4.1) — nenhum aparelho conectado |
| ffmpeg 9.0.1 | presente |
| winget | presente |
| Node / npm / Python / .NET / Java / Android SDK / Unity | **ausentes** |
| Claude Code, Codex, Gemini CLI, Docker, gh | ausentes |

## Instalado por esta sessão (escopo de usuário, sem admin)

| Ferramenta | Versão | Origem |
|---|---|---|
| Python | 3.13.15 | winget `Python.Python.3.13` |
| Bun | 1.4.3 | winget `Oven-sh.Bun` |
| fnm → Node LTS | Node 24.22.0 / npm 11.19.1 | winget `Schniz.fnm`; alias `default` adicionado ao PATH do usuário |
| uv | 0.13.1 | winget `astral-sh.uv` |

## Implicações

1. **Cliente de IA principal headless:** OpenCode (já autenticado). Claude Code não está instalado e não há
   credencial Anthropic; o roadmap previa Claude Code como primeiro cliente → **desvio registrado no ADR-002**.
2. **Unity** precisa de Unity Hub (admin, UAC) e **login + licença Personal** (credencial do usuário).
3. **Android**: SDK/NDK/OpenJDK serão instalados como módulos do Unity (não há Android Studio).
4. GPU dedicada permite renderização real em batchmode para capturas (sem `-nographics`).
