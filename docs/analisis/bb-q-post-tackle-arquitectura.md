# BB-Q Parte A — Cómo puede un perk de TACKLE reaccionar al resultado de la entrada

**Revisión de arquitectura, 19 sep 2026. No hay código.** Ficha del síntoma: `docs/pendientes/BB-Q.md`.
Mapa del sistema de eventos levantado para esta revisión (léelo antes de discutir alternativas): §1.

La pregunta no es "cómo arreglo `steamroller`". Es: **el modelo de eventos publica los eventos de acción
ANTES de resolverlos, por diseño; ¿cómo reacciona entonces un perk al resultado?**

---

## 1. El mapa: qué información existe en cada fase

Flujo real de una entrada (`Sim/Engine/MatchEngine.cs`, `ResolveTackle`):

| # | Fase | Línea | ¿Llega al bus de perks? | Qué se sabe ya |
|---|---|---|---|---|
| 1 | `PublishBeforeResolving(Tackle, "attempted", tackler, opponent: carrier)` | `:2172` | **SÍ** | quién entra y a quién. **Nada del resultado.** |
| 2 | `TackleWinChance`, `foulChance`, tiradas `isFoul`/`isWin` | `:2175-2197` | — | — |
| 3 | `Emit(Tackle, "won"/"missed"/"foul", publish: false)` | `:2203` | **NO** | el resultado, pero no se publica |
| 4 | `carrier.EnterState(KnockedDown)` | `:2221` | — | el rival está en el suelo |
| 5 | `SetOwner(tackler)` + `Emit(Recovery, "tackle", tackler)` | `:2223` | **SÍ** | **post-resolución, publicado** |
| 6 | `ResolveInjury` → `EmitCancellable(Injury, …)` | `:2233`, `:2568` | **SÍ** (cancelable) | lesión decidida, aún no aplicada |

### Los tres hechos que gobiernan toda la decisión

**(a) El evento TACKLE resuelto no llega nunca a ningún perk.** Se emite con `publish: false` en `:2203`,
`:2211` y `:2471`. El bus solo ve `"attempted"` y `"block"`. Lo mismo pasa con SHOT: el bus solo ve
`"attempted"`, nunca `onTarget`/`offTarget`. El contrato está escrito en el propio helper (`:3275-3278`) y
en `PublishBeforeResolving` (`:3309-3313`): *"el evento definitivo se emite después con su Detail real y con
`publish: false`, para que la secuencia de eventos siga teniendo exactamente una entrada"*. **Es un
invariante deliberado, no un descuido.**

**(b) `detail()` existe, está expuesta, traducida… y no la usa NINGÚN perk.** Verificado sobre los 94
ficheros de `data/perks/`: cero coincidencias. Está declarada (`ConditionCompiler.cs:89`), resuelta
(`:731-733`), con plantillas de descripción (`data/l10n/*/templates.json`, `detailEq`/`detailNe`) y un
catálogo de literales conocidos. Es una primitiva pagada y sin estrenar.

**(c) Ya existe un evento post-resolución para este caso exacto.** `Emit(EventType.Recovery, "tackle",
tackler)` en `:2223` se publica (`publish: true` por defecto) **inmediatamente después** del derribo de
`:2221`. Y la implicación es exacta, no aproximada: en el camino `isWin` el motor **siempre** ejecuta
`EnterState(KnockedDown)` antes del `Emit`. Por tanto, dentro de este motor,

> **`Recovery` con `detail() == 'tackle'` ≡ "mi entrada dejó al rival en el suelo"**

que es literalmente lo que pide el `_doc` de `steamroller`. Tres perks ya usan `RECOVERY` como disparador
(`lane_reader`, `road_warrior`, `sweeper_keeper`), así que el disparador está probado.

### Un hecho más, que cambia el sentido de la alternativa 3

**Ligar el entrado a `target` iría contra la convención del propio motor.** De los 17 sitios que llevan un
segundo jugador, **13 usan `opponent:` y solo 4 usan `target:`** — y los cuatro (`Substitution`,
`PassCompleted`, `PassAttempted`, `Goal`) son relaciones del **mismo equipo**: receptor, asistente,
saliente. **En ningún evento adversarial se liga `target`.** La convención es consistente: `target` = el
otro del mismo equipo, `opponent` = el rival.

O sea que `steamroller` no cayó en un hueco del motor: **escribió el identificador equivocado**, y es el
**único perk del catálogo** que referencia `target` en una condición sobre un evento adversarial.

---

## 2. Las alternativas

Las cuatro que pedía el encargo, más dos que salieron del mapa.

### Alt 1 — Reutilizar/ampliar `detail()` para leer el resultado

