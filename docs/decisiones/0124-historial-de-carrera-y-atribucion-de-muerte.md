# 0124 — El historial de carrera es un registro tipado, y la muerte se atribuye

Estado: **Aceptada** (22 sep 2026). Frontera y abstracción nueva, así que pasa por `architecture-review`
(esta nota). Implementa la **Fase 1A** de la ADR 0122 y la **F1** de `docs/plan-evolucion-knavall.md`.

## Problema

El motor calcula por partido y por jugador `MatchReport.PlayerMatchStats` —goles, asistencias, tiros,
pases, entradas, entradas ganadas, faltas, tarjetas, minutos— y **`RunPlayer` no persiste ninguna**
*(LEÍDO)*. Sin hechos acumulados no hay personaje, y RF-122 (obituario con estadísticas) es hoy imposible
aunque se implemente, porque no habría nada que poner.

Dos preguntas de arquitectura, y ninguna es obvia.

---

## Decisión 1 — El historial va en un **registro tipado**, no en `Counters`

### El patrón ya existente, que es lo primero que hay que mirar

El repositorio tiene **tres** convenciones para estado acumulado, no una:

| convención | dónde | vocabulario | gobernado por |
|---|---|---|---|
| **(a)** `RunState.Counters` | nivel de run | **claves libres** (`itemStock:`, `clinicRolls:`) | nada; el esquema las bendice explícitamente *«para no subir de versión cada vez que entra un sistema»* |
| **(b)** `RunPlayer.Counters` | por jugador | **ids de perk** | RF-070: acumulación de perk, con tope `maxValue` |
| **(c)** propiedades `init` tipadas de `RunPlayer` | por jugador | cerrado | cada una cita su RF |

La pregunta real no es «¿diccionario o registro?», sino **«¿el historial se parece a (b) o a (c)?»**.

### Decisión: (c), un registro tipado `RunCareer`

**Cuatro razones, en orden de peso:**

1. **Los ciclos de vida son incompatibles, y esto es lo decisivo.** `RunPlayer.Counters` está gobernado por
   la semántica de perk: topes `maxValue` y ámbitos de límite. **El historial no debe topar ni reiniciarse
   nunca.** Meterlo en la misma bolsa significa que una regla futura de perk puede truncar el historial en
   silencio — y el proyecto **ya tiene un fallo exactamente de esa forma** (`LimitScope.Run` no se reinicia
   nunca, B3/H3). No se mezcla estado con dueños distintos.
2. **El vocabulario es cerrado.** Partidos, goles, asistencias, entradas, entradas ganadas, faltas,
   tarjetas, lesiones causadas, muertes causadas, minutos. Las claves libres existen para vocabularios
   **abiertos**; usarlas aquí no compra nada y cuesta la seguridad de tipos.
3. **Elimina complejidad en vez de moverla** (la pregunta que esta skill obliga a responder): un registro
   tipado **no tiene orden de iteración**, así que RT-041 deja de aplicarle. Un diccionario más exigiría
   `SortedDictionary` ordinal — resoluble, pero es complejidad movida, no eliminada.
4. Evita que una clave de historial colisione con un id de perk.

### Forma

`RunCareer` como propiedad `init` de `RunPlayer`, con una instancia vacía compartida — **mismo patrón que
`NoCounters`** y mismo estilo que el resto de propiedades del registro, cada una con su comentario y su RF.

### Coste aceptado: sube el esquema de guardado

`RunPlayer` gana un objeto `career`, así que **`RunState.CurrentSchemaVersion` pasa de 4 a 5**. Se conserva
la regla del proyecto: cargar otra versión es **un error explícito**, nunca una migración silenciosa. Los
guardados existentes dejan de cargar, que es el comportamiento ya establecido en los cuatro saltos previos.

---

## Decisión 2 — `Kill` recibe al matador, y **sin valor por defecto**

### El hallazgo que corrige el plan previo

`docs/plan-fase-memoria-y-atribucion.md` daba por supuesto **un** llamante de `Kill` y proponía un parámetro
opcional. **Las dos cosas eran incorrectas** *(LEÍDO, verificado)*:

- `MatchEngine.cs:2592` — `Kill(victim, "severeInjury")` dentro de `ResolveInjury`, que conoce al `tackler`.
- `EffectEngine.cs:904` — `_engine.Kill(marked, "perk:" + subscription.Perk.Id)`, el **perk letal**, donde
  `PerkSubscription` expone `Owner` *(`EffectEngine.cs:14-22`)*.

