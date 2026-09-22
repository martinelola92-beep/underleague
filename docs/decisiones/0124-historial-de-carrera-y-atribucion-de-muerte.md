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
