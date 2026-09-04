# Entorno de desarrollo

Máquina: Windows con WSL2 (Ubuntu 24.04). Claude Code corre en WSL. Godot corre en Windows. El repositorio vive en `~/underleague` dentro de WSL.

## Reparto

| Parte | Dónde se compila | Herramienta |
|---|---|---|
| `/Sim`, `/Sim.Tests`, `/Balance`, `/tools` | WSL | `dotnet` (SDK 10, fijado por `global.json`) |
| `/Game` | **WSL** | Godot 4.6.3 mono para Linux, en `~/.local/opt`, enlazado en `~/.local/bin/godot` |
| `/data`, `/docs` | Cualquiera | — |

**Godot se ejecuta desde WSL, no desde Windows.** El editor de Windows **no puede abrir el proyecto**: Godot no admite rutas UNC y rechaza `\\wsl$\Ubuntu\...`, y `\\wsl$` tampoco se puede mapear como unidad de red (error de sistema 67). La solución es Godot para Linux dentro de WSL, que trabaja sobre el árbol nativo:

```bash
godot --headless --path Game --import          # importar recursos
godot --headless --path Game --quit-after 30   # ejecutar sin ventana
godot --path Game                              # editor gráfico, vía WSLg
```

El editor gráfico funciona porque WSLg está disponible (`/mnt/wslg`). La instalación de Windows se conserva por si hace falta exportar, pero no se usa para desarrollar.

## Estado el 4 de septiembre de 2026

| Prerrequisito | Estado | Instalación |
|---|---|---|
| Git en WSL | 2.43 | — |
| .NET SDK 10 en WSL | 10.0.111 (también 8.0.130) | `sudo apt install -y dotnet-sdk-10.0` |
| `csharp-ls` (plugin `csharp-lsp` de Claude Code) | 0.27.0 | `dotnet tool install --global csharp-ls`. Requiere SDK 10: con solo el SDK 8 falla con "DotnetToolSettings.xml was not found" (la última para .NET 8 es la 0.16.0). `~/.dotnet/tools` está en el `PATH` vía `.bashrc` |
| Godot 4.6.3 mono en WSL | Instalado en `~/.local/opt`, enlace en `~/.local/bin/godot`; **es el que se usa para desarrollar** | descarga del release de GitHub y descompresión |
| .NET SDK 10 en Windows | 10.0.400 en `C:\Program Files\dotnet` (instalado 4 sep 2026) | `winget install Microsoft.DotNet.SDK.10` (pide UAC) |
| Godot 4.6.3 mono en Windows (solo para exportar) | 4.6.3 en `%LOCALAPPDATA%\Microsoft\WinGet\Packages\GodotEngine.GodotEngine.Mono_Microsoft.Winget.Source_8wekyb3d8bbwe\Godot_v4.6.3-stable_mono_win64\` (instalado 4 sep 2026; sin alias `godot` por no ser admin) | `winget install GodotEngine.GodotEngine.Mono -v 4.6.3` (no la versión estándar) |
| Git LFS | Falta, no necesario hasta fase 3 | `sudo apt install git-lfs` |
| Identidad de git | **Sin configurar** | `git config --global user.name "…"` y `user.email "…"` |

## Comprobación rápida

```bash
dotnet --list-sdks          # debe listar 10.0.x
dotnet build Underleague.sln
dotnet test Sim.Tests
```

## Claude Code

Plugins instalados a nivel de usuario: `csharp-lsp`, `commit-commands`, `claude-md-management`, `context7`, `skill-creator` (marketplace oficial), `dotnet-skills` (marketplace `Aaronontheweb/dotnet-skills`) y `godot-prompter` (marketplace `jame581/skillsmith`). Marketplace `Randroids-Dojo/skills` añadido sin instalar nada (su plugin `godot` es candidato para la fase 4). Skills del proyecto en `.claude/skills/`; subagentes del proyecto en `.claude/agents/` (`deep-reasoner` opus, `fast-worker` sonnet). Permisos preaprobados para `dotnet build/test/run` en `.claude/settings.json`.
