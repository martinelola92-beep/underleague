# ADR 0156 — La orden táctica también mueve el éxito, no sólo la colocación

Fecha: 27 sep 2026 · Estado: **aceptada**. **Propuesta del revisor**: *«si estamos en ataque los atributos
de ataque de todos los jugadores suben un poco y en defensa los de defensa; o si no, el porcentaje de
éxito. Subir uno y bajar el otro. Algo así.»* Completa la [ADR 0154](0154-la-orden-tactica-se-cambia-en-el-partido-y-mueve-las-lineas.md).

## Decisión

1. **La orden aplica un multiplicador de cuota a los canales de probabilidad de todo el equipo**
   (`ai.mentalityOdds`), por el mismo sitio que los perks (`MatchEngine.Odds`) y compuesto con ellos:

   | | tiro a puerta | regate | entrada | intercepción | parada |
   |---|---|---|---|---|---|
   | Ofensivo | +30 | +30 | −20 | −20 | −20 |
   | Defensivo | −20 | −20 | +30 | +30 | +30 |

   Porcentaje de cuota con signo, el formato de los perks (−20 es el inverso de +20). Neutral no puede
   mover nada (lo valida el cargador). Se hace con el **éxito** y no con los **atributos** porque los
   atributos alimentan también energía, velocidad y lesiones, que no son lo que la orden cambia.
2. **La subida ofensiva de líneas se modera** a defensas +1,5, medios +1, delantero +0,5 (era +3/+2/+1):
   con la subida grande, pasar a ofensivo **cuando se va perdiendo** reducía las remontadas (medido abajo),
   que es justo lo primero que hará el jugador.

## Medición (1.200 partidos de partido entero; 3.000 para el cambio tardío en el tick 1.100)

| variante | ofensivo a favor/en contra (gana) | defensivo a favor/en contra | ofensivo al ir perdiendo: remonta o empata (neutro 10,1 %) | defensivo al ir ganando: ventaja mantenida (neutro 91,7 %) |
|---|---|---|---|---|
| sólo colocación (ADR 0154) | 1,00 / 0,86 (51 %) | 0,60 / 0,71 | — | 91,0 % |
| ±20 en los dos sentidos | 0,93 / 0,91 (46,9 %) | 0,58 / 0,65 | **6,7 %** | 92,2 % |
| +25 sólo a lo propio | 1,00 / 0,78 (52,5 %) | 0,62 / 0,63 | 8,6 % | 92,2 % |
| +30 / −10, subida grande | 0,98 / 0,85 (51,4 %) | 0,61 / 0,59 | 7,5 % | 92,3 % |
| +30 / −10, **sin** subida | 1,02 / 0,78 (53,2 %) | 0,61 / 0,59 | 9,5 % | 92,3 % |
| +30 / −10, subida moderada | 1,15 / 0,82 (57,0 %) | 0,61 / 0,59 | 10,7 % | 92,3 % |
| **+30 / −20, subida moderada (publicada)** | **1,13 / 0,88 (55,8 %)** | **0,59 / 0,60** | **10,3 %** | **92,2 %** |

Neutro: 0,87 / 0,78, gana el 46,9 %.

- **Defensivo encaja un 24 % menos** (y marca un 32 % menos). **Ofensivo marca un 30 % más** (y encaja un
  13 % más). Es lo que pidió el revisor.
- **La subida grande de la defensa era probablemente la que hundía las remontadas** (LIKELY, no CONFIRMED:
  7,5 % frente a 9,5 % está a ~1,5 errores típicos). Con la moderada deja de ir en contra, pero 10,3 %
  frente a 10,1 % es ruido.
- **Cambiar de orden en el último tercio mueve poco, con cualquier variante**: la ventaja se conserva el
  92 % de las veces y en un tercio entra poco más de medio gol; y la **urgencia** de la ADR 0140 ya lleva
  al equipo a atacar cuando pierde y a defender cuando gana. El botón tiene efecto claro en el partido
  entero —elegir estilo contra un rival— y pequeño como reacción tardía.
- **Queda un sesgo, anotado para la fase de balance**: ofensivo gana más que neutro (55,8 % frente a
  46,9 %). Con las palancas medidas, que ofensivo no domine y que sirva al ir perdiendo tiran en sentidos
  opuestos: para no dominar tiene que encajar bastante más, y encajar más es lo que quita las remontadas.
  El rival (IA) juega siempre en neutro.

## Revisión independiente (27 sep 2026)

- **El caso del último tercio no discrimina**: con ~900 partidos por grupo el error típico de «remonta» o
  «mantiene la ventaja» es de ~1,2-1,4 puntos, así que casi todas las filas de esa columna están a 1-2
  errores entre sí. Las conclusiones de esa columna son LIKELY como mucho. Lo que sí es claro es el partido
  entero.
- **Dos canales tocan más de lo que su nombre dice**: `Intercept` también gobierna el **bloqueo de tiros**
  (defensivo bloquea más) y `Tackle` la **carga sin balón** (`Block`), que es herramienta de ataque: con
  ofensivo el equipo carga peor. Anotado, sin cambiar: separarlo pide canales nuevos.
- **La orden no puede tocar falta, tarjeta ni lesión**: lo rechaza el cargador (sería daño no anunciado,
  regla 11). Indirectamente sí mueve lesiones —una carga ganada puede lesionar— sin medir.
- También afecta al **penalti** (tiro y parada no distinguen). Anotado.
- **Diseño**: es un modificador numérico global que el jugador no ve —contra «comportamiento observable >
  modificadores invisibles»—; lo propuso el revisor y los botones no lo explican. Y ofensivo sigue siendo la
  opción dominante contra una IA siempre neutra (55,8 % frente a 46,9 %).

## Reajuste con la ADR 0155 (27 sep 2026)

Con la reanudación del que la saca, ofensivo dejaba de encajar más que neutro (1,20 / 0,88 frente a
0,96 / 0,91). Castigar más su defensa (−30) sólo lo igualaba (0,911 / 0,912). **Se devuelve la subida
ofensiva de líneas a +3 / +2 / +1**, que es la que da el precio natural de atacar; la razón para moderarla
(que hundía las remontadas) resultó estar dentro del ruido. Resultado, 1.200 partidos:

| orden | a favor | en contra |
|---|---|---|
| Neutro | 0,959 | 0,912 |
| Defensivo | 0,677 (−29 %) | 0,733 (−20 %) |
| Ofensivo | 1,120 (+17 %) | 0,938 (+3 %) |

El precio de ofensivo es pequeño (+3 %): sigue siendo la opción fuerte contra una IA neutra.

