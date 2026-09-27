# ADR 0158 — El árbitro existe: nombre, rasgo, memoria y criterio que pesa

Fecha: 27 sep 2026 · Estado: **aceptada**. **Decisión del revisor**: *«árbitro con memoria, pero no se ve el
árbitro ni se le da la importancia suficiente»* y *«ataca árbitro primero»* (`docs/plan-diversion.md`).
**Implementa** RF-061, RF-061b, RF-062, RF-063 y la mitad de RF-119 que faltaba (faltas no señaladas).
**Cierra** D-22 (el motor ignoraba el rasgo) y el hueco `ui.scout.refereeGap`.
**Enmienda** la lectura de «casero» de RF-061 (abajo). **No incluye** el soborno (RF-064b..e): ADR aparte.
**Requisitos:** RF-012d, RF-055b, RF-061, RF-061b, RF-062, RF-063, RF-064, RF-119, RT-014, RT-021, RT-022,
RT-030, RT-031, RT-057, RT-096

## Lo que había (Regla G)

- **Criterio**: existe en el motor (`MatchEngine._bias`, −100..100, positivo favorece al local, que en la
  run es siempre el jugador, W-15). Mueve la falta, la tarjeta y el penalti (`BiasRollShift`).
- **Pero no pesa.** Cada acción sucia lo desplaza **1-2 puntos** (`tuning.referee.biasShift*`), así que en un
  partido apenas sale de 0. RF-064 dice que con ±60 el árbitro es decisivo; nunca se llega.
- **Árbitros de la run**: `RunState.Referees` guarda 6-8 (`RunReferee`, RF-061b) con rasgo y sobornos
  recibidos, pero `DefaultRunSystems.CreateReferees` los crea **todos neutros** y con nombre
  `referee_<i>`. El motor **ignora** `RefereeSetup.Trait` (D-22).
- **Falta no señalada**: el motor ya la emite (`Foul` con detalle `unseen`, ADR 0090) y mueve el criterio;
  sólo el informe no la cuenta.
- **Turba**: el árbitro ya se va (ADR 0145, `RefereeLeaves`).

## Decisión

### 1. Árbitros con nombre, de datos

`data/referees/referees.json` (esquema propio, RT-031/RT-032): un **plantel de 12-16 árbitros**, cada uno
con `id`, nombre localizado, rasgo, una **muletilla** localizada (tono de humor, la dice el pregón) y, si es
tuerto, el **lado** que no ve (`blindSide`: `top` o `bottom`). La run toma 6-8 al empezar (RF-061b) con un
flujo derivado de la semilla de run, **no** del de partido (RT-022). Qué árbitro pita cada nodo sigue siendo
derivable del nodo, como hoy.

### 2. Los rasgos cambian el partido (tabla en `data/sim/tuning.json`, RT-096)

Cifras **provisionales, sin medir** (Regla H). Se miden en el lote de esta ADR y se ajustan en la fase de
balance, no aquí.

| rasgo | efecto |
|---|---|
| neutro | el de hoy |
| estricto | pita el 95 % de las faltas; tarjetas +50 %; mueve el criterio ×1,5 |
| permisivo | pita el 55 %; tarjetas −50 %; mueve el criterio ×0,5 |
| casero | **favorece al rival**: arranca en −20 y mueve el criterio contra ti ×1,5 (abajo) |
| tuerto | no ve **ninguna** falta en su lado ciego (media banda, anunciada) |
| cobarde | nunca saca roja (ni por doble amarilla) |
| corrupto | como neutro en el campo; abarata el soborno (ADR del soborno) |
| incorruptible | como neutro en el campo; no admite soborno (ADR del soborno) |

**Enmienda de «casero».** RF-061 dice «favorece al equipo local». En la run el jugador es **siempre** el
local (W-15), así que leído al pie de la letra sería un árbitro que ayuda al jugador siempre. Se lee como
lo que el jugador vive: en la liga juegas en campo ajeno, y el casero es **el árbitro de la casa del
rival**.

### 3. El criterio pesa

Los desplazamientos de RF-063 se escalan a la magnitud que describía el diseño de fase 1b (§1.4: 10/15/20):
falta vista 5, no vista 3, dura +3, sin balón +2, lesión +5, amarilla +3, roja +8. **Provisional, sin
medir.** El objetivo verificable es de sensación, no de décima: que **un partido sucio lleve el criterio
más allá de ±30** y que se note en faltas y tarjetas señaladas.

