# BI-F — El saque de puerta se lo cabeceaban de vuelta en la bota del portero

Estado: **CERRADA** (24 sep 2026). Reportada por el revisor jugando la build.

## Observación

*«El portero intenta sacar de puerta con balón aéreo pero los jugadores rivales están demasiado cerca y lo
interceptan. Creo que es porque el portero no le da suficiente vuelo al balón.»*

## Lo que la medición dijo, y en qué corrigió la hipótesis

60 partidos con traza. **La hipótesis del revisor sobre el mecanismo era la natural, y era la equivocada —
pero el síntoma que describía era exacto.**

- El portero **sí** levanta el balón: altura máxima tras el saque, mediana **1,33**; sólo el 5 % de los
  saques son rasos del todo. → «no le da vuelo» **REJECTED** como causa.
- Los saques interceptados lo son con el balón a altura **0,00**, mediana y máximo. → no se interceptan en
  vuelo: **se disputan después de caer**.
- Sesenta ticks después del saque el balón estaba a **3,8 casillas** de la portería propia —nuestro propio
  tercio— y suelto en el 85 % de los casos.
- La altura **máxima** del vuelo era **1,07**, cuando la parábola del despeje tiene el pico en **1,40**. Un
  vuelo que no llega a su propio pico es un vuelo **interrumpido**.

Esa última cifra es la que destapó la causa.

## Causa (CONFIRMED)

**El duelo aéreo se disputaba también mientras el balón SUBÍA.** La ADR 0139 §4 dice que el duelo se
resuelve «durante el vuelo, cuando el balón **baja** a la altura de un salto», pero el código sólo
comprobaba la banda de altura. Resultado: el delantero que presiona cabeceaba el saque de vuelta **a medio
metro de la bota del portero**, antes de que el balón llegara a despegar.

Un balón que sube va **a favor** de quien lo golpeó y nadie debería poder quitárselo en el primer metro; uno
que baja es de quien salte.

## Arreglo, en tres piezas

1. **El duelo aéreo, sólo mientras baja.** Es lo que la ADR ya decía; faltaba escribirlo en el código.
2. **En un saque de puerta los rivales salen del área**, que es la regla del fútbol. La barrera genérica de
   dos casillas dejaba al que presiona **dentro** del área esperando el rechace. Reutiliza la misma salida
   por el borde más cercano que el penalti (ADR 0143).
3. ~~**La presión acorta el despeje.**~~ **RETIRADA antes de publicar**, por la revisión independiente, y
   la retirada vale más que la pieza. Eran dos cosas a la vez y las dos mal traídas:

   - **`clear.baseDistanceCells` 6,0 → 9,0** es un **+50 %** a un número cuyo propio `_doc` en
     `data/sim/tuning.json` dice, con esas palabras, *«VALORES DE PARTIDA SIN CALIBRAR: este paquete tiene
     prohibido el tuning contra lotes»*. Justificado con prosa, sin una medición que distinga 9 de 7 o de
     12, y en el mismo trabajo que cita la instrucción del revisor de no calibrar.
   - **`clear.pressurePenaltyCells`** es una **mecánica nueva** y no pasó por `game-design-review`
     (Regla B). Y la defensa que se le escribió —«no es un modificador invisible, es la distinción legible
     entre despejar con tiempo y con alguien en la nuca»— **no era cierta**: no hay evento, ni detalle en
     el despeje, ni nada en la traza que le diga al jugador que ese despeje salió corto por presión. Es
     literalmente un modificador numérico invisible, y el principio del proyecto es el contrario.

   Puede ser la mecánica correcta —despejar apurado **debería** llegar menos lejos—; lo que falta es el
   paso que lo decida. Queda como candidato para `game-design-review`, con la pregunta de diseño ya
   planteada: cómo se hace **observable**.

## Qué queda, y qué quedó sin aislar

Tras la retirada, el arreglo son **dos** piezas: el duelo aéreo sólo en bajada y los rivales fuera del área.

**Y hay que decir que el experimento estaba confundido**: las tres piezas se cambiaron juntas y se midió una
sola cifra. La remedición de abajo (vuelo 1,07 → 1,40) la explica **entera la pieza 1**, porque la altura de
pico es `clear.peakHeightCellsMilli = 1400` y no se tocó. La pieza 2 —los rivales fuera del área— sigue
**sin medición propia** que la aísle: es la regla del fútbol y responde literalmente a lo que el revisor
describió («los jugadores rivales están demasiado cerca»), pero no hay número que diga cuánto aporta.
Anotado, no tapado. Además **teletransporta** (`ClearAreaOfOpponents`), lo que la pone en la lista de
hermanos de [BC-A](./BC-A.md).

## Remedición

| | antes | después |
|---|---|---|
| altura máxima del vuelo tras el saque | 1,07 (pico truncado) | **1,40** (la parábola entera) |

El vuelo **se completa**: el despeje llega a media distancia del campo en vez de morir en la bota.

## Lo que queda abierto

**Quién gana la segunda jugada en el medio campo es otra pregunta**, y no se ha tocado: a los sesenta ticks
el balón sigue suelto la mayoría de las veces y el rival lo recupera tan a menudo como el que sacó. Eso es
la misma familia que BI-D (el balón tiene dueño un tercio del partido) y es calibración, no mecanismo.
