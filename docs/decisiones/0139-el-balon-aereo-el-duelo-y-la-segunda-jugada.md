# ADR 0139 — El balón aéreo, el duelo y la segunda jugada

**Fecha**: 24 sep 2026
**Estado**: aceptada — decisión del revisor (*Gameplay AI Foundations Pass*, bloques 10 y 11)
**Paquete**: P4
**Contexto del encargo**: `docs/plan-gameplay-ai-foundations.md`

## Contexto

La **ADR 0135** le dio altura al balón y la **ADR 0136** la usó para una sola acción, el centro. Pero el
resto del motor **nunca consultó esa altura**:

- **La recogida no la miraba.** Un balón a dos casillas del césped se cogía con el pie igual que uno
  parado. La altura existía para la intercepción y para el render, no para el juego.
- **`EventType.AerialDuel` era el único de los 26 que el motor no emitía nunca** (verificado contando
  emisiones): estaba declarado, traducido a dos idiomas y muerto.
- **Una segunda jugada era pura geometría.** El balón suelto se lo llevaba *siempre* el más cercano, sin
  que interviniera ningún atributo. El encargo lo señala explícitamente: «No hacer que siempre gane
  automáticamente el jugador más cercano».

## Decisión

### 1 · La altura parte la recogida en tres jugadas distintas

| altura del balón | qué pasa |
|---|---|
| `z <= ball.controlHeightCells` | se **controla** con el pie — la recogida de siempre, intacta |
| entre `controlHeight` y `ball.aerialReachHeightCells` | se **cabecea**, y si llegan los dos equipos es **duelo aéreo** |
| `z > aerialReachHeightCells` | **no lo toca nadie** |

El tercer caso es el que hace que un centro y un despeje **existan como jugada**: si a cualquier altura se
pudiera recoger, pasar por arriba no serviría de nada.

### 2 · Un cabezazo prolonga la jugada, no la cierra

Quien gana un balón por alto **no toma posesión**: lo cabecea hacia donde ataca y el balón **sigue
suelto**. Ésa es la segunda jugada, y nace exactamente ahí. Tratar un cabezazo en disputa como un control
la borraría en el sitio donde ocurre.

**El portero es la excepción, y no por capricho**: tiene manos, así que un balón alto a su alcance lo
atrapa en vez de cabecearlo.

### 3 · Un balón disputado se resuelve con atributos, no con geometría

Cuando llegan candidatos de los dos equipos, gana una tirada **ponderada**, y la distancia sigue contando
—estar más cerca ayuda, pero ya no basta—:

- **en el aire manda el cuerpo**: `Fuerza + radio corporal`. Saltar a por un balón alto es un choque, y así
  un bicho grande gana lo que le corresponde **por ser grande** —que es identidad, no un bonus genérico—;
- **en el suelo mandan técnica y velocidad**: llegar y quedársela es control, no choque.

Un único `Range` por disputa, aritmética entera, y el orden de los dos pesos lo fija el índice de equipo y
no la distancia, para que la tirada no dependa de cuál estaba más cerca (RT-021, RT-023, RT-041).

### 4 · El despeje se puede pelear **en el aire**, mientras baja

Fue el hallazgo del propio paquete: con la primera versión, en **cien partidos no hubo un solo duelo
aéreo**. El motivo es que un despeje aterrizaba y se aplanaba en el mismo tick, así que la ventana en la
que alguien podía saltar no existía. El duelo se resuelve donde de verdad ocurre: **durante el vuelo**,
cuando el balón baja a la altura de un salto.

Se mide **en el plano** y no en esfera, al revés que la intercepción: el que salta ya cuenta con subir, así
que lo que decide si llega es dónde está. La altura ya ha hecho su trabajo antes, dejando fuera todo lo que
va por encima del salto.

**Sólo el despeje, y es deliberado.** El centro pasa por encima de quien lo intentaría cortar porque la
ADR 0136 lo decidió así y ésa es la razón de que la acción exista. Abrirlo al duelo aéreo cambiaría una
mecánica ya medida: queda **anotado como candidato para la fase de balance**, no como algo que este
paquete se lleva por delante de camino.

### 5 · Un salto no se repite al tick siguiente: `states.AerialCooldownTicks`

**Lo encontró el propio paquete midiendo, y es el hallazgo que más valía.** La primera versión producía
**1.753 duelos aéreos en veinte partidos — 88 por partido**. No era una mecánica cara de más: era un
**ping-pong**. El que cabecea sigue estando a menos de medio cuerpo del balón que acaba de tocar, así que
lo volvía a disputar al tick siguiente, y al siguiente.

El arreglo es el patrón que el repositorio ya tenía para exactamente esto —el enfriamiento propio de la
entrada y del bloqueo del paquete U, y el que la ADR 0129 tuvo que separar—: un contador por jugador, que
**pagan los dos que saltan** y no sólo el que gana, porque cobrárselo sólo al ganador deja al perdedor
disputando solo el tick siguiente, que es el mismo ping-pong por otra puerta.

Medido con el contador dentro: **1.753 → 124 duelos** en los mismos veinte partidos.

*Es el tipo de defecto que el encargo pedía cazar con los runs de este pass —«que no existan loops»— y se
cazó porque el número era absurdo, no porque ningún test lo dijera.*