**Los dos caminos conocen al matador.** La atribución no se limita a las entradas: cubre también los perks
letales, que son precisamente los que hoy ya aparecen en el bando de muerte («rematado por *Sed de
médula*»). *Esto duplica el alcance de la decisión respecto a lo que el plan preveía.*

### Decisión

`Kill(MatchPlayer victim, string detail, MatchPlayer? killer)` — **nullable pero sin valor por defecto**.

El tipo sigue admitiendo «no hubo matador» (habrá vías futuras sin uno), pero **al no tener valor por
defecto, el compilador obliga a cada llamante nuevo a decidir explícitamente**. Es la misma filosofía que
RT-032 aplica a los datos: un caso no contemplado es un error explícito, nunca un silencio. Un parámetro
opcional habría dejado que una vía de muerte futura perdiera la atribución sin que nada avisara.

---

## Dónde vive cada memoria: dos mecanismos para dos ciclos de vida

No es una inconsistencia, es la consecuencia de la decisión 1:

| hecho | dónde | por qué | ¿sube versión? |
|---|---|---|---|
| carrera de un jugador **propio** | `RunPlayer.Career` (registro tipado) | vocabulario cerrado, vive lo que vive el jugador | **sí, 4 → 5** |
| «quién knaveó a quién» contra un **rival** | `RunState.Counters`, clave libre | vocabulario abierto (pares rival×jugador), vive lo que vive la run | **no** |

---

## Determinismo

- **Ningún RNG nuevo.** Esto es contabilidad (RT-021).
- **Aritmética entera** (RT-023).
- La acumulación posterior al partido recorre los jugadores **por id ascendente** (RT-041). El registro
  tipado no tiene orden interno que ordenar.
- **`Kill` no cambia de comportamiento**: solo registra. *Criterio de aceptación duro: las 43 puertas
  idénticas a la línea base. Si se mueven, se ha desplazado el consumo de RNG y eso es un fallo, no un
  efecto.*
- **RT-024 tiene que seguir verde en Windows y Linux**, no solo en WSL.

## Efectos de segundo orden

- `PlayerMatchStats` gana dos campos (`InjuriesCaused`, `DeathsCaused`). Lo consumen `/Balance`
  (`players.csv`) y `PostMatchView`: el cambio es aditivo, pero **hay que revisar los escritores de CSV**.
- Los jugadores **rivales** también producen `PlayerMatchStats` y hoy se descartan
  (`MatchResolution.cs:96`). `RunCareer` es **solo para la plantilla propia**; los hechos contra rivales van
  por el otro mecanismo.
- RF-122 (obituario) deja de estar bloqueado, pero **no se implementa aquí**.

## Lo que esta ADR NO decide

- Qué se enseña y dónde (es F2, presentación).
- La reaparición de rivales (`MapGenerator` no se toca).
- Ninguno de los seis gates abiertos de la ADR 0123.

## Hermanos

- `docs/decisiones/0122-primero-memoria-y-atribucion-no-mas-contenido.md`
- `docs/plan-evolucion-knavall.md` §F1 · `docs/plan-fase-memoria-y-atribucion.md` (corregido por esta ADR
  en el número de llamantes de `Kill` y en la opcionalidad del parámetro)

---

# Enmienda (22 sep 2026) — tras la revisión independiente

La revisión independiente (Regla E) encontró cuatro fallos reales y un hueco de proceso: **faltaba
`game-design-review`** (Regla B). Tenía razón: lo justifiqué como fontanería y hay **tres reglas de juego**
dentro. Se responden aquí, y se corrige lo demás antes de commitear.

## Las tres reglas de juego, decididas

### R1 · Un partido cuenta solo si se pisó el campo (`TicksOnPitch > 0`)

**No existe en `docs/requisitos.md`: es una regla nueva y se declara como tal.** Lo que el jugador lee en
el obituario es una carrera («11 partidos»), y un suplente que no llegó a entrar no puede reclamarlos sin
que la cifra mienta. *Alternativa considerada y descartada*: contar convocatorias aparte de partidos
jugados — añade un campo al vocabulario cerrado para una distinción que el obituario no necesita. El
banquillo ya tiene su propio coste en otro sitio (`MatchesBenched`, RF-111), así que esto no introduce un
castigo nuevo. **Se demuestra** con un test de suplente que no entra: `Matches` no sube y el resto de la
carrera queda intacta.