- **Resuelve**: nada por sí sola. Hoy `detail()` sobre TACKLE solo puede valer `"attempted"` o `"block"`.
- **NO resuelve**: para que `detail() == 'won'` funcionara habría que **publicar también el evento
  resuelto**, es decir publicar TACKLE dos veces por entrada.
- **Impacto**: catastrófico y catalog-wide. Los **12 perks con trigger TACKLE** y los **7 con SHOT** se
  evaluarían dos veces por acción. `charge` y `double_shot` (`extraAction`) encadenarían el doble;
  `last_ditch`, `back_to_back`, `game_management`, `iron_studs`… aplicarían su modificador dos veces.
- **Compatibilidad con el modelo pre-resolución**: **rompe el invariante** de "exactamente una entrada".
- **Vocabulario**: no lo amplía.
- **Complejidad**: baja de escribir, altísima de razonar.
- **Riesgo de regresión**: **muy alto**, silencioso, y sobre todo el catálogo.
- **Reutilizable**: no.
- **Veredicto: DESCARTADA.**

### Alt 2 — Un post-evento nuevo de resultado de TACKLE

- **Resuelve**: el caso general, incluidos `missed` y `foul`.
- **NO resuelve**: nada que Alt 0 no resuelva ya para el caso `won`.
- **Impacto**: `EventType` nuevo → toca el mapa de eventos, `_byTrigger`, l10n, el generador de
  descripciones, el log y el CSV. Y **duplica** a `Recovery "tackle"`, que ya cubre el caso ganado.
- **Compatibilidad**: buena (es post-resolución puro).
- **Vocabulario**: no lo amplía.
- **Complejidad**: media-alta.
- **Riesgo de regresión**: medio.
- **Reutilizable**: sí, pero paga por adelantado una generalidad que ningún perk del catálogo pide hoy.
- **Veredicto: NO AHORA.** Reabrir solo si aparece un perk que necesite el resultado `missed` o `foul`.

### Alt 3 — Ligar `target` correctamente en TACKLE

- **Resuelve**: la causa 1 de BB-Q (el identificador sin ligar).
- **NO resuelve**: ni la causa 2 (`'down'` = lesionado/muerto) ni la 3 (el derribo aún no ha ocurrido).
  **Por sí sola deja `steamroller` en 0 %.**
- **Impacto**: rompería la convención `target`=mismo equipo / `opponent`=rival en los 13 sitios
  adversariales, o crearía una excepción solo para TACKLE. Y `PerkScope.Target`
  (`EffectEngine.cs:663`) cambiaría de significado para cualquier perk con ese scope.
- **Veredicto: DESCARTADA como arreglo.** Pero ver Alt 5: el hecho de que un dato pudiera pedir un
  identificador que el disparador no liga **sí** es un agujero real.

### Alt 4 — Primitiva nueva de condición que lea `PlayerState`

- **Resuelve**: expresar "está derribado / celebrando / lesionado" desde `/data`. Hoy **no hay ninguna
  función que lea el estado** (las 22 están en `docs/pendientes/BB-Q.md`). El motor sí representa la
  semántica y dos efectos ya la tocan —`earthquake` escribe `setState KnockedDown`, `blood_scent` escribe
  `PreferKnockedDownTackleTarget`— pero por C#, nunca desde una condición.
- **NO resuelve**: **BB-Q**. En el instante en que la condición de un perk de TACKLE se evalúa, el rival
  todavía no está derribado (causa 3). Una primitiva de estado sobre un disparador pre-resolución sigue
  dando falso.
- **Impacto**: superficie pública de `/data` (RT-031/RT-034), esquema, l10n, descripciones.
- **Vocabulario**: lo amplía. Requiere `game-design-review` (mecánica nueva) y `architecture-review`.
- **Complejidad**: media. **Riesgo**: bajo si es de solo lectura. **Reutilizable**: mucho.
- **Veredicto: ÚTIL, PERO ES OTRO TICKET.** No es el arreglo de BB-Q y no debe justificarse con él.

### Alt 0 (del mapa) — Usar el `Recovery "tackle"` que ya existe · **RECOMENDADA**

`steamroller` pasa a `trigger: RECOVERY` con `condition: detail() == 'tackle'`.

- **Resuelve**: las **tres** causas de golpe. El evento es post-resolución (causa 3), el `actor` es el que
  entró y está ligado (causa 1), y no hace falta preguntar por ningún estado porque "entrada ganada" ya
  implica "rival derribado" (causa 2).
- **NO resuelve**: un perk que necesite saber **quién** cayó — `Recovery` no liga `target` ni `opponent`
  en ninguno de sus 6 sitios. Tampoco cubre las ramas `missed`/`foul`.