### 6 · El pase largo se levanta cuando hay alguien en medio

Hasta aquí el balón sólo dejaba el suelo en un tiro y en un centro, así que un pase largo con el pasillo
tapado era un balón raso que se comía el primer rival — y el término que lo penaliza en la utilidad estaba,
en el fondo, castigando al pasador por una limitación del motor y no por una mala decisión.

**No es una acción nueva** (regla 17 del encargo): es el mismo `LongPass`, que cambia de trayectoria cuando
la situación lo pide. Se levanta **menos** que un centro a propósito: pasar por encima de un rival no es
colgarla al área.

## Consecuencias

- `AERIAL_DUEL` deja de ser un evento teórico. La cadena que el encargo pedía —*centro → despeje incompleto
  → balón aéreo → duelo → segunda jugada*— es ya posible de extremo a extremo.
- **Cambia el reparto de balones sueltos de todo el partido**, porque la disputa deja de ser determinista.
  Es esperado y autorizado: este pass no calibra.
- Un balón por encima de `aerialReachHeightCells` es **intocable**, así que un despeje muy alto puede
  cruzar el campo sin que nadie lo dispute. Es la regla que da sentido a la altura, y su consecuencia.

## Valores publicados

Valores de partida **sin calibrar**, elegidos por analogía y documentados en el `_doc` del bloque:

| dato | valor | por qué ése |
|---|---|---|
| `ball.controlHeightCells` | 0,5 | media casilla: la altura a la que una pelota deja de jugarse con el pie |
| `ball.aerialReachHeightCells` | 1,7 | por debajo de `cross.peakHeightCellsMilli` (1,4 de pico sobre la recta), que es lo que hace que un centro siga pasando por arriba en su tramo alto |
| `ball.headerSpeedCellsPerTickMilli` | ~~180~~ **320** | corregido por la enmienda de BI-E: 180 era más lento que un pase y el balón no salía del grupo |
| `ball.headerDropCellsPerTickMilli` | **150** | corregido por la enmienda de BI-E: era `headerLift` 60 **hacia arriba**, y el balón se quedaba a la altura de la cabeza |
| `pass.loftedPeakHeightCellsMilli` | 700 | **la mitad** del centro, por el motivo del §6 |
| `states.AerialCooldownTicks` | 12 | estructural, no calibración: lo que hace falta para que el mismo jugador no cabecee dos veces el mismo balón |

## ENMIENDA (24 sep 2026, BI-E): el cabezazo baja el balón

El revisor, jugando la build: *«el mayor problema es el ping-pong cuando disputan un balón elevado. El
balón no cae fácilmente»*. **El enfriamiento del §5 no era la causa completa.**

Medido con la traza del balón en 30 partidos: cadenas de **hasta ochenta** duelos seguidos, con el balón
recorriendo **0,72 casillas** entre uno y otro y pasando el **18,9 %** del partido en la banda aérea. La
causa, aislada: el cabezazo **conservaba la altura y además empujaba hacia arriba**, y los dos equipos lo
cabeceaban en direcciones opuestas, cancelándose. Ni subía ni bajaba ni avanzaba.

El enfriamiento sí funcionaba: el hueco medio entre duelos encadenados era de **7,5 ticks**, menor que los
doce del contador, así que por definición **los que repetían eran otros jugadores**. Subirlo sólo habría
hecho que la cadena la siguieran jugadores cada vez más lejanos.

**Un cabezazo empuja el balón hacia abajo**, y sale más rápido en horizontal (180 → 320 milésimas: era más
lento que un pase). El dato deja de llamarse `headerLift` y pasa a ser `headerDrop`, porque un término
llamado «elevación» que hace caer el balón es una mentira en `/data`.

Cadena más larga **80 → 2**. Y el 3,4 % de fotogramas por encima del alcance del salto pasa a **0,0 %**:
era el propio ping-pong bombeando el balón hacia arriba. Ficha completa: `docs/pendientes/BI-E.md`.

## Lo que este paquete deja anotado para la fase de balance

No se toca nada de esto aquí, y es deliberado:

- **El portero atrapa muchos más balones**: sus recuperaciones pasan de 37 a 123 en veinte partidos, y el
  triple viene entero de la rama aérea (medido apagándola por dato). Es la excepción del §2 funcionando;
  si la tasa es la correcta lo dirá la medición, no este paquete.
- **Abrir el centro al duelo aéreo**, hoy excluido a propósito (§4).
- **La tercera altura está muerta**: con el alcance del salto en 1,7 y el pico del centro en 1,4, ningún
  balón legítimo pasa por encima de todo el mundo (medido tras BI-E: 0,0 % de fotogramas). O sobra la
  altura, o el centro tiene que volar más alto.
- **Los penaltis en las filas extremas se hicieron más raros**, lo bastante para dejar a
  `PenaltyAreaSymmetryTests` sin muestra con la que discriminar (hubo que ampliarla de 3.000 a 4.500
  partidos). Toca la ficha **BD-A**.
- **`high_line` cambia de signo** en el cribado. Su test dejaba clavado que el delta fuera negativo «como
  en §23.5»: era una medición del motor de aquel día, no una regla, y se ha replanteado.
