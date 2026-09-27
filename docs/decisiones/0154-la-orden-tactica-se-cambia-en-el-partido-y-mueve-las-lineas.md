# ADR 0154 — La orden táctica se cambia durante el partido y mueve las líneas

Fecha: 27 sep 2026 · Estado: **aceptada**. **Decisión del revisor.** Enmienda la
[ADR 0140](0140-marcador-minuto-y-orden-tactica.md) §4 («la orden es estado inicial») y completa su
«`/Game` necesita un selector».

## Decisión del revisor

> *Implementa la posibilidad de que el jugador escoja sobre la táctica de su equipo durante el partido. Lo
> que tenemos ahora es neutro, tenemos que poner un "slider" o botonera con 3 opciones: defensivo, neutro u
> ofensivo. Defensivo tiene que hacer que el delantero baje líneas. Ofensivo que los defensas suban más.
> […] El resultado tiene que ser que en defensivo el equipo recibe menos goles y en ofensivo marca más.*

## Lo que había

La ADR 0140 ya tenía la orden (`Mentality`: multiplicadores por acción en `weights.json`, más la urgencia de
marcador y minuto), pero sólo **antes** del partido, sin selector en `/Game` y **sin mover a nadie de
sitio**. Medido (1.200 partidos `TestMatches.Reference`, casa con la orden contra un rival neutro): el
ofensivo marcaba más **sin** encajar más —dominante, la degeneración que la propia ADR 0140 dejó para
vigilar— y el defensivo no encajaba claramente menos.

## Decisión

1. **La orden mueve las líneas** (`ai.mentalityShift`, casillas hacia la portería rival por orden y
   puesto, sumadas a la casilla-hogar en `UpdateBlockShift`):

   | | portero | defensa | medio | delantero |
   |---|---|---|---|---|
   | Defensivo | 0 | −1 | −2 | −3 |
   | Neutro | 0 | 0 | 0 | 0 |
   | Ofensivo | 0 | +3 | +2 | +1 |

   Neutro es 0 por definición: la capa es inerte y un partido neutro sale **idéntico byte a byte** al de
   antes (lote de 4.000 comprobado). Es la **orden** del jugador, no la urgencia: la urgencia sigue
   empujando las acciones (ADR 0140) y no cambia la altura a la que el entrenador puso las líneas.
2. **Se cambia durante el partido**: `OrderChange(Tick, Order)` viaja como estado inicial
   (`MatchDecisions` → `RunEngine.BuildMatch` → `TeamSetup.OrderChanges`), igual que las sustituciones de
   la ADR 0094; el motor lo aplica al llegar a su tick. Un cambio en T no altera nada anterior a T (test),
   así que la pantalla vuelve a reproducir el partido y sigue desde donde se pulsó.
3. **`/Game`**: botonera de tres (Defensa · Neutro · Ataque) a la izquierda del tablero, simétrica a la de
   velocidad; la vigente en oro. Se aplica desde el tick siguiente al que enseña la pantalla, por el mismo
   camino que una sustitución. Cada partido empieza en Neutro.

## Calibración (1.200 partidos, mismas semillas, casa contra rival neutro)

| orden | a favor | en contra | gana |
|---|---|---|---|
| Neutro | 0,882 | 0,810 | 46,7 % |
| Defensivo (0 / −1 / −2, primera prueba) | 0,664 | 0,763 | — |
| **Defensivo (−1 / −2 / −3)** | **0,600** | **0,708** (−13 %) | 34,5 % |
| Defensivo (−2 / −3 / −4) | 0,533 | 0,670 | 33,3 % |
| Ofensivo (+1,5 / +1 / 0, primera prueba) | 1,073 | 0,769 (−5 %: dominante) | 55,8 % |
| **Ofensivo (+3 / +2 / +1)** | **1,003** (+14 %) | **0,861** (+6 %) | 51,0 % |

- Defensivo encaja un 13 % menos a cambio de un tercio del ataque: es para defender un resultado, no para
  jugar siempre así. Más bloqueo y más entradas (multiplicadores) no bajaban los goles en contra; sólo
  hundir el bloque entero lo hace.
- Ofensivo marca un 14 % más y encaja un 6 % más: la defensa adelantada deja espacio a la espalda. Con la
  subida más corta era mejor en todo, y nadie elegiría otra cosa.
- **Queda un sesgo**: ofensivo sigue ganando algo más que neutro de media (51 % frente a 47 %). Anotado
  para la fase de balance; la puerta `MatchOrderTests.DefensiveConcedesLessAndOffensiveScoresMore` fija la
  promesa del revisor (defensivo encaja menos, ofensivo marca más, y cada una cuesta algo).
- Valores con procedencia en esta tabla; medidos sólo con `TestMatches.Reference` (calidad 50 contra 50).

## Consecuencias

- La IA rival sigue en Neutro (más la urgencia). Que el rival elija orden es otra decisión.
- Cada cambio de orden vuelve a simular el partido y a entrar en el nodo, como una sustitución.
- `docs/requisitos.md` §1 decía «toda la decisión ocurre entre partidos»; enmendado.

## Revisión independiente (27 sep 2026)

- **Defecto corregido**: pulsar una orden mientras se anuncia una muerte (el bando y la bandeja esperan su
  retardo) los borraba y el jugador perdía la decisión de sustitución. La botonera se bloquea con una
  muerte o una decisión pendientes y mientras la reproducción está congelada en un anuncio.
- **Defecto corregido**: el cambio reanudaba un fotograma tarde y lo que pasara justo ahí se quedaba sin
  cartel ni voz. Ahora reanuda desde el fotograma del cambio.
- **Verificado por la revisión**: la calibración se reproduce exacta; la orden sobrevive a los sistemas que
  reconstruyen el `TeamSetup`; volver a entrar en el nodo no cobra dos veces; la micro-gestión (ofensivo
  con balón, defensivo sin él) es peor que neutro.
- **Ofensivo sigue algo por encima de neutro en seis escenarios** (raza, calidad, orden rival), cada uno
  por debajo de 2σ. Anotado para la fase de balance.

## Lo que la orden defensiva NO hace (medido)

El uso real del botón es reaccionar al marcador. **Pasar a defensivo en el último tercio con ventaja no se
nota**: de 914 partidos que iban ganando en el tick 1.100 (3.000 medidos), mantienen la ventaja el 91,5 %
en neutro, el 90,7 % con este defensivo y el 91,4 % con uno más hundido (−2/−3/−4). La ventaja ya se
conserva nueve de cada diez veces y un −13 % de goles en contra aplicado a un tercio de partido es menos de
un punto: por debajo del ruido. Además, la urgencia de la ADR 0140 ya lleva las acciones hacia lo defensivo
cuando se va ganando. Para que «defender un resultado» se note, el defensivo tiene que recortar los goles
en contra mucho más, y hundir el bloque no escala (−17 % en el más hundido). **Decisión pendiente del
revisor.**

Sin tratar, anotado: el desplazamiento cambia de golpe al pulsar (las casillas-hogar saltan, el equipo anda
hacia ellas; el bloque táctico en cambio se interpola); el desplazamiento no se mezcla con la urgencia; en
defensas el +3 lo recorta el techo del bloque (`CapToDefensiveLine`) salvo con el balón por delante; los
textos de la botonera sólo en español, como el resto de la retransmisión.

