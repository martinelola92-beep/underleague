# Plan de fases

Del §7 de requisitos, con los entregables concretos y el estado. **Regla de fase:** no se produce arte hasta que el diseño de la fase 2 esté cerrado; el arte previo se descarta.

## Estado actual

**Fase 2 implementada y medida; el criterio de salida operativo se cumple, el de sensación no lo he
comprobado yo.** Última medición de cierre: **ADR 0100**, 1.200 runs × 2 semillas. Estado al **13 de
septiembre de 2026**, tras las ADR 0095-0100 y las dos partidas jugadas por el revisor (paquetes AW y AZ,
veintisiete anotaciones, todas cerradas).

El criterio operativo de `fase2-diseno.md` §0 —*"una run completa se puede jugar de principio a fin desde
código, es reproducible, y sus decisiones tienen consecuencias medibles en `/Balance`"*— **se cumple**, y
la métrica principal de la fase, **la curva de puertas de la ADR 0033, está en verde en las doce celdas**.
El criterio nominal (*"el jugador dice «una run más»"*) solo lo puede responder el revisor.

**Puertas: 7 clases, 43 hechos**, unos 5 min en Release en una sola invocación.

| Métrica | Banda | Medido (semillas 1 / 7) | |
|---|---|---|---|
| Curva de puertas (12 celdas, ADR 0033) | tabla de la ADR | todas dentro | ✅ |
| Duración de una run completa | 18-22 partidos | 20,0 | ✅ |
| Tasa de victoria de la run | 20-30 % (ADR 0040) | **25,33 / 27,00** | ✅ desde la ADR 0095 |
| Muertes por run | 1,5-3 (ADR 0048) | 1,85 / 1,85 | ✅ |
| Sumideros por acto (RF-114k) | 2-3, nunca 4 | 2,78 / 2,79 | ✅ |
| Compras por visita al mercado | 1-2 | 1,20 / 1,22 | ✅ |
| Oro sobrante | ≤ 15 % | 8,67 / 8,78 | ✅ |
| Runs sin poder comprar | 10-25 % | 12,92 / 11,33 | ✅ en las dos semillas desde la ADR 0100 |
| Arcos de build cerrados | ≥ 2 % | 46,2 / 44,3 | ✅ desde la ADR 0096 |
| **Fracción asequible del surtido** | 20-35 % | **63,4 / 63,9** | ❌ y se opone a las dos anteriores (Z-K) |
| **Ventaja de la política contextual** | ≥ 8 | **4,17 / 8,00** | ❌ **nunca cumplida** (histórico −2,1 a +5,6) |
| **Ganar sin pasar por el mercado** | ≤ 5 % | **6,58 / 8,08** | ❌ faltan ~2,8 puntos |

**Verde no quiere decir «en banda».** `FullRunGateTests.TheMetricsThatDoNotMeetTheirDesignBandStayWhereTheyWereMeasured`
es una valla anti-regresión con cotas anchas a propósito (`runWinRate` 5-40, `affordableShareAtMarket`
25-70, `leftoverGoldShare` 5-32, `purchasesPerMarket` 0,5-2), no la banda de diseño. Y
`contextualAdvantage` **no lo comprueba ninguna puerta**: se calcula, se imprime en `summary.csv` y CI no
vería un empeoramiento.

**Los seis objetivos de separación entre perfiles de la ADR 0056**, que son lo que decide si construir
bien se nota, van **2 de 6**: la build buena llega al 61,10 % de victorias ordinarias en el acto 2 pero se
queda en 49,40 % en el acto 3 (objetivo 60), la mediocre mide 47,60 % (objetivo 42-45), la mala completa la
run el 10,50 % de las veces (objetivo < 2) y el suelo sin build está en 10,92-11,33 % (objetivo < 10). Solo
el objetivo 6 (run en 20-30 %) y el 1 en su mitad del acto 2 están cumplidos.

**Lo que sigue abierto y bloquea el cierre de la fase 2**, con su fila en `pendientes.md`:

- ~~**CAT-B**~~ — **cerrada** el 13 sep 2026 (ADR 0101): el consumible se compra, se equipa en la pantalla
  de Equipo y llega al partido, y la política automática hace lo mismo para poder medirlo. Costó 2,5 puntos
  de `runWinRate` (25,33 → 22,8) y dejó `affordableShareAtMarket` peor (63,4 → 71,3), porque su precio de 45
  estaba maquillando esa métrica. Queda **CAT-C**: el slot manual no se puede pulsar en `/Balance`, así que
  la medición subestima a la familia.
- **AL-A** — el recorrido de un perk lo fija la base de su canal, no su magnitud (×2 sobre `pass` vale 0,19
  puntos; sobre `intercept`, 15,54). Es la raíz que heredan AK-B, AO-A, AU-B y AJ-C, y bloquea los
  objetivos 4 y 5 de la ADR 0056.
- **AZ-H** — el mercado no discrimina lo suficiente; es la causa directa de las tres filas rojas de arriba.
- **D-34** — RT-055 no se cumple (las nueve builds coherentes ganan 68,3-86,3 % a `human_none`, techo 70 %)
  porque el punto de comparación es una plantilla con cero perks. Cambiarlo **exige un ADR**.
