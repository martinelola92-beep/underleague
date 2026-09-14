# 0111 — La cadena de pases se calibra contra el juego que hay

Estado: **Aceptada** (14 sep 2026). Deriva de `docs/auditoria-cadena-de-pases.md` y
`docs/ba-e-goles-sin-angulo.md`. Revisa el rango de `passChainAvgLength` de `balance.md` (RT-057).

## Problema

`passChainAvgLength ≥ 2,00` se convirtió en el filo con el que tropezaban todos los arreglos de juego. En
una misma tanda de trabajo la rozaron **tres** cambios independientes —BA-D (el sacador camina), la palanca
B de BA-E (penalizar el ángulo en la utilidad) y la palanca A (desmarcarse hacia la portería)—, todos por
centésimas, con el baseline en **2,01**.

Antes de moverla había que responder si ese 2,00 describía el juego o solo el sitio donde el baseline se
había parado.

## Lo que se midió

*(Todo en `docs/auditoria-cadena-de-pases.md`.)*

**1. Es un número heredado, no una propiedad medida.** La banda 2-4 se escribió en el **primer commit del
repositorio** (`fd65b1c`, requisitos v0.9) y **nunca se revisó**. `docs/requisitos.md` la presenta bajo el
encabezado literal **«Rango objetivo inicial»**. Es la única métrica de sensación de fútbol que sigue en su
valor de antes de que existiera el motor: alternancias, lesiones, tercio y tiros se revisaron con las
ADR 0081, 0082, 0093 y 0109.

**2. El suelo cortaba por el medio de la distribución.** Sobre 500 partidos y 8.107 cadenas:

| p05 | p25 | p50 | p75 | p95 |
|---|---|---|---|---|
| 1,64 | 1,93 | 2,10 | 2,32 | **2,77** |

**El 33,4 % de los partidos ya caía por debajo de 2,00 sin tocar nada.** La puerta no comprobaba una
propiedad del juego: comprobaba si la media de mil partidos caía por encima de la mediana de una
distribución ancha que la cruza continuamente.

**3. El techo de 4 no se ha rozado nunca.** El percentil 95 **por partido** es 2,77 y las cadenas de cinco o
más pases son el 5,3 % del total. La banda no estaba centrada en el juego por ninguno de los dos lados.

**4. La métrica ignora un tercio del juego por construcción.** `MatchEngine.EndPlay` solo cuenta una cadena
si tuvo **al menos un pase** (`if (_playPasses >= 1)`), y el **35,2 %** de las jugadas no tienen ninguno.
Mide «cuando se encadenan pases, cuántos», no «pases por posesión». El **40,5 %** de las cadenas son de un
solo pase.

## Decisión

`passChainAvgLength` pasa de **2-4** a **1,8-3,5**.

- **Suelo 1,8**: con el 40,5 % de las cadenas ya en un solo pase, una media de 1,8 significaría en torno al
  55 % de cadenas de un pase. Eso sí es «el equipo dejó de encadenar», que es lo que la puerta debe cazar.
  2,00 cazaba «el equipo juega directo», que es la identidad declarada del juego.
- **Techo 3,5**: sigue por encima de todo lo medido (p95 por partido 2,77) con margen, pero deja de ser un
  número inalcanzable por construcción. El techo existe para impedir el fútbol de posesión, y 3,5 lo impide
  igual que 4.
- **No se toca el motor.** Esta ADR cambia una banda, no el juego: con ella puesta, el baseline sigue en
  1,92-2,01 / 1,99-2,08 y ninguna otra métrica se mueve.

## Lo que esta ADR **no** desbloquea

La mini-auditoría se abrió para saber si revisar esta banda permitía aplicar la **palanca A** de BA-E
—`FindSpace` midiendo el avance hacia la portería en vez de hacia la banda—, que es la corrección
conceptualmente correcta del problema de los goles sin ángulo: lleva los tiros sin ángulo del 32,4 % al
13,0 % y los de la línea de fondo del 30,1 % al 14,2 %, en las dos semillas.

**No lo desbloquea, y el motivo no es la cadena.** Medido: A sola pone **ocho** puertas en rojo, y son de
diferenciación de builds —`coherentBuildsBeatNone_orc_violence` 55,83 contra 58, `badBuildsLoseToNone` en
dos builds, `randomBuildLosesToNone_human_random` 44,17, la curva de jefes de la ADR 0033— además de sacar
las lesiones de banda (0,94).

Pagar la violencia con `tackleDistanceMaxCells` 1,0 → 0,9 devuelve las lesiones a 0,88 y **mejora** el
cuadro a cinco rojas, pero no lo arregla: **la degradación es de A, no del precio**.

**INFERIDO, y es la misma lección que dejó el ensanchado de zonas de la ADR 0109:** cambiar la regla
posicional **global** aplana el juego de colocación. Si todo el mundo se desmarca con un criterio distinto,
la diferencia entre una build bien colocada y una mal colocada se encoge. El desmarque es donde vive esa
diferencia, así que no se puede reescribir sin pagarla.

**BA-E queda abierta** con su diagnóstico completo (`docs/ba-e-goles-sin-angulo.md`): A es correcta
conceptualmente y **rechazada por coste**, B descartada, y C —el ángulo en la resolución del tiro— sigue
disponible como corrección parcial y segura si el revisor la quiere. La corrección buena tendrá que ser
**local** al delantero en zona de remate, no un cambio de la regla de desmarque de los siete.

## Reversión

Devolver el rango a `2, 4` en `Sim/Analysis/MatchMetrics.cs` y en la tabla de `docs/balance.md`. No hay
cambio de motor que deshacer.
