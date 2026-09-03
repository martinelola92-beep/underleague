# Plan de fases

Del §7 de requisitos, con los entregables concretos y el estado. **Regla de fase:** no se produce arte hasta que el diseño de la fase 2 esté cerrado; el arte previo se descarta.

## Estado actual

**Fase 0, no iniciada.** 3 de septiembre de 2026. Existen requisitos v0.9, este repositorio con documentación y las skills de Claude Code. No hay solución .NET ni proyecto Godot.

## Fase 0: simulador sin gráficos

Criterio de salida: las métricas de RT-056 entran en rango y los equipos mejores ganan más con sorpresas creíbles (medido en `/Balance`).

Entregables:

1. Solución `Underleague.sln` con `/Sim`, `/Sim.Tests`, `/Balance`, `/tools/DataValidator`. CI en GitHub Actions con matriz Windows/Linux (RT-024, RT-054).
2. RNG PCG32 + splitmix64 para derivar flujos (RT-021, RT-022). Tests.
3. Esquema `Run` y `MatchState` versión 1 en código y JSON Schema (RT-030). Solo los campos que la fase 0 usa; el resto se documenta en `modelo-datos.md`.
4. Campo 16x5, casillas-hogar, correas, posiciones y restricciones de colocación (RF-040..045).
5. Las tres máquinas de estado en código con `CanPerform(state, action)` (RT-089c).
6. IA de utilidad con pesos en `data/ai/` por posición y rasgo (RT-090..097) y volcado de tabla por tick (RT-098).
7. Portero con fórmula de parada (RF-057c).
8. Árbitro neutro con criterio fijo 0: faltas, tarjetas, penalti (para que la máquina de partido esté completa).
9. Jugadas y fases de jugada (RF-051), saque, reanudaciones instantáneas (RF-052/053).
10. Generación de jugadores: media de raza + desviación individual + posición + rasgos, con 3 razas (humanos, orcos, elfos; decisión pendiente D-5) y generador de nombres (RF-020b, RF-024b/c).
11. `/Balance` con CLI, CSV y las 7 métricas de RT-056. Objetivo de 10.000 partidos < 60 s (RT-051).
12. Log de texto por tick/evento (RF-121) como única ventana al simulador.
13. Test de determinismo (RT-024), test estadístico de 1.000 partidos (RT-081), test de arquitectura (RT-011).

Fuera de la fase 0: perks, lesiones persistentes, turba, sobornos, vínculos, cualquier UI.

## Fase 1: motor de efectos y pantalla de Equipo

Criterio de salida: dos builds distintas ganan de formas distintas y se nota.

- Bus de eventos con contexto completo (RF-066/067), orden RT-041, recursión RT-042, registro RT-043.
- Perks como datos con NCalc (RT-033/034), 20 perks, descripciones generadas (RT-035).
- Niveles y experiencia (RF-025..027), rarezas (RF-023), fichajes con rareza.
- Proyecto Godot `/Game` con círculos de colores; pantalla de **Equipo** (UI-020/021), alineación previa, partido, recompensa, informe post-partido (RF-119).
- Formato del evento de telemetría (RT-065).
- Métricas de fase 1 en `balance.md`.

## Fase 2: bucle de run completo

Criterio de salida: el jugador dice "una run más" sin arte terminado.

- Mapa por capas (RF-010..014), 8 partidos + 1 jefe, nodos de mercado (RF-114..114f) con canteranos (RF-114b..d), lesiones y clínica (RF-090..094), equipamiento (RF-075..078), mercenarios (RF-110..113), economía (RF-114g..k), reroll (RF-071b), guardado ironman (RT-061) con snapshot de `/data` (RT-061b), modo de depuración (RT-062).
- Máximo 30 perks y 12 objetos.
- Cierre del diseño: se resuelven las decisiones pendientes D-2, D-3, D-6, D-7, D-10. A partir de aquí se puede encargar arte.

## Fase 3: identidad

Criterio de salida: el partido se lee sin necesidad del log.

- Pixelart definitivo y animaciones (RA-001..019b), highlights (RA-020..022, RF-115..117), vínculos (RF-100..106), gol de oro con turba (RF-055b..056), taller de prótesis (RF-095..095c), árbitro con rasgos, criterio y sobornos (RF-061..064g), ceremonia de muerte y memorial (RF-122). Git LFS (RT-004).
- Métricas de violencia (RF-064e/g) en `/Balance`.

## Fase 4: demo de Steam

Criterio de salida: wishlists suficientes para justificar el lanzamiento.

- Steamworks, sonido, localización es/en (RT-073), mando y Steam Deck (RT-070/071), telemetría opcional (RT-065), primera run guiada (RF-123), divisiones (RF-128), logros de desbloqueo (RF-125).

## Después del lanzamiento

Copa con semilla diaria (RF-128c, requiere RT-023b), 4 razas DLC (RF-030), rivalidades (RF-103), Steam Workshop (RT-064).
