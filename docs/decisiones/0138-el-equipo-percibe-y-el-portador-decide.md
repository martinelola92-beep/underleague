# ADR 0138 — El equipo percibe, y el portador tiene respuestas

**Fecha**: 24 sep 2026
**Estado**: aceptada — decisión del revisor (*Gameplay AI Foundations Pass*)
**Paquetes**: P1 (percepción compartida) y P2/P4a (proteger y despejar)
**Contexto del encargo**: `docs/plan-gameplay-ai-foundations.md`

## Contexto

El encargo del revisor abre una fase cuyo objetivo **no es balancear** sino *completar las piezas que
faltan para que once bichos se comporten como un equipo de fútbol 7*. Esta ADR cubre las dos primeras.

Dos hechos medidos justifican empezar por aquí:

1. **Los jugadores no comparten nada.** `FindSpace`, `OfferSupport` y `Block` son individuales y sin
   comunicación; el pase en profundidad es un monólogo —el pasador lee el destino que el receptor ya había
   elegido y el receptor no se entera—; y `CoverSpace` es la única acción de colocación que no mira a los
   compañeros, así que dos del mismo equipo pelean la misma casilla (BB-K).
2. **El portador es pasivo ante la entrada.** En `TackleWinChance` sólo entra su `Technique`; no existe
   ninguna forma de resistir que no sea el canal pasivo de perk `TackleEvasion`. Si pierde, `KnockedDown`
   automático. Sus únicas salidas eran deshacerse del balón o que se lo quitaran.

## Decisión

### 1 · La percepción compartida se añade a `UtilityContext`, que ya era esa capa

`UtilityContext` se documenta a sí mismo como «la vista del mundo que necesita la IA, rellenada una vez por
tick y reutilizada en todas las decisiones de ese tick» (RT-051), y ya cachea el estado táctico, el
perseguidor designado, el equipo que sostiene el balón y el balón muerto. **El patrón del repositorio para
esto ya existía**, así que se extiende en vez de crear un sistema paralelo.

Se añaden, calculados en `UpdatePerception()` una vez por tick y antes de que nadie decida:

- `Carrier[team]` — quién lleva el balón de cada equipo.
- `Pressure[i]` / `PressureCount[i]` — cuánto y cuántos aprietan a cada jugador, 0-100.
- `Marked[i]` — si alguien le marca. `Marking` ya calculaba el emparejamiento; **nadie leía la dirección
  contraria**, que es justo la que necesita el que va a recibir.
- `Openness[i]` — cuán libre está para recibir.
- `AttackingDepth[i]` — si ataca el espacio. Lo escribirá el arranque coordinado (P3).
- `Danger[team]` — amenaza sobre la portería propia, 0-100.

**Justificación contra el protocolo de arquitectura** («¿elimina complejidad o la mueve?»): la elimina.
Hoy cada evaluador redescubre por candidato, por jugador y por tick lo mismo. Cachearlo quita trabajo
cuadrático repetido.

**No se inventa ningún radio nuevo**: la presión usa `PitchConstants.PressureRadius`, que es ya la
definición de «me aprietan» del motor, y el peligro usa el alcance de tiro de `/data` —el balón amenaza mi
portería cuando está a distancia de disparo de ella—.

**Verificado inerte**: con la percepción calculada y todavía sin lectores, 500 partidos salen **byte a byte
idénticos** a HEAD (`matches.csv` y `summary.csv`). Era la propiedad que había que comprobar antes de
apoyar nada encima.

### 2 · Dos acciones nuevas, y sólo dos: `Shield` y `Clear`

La regla 17 del encargo prohíbe resolver cada problema con una acción nueva. El criterio que se adopta y
que se aplicará al resto del pass: **una acción nueva sólo si su consecuencia sobre el balón es propia.**

- **`Shield` (proteger)** — el portador conserva el balón **sin avanzar**, se interpone, y **resiste con su
  fuerza en vez de con su técnica**. Consecuencia propia: renuncia a avanzar a cambio de no perderlo.
- **`Clear` (despejar)** — el balón sale **alto, largo y sin destinatario**, y al caer **no es de nadie**.
  Consecuencia propia: es la renuncia deliberada a la posesión, y produce el balón en disputa que es la
  entrada del duelo aéreo y de la segunda jugada.

Todo lo demás del encargo —presión según marcador, portero que sale, centrocampista todocampista,
represalia— se resolverá con contexto, percepción y utilidad, **sin acciones nuevas**.

### 3 · Proteger es un compromiso con duración, como conducir

