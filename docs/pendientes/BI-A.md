# BI-A — Lo que la revisión independiente dejó abierto en el audio

Estado: **abierta** (23 sep 2026). Cuatro defectos de la revisión **ya arreglados** en el mismo paquete
(ver abajo, «Cerrado»); lo que queda aquí son los que cuestan más que su beneficio inmediato, más una
medición que falta y que sostiene una afirmación que hoy está escrita sin respaldo.

Origen: revisión independiente del primer paquete de audio (`Game/audio`, `AudioManager`, `MomentSounds`,
`MatchEventSounds`, `ScreenAudio`). La revisión midió, no leyó: encontró el defecto de la bolsa
comparando identificadores de instancia de los recursos cargados.

## Cerrado en el propio paquete

1. **Cada variante entraba dos veces en su bolsa** [CONFIRMED, arreglado]. `DirAccess.GetFiles()` devuelve
   `X.wav` **y** `X.wav.import`, y la normalización cargaba el mismo recurso por las dos entradas. Rompía
   exactamente la propiedad que el gestor promete (dos iguales seguidas dejaban de ser imposibles) y dejaba
   muerto el atajo de los pools de un solo fichero. Arreglado con una criba de nombres; el volcado de
   arranque pasa a imprimir **cuántos sonidos**, no solo cuántos pools, que era lo que hacía el fallo
   invisible desde la consola.
2. **El hueso crujía dos veces en cada decisión de sustitución** [CONFIRMED, arreglado]. El director se
   resincroniza en `decisionFrame + 1` y el audio lo hacía en `decisionFrame`, con el consumo en `<=`: el
   suceso de la propia lesión —que ya había sonado antes de abrirse la bandeja— quedaba pendiente y volvía.
3. **Lesión y muerte del mismo jugador comparten tick** y las dos piden `combat/crush`: dos crujidos
   solapados para un hueso [CONFIRMED, arreglado]. Regla nueva: el mismo pool no suena dos veces en el
   mismo fotograma.
4. **La grada celebraba el gol del rival** [CONFIRMED, arreglado]. `MomentSounds` no miraba `moment.Team`.
   Ahora el balón suena igual y la grada cambia de bando. Y el pitido final pierde el `crowd/cheer`, que
   **aplaudía una derrota**: la reacción correcta necesita el resultado, que ese nivel no conoce.

## Abierto

### 1. El hermano del desfase sigue en la cámara

`ResyncShotGestures` tiene el mismo off-by-one que tenía el audio (`< frame` contra un consumo en `<=`):
un tiro en el fotograma de una decisión repite el acercamiento. **No se ha tocado a propósito**: mueve la
cámara, así que quiere su propia comprobación visual y no entra de rebote en un paquete de audio. Es menos
grave que el del audio porque la coincidencia es rara —en el audio era sistemática, la decisión *es* el
fotograma de la lesión—.

### 2. Nadie ha medido un partido entero a ×1

La única traza que existe viene del **recorrido de capturas**, que va a saltos de `SeekTo`. Con eso **no se
puede sostener** que ocho reproductores basten: el caso que estresa el pool —pase, entrada, grito y grada
encadenados a 15 ticks/s— no se ha ejecutado nunca. La afirmación «~1 sonido por segundo» que está escrita
en `AudioManager` es una **estimación** a partir de `summary.csv` (pases ≈ `possessionChanges` ×
`passChainAvgLength`, más tiros, entradas y faltas, sobre los ~99 s de partido a ×1 de `docs/ui/README`
§6), no una medición: [LIKELY], no [CONFIRMED]. Falta una traza de un partido completo y otra que cruce
una decisión de sustitución.

### 3. Un momento se puede descartar, y ahora eso silencia cosas

Una voz que pausa **vacía la cola** del director y una voz en cola caduca a 1,5 s. `Mob`,
`RefereeLeaves`, `Red` y la lesión grave *del rival* son nivel ≥3 **sin pausa**, así que pueden
desaparecer. Antes eso significaba «no se ve el estandarte»; ahora significa **la turba entra al campo en
silencio** y **el árbitro se va sin bronca**. Consecuencia de segundo orden de haber colgado una capa del
director: la de campo sí suena siempre, así que una lesión grave del rival puede **crujir sin gritar**.

### 4. No hay forma de bajar el volumen

`SetBusVolumeLinear` no lo llama nadie: no hay pantalla de ajustes. El estado actual es **música
obligatoria**, y para un premium de Steam es de lo primero que se reporta. Los tres buses existen para eso.

### 5. Deuda menor, anotada para no redescubrirla

- **`"severe"` como literal en `/Game`** (`MatchEventSounds`): tercera copia del mismo texto fuera de
  `/Sim`. Si `/Sim` cambia el detalle, el hueso deja de crujir **en silencio y sin test**.
- **`SeekTo` repite los sonidos de momento** (anula `_lastStampMoment`/`_lastVoiceMoment` y vuelve a
  presentar). Hoy solo lo usa el arnés de capturas; deja de ser inocuo el día que haya rebobinado.
- **Sin `godot --import` en una clonación limpia no hay audio**, y el síntoma parece un fallo de código.
  Documentado en `Game/audio/README.md`, pero **nada en CI lo hace**.
- **`_pools` es `OrdinalIgnoreCase`**: dos carpetas que difieran solo en mayúsculas colisionan en Linux y
  la segunda sobrescribe a la primera sin error.
- **`sfx/death/` mezcla taxonomías**: las demás carpetas dicen *quién* produce el sonido, esa dice *qué
  significa*. Y `Death_Horn.mp3` y el `referee/horn` vacío son probablemente el mismo cuerno.
- **`PoolsFor` devuelve el array estático compartido** y `EventSound` guarda la referencia. Nadie muta
  nada, pero el tipo no lo impide; `IReadOnlyList<string>` cuesta lo mismo.
- **`AudioManager.Instance` no se limpia** (sin `_ExitTree`), como los demás autoloads del proyecto.

### 6. Una regla de dirección se escribió sin ADR

La regla «la capa de campo solo suena a ×1, la de retransmisión a cualquier velocidad» se añadió a
`docs/ui/README` §6, que el repositorio marca como **dirección acordada con el revisor**. Es una decisión
de presentación nueva —la tabla original hablaba de gestos de cámara, no de audio— y el encargo era «haz
un primer uso», no «fija la doctrina de audio». **Si el revisor la valida, quiere ADR; si no, se quita.**