### R2 · Una muerte por reincidencia suma a la vez lesión y muerte al mismo causante

**Se conserva, y ahora es una decisión, no un accidente.** No es doble contabilidad de un hecho: son **dos
hechos distintos sobre un mismo acto**. RF-093 vía 1 define la muerte como *una lesión grave sin tratar que
se repite* — la lesión **ocurrió**, y además el jugador murió.

Importa porque **RF-125 pone un umbral**: «provocar 30 lesiones en una sola run desbloquea orcos».
*Alternativa considerada y descartada*: que la muerte excluya la lesión. Se rechaza porque haría que el
logro de las 30 lesiones fuera **más difícil cuanto más letal** sea la build — exactamente al revés de lo
que pide un desbloqueo de orcos. **Se demuestra** con un test que fija el doble conteo.

### R3 · El vocabulario cerrado se amplía con el lado de la víctima

**Se corrige la decisión original.** RF-122 pide un obituario «con **sus** estadísticas de la run», y el
vocabulario cerrado solo guardaba lo que el jugador **hizo**, nunca lo que **sufrió** ni **a manos de
quién**. Cerrar un vocabulario es un acto de diseño y reabrirlo después cuesta otro salto de esquema, así
que se amplía ahora:

- `RunCareer` gana **`InjuriesSuffered`**.
- El evento `DEATH` pasa a emitirse **con el matador como `opponent`**, igual que ya hace `INJURY`
  (`EmitCancellable(EventType.Injury, …, opponent: tackler)`). Así `PlayerDeathDetail` puede registrar a
  quién, y el bando de muerte podrá decir «a manos de X» en F2.

**Esto cierra el fallo más grave de la revisión:** la ADR se titulaba «atribución de muerte» y, cuando el
matador era un **rival**, no guardaba nada — que es precisamente el caso que da nombre al juego («ese clan
me knaveó al capitán»). Los tests verdes demostraban que un entero subía durante el partido, no que la
muerte quedara atribuida.

## Correcciones aceptadas antes de commitear

| # | qué | por qué |
|---|---|---|
| 1 | **Tests de `MatchResolution` paso 3b** | es el **único** camino de producción que escribe `Career` y no tenía ni un test: un `with` mal puesto pasaba los 1.073 |
| 2 | **`RivalHistory`: corregir su documentación** | decía implementar «quién knaveó a quién» y su tipo no lleva **ningún id de jugador**: es un historial de enfrentamientos. Habría sido el décimo caso del patrón «el texto promete lo que el código no hace» **dentro del paquete que existe para arreglarlo** |
| 3 | **Emitir `InjuriesCaused`/`DeathsCaused` en `/Balance`** | la propia ADR lo listaba como efecto de segundo orden y no se hizo. Sin salir por el arnés **no son medibles**, y «medición > intuición» es principio rector |
| 4 | **Mover los dos campos nuevos al final de `PlayerMatchStats`** | se insertaron entre `Injured` y `TicksOnPitch`; al final es más barato para cualquier consumidor posicional futuro |
| 5 | **Arreglar el aserto que pasa por construcción** | `Assert.Equal(0, victim.InjuriesCaused)` era cierto aunque el `++` estuviera mal puesto |
| 6 | **Corregir la tabla «Dónde vive cada memoria»** | citaba la memoria de rival en `RunState.Counters` como si existiera; es un plan, no código |

## Anotado y aplazado, con ficha

- `injure` con `target: "actor"` permitiría acreditar lesiones a un compañero o a la propia víctima. **Mecanismo
  real sin evidencia de activación** (ningún dato lo usa), pero el mensaje del cargador ya es falso.
- El paso 3b aplica las estadísticas del partido completo **ignorando `defeatTick`**, al contrario que el
  bucle de bajas del mismo fichero.
- `MatchResolution.PlayedTicks` busca por `PlayerId` **sin filtrar equipo** — hermano del mismo patrón.
- RF-125 será **subcontable** si se suma `Career.InjuriesCaused` de la plantilla: las contribuciones de un
  jugador vendido o dado de baja desaparecen con `WithoutPlayer`.
- `RunCareer` es un historial **solo de partido**: las lesiones de carta de evento no entran.
