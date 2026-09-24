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
3. **La presión acorta el despeje.** Un despeje libre y uno angustiado llegaban exactamente igual de lejos.
   Ahora el alcance base sube y la presión sobre el que despeja lo recorta: un saque de puerta se golpea
   **solo** y con el balón parado, así que llega lo que debe; un central con un delantero en la nuca la
   manda mucho menos lejos.

## Remedición

| | antes | después |
|---|---|---|
| altura máxima del vuelo tras el saque | 1,07 (pico truncado) | **1,40** (la parábola entera) |

El vuelo **se completa**: el despeje llega a media distancia del campo en vez de morir en la bota.

## Lo que queda abierto

**Quién gana la segunda jugada en el medio campo es otra pregunta**, y no se ha tocado: a los sesenta ticks
el balón sigue suelto la mayoría de las veces y el rival lo recupera tan a menudo como el que sacó. Eso es
la misma familia que BI-D (el balón tiene dueño un tercio del partido) y es calibración, no mecanismo.