### 4. Memoria: el árbitro se acuerda de ti

`RunReferee` gana **`Grudge`** (−40..40, positivo a tu favor): al terminar un partido que pitó, pasa a ser
**la mitad del criterio final**, acotado. El siguiente partido con ese árbitro **empieza** con ese criterio
(más el −20 del casero). Así la memoria es **legible y anticipable**: el ojeo dice «Bartolo se acuerda de ti:
empieza a −15». Es también lo que da sentido a portarse bien en un partido contra el árbitro con el que vas
a jugar el jefe. **Sube el esquema de guardado 5 → 6** (RT-030; cargar otra versión sigue siendo error
explícito).

### 5. Informe: faltas no señaladas

`RefereeReport` gana las faltas **no señaladas** por equipo, contadas en la capa de vista desde la secuencia
de eventos (`Foul`/`unseen`). Sin cambio en el motor.

### 6. `/Game`: que se vea

- **Ojeo** (RF-012b, RF-061): ficha del árbitro con nombre, rasgo, lo que hace el rasgo (una línea compuesta
  desde la tabla, RT-035), su muletilla y la línea de memoria. Se retira `ui.scout.refereeGap`.
- **Mapa** (RF-061): el nodo de partido dice qué árbitro pita.
- **Partido** (RF-062, RF-063): el **criterio siempre visible** en el tablero y un **texto flotante** con cada
  desplazamiento. Un **árbitro en el campo**: avatar de presentación que sigue la jugada a distancia, levanta
  el brazo cuando pita y **mira hacia otro lado** en la falta no señalada, y sale corriendo al empezar la
  turba. Es presentación pura: su posición no existe en el motor, no decide nada y sólo reacciona a eventos
  (RT-014).
- **Informe**: faltas no señaladas y se retira `ui.report.refereeGap`.

## Las diez preguntas (`game-design-review`)

1. **Qué experimenta el jugador.** Un personaje con nombre que ve venir en el ojeo, que se enfada con él
   durante el partido y que se acuerda después.
2. **Qué decide.** Qué partido elegir en el mapa según quién pita; alinear a sus Brutos o no contra un
   estricto; colocar la violencia en el lado ciego del tuerto; portarse bien antes de volver a encontrarse
   con un árbitro.
3. **Qué debería decidir.** Lo mismo. El riesgo es que el rasgo sea invisible en el efecto (se mide).
4. **Regla.** RF-061..063, con la enmienda de «casero».
5. **Sistemas.** `/Sim`: `RunReferee` y la creación de árbitros (`Run/Systems/Referees/`, catálogo cargado
   con los demás sistemas de run), `RefereeFor` (criterio inicial), `MatchResolution` (memoria),
   `MatchEngine` (rasgos: tasa de pitido, tarjetas, roja, lado ciego, multiplicador de desplazamiento),
   `PostMatchView`. `/data`: `referees/`, `sim/tuning.json`. `/Game`: ojeo, mapa, tablero del partido, vista
   3D, informe.
6. **Alternativas.** (a) Sólo presentación, sin rasgos: el árbitro se ve pero no importa, que es justo la
   queja. (b) Rasgos como perks de equipo: esconde al árbitro detrás de un sistema ajeno.
7. **Trade-off.** Un árbitro con peso añade varianza a los partidos; se compensa con que **todo se anuncia**
   en el ojeo y el mapa (RF-012d).
8. **Estrategias.** Da contrapeso real a las builds de violencia (RF-064e) y un motivo para las mitigaciones
   (RF-064f) que hoy no tienen enemigo.
9. **Degeneración.** Un árbitro permisivo más una build de violencia puede disparar las lesiones; un
   estricto puede vaciar el campo de rojas. Se vigilan `injuriesPerMatch`, `redCardsPerMatch` y la tasa de
   victoria de las builds de violencia. El cobarde elimina la roja y quita un freno: se mide.
10. **Cómo se demuestra.** Tests de cada rasgo con partidos construidos (misma semilla, rasgo distinto,
    comparación de faltas pitadas o rojas); test de memoria en la run; test de carga y validación de
    `data/referees/`; determinismo (RT-024); lote de campaña con las puertas antes y después; capturas del
    ojeo y del partido.
