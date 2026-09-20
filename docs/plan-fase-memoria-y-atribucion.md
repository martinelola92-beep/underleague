# Plan de la Fase 1 — que el juego recuerde y explique

Plan de ejecución de la **ADR 0122**. Es el 10 % de planificación del esquema 10-80-10: arquitectura,
interfaces, criterios de éxito y restricciones **por escrito antes de que nadie codifique**.

Escrito el 20 sep 2026. **Nada de esto está implementado.**

---

## 1A · Historial individual persistente

### El hecho de partida, verificado

El motor **ya calcula** por partido y por jugador (`Sim/Engine/MatchReport.cs:39-53`,
`PlayerMatchStats`): `Goals`, `Assists`, `Shots`, `PassesAttempted`, `PassesCompleted`, `Tackles`,
`TacklesWon`, `Fouls`, `Cards`, `Injured` (recibida, booleana), `TicksOnPitch`, `LeftPitchTick`.

`RunPlayer` (`Sim/Run/RunState.cs:122-162`) **no persiste ninguna**. *(LEÍDO, verificado.)*

### Los dos campos que el revisor pide y el motor NO atribuye hoy

Esto es lo que convierte 1A de «fontanería» en un cambio con un trozo de motor, y hay que presupuestarlo:

| campo | estado | qué falta exactamente |
|---|---|---|
| **lesiones causadas** | el causante se conoce, no se agrega | `EventType.Injury` se emite con `opponent: tackler` (`MatchEngine.cs:~2568`). Falta sumarlo al causante en `PlayerMatchStats` |
| **muertes causadas** | **el causante se pierde** | `Kill(MatchPlayer victim, string detail)` (`:3243`) **no recibe al matador**. Su única llamada (`:2592`) está dentro de `ResolveInjury`, que **sí** conoce al `tackler` |

*El «rematado por «Sed de médula»» del bando de muerte es atribución a un **perk**, no a un jugador.*

**Estos dos son precisamente los contadores que más historia producen** —son los que construyen al
carnicero— así que no son opcionales dentro de 1A.

### Diseño propuesto (a validar en `architecture-review`, no decidido aquí)

1. **Ampliar `PlayerMatchStats`** con `InjuriesCaused` y `DeathsCaused`. Es un `record` de `/Sim`; ampliarlo
   no cruza ninguna frontera.
2. **Dar un matador opcional a `Kill`**: `Kill(MatchPlayer victim, string detail, MatchPlayer? killer = null)`,
   y pasarlo desde `ResolveInjury:2592`. **El parámetro opcional mantiene intactas las demás vías de
   muerte** (RF-093 vía 1, la lesión grave repetida, que no tiene matador y debe seguir sin tenerlo).
3. **Acumular en `RunPlayer`** un registro nuevo (`Career` o similar) en vez de reutilizar `Counters`.
   *Motivo:* `Counters` es el canal de los perks con `accumulatesAcrossMatches`, topa en `maxValue` 3-5 y
   se reinicia por reglas de perk. Mezclar el historial ahí lo haría rehén de esas reglas. **Decisión de
   frontera: es la pregunta central para `architecture-review`.**
4. **Subir `RunState.CurrentSchemaVersion` de 4 a 5** (`RunState.cs:359`) y actualizar
   `data/schemas/run-save.schema.json`. El proyecto no migra en silencio: cargar otra versión es un error
   explícito, y esa regla se conserva.

### Restricciones que no se negocian

- **RT-021**: nada de RNG nuevo. Esto es contabilidad, no azar.
- **RT-023**: aritmética entera.
- **RT-041 / orden determinista**: la acumulación recorre jugadores por id ascendente. Nunca se itera un
  `Dictionary` sin ordenar para algo que afecte al resultado.
- **RT-024**: el test de determinismo tiene que seguir en verde. *Es el riesgo real de 1A*: cambiar la firma
  de `Kill` toca una ruta de partido.
- **`/Sim` no conoce Godot** (RT-011) y no hace E/S (RT-012).

### Criterio de éxito de 1A

1. **Medible**: tras una run completa de `--full-runs`, la ficha de al menos un jugador superviviente
   contiene un registro que no era derivable al empezar la run. *Instrumento: el propio guardado, sin
   herramienta nueva.*
2. **Medible**: las 43 puertas idénticas a la línea base. **1A no debe mover ni una métrica**: si mueve
   alguna, es que ha tocado el consumo de RNG y eso es un fallo, no un efecto.
3. **Del revisor, jugando**: «Grok Rompehuesos · 11 partidos · 7 entradas · 3 lesiones · 1 muerte»
   **se lee como alguien**.

### Lo que 1A NO hace

No implementa el obituario (RF-122) ni el memorial: 1A es su prerrequisito. No muestra el historial en
`/Game` más allá de la ficha de jugador. No toca el catálogo.

---

## 1B · Un perk conductual con `modifyUtility`

### El hecho de partida, verificado

`modifyUtility` (C1) está **en el motor** (`Utility.cs:159-164`, `MatchPlayer.PerkActionBonusPercent`,
`EffectEngine` con su `case EffectType.ModifyUtility`), pilotado y medido al 24 % con las **siete métricas
obligatorias de RT-056 `IN` en las dos semillas y sin regresión** (`c1-piloto-cazagoles-diseno.md` §7).