- **Impacto sobre otros perks**: ninguno. No toca el motor de eventos ni ningún perk existente.
- **Compatibilidad con el modelo pre-resolución**: **total** — lo respeta en vez de esquivarlo.
- **Vocabulario**: **no lo amplía**. Estrena `detail()`, que llevaba sin usarse desde que se escribió.
- **Complejidad**: **la menor de todas.** Un único obstáculo, y es de dos líneas:
  `extraAction` solo admite `SHOT` y `TACKLE`, tanto en el cargador (`PerkLoader.cs:1172`) como en el
  `switch` de `ExecuteExtraAction` (`EffectEngine.cs:1091-1098`). Hay que admitir `RECOVERY` → `RepeatTackle`.
- **Riesgo de regresión**: **bajo y acotado.** Ningún perk actual combina `extraAction` con `RECOVERY`, así
  que la rama nueva nace sin usuarios salvo el que la pida. La recursión sigue cortada por `_maxDepth`
  (RT-042) como en `charge`.
- **Reutilizable**: sí, para toda la familia "cuando recupero el balón" — que ya tiene tres perks.

### Alt 5 (del mapa) — Validar en carga que una condición solo pida identificadores que su disparador liga

Ninguna de las anteriores impide que se vuelva a escribir el error. Hoy `stat(target,'down')` sobre
`TACKLE` **carga sin una queja** y se evalúa a 0 para siempre.

- **Resuelve**: convierte un fallo silencioso en error de carga, que es exactamente lo que exigen RT-032 y
  RT-083 (*"un dato inválido es un error explícito, nunca silencioso"*).
- **NO resuelve**: BB-Q. Lo habría **prevenido**.
- **Impacto**: hay que declarar, por `EventType`, qué identificadores liga. El mapa ya está levantado (§1)
  y es corto: 4 eventos ligan `target`, 13 ligan `opponent`, el resto ninguno.
- **Riesgo**: **medio, y hay que medirlo antes**: la validación podría rechazar perks del catálogo que hoy
  cargan. Se implementa primero como auditoría que solo informa, y solo se sube a error cuando el catálogo
  esté limpio.
- **Veredicto: RECOMENDADA, independientemente de cuál se elija para BB-Q.**

---

## 3. Propuesta

**Alt 0 para BB-Q, Alt 5 para que no se repita.** Alt 4 como ticket propio, justificado por sí mismo.

| | qué | dónde | por qué ahora |
|---|---|---|---|
| 1 | `extraAction` admite `RECOVERY` → `RepeatTackle` | `EffectEngine.cs:1091`, `PerkLoader.cs:1172` | desbloquea Alt 0; 2 líneas, sin usuarios previos |
| 2 | `steamroller` → `trigger: RECOVERY`, `condition: detail() == 'tackle'` | `data/perks/steamroller.json` | **cambio de `/data`: requiere tu aprobación** |
| 3 | Auditoría (solo informa) de identificadores no ligados por disparador | `Sim.Tests` primero | mide si hay más casos antes de decidir si sube a error de carga |
| 4 | Primitiva de condición para `PlayerState` | ticket aparte | no se justifica con BB-Q |

**Criterio de aceptación**: quitar el `Skip` de
`SteamrollerConditionTests.SteamrollerChainsAtLeastOnceWhenItsCarrierWinsTackles` y que pase sin tocar
su umbral.

### Las seis preguntas de `architecture-review`

1. **¿Qué frontera toca?** Solo `/Sim` y `/data`. `/Game` no se entera: no hay evento nuevo, así que RT-014
   y RT-011 quedan intactos. Alt 2 y Alt 4 sí tocarían la superficie pública.
2. **¿Elimina complejidad o la mueve?** La elimina: usa un evento que ya existe en vez de añadir uno.
   Alt 1 y Alt 2 la mueven o la duplican.
3. **¿Determinismo?** Sin aritmética nueva ni aleatoriedad nueva. El orden de `extraAction` lo sigue
   fijando `PublishAtDepth` (RT-041) y la recursión la sigue cortando `_maxDepth` (RT-042).
4. **¿Paralelismo?** No aplica: nada del arnés cambia.
5. **Efectos de segundo orden.** El que hay que vigilar: `Recovery "tackle"` se publica **después** de
   `SetOwner(tackler)`, así que la entrada extra ocurre con el balón ya en poder del que entró — situación
   distinta de la de `charge`, que repite antes de saberse el resultado. Es un cambio de comportamiento
   real respecto a lo que el `_doc` imagina, y hay que medirlo cuando se implemente.
6. **¿Rompe RT-024?** No: ningún cambio de orden ni de fuente de aleatoriedad.

---

## 4. Lo que esta revisión NO hace

No implementa nada. No toca `/data` ni `steamroller`. No cambia el cribado ni ningún umbral. El `Skip` del
test de regresión sigue siendo temporal y explícito, no una solución. Y **el punto 2 de la propuesta es un
cambio de `/data`: no se hace sin tu aprobación.**