- **D-28, D-30, D-31, D-33** — la segunda mitad de `scalingRewardsGoodBuilds` no es alcanzable; la escala
  de valor de perk no cabe en una tabla única; la correa sigue sin comprar nada; falta medir el bono de
  adyacencia de `Trait.Leader`.
- **AY-A** — el indicador de riesgo letal dejó de ser exacto tras el paquete AY: es un techo para el
  marcado y un cero falso para los demás, contra RF-012d. Su propia fila dice que hay que elegir **antes de
  la siguiente build**, y la build del 13 de septiembre salió sin esa decisión tomada.
- **AX-A** — `CompiledCondition` no es reentrante; acotado y contenido, pero cualquier `Parallel.For` nuevo
  sobre un catálogo compartido vuelve a picar en silencio.

Las cuatro decisiones que esta sección listaba en septiembre (Z-F a Z-L) ya no son las vivas: la ADR 0036
se aplicó (Z-H, Z-L cerradas) y la ADR 0040 cambió la banda a 20-30 (Z-G cerrada).

Detalle completo de las mediciones en `docs/balance/fase2-resultados.md` —cuyo §7 es de la primera
medición y está superado en sus puntos 1 a 5— y en las ADR 0095-0100.

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

- Mapa por capas (RF-010..014), **35 nodos y hasta 20 partidos** por run (D-2/D-10), nodos de mercado (RF-114..114f) con canteranos (RF-114b..d), lesiones y clínica con sus tres servicios (RF-090..094, ADR 0099), nodo de evento como carta con opciones (ADR 0100, `data/events/`), equipamiento (RF-075..078, ADR 0036), mercenarios (RF-110..113), economía (RF-114g..k), reroll (RF-071b), guardado ironman (RT-061) con snapshot de `/data` (RT-061b), modo de depuración (RT-062). La ADR 0097 **retiró el nodo de inscripción**: el hueco de plantilla se compra en el mercado.
- Máximo 30 perks y 12 objetos. **Hoy: 61 perks** (5 habilidades raciales + 56 obtenibles, 15 acumulativos por RF-070), **34 objetos** y 4 consumibles — catálogo derivado en `docs/catalogo-perks-y-objetos.md`.
- Cierre del diseño: se resuelven las decisiones pendientes D-2, D-3, D-6, D-7, D-10. **Las cinco están
  cerradas** (paquete Z, `pendientes.md`), más D-9 (paquete Y). A partir de aquí se puede encargar arte.

## Fase 3: identidad

Criterio de salida: el partido se lee sin necesidad del log.

**Dirección técnica decidida (ADR 0102, 13 sep 2026): 3D con toon shading y cámara ortográfica fija en tres cuartos**, que se lee como 2D. La rejilla sigue siendo 16×5 y la simulación sigue siendo plana: la tercera dimensión es **solo presentación** y cualquier altura que el render se invente es decorado que nunca se lee de vuelta. Retira el presupuesto de 115 frames de RA-017 —se paga por modelo, rig y clip, no por frame y raza— y con él RA-006, RA-007, RA-010, RA-011 y RA-015; §5 de requisitos hay que reescribirlo y hasta entonces manda el ADR. Los highlights (RA-020..022) **no se ven afectados**: nunca fueron pixelart.

Orden de trabajo: (1) el ADR, hecho; (2) cámara y escena de partido con **cápsulas grises** a las proporciones de RA-002, leyendo la traza que ya existe; (3) **prueba de silueta** sobre eso —si en tres cuartos con sombra se distinguen las cinco razas y se sigue el balón, el criterio de salida está encaminado y no se ha gastado en arte—; (4) `docs/ui-partido.md` como briefing, escrito sobre una geometría ya probada, antes de encargar nada.

- Pixelart definitivo y animaciones (RA-001..019b), highlights (RA-020..022, RF-115..117), vínculos (RF-100..106), gol de oro con turba (RF-055b..056), árbitro con rasgos, criterio y sobornos (RF-061..064g), ceremonia de muerte y memorial (RF-122). Git LFS (RT-004): **instalado** el 13 sep 2026 (3.8.0, en `~/.local/bin`; sin reglas de seguimiento todavía, porque no hay arte).
- **El taller de prótesis (RF-095..095c) sale de la fase 3** (decisión del revisor, 13 sep 2026): pasa a una expansión futura. Consecuencia que conviene tener escrita: era el **cuarto sumidero de oro** y la palanca de fase 3 que atacaba directamente `affordableShareAtMarket` y `contextualAdvantage`, las dos métricas del mercado que llevan fuera de banda desde siempre (AZ-H). Sin él, esas dos hay que atacarlas desde dentro de la fase 2.
- Métricas de violencia (RF-064e/g) en `/Balance`.

## Fase 4: demo de Steam

Criterio de salida: wishlists suficientes para justificar el lanzamiento.

- Steamworks, sonido, localización es/en (RT-073), mando y Steam Deck (RT-070/071), telemetría opcional (RT-065), primera run guiada (RF-123), divisiones (RF-128), logros de desbloqueo (RF-125).

## Después del lanzamiento

Copa con semilla diaria (RF-128c, requiere RT-023b), 4 razas DLC (RF-030), rivalidades (RF-103), Steam Workshop (RT-064), **taller de prótesis (RF-095..095c)** — sacado de la fase 3 el 13 sep 2026 por decisión del revisor.