**Y está desconectado del dato**: `modifyUtility` **no aparece en el enum de
`data/schemas/perks.schema.json`** *(LEÍDO, verificado: el enum tiene `modifyAttribute`, `modifyBias`,
`modifyExperience`, `modifyKnockdownTicks`, `modifyLeash`, `modifyMarkBias`, `modifyProbability`,
`modifyTackleBias`, `modifyTraitScalar`, `modifyZoneShape` — y no éste)*.

### Lo que falta, exactamente

1. `modifyUtility` en el enum del esquema, más los campos `utilityAction` y `utilityZone` con su vocabulario
   cerrado (RT-032: un dato inválido es un error explícito, nunca silencioso).
2. Soporte en `Sim/Perks/PerkLoader.cs` para esos campos.
3. **Techos por acción y por puesto en el cargador (C10)**: un `×3` a `Shoot` en un portero tiene que ser un
   **error de datos**, no una anécdota. El catálogo conceptual ya lo marca como prerrequisito de C1.
4. `data/perks/cazagoles.json` por `perk-authoring`: familia, rareza, `minAct`, `frequency`, RF-069, y el
   conflicto ya señalado con `C-28 Sangre fría`.
5. La descripción generada (RT-035) tiene que producir **una frase de conducta**, no una suma. Es la primera
   vez que hay un efecto conductual del que generarla.

### Deuda del piloto que hay que saldar aquí

- **`independent-reviewer` (Regla E)**: C1 ya tocó `/Sim` en el commit `74b1231` y **nunca pasó por
  revisión independiente**.
- **RT-024 con Cazagoles realmente equipado**: el piloto solo verificó que el motor es idéntico con C1
  **inerte**. Es barato y está pendiente.
- El **24 % no es un valor cerrado**: es lo medido, no lo decidido.

### Criterio de éxito de 1B — y es el que el revisor ha fijado

**No es la tasa de victoria.** Es: *«¿ahora puedo reconocer en el campo qué tipo de jugador es?»*

Instrumento ya existente para la mitad objetiva de esa pregunta: el **histograma de acción elegida por
jugador y partido** contra el control emparejado de la ADR 0087
(`Sim.Tests/Analysis/ActionHistogramTests.cs`, `docs/analisis/tanda-0-histograma-de-accion.md`). Está
calibrado: distingue un efecto real pequeño (`sweeper_keeper`, L1 = 0,0013) de un cero exacto
(`own_third_anchor`, L1 = 0).

- **L1 claramente distinto de cero con activación completa** → el perk cambia lo que el jugador *intenta
  hacer*. Es lo que ningún `modifyProbability` ha conseguido nunca.
- **Aviso de potencia, ya aprendido**: si el portador activa en pocos partidos, un L1 = 0 no prueba nada
  (el límite AT-C, y el caso `bulwark_stance` que activó en 2 de 40). **Hay que comprobar la exposición
  antes de leer el resultado.**

La otra mitad de la pregunta la responde el revisor mirando un partido.

### Lo que 1B NO hace

**Un perk. Uno.** No se abre la compuerta a convertir el catálogo. No se toca `MinPassChainRatio`. No se
implementa C2 (el vocabulario de situación): Cazagoles necesita **una** cláusula, el tercio del actor, que
ya tiene función de una línea (`Pitch.ZoneOf`).

---

## Orden de trabajo y reparto

1. `architecture-review` sobre la frontera de 1A (¿registro nuevo o `Counters`?) — **antes de codificar**.
2. 1A implementado y con las 43 puertas idénticas a la línea base.
3. `independent-reviewer` sobre 1A (toca `/Sim` y el guardado: Regla E).
4. `game-design-review` sobre 1B (mecánica nueva: Regla B) + `architecture-review` (primitiva de motor:
   Regla C).
5. 1B implementado, con el histograma de acción y `balance-measure` (Regla D).
6. `independent-reviewer` sobre 1B **y sobre la deuda del piloto C1**.
7. Informe al revisor y **partida**, que es donde se responde la pregunta que importa.

Delegable a `opencode-worker` con especificación cerrada: el esquema JSON y su validación, los tests de
round-trip del guardado, la ficha de `cazagoles.json` una vez decidida en `perk-authoring`.
**No delegable**: la decisión de frontera de 1A, la firma de `Kill`, y la lectura del histograma.

## Riesgos anotados antes de empezar

1. **1A puede mover el consumo de RNG sin querer** al tocar `Kill`. Es el patrón que ya apareció tres veces
   en una sesión (`project-state.md`): cualquier cambio real de `/Sim` desplaza el consumo de RNG y cruza
   márgenes de puertas. *Mitigación: 1A no debe mover ninguna métrica; si la mueve, se investiga antes de
   seguir.*
2. **1B es un recalibrado, no un parche.** Un bono a `Shoot` cambia cuántos tiros hay. Presupuesto medido
   el 20 sep 2026: `injuriesPerMatch` **0,81 con techo 0,90** y `tacklesPerMatch` 12,01 con techo 14.
   Cazagoles no toca `Tackle` —el pre-registro del piloto lo usó como comprobación de «qué NO debería
   moverse»— pero cualquier segundo candidato conductual sí lo tocaría.
3. **El criterio de aceptación es cualitativo.** No se puede automatizar «¿me acuerdo de mis jugadores?».
   El plan lo acepta: el instrumento objetivo (histograma, puertas, guardado) sirve para **descartar** que
   no funciona, no para **demostrar** que sí. Esa parte la decide una partida.