`Shielding` es un estado de decisión **con contador**, hermano exacto de `Dribbling` desde la ADR 0137. Es
deliberado y por el motivo que aquella ADR midió: un estado de decisión **sin** contador se reevalúa cada
`decisionIntervalTicks` y la conducta no llega a existir —es lo que le pasaba al regate, que ocurría 0,85
veces por partido—. Y, como allí, **proteger exige llevar el balón**: si te lo quitan, el estado se corta.

### 4 · Precondiciones duras que describen la situación, no penalizaciones que la disfracen

- Proteger se **descarta** sin presión: proteger un balón que nadie te disputa no significa nada.
- Despejar se **descarta** sin peligro: despejar desde el círculo central no es prudencia, es regalar el
  balón.

Es la forma que el repositorio ya usa (el centro exige compañero en la zona de remate; la entrada exige
rival al alcance), y evita el patrón que la auditoría de IA diagnosticó como «función acantilado»: una
acción que está fuera casi siempre por una penalización enorme y, cuando entra, arrasa.

### 5 · La fuerza decide *cuánto*, no *si*

La decisión de despejar **no mira la fuerza**; la distancia del despeje **sí**. Es el reparto invertido que
la ADR 0136 le dio al remate de centro, y por el mismo motivo: golpear no es colocar.

### 6 · El arranque coordinado reutiliza la ventana de armado del pase, sin sistema nuevo

El pase en profundidad era un monólogo: el pasador elegía una casilla por delante de un compañero leyendo
el destino que **ese compañero ya había elegido por su cuenta**, y el compañero no se enteraba de nada.

La corrección **no necesita un bus de mensajes ni una cola de intenciones**, porque el motor ya tenía el
canal: entre que el portador decide pasar y que el balón sale pasan `states.PassingTicks` ticks. Esa
ventana, que hasta ahora no hacía nada, es el arranque coordinado:

1. el pasador **publica una oferta** (`PassIntent`: quién, a quién, a qué casilla, hasta qué tick);
2. durante el armado el receptor la ve en su propia evaluación de `FindSpace` y **puede** salir hacia ese
   espacio; mientras tanto cuenta como `AttackingDepth` para el resto del equipo;
3. cuando el pase sale, `Utility.PassTarget` —que ya adelantaba el balón a donde el receptor iba— apunta a
   donde los dos han acordado.

**Es una oferta, no una orden**: el bono entra como un sumando más en la misma comparación, así que un
desmarque claramente mejor sigue ganando. Por eso vive en el contexto compartido y no como un campo
impuesto en el receptor.

**Sólo los balones al espacio publican oferta** —pase en profundidad y centro—. Un pase al pie no le pide
nada al receptor: `PassTarget` ya lleva el balón a donde él va. Publicar una oferta para eso sería
maquinaria sin consecuencia.

La oferta **caduca sola**, al acabar el armado más un margen de gracia: el que ataca el espacio no deja de
correr en el instante del golpeo. Y se cae si el receptor sale del campo.

## Consecuencias

- **El flujo de RNG se desplaza** en cuanto alguien despeja (la dispersión lateral consume una tirada), así
  que todos los partidos cambian. **Es esperado y está autorizado**: este pass tiene prohibido el tuning y
  las 43 puertas no son su criterio. La calibración es la fase siguiente.
- Los tests de este paquete afirman **reglas y alcanzabilidad**, nunca huellas de RNG ni frecuencias. Un
  test del tipo «se protege N veces por partido» fijaría como diseño una distribución que aún no se ha
  decidido.
- `EventType.Clearance` es nuevo. Tiene evento propio y no un detalle de `PassFailed` porque para el
  jugador es una jugada reconocible —«la sacó de la línea»— y no un pase que salió mal.
- Quedan abiertos, y son de paquetes posteriores: que el balón suelto de un despeje se dispute **con
  atributos** y no por cercanía (P4), y que el portero deje de atrapar siempre (P5).

## Valores publicados

Todos son **valores de partida sin calibrar**, elegidos por analogía con términos hermanos y documentados
como tales en el `_doc` de cada bloque. Ninguno se ha movido contra un lote, que es exactamente lo que este
pass prohíbe.

| dato | valor | por qué ése |
|---|---|---|
| `states.ShieldingTicks` | 12 | el mismo compromiso que `dribble.driveTicks` |
| `tackle.shieldResistance` | 1000 | ~un cuarto de `baseWin`, el orden del efecto de 13 puntos de atributo |
| `clear.peakHeightCellsMilli` | 1400 | **el del centro**: la altura que el motor ya sabe que salva a un defensa (ADR 0136) |
| `clear.baseDistanceCells` | 6,0 | algo más de un tercio del campo |
| `clear.strengthDistanceMilliPerPoint` | 40 | ~2 casillas de diferencia entre el más fuerte y el más débil |
