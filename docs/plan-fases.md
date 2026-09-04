# Plan de fases

Del §7 de requisitos, con los entregables concretos y el estado. **Regla de fase:** no se produce arte hasta que el diseño de la fase 2 esté cerrado; el arte previo se descarta.

## Estado actual

**Fase 2 implementada y medida** (5 de septiembre de 2026). El bucle de run se juega entero desde
código, es reproducible y sus decisiones tienen consecuencias medibles: mapa por actos con mercado
garantizado, economía, mercado con canteranos y mercenarios, clínica, equipamiento, recompensas con
reroll, tres jefes con modificadores de regla, guardado ironman y `--full-runs` con tres políticas
automáticas. Cinco puertas automáticas en verde (`Trait("Category","Gate")`), `DataValidator` sin
errores y la suite completa en verde.

**La métrica principal de la fase, la curva de puertas de la ADR 0033, se cumple en las doce celdas**
(`docs/fase2-diseno.md` §16.6). Lo que **no** se cumple es la mitad de las métricas de apoyo de §10 y de
la ADR 0037, con causa identificada y número en `docs/balance/fase2-resultados.md`:

| Métrica | Rango | Medido |
|---|---|---|
| Curva de puertas (12 celdas, ADR 0033) | tabla de la ADR | **todas dentro** |
| Duración de una run completa | 18-22 partidos | 20,0 |
| Derrotas por bajar de 5 jugadores | < 35% | 0,0 |
| Sumideros que paga el oro de un acto (RF-114k) | 2-3, nunca 4 | 2,40 |
| Compras por visita al mercado (ADR 0037) | 1-2 | 1,43 |
| Tasa de victoria de la run | 25-40% | **13,0** |
| Muertes por run | 0,5-2 | **0,00** |
| Ventaja de la política contextual (ADR 0037) | >= 8 puntos | **+5,0 / +0,8** |
| Fracción asequible del surtido / oro sobrante / visitas en blanco | 20-35 / <15 / 10-25 | **40,5 / 23,2 / 49,2** |

Las cuatro cosas que hay que decidir antes de cerrar la fase están en `pendientes.md` Z-F a Z-L: que el
club inicial traiga una build (RF-023/RF-005, exige ADR), que la banda de tasa de victoria de §10 baje a
20-30% para ser compatible con la ADR 0033, que se aplique la **ADR 0036** (el equipamiento no vale nada
hoy y bloquea el criterio de la ADR 0037), y que la fórmula de lesión deje de medirse contra el nivel 1
(hoy un equipo que sube de nivel es inmune a las lesiones, y con ellas se van la clínica, las muertes y
el desgaste).

**Fase 1 cerrada** (4 de septiembre de 2026) con el bloque de rediseño espacial (ADR 0020-0030) y su
reajuste único (paquete U). El criterio de salida de la fase 1 —"dos builds distintas ganan de formas
distintas y se nota"— **se cumple y está automatizado**: la puerta `Sim.Tests/Analysis/BuildGateTests.cs`
está activa y en verde, junto con la de fase 0 y la nueva de rareza y jefe final.

Criterio de salida de fase 0, remedido (RT-056, 2.000 partidos, semilla 1): alternancias 23,6 · cadena
media de pases 2,32 · tiros 11,8 · resultados 1-0..3-2 79% · tercio máximo 40,0% · entradas 9,96 · lesiones
0,62 · mejor equipo (Δ20) 69,4%. **Todo dentro de rango** salvo los empates al final del reglamentario
(27,8%, inconsistencia I-11, `INFO`) y los partidos de más de cinco goles (10,9%, `INFO`).

Criterio de salida de fase 1 (6.720 partidos, semilla 1): las nueve builds coherentes ganan entre el 67,7%
y el 83,1% a la referencia de su raza (umbral 58%), las cuatro malas se quedan entre el 9,2% y el 42,1%
(umbral 45%), la aleatoria en 55,6%, `buildsWinDifferently` 3,05× en lesiones y 1,39× en cadena de pases,
ningún perk muerto y RF-069 en 64/31/4.

Métricas de rareza (ADR 0027): común de nivel 8 contra legendario de nivel 2, 49,8%; contra legendario de
nivel 8, 38,8%; equipo sin ningún legendario contra el jefe final, 57,9%.

Detalle completo, palancas movidas y conclusiones de diseño en **`docs/balance/fase1b-resultados.md`**;
deuda abierta en `pendientes.md` D-28 a D-33, con el equilibrio entre razas (D-29) como primer trabajo de
balance de la fase 2.

**Fase 0 implementada** el 3 de septiembre de 2026. Existen `/Sim` (motor completo sin perks),
`/Sim.Tests`, `/Balance` y `/tools/DataValidator`, con CI en Windows y Linux. Rendimiento tras el rediseño:
231 partidos/s en Release, por encima de los 167 que exige RT-051.

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
- Máximo 30 perks y 12 objetos. **Al cierre del paquete Z: 53 perks (15 acumulativos, RF-070) y 12 objetos.**
- Cierre del diseño: se resuelven las decisiones pendientes D-2, D-3, D-6, D-7, D-10. **Las cinco están
  cerradas** (paquete Z, `pendientes.md`), más D-9 (paquete Y). A partir de aquí se puede encargar arte.

## Fase 3: identidad

Criterio de salida: el partido se lee sin necesidad del log.

- Pixelart definitivo y animaciones (RA-001..019b), highlights (RA-020..022, RF-115..117), vínculos (RF-100..106), gol de oro con turba (RF-055b..056), taller de prótesis (RF-095..095c), árbitro con rasgos, criterio y sobornos (RF-061..064g), ceremonia de muerte y memorial (RF-122). Git LFS (RT-004).
- Métricas de violencia (RF-064e/g) en `/Balance`.

## Fase 4: demo de Steam

Criterio de salida: wishlists suficientes para justificar el lanzamiento.

- Steamworks, sonido, localización es/en (RT-073), mando y Steam Deck (RT-070/071), telemetría opcional (RT-065), primera run guiada (RF-123), divisiones (RF-128), logros de desbloqueo (RF-125).

## Después del lanzamiento

Copa con semilla diaria (RF-128c, requiere RT-023b), 4 razas DLC (RF-030), rivalidades (RF-103), Steam Workshop (RT-064).
