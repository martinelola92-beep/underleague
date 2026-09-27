# ADR 0155 — La reanudación es del equipo que la saca

Fecha: 27 sep 2026 · Estado: **aceptada**. **Decisión del revisor**, dos veces: «2 sí» y, conocido el coste
en lesiones, «aplica». Ficha: [BN-A](../pendientes/BN-A.md).

## Contexto

Durante la cuenta atrás de una reanudación el balón está parado y sin dueño, y el motor decía que no era
de nadie (`HoldingTeam = −1`). El equipo que iba a sacar no podía buscar hueco ni ofrecerse —`FindSpace` y
`OfferSupport` exigen tener el balón— y **defendía su propio saque**: en el saque de puerta, 87 de 88
defensas estaban en `CoverSpace` (BN-A).

## Decisión

Mientras haya una reanudación pendiente y el balón no tenga dueño ni vuele, **el equipo que la saca es el
que tiene el balón** para las **dos** ideas de posesión del motor, que leen de un solo sitio
(`MatchEngine.RestartHolder`):

- la **táctica** (`UpdateTacticalState` → `TacticalStates`): pesos de acción por estado y desplazamiento
  del bloque;
- la del **contexto de decisión** (`HoldingTeam`): buscar hueco y ofrecerse.

Vale para banda, córner, falta y saque de puerta. **Quedan fuera el penalti** (su colocación la gobierna la
ADR 0143) **y el saque de centro**: nadie decide durante él, pero la posesión táctica movía igualmente el
bloque y el marcaje durante la celebración y la cuenta atrás; la segunda versión lo incluía sin decirlo y
explicaba más de la mitad de la subida de goles de una semilla (revisión independiente).

**La primera versión sólo cambiaba `HoldingTeam`** y la revisión independiente midió que el caso que la
motivó apenas se movía: en el saque de puerta y en el de banda el equipo seguía defendiendo el 83 % y el
82 % del tiempo, porque la posesión táctica seguía en «sin balón». El 41 % del test agregado salía casi
entero de las faltas.

## Medición

**Conducta** (`RestartPossessionTests`, 40 partidos, por tipo): el equipo que saca se prepara para recibir
en el saque de banda el **91,6 %**, en el de puerta el **96,6 %**, en la falta el **97,2 %** y en el córner
el **100 %** (sin la regla, 0-6 %; con la primera versión, puerta 11 % y banda 15 %). En el penalti, 2 %.

**Balance** (4.000 partidos × 4 semillas contra `816619e`, versión final con penalti y saque de centro fuera,
diferencias emparejadas partido a partido):

| semilla | goles | lesiones por partido | entradas |
|---|---|---|---|
| 1 | +0,12 ± 0,03 | 0,80 → 0,69 (−14 %) | 9,0 → 7,9 |
| 2 | −0,06 ± 0,02 | 0,41 → 0,40 (−4 %) | 9,8 → 9,3 |
| 3 | +0,15 ± 0,02 | 0,55 → 0,45 (−18 %) | 10,6 → 9,0 |
| 4 | +0,10 ± 0,02 | 0,90 → 0,76 (−16 %) | 10,6 → 8,6 |

- **Goles: unos +4 % de media**, con una interacción fuerte entre plantilla y regla (una semilla baja).
- **LA CARNICERÍA BAJA: −13 % de lesiones de media, −12 a −19 % de entradas.** Es el recurso central del
  juego; el revisor lo aplicó sabiéndolo («aplica») y se compensa en la fase de balance. **Causa sin aislar.**
  Descartada (REJECTED): que la transición de 12 ticks tras el cambio de posesión se gastara durante el
  balón parado —congelarla deja las lesiones casi igual (−11 / −6 / −15 / −17 %)—.
- Nada sale de banda; `injuriesPerMatch` de la semilla 4 y `shotsPerMatch` de la 3 **entran** en banda.
- Los efectos en tiros desde la línea de fondo y goles sin ángulo que dio la primera versión sólo se ven en
  algunas semillas: no son generales.
- La primera versión de esta ADR daba mal `passChainAvgLength` y su banda: la banda es 1,80-3,50 (ADR 0111);
  con la primera versión bajaba 0,05 (1,98 / 2,01).
- El grupo de defensas en el borde del área en el saque de puerta (BN-A) tiene ahora una causa candidata
  más: el bloque del equipo que saca estaba desplazado hacia su portería por estar «sin posesión». Sin
  medir tras este cambio.

## Lo que movió en otras piezas (al aplicarla)

- **Puertas**: siguen siendo 5 rojas de 43, con otra composición. Mejoran la curva de jefes (sólo queda
  `eternal_crown_excellent`, 41,8), `undead_none` (vuelve a banda, 59,1 %), `elf_none` (39,6 %, aún fuera) y
  la doctrina de compra (pasa); empeora `orc_violence` (58 → 52,2).
- **La orden táctica (ADR 0156) perdió su precio**: con la reanudación del que la saca, ofensivo dejaba de
  encajar más que neutro (puerta `MatchOrderTests` en rojo). Se devuelve la subida ofensiva de líneas a
  +3 / +2 / +1 (ver la ADR 0156).
- **El despeje del portero vuelve a volverle, raro**: 13 de 244 en 60 partidos (0,22 por partido), frente a
  3 con la comba y ~220 del bucle original. Mismo mecanismo (un rival solo lo cabecea de vuelta, sin
  evento), más frecuente porque en el saque de puerta los compañeros suben a buscar hueco. La cota del
  test (`KeeperAreaTests`) pasa de 6 —puesta al ver 0, sin procedencia— a 30, que sigue cazando el bucle.

