# 0114 — La cámara no es fija: tiene cuatro estados

Estado: **Aceptada** (15 sep 2026, decisión del revisor). **Sustituye la parte de cámara fija de la
ADR 0102**; el resto de la ADR 0102 —3D, toon, proyección ortográfica en tres cuartos— sigue vigente. El
desarrollo completo está en `docs/estilo-visual.md` §5ter, y el cálculo que lo sostiene en §5bis.

## Problema

La ADR 0102 fijó una cámara **ortográfica fija** en tres cuartos, y de ahí salía un briefing de arte con
una exigencia imposible: **cada personaje tenía que resolver toda su identidad en ~37 px de ancho** (la
cuenta, con el campo de siete filas: 1120×500 px para 16×7 casillas = 66 px por casilla, y una ficha mide
0,56 casillas).

Eso tenía dos consecuencias malas a la vez:

1. **El 3D no se amortizaba nunca.** Un modelo con barba, cicatrices, colmillos y expresión cuesta lo
   mismo de hacer se vea o no, y a 37 px no se ve nada de eso. El presupuesto de píxeles del 3D resultó ser
   casi idéntico al del sprite 2D de RA-001 a escala 3x (36×51): el 3D no daba más píxeles, solo más
   libertad de forma dentro de los mismos.
2. **La prueba de silueta se volvía un cuello de botella de diseño.** Con cinco razas que deben
   distinguirse en blanco y negro, y detalles interiores que no leen nunca, quedaba muy poco margen.

## Decisión

**La cámara tiene cuatro estados**, y la transición entre ellos es parte del lenguaje visual del juego:

| estado | escala | cuándo |
|---|---|---|
| **Táctica** | 1× | El estado **normal**. Campo completo, jugadores a ~37 px. No se interrumpe la lectura |
| **Acción** | ~1,5-2× | Zooms y desplazamientos **suaves y no intrusivos** en regates, entradas, tiros y perks. **No es una cinemática**: no pausa ni secuestra la simulación |
| **Cinematográfica** | ~2-3× | **Excepcional**: gol, lesión importante, muerte, roja, habilidad excepcional. Puede bajar la elevación para que se lea una cara. **Solo a 1×** |
| **Presentación** | 3×/4×+ | Menús, fichaje, recompensas, MVP, estadísticas. Fuera del flujo del partido |

Y el briefing de arte cambia de una escala a tres: **~37 px** reconocer la raza · **~80-120 px** apreciar
al personaje · **300+ px** apreciar el diseño entero.

## Lo que lo acota, y es lo que evita que se convierta en un desfile de cinemáticas

**Medido**: el reglamento son **1.200 ticks a 15/s = 80 segundos**, y un partido genera del orden de **23
eventos notables** (12,59 entradas, 7,47 tiros, 2,30 goles, 0,80 lesiones). Eso es **uno cada 3,5
segundos**. Una cámara que reaccionara a cada evento no diría «mira esto»: sería un metrónomo. Y la
reproducción admite **1×, 4× y 16×**, así que a 4× ese ritmo pasa a uno cada 0,86 s reales y a 16× a uno
cada 0,22 s.

De ahí dos reglas duras:

1. **Un evento que merece un ajuste de cámara no es lo mismo que un evento que merece una cinemática.**
   Tiros, entradas y regates: como mucho, cámara de **acción**. Gol, lesión, muerte, roja y habilidad
   excepcional: cámara **cinematográfica**.
2. **Se corta por velocidad**: 1× las tres · 4× táctica y movimientos de acción no intrusivos · 16× **solo
   táctica**.

## Consecuencias

- **No toca `/Sim` en absoluto.** La cámara **consume** el flujo de eventos y no decide nada del partido
  (RT-014), y ese flujo ya lleva todo lo necesario: `SHOT`, `TACKLE`, `GOAL`, `INJURY`, `DEATH`, `CARD` y,
  desde la **ADR 0112**, `PERK_TRIGGERED`.
- **Un solo canal de «mira esto».** El aviso de perk de la ADR 0112 —el cartel de 1 s sobre la cabeza— es
  la versión barata de esta misma idea. Se reparten el trabajo: **el cartel marca lo pequeño y frecuente,
  la cámara marca lo excepcional.** No se construyen dos sistemas gritando a la vez.
- **Cierra una decisión de arte abierta**: anatomía correcta contra deformación de tebeo, **a favor de la
  deformación**, por un motivo que no existía sin esta ADR — al acercarse, la cámara **delata** la anatomía
  3D convencional.
- **Ordena el detalle interior en vez de descartarlo.** La regla de que solo cuenta el contorno sigue
  valiendo **para la vista táctica**; el detalle interior pasa a ser de segundo nivel, que cobra valor a
  80-120 px. Lo que no cambia: la identidad de raza tiene que resolverse en el contorno, porque es a 37 px
  donde hay que distinguir veinte cuerpos.
- **Coste que hay que presupuestar**: la cámara cinematográfica quiere bajar la elevación, y eso es un
  **segundo eje** además del zoom.

## Riesgo abierto

Afinar el *feel* de una cámara es lo que peor se hace a ciegas, y aquí Godot corre en WSL **sin editor
gráfico**: lo visual se comprueba con capturas por Xvfb. Y hoy **la escena de capturas no produce nada**
(`docs/pendientes/BA-L.md`). Implementar esto antes de cerrar BA-L es trabajar sin instrumentos justo en
el sistema que más se juzga por el ojo.
