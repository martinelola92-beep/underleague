# ADR 0159 — Eventos, segunda tanda: mecánicas nuevas y catálogo de 18 cartas

Fecha: 27 sep 2026 · Estado: **aceptada**. **Decisión del revisor**: los eventos son prioritarios y «no hay
variedad» (`docs/plan-diversion.md` §1). Amplía la **ADR 0100** sin cambiar sus reglas: todo coste se ve
antes de elegir, no hay tiradas dentro de una opción y quien pone el cuerpo lo elige el jugador.
**Enmienda RF-072** para un único caso (el donante, abajo).
**Requisitos:** RF-011, RF-012d, RF-020, RF-022c, RF-043, RF-072, RF-114j, RT-031, RT-035
**Relacionada:** ADR 0100, ADR 0158 (la memoria del árbitro, que dos cartas usan), ADR 0048 (la muerte es
rara)

## Mecánicas nuevas (fase 1)

Todas **sin estado nuevo en el guardado**: se aplican al elegir y reutilizan lo que ya existe.

| efecto | qué hace | reutiliza |
|---|---|---|
| `grantItem` | un objeto de la rareza dada, al inventario guardado | `RunState.StoredItems` |
| `grantConsumable` | un consumible concreto, o uno de una familia | `OwnedConsumables` |
| `grantTrait` / `removeTrait` | añade o quita un rasgo al señalado | `RunPlayer.Traits` (máximo 3, RF-022c) |
| `attribute` | ±N a un atributo del señalado, permanente | `RunPlayer.Attributes` (la subida de nivel suma, no recalcula) |
| `level` | −1 nivel al señalado, con su pérdida de atributos | `Progression` |
| `refereeGrudge` | ±N a la memoria de **un árbitro con nombre** de la run | `RunReferee.Grudge` (ADR 0158) |
| `recruit` | un canterano gratis (con hueco en la plantilla) | la generación de canteranos del mercado |
| `sacrifice` | el señalado **muere**; su perk de mayor valor pasa a un **segundo señalado** con hueco | `RunPlayer.Perks`, la ceremonia de muerte |

**`refereeGrudge` y el árbitro con nombre.** La carta se deriva del nodo, así que también se deriva **qué
árbitro** sale en ella (el mismo flujo de recompensas). El texto lo nombra: *«Bartolo te invita a cenar»*.
El efecto mueve **su** memoria, y el ojeo del siguiente partido que pite lo enseña. No hace falta estado de
«próximo partido».

**`sacrifice` enmienda RF-072** («un perk asignado no se transfiere»): se transfiere **sólo** con la muerte
elegida de su portador. Es el mismo movimiento que la Herencia (`InheritanceSystem`), pero voluntario y
con el receptor señalado. El receptor necesita hueco de perk; si nadie lo tiene, la opción no se puede
pulsar. La decisión pasa a admitir un **segundo objetivo** (`ChooseEventOption.SecondTargetPlayerId`).

**Fase 2, fuera de esta ADR** (necesitan estado nuevo en el guardado): deudas diferidas, rival tocado en el
próximo partido y prótesis del herrero.

## Catálogo: 12 cartas nuevas (18 en total)

Tono de humor; cifras **provisionales, sin medir** (Regla H). Toda carta conserva «seguir camino».

| carta | opciones |
|---|---|
| **La cena con el colegiado** | pagar la cena (oro) → +20 de memoria de ese árbitro · invitar y **no pagar** → −15 de memoria y +3 de oro |
| **El árbitro en la cuneta** | llevarlo en tu carro → +25 de memoria de ese árbitro y un jugador señalado con lesión leve (le toca empujar) |
| **El chamarilero** | pagar poco → un objeto **raro** · pagar mucho → un objeto **legendario** |
| **El boticario ambulante** | comprar un consumible sucio · comprar uno médico · robarle uno → −10 de memoria del árbitro de la comarca |
| **La bruja del córner** | un señalado gana el rasgo `Cold` y pierde un nivel |
| **Pelea en el vestuario** | castigar al señalado: pierde `Aggressive` · premiarle: gana `Leader` y otro señalado queda con lesión leve |
| **El foso** | un señalado pelea: +6 de fuerza y lesión leve |
| **La peña del pueblo** | aceptar a su chaval (canterano gratis) · aceptar su dinero (oro) |
| **El donante** | sacrificar a un señalado: su mejor perk pasa a otro señalado |
| **El santero** | pagar: un consumible sobrenatural · rezar: todos los leves se curan |
| **La escuela de carreras** | un señalado: +6 de velocidad y −3 de técnica |
| **El sastre del gremio** | pagar: un objeto común · regatear: un objeto común y −5 de memoria del árbitro de la comarca |

«El árbitro de la comarca» es el árbitro con nombre derivado de la carta.

## Las diez preguntas (`game-design-review`)

1. **Qué ve el jugador.** Cartas con personajes, humor y un precio claro, en vez de seis variaciones de
   «oro o experiencia».
2. **Qué decide.** Qué quiere su run: oro, objetos, identidad de un jugador (rasgos y atributos) o
   relación con un árbitro concreto.
3. **Qué debería decidir.** Lo mismo. Una carta que siempre se toma o nunca se toma es un defecto, y se
   mide.
4. **Regla.** RF-011 y ADR 0100; enmienda de RF-072 sólo por sacrificio.
5. **Sistemas.** `/Sim`: `EventEffectKind`, `EventSystem.Apply`, `EventView` (plantillas de cada efecto),
   `ChooseEventOption` (segundo objetivo), `RunPolicy.VisitEvent` (tasar los efectos nuevos). `/data`:
   `data/events/*.json`, su esquema y `templates.json` §`eventEffects`. `/Game`: `NodeScreen` (segundo
   objetivo).
6. **Alternativas.** Tiradas dentro de las opciones: descartado por la ADR 0100.
7. **Trade-off.** Cada opción cuesta algo visible: oro, un nivel, una lesión, un jugador o la relación con
   un árbitro.
8. **Estrategias.** Las cartas de árbitro hacen de la memoria del árbitro (ADR 0158) algo que se
   **gestiona**; las de rasgo y atributo fabrican especialistas, igual que el entrenamiento nuevo.
9. **Degeneración.** El donante puede concentrar perks en una estrella: lo limita el hueco de perk de la
   rareza (RF-023) y el coste de un jugador. `deathsPerRun` se vigila; la muerte elegida no es daño no
   anunciado (ADR 0048).
10. **Cómo se demuestra.** Un test por efecto nuevo; validación del esquema; que toda carta tenga salida
    (el test de BA-A); `eventsTakenPerRun` y `eventsDeclinedPerRun` en el lote de campaña.
