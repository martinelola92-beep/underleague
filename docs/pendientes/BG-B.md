# BG-B — La colocación del jugador se tiraba al construir el partido, y el indicador de riesgo no se enteraba

**Estado:** **CONFIRMED** · arreglado en la ADR 0134 · queda abierto lo que el arreglo destapa (ver abajo)

## Observación

Encontrado el 23 sep 2026 al implementar el once efectivo ([ADR 0134](../decisiones/0134-el-once-efectivo.md)),
no buscándolo. `RunLineup.Build` reasignaba **las siete casillas por rol** en cada partido: arrastrar un
defensa al puesto de delantero en la pantalla de Equipo se guardaba en el estado (`RunEngine.ApplyLineup`
→ `WithLineup`, verbatim) y después `Build` lo devolvía a una casilla de defensa. **La colocación del
jugador no llegaba al campo.**

Lo que lo convierte en un problema de previsibilidad y no solo en una función que ignora un dato: el
indicador de riesgo de muerte de RF-012c **sí** leía las casillas guardadas. Así que mover fichas **movía el
número** y no movía el partido. El jugador «reducía el riesgo con la alineación» —condición 3 de la
[ADR 0048](../decisiones/0048-morir-estando-sano.md), que es requisito y no aspiración— contra una
colocación que no se iba a jugar.

## Evidencia

**CONFIRMED por un test que ya existía y que era vacío sin saberlo.**
`Sim.Tests/Run/Systems/LethalRiskTests.MovingThePlayersChangesTheNumber` intercambia las casillas del jugador
marcado y del más lejano al carnicero y exige que el riesgo del marcado baje. Pasaba en verde porque
`RunEngine.LethalRisks` recorría **la alineación que se le pasaba**, casillas incluidas: comprobaba el
indicador, no el juego.

En cuanto los avisos pasaron a mirar el **once efectivo** —el que `Build` construye, que es el que se
juega— el mismo test se puso rojo con su propio mensaje: *«mover al marcado lejos del portador no le ha
bajado el riesgo: la colocación no es una palanca»*. No hizo falta ningún experimento nuevo: el instrumento
ya estaba escrito y decía la verdad en cuanto se le apuntó al sitio correcto.

## Por qué nadie lo había visto

Ningún camino **medido** coloca a mano. `/Balance` y las políticas automáticas componen con
`RunLineup.Compose`, que reparte el 2-3-1 por rol; el arranque de la run usa `RunLineup.Default`, que hace lo
mismo. Con las casillas guardadas ya iguales a las que `Build` iba a asignar, el descarte era invisible para
las 43 puertas. Solo un humano arrastrando fichas producía una colocación distinta, y eso no lo mide nadie.

Es el mismo patrón que [BG-A](./BG-A.md) y que [BC-H](./BC-H.md): **dos representaciones del mismo hecho —lo
que el jugador quiso y lo que el motor hace— que nadie obligaba a coincidir.**

## Arreglo

`RunLineup.PlaceOutfield` (ADR 0134): se respeta la casilla que el jugador dio a cada titular y el 2-3-1 por
rol se reparte **solo** entre los que no tienen ninguna. Dos pasadas —primero se reclaman las elegidas, luego
se rellenan las libres— para que el resultado no dependa del orden. La portería sigue siendo casilla fija
(RF-041). Sin alineación guardada sale exactamente el 2-3-1 de siempre, que es la razón por la que las 43
puertas no se mueven.

## Hallazgo de paso: contra la mitad de los rivales letales, la colocación no es una palanca

Medido al reconstruir el test, sobre **23 escenarios letales** (semillas 1..60, orco, primer nodo de partido
con portador letal accesible): en **11 el marcado es el portero** y en **12 un jugador de campo**. Siempre
**un solo marcado** —`VictimsPerActivation` = 1 con un portador—, con riesgos entre 472 y 6.660 en base
10.000.

Cuando el marcado es el portero, la palanca de colocación **no existe**: su casilla es fija (RF-041) y no se
le puede alejar. Es geométricamente coherente —el delantero rival, que es quien suele llevar el perk, queda
pegado a nuestra portería: `Matchup((6,3),(0,3)) = 1`— pero significa que la condición 3 de la ADR 0048 se
sostiene en esos casos **solo** sobre las otras dos palancas: sentarlo o curarlo. Y sentar al portero es
peculiar, porque el sustituto es un jugador de campo que ocupa **esa misma casilla**.

No es un fallo del arreglo: era así antes y nadie lo había mirado. Se anota porque la ADR 0048 declara esa
condición como **requisito, no aspiración**, y porque el test que la vigilaba pasaba en verde ejerciendo una
colocación ilegal (mover al portero fuera de su área), o sea que **no la vigilaba**.

## Lo que queda abierto

1. **El jugador puede ahora colocar como quiera, y eso no se ha medido.** Un 1-4-1, los siete arriba, o el
   bruto en la banda del carnicero son formaciones que el balance de fase 1 **nunca ha visto**: todas sus
   cifras salen del 2-3-1. No es un riesgo del arreglo —antes la libertad simplemente no existía— pero sí
   una superficie nueva, y conviene medir si hay una colocación degenerada antes de dar por buena la
   pantalla de Equipo.
2. **La pantalla no dice qué colocaciones son legales.** Hoy el único límite es que no se repita casilla y
   que la portería sea del portero; nada impide dejar la defensa vacía.
3. **RF-041 no dice si la colocación es libre o por rol.** Conviene cerrarlo por escrito ahora que importa.

## Hermanos

[BC-H](./BC-H.md) (misma causa, sobre quién juega), [BG-A](./BG-A.md) (misma causa, sobre quién cobra).
