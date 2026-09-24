# ADR 0144 — El censo de utilidad, y la descarga

**Fecha**: 24 sep 2026
**Estado**: aceptada — decisión del revisor (*Gameplay AI Foundations Pass*, bloque 3)
**Paquete**: P10
**Contexto del encargo**: `docs/plan-gameplay-ai-foundations.md`

## Contexto

El encargo prohíbe expresamente resucitar una acción muerta subiéndole el peso, y obliga a determinar, para
cada una: qué situación futbolística representa, qué necesita, qué gate la impide, **si ese gate es
conceptualmente correcto**, y si tiene una utilidad competitiva real.

Eso exige separar dos cosas que piden arreglos **opuestos** —lo dejó escrito la primera auditoría de IA—:

- una acción que se **descarta** por precondición no la despierta ningún peso;
- una que **compite y pierde** no la arregla tocarle la precondición.

Y esa separación no se podía hacer: las nueve auditorías la midieron con scripts de usar y tirar, y su
propia conclusión fue que las métricas *«nunca se instalaron como permanentes»*.

## Decisión

### 1 · El censo de utilidad es un instrumento del motor, no un script

`UtilityCensus` cuenta, por acción y por puesto: cuántas veces se **eligió**, cuántas se **descartó**,
cuántas **compitió y perdió**, y **lo lejos que se quedó** del ganador cuando perdió.

Es **contabilidad pura**: no consume aleatoriedad, no decide nada, está apagado por defecto
(`SimConfig.Census` es `null`) y está fijado por test que un partido con censo es idéntico a uno sin él.
Arrays por índice, ningún diccionario (RT-041).

### 2 · Diagnóstico medido, y dos de las tres acciones «muertas» tenían el gate bien

Censo sobre 60 partidos, antes de tocar nada:

| acción | elegida (por mil decisiones) | descartada | compitió y perdió | hueco medio |
|---|---|---|---|---|
| `OfferSupport` | **0,01** | 70,3 % | 29,7 % | **803** |
| `PressCarrier` | 0,56 | **86,3 %** | 13,7 % | 507 |
| `Block` | 0,49 | **70,0 %** | 30,0 % | 687 |

- **`PressCarrier`: el gate es correcto y se queda.** Se descarta porque **casi nunca hay poseedor**: el
  balón tiene dueño alrededor de un tercio del partido (BI-D), así que dos tercios de sus descartes son la
  realidad del juego. No se puede presionar a quien no lleva el balón. *Dejarla como está es una
  conclusión, no una omisión.*
- **`Block`: el gate es correcto y se queda.** Cargar a un rival sin balón exige estar en la jugada activa
  y a su alcance (RF-057). Que eso no se cumpla dos de cada tres veces describe el juego, no un defecto.
  Lo que le pasa cuando sí compite —pierde por 687— es calibración, y la calibración es la fase siguiente.
- **`OfferSupport`: muerta de verdad, y por un motivo concreto.** No estaba mal calibrada: **no
  representaba ninguna situación** que el desmarque no cubriera mejor.

### 3 · `OfferSupport` pasa a ser **la descarga**

La ADR 0022 creó `FindSpace` explícitamente «para sustituir el punto fijo de `OfferSupport`» y nadie retiró
el punto fijo. Se quedó yendo a un sitio calculado sin mirar nada, compitiendo contra dieciséis candidatos
evaluados, y perdiendo siempre.

Ahora representa una situación propia, y es justo la que le faltaba al motor desde que el portador puede
verse atrapado (ADR 0141): **venir corto a dar salida a un compañero al que están apretando**, aunque eso
signifique ir hacia atrás.

No se pisa con el desmarque, y es deliberado: **`FindSpace` busca alejarse de los rivales y avanzar; la
descarga es lo contrario.** Precondición dura —sin compañero apretado no hay descarga que dar—, con la
misma forma que proteger y despejar (ADR 0138): describir la situación en vez de disfrazarla con una
penalización enorme.

**Medido después**: de **0,01 a 18,63** elecciones por mil decisiones, con un 94 % de descartes. Eso es una
acción **situacional**, que es lo que debe ser: aparece cuando su situación aparece.

## Dos tests que dejaron de detectar nada **por haber acertado**

Los dos comprobaban una regla a través de cientos de partidos simulados, y el propio éxito del pass les
quitó el caso que buscaban. Los dos se replantean sobre la regla, que es lo que su nombre dice:

- **`TheApertureFloorBoundsTheAimError`** comparaba goles totales de 240 partidos con el suelo de apertura
  en dos valores. Dejó de detectar diferencia en cuanto los disparos sin ángulo se hicieron raros — que es
  **exactamente** lo que la ADR 0136 (el centro) venía a conseguir. Que el suelo acote una asíntota es
  aritmética, no estadística: ahora se afirma sobre `AimApertureFactor`.
- **`TheClearanceReleasesExactlyAtTheDurationCapAndNotBefore`** buscaba en partidos reales un sacador que
  retuviera el balón más de treinta ticks, y no lo encontró ni en seiscientos: desde que el sacador decide
  dentro de su reanudación (ADR 0143) y puede dársela a un compañero presionado (ADR 0141), encuentra
  destinatario en unos pocos ticks. La red de seguridad de RF-052 **sigue haciendo falta** —un sacador
  atascado no puede congelar la barrera— pero perseguirla por semillas era buscar un caso que el motor ya
  no produce. Ahora se afirma sobre `ShouldReleaseClearance`.

Es el mismo patrón que este pass ha encontrado media docena de veces: **un test que mide una regla a través
de una estadística se rompe cuando la estadística se mueve**, tenga o no tenga razón.

## Consecuencias

- El censo queda disponible para la **fase de balance**, que es donde tiene que gastarse: es la fotografía
  por acción, por puesto y por motivo de descarte que el encargo pide en su bloque 22.
- La descarga cambia la circulación cerca del portador presionado, así que moverá posesión y cadenas de
  pase. Es el efecto buscado y se medirá luego.
- **No se ha tocado ningún peso base** para resucitar nada. Lo único que cambió de `OfferSupport` es *qué
  significa*.
