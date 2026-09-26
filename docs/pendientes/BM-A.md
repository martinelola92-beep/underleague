# BM-A — Un perk no puede derribar al que le entra

Estado: **Implementada** (26 sep 2026): regla del cargador, «un derribado por efecto no disputa», la misma
regla en la auditoría y en las descripciones, y Muro convertido. Quedan abiertas la hermana
[BM-B](./BM-B.md) y las notas del final. Paso 1 del orden del revisor en `project-state.md` («composición:
la regla genérica del cargador»).

## Síntoma

Tres actos del diseño (`docs/analisis/perks-de-cuota-a-acto.md` §3.1 y §3.6) necesitan que el efecto caiga
sobre **quien le entra al portador**: **Muro**, **Pies de plomo / Raíces** («el rival rebota») y **Toque
élfico** («se escurre»). El cargador no dejaba escribirlos: `PerkLoader.ValidateEffect` admitía `setState`
sólo con `target ∈ {target, opponent, opposingTeam, adjacentOpponents}`, **por el nombre del objetivo**, no
por la relación con el portador.

## Lo que se leyó (26 sep)

1. **En los eventos de contacto el `opponent` es siempre rival del `actor`** (entrada, bloqueo, regate,
   parada, tiro bloqueado, pase fallado, duelo aéreo, falta no vista). **No en `INJURY` ni en `DEATH`**: ahí
   el actor es la víctima y el opponent el causante, y el causante puede ser **un compañero o la propia
   víctima** (fuego amigo, BE-B, `FriendlyFireAttributionTests`). *La primera versión de esta ficha lo daba
   por CONFIRMED para todos los eventos; la revisión independiente lo tumbó.*
2. **Por eso la rivalidad con el portador se decide por el alcance (`scope`) y el disparador**, no por el
   nombre del objetivo:
   | scope \ target | `opponent` | `actor` | `target` |
   |---|---|---|---|
   | `actor`, `team` | rival | el portador / un compañero | depende del evento (en `PASS` es un compañero) |
   | `opponent` | **el propio portador** | rival | depende |
   | `opposingTeam` | del bando del portador | rival | depende |
   | `target`, `any` | no garantizado | no garantizado | no garantizado |
   | con `INJURY`/`DEATH` | no garantizado | no garantizado | no garantizado |
   `opposingTeam` y `adjacentOpponents` son rivales siempre. La regla vieja dejaba pasar dos casos malos
   —`scope: opponent` + `target: opponent` **se derriba a sí mismo**, y `target: target` puede derribar a
   un compañero en un `PASS`— y prohibía el bueno. Ningún dato los usaba.
3. **La regla sola no basta.** `TACKLE` y `DRIBBLE_ATTEMPTED` se publican **antes** de resolverse
   (`ResolveTackle`, `ResolveBlock`, `TryDribbleDuel`), y la resolución **no miraba** si su actor seguía en
   pie: un entrador derribado por un perk del portador tiraba igual la victoria y, si ganaba, `SetOwner` le
   daba el balón a un jugador en el suelo.

## Nota de diseño (`game-design-review`, las diez preguntas)

1. **Qué experimenta el jugador.** Un rival le entra a su Muro y es **el rival** el que acaba en el suelo,
   con el pergamino del perk sobre el Muro (ADR 0112). La entrada no se resuelve: ni robo, ni falta, ni
   lesión.
2. **Qué decide.** A quién pone el perk: sólo sirve en un jugador con la etiqueta `Bulwark` (condición) y
   que reciba entradas, o sea que lleve el balón o sea blanco de cargas.
3. **Qué debería decidir.** Lo mismo; y el rival, que lo ve en el ojeo (RF-012b), decide si le entra.
4. **Regla.** RF-067 (el alcance *opponent* ya existe) y el invariante de `setState` de `fase1-diseno.md`
   («solo objetivos rivales; validado»). **No hay regla nueva**: la validación dice lo que la regla ya
   decía —*rival del portador*— en vez de una lista de nombres. Y el invariante del motor «un derribado no
   tiene acciones» (`StateMachine.LegalActions`) pasa a aplicarse **dentro** de la resolución que un
   efecto interrumpe.
5. **Sistemas.** `/Sim`: `PerkLoader` (cargador), `MatchEngine` (el corte), `DescriptionGenerator` y
   `PerkBalanceClassifier` (la misma regla, en la descripción y en la auditoría). `/data`: `bulwark_stance`,
   plantillas `setState` y `triggersReceived.DRIBBLE_ATTEMPTED`. `/Game`: nada.
6. **Alternativas.** (a) Excepción por perk (`hitsTackler`) — rechazada: es la excepción que el revisor
   pidió no hacer. (b) Objetivo nuevo `rival` resuelto en tiempo de partido — rechazada: duplica
   `actor`/`opponent` y esconde en el motor lo que el cargador puede comprobar al arrancar (RT-032). (c)
   Filtro en tiempo de partido que ignore a los compañeros — rechazada como regla principal: un dato que
   nunca hace nada tiene que ser un **error** explícito, no un silencio (RT-083).
7. **Trade-off.** Un Muro con el balón lo conserva ante una entrada cada 20 s; no gana nada más. Es la
   defensa del que aguanta, no la del que roba.
8. **Estrategias.** Abre la familia «castigar al que entra», que no existía (todos los derribos del
   catálogo eran del que entra al que recibe). Contra un Muro, `dirty_play`/`ankle_bite` pueden quedarse
   sin disparar: la entrada que los activa ya ocurrió, pero su resolución no.
9. **Degeneración.** Un derribo por cada entrada recibida anula el juego defensivo rival contra ese
   jugador: por eso el enfriamiento (RF-069c), que con `TACKLE` sí muerde. **No se aplica a las habilidades
   raciales**: Toque se dispara 11,5 veces por partido y una racial es gratis e irrenunciable (ADR 0026,
   techo de +2,5 puntos).
10. **Cómo se demuestra.** Tests del cargador y del motor (`Sim.Tests/Perks/DownedActorTests.cs`); A/B
    de campaña contra `HEAD` con el catálogo sin tocar (idéntico byte a byte); censo de activaciones y
    valor de Muro con el protocolo de la tabla.

## Lo que se implementó, y lo que obligaron a cambiar las medidas

- **Cargador**: `PerkLoader.IsRivalOfOwner(trigger, scope, target)` sustituye la lista de nombres.
- **Motor**: tras la publicación previa de entrada, bloqueo y regate, `MatchEngine.StoppedByEffect` corta
  la resolución si **un efecto de perk** derribó al actor en ese tick (`MatchPlayer.EffectKnockdownTick`,
  que sólo escribe `KnockDown`) o lo sacó del campo.
- **El recorte, por medida**: la primera versión cortaba ante **cualquier** derribo (`!CanTouchBall`), y el
  A/B contra `HEAD` (`--full-runs 150 --seed 3`) cambió 44 de 450 runs. Instrumentado: los 12 casos de un
  lote de 20 eran `charge`/`bull_rush`, cuya entrada repetida se resuelve **dentro** de la publicación
  previa (BM-B). Es comportamiento medido de dos perks, así que la regla se acota a los derribos **de
  efecto**. Con el recorte, los tres CSV salen **idénticos byte a byte** a `HEAD` antes de convertir Muro
  (reproducido también por la revisión independiente).
- **Muro** (`bulwark_stance`): `TACKLE`, `scope: opponent`, `setState(actor, 6)` —6 es
  `dribble.lostKnockdownTicks`, el golpe que ya se lleva quien pierde un duelo cuerpo a cuerpo— y 20 s de
  enfriamiento (los de `nutmeg`, sin medir). **Primero se probó en `DRIBBLE_ATTEMPTED`** («el que intenta
  regatearle rebota», como decía el diseño) **y era casi inerte**: 0,17 activaciones por partido en el
  censo mixto y **0 de 80** partidos en el test de calibración, porque en 32 partidos enano contra enano el
  defensor enano con `Bulwark` no afrontó ningún regate. En `TACKLE`, que es además lo que pedía el paso
  1 («el actor que le entra»): **42,5 % de los partidos con portador enano, 2,5 % con humano**, 0,38 por
  partido en el censo mixto, y valor **3** con el protocolo de la tabla (semillas 5 y 11; antes 2).
- **La misma confusión en otras dos capas** (Regla J), arreglada con la misma regla:
  - **Auditoría** (`PerkBalanceClassifier.ClassifyTargetShape`): contaba `target: actor` siempre como el
    portador. Aparecen **tres perks ya mal clasificados antes de este paquete**: `grudge` (el efecto cae
    sobre quien le hace la falta), `killing_range` (sobre el compañero que dispara) y `second_wound` (sobre
    el rival lesionado). Con Muro, los cuatro pasan a `MultiTarget`; el lote de cribado baja de 29 a 26. El
    valor de la tabla no depende de esta clasificación.
  - **Descripción** (`DescriptionGenerator.DescribeTarget`): con `scope: opponent`, `actor` se leía «el
    jugador», que parece el propio. Ahora se nombra por su papel respecto al portador («el rival»). Y la
    plantilla `setState` decía **«queda derribado más tiempo»** para todos los derribos del catálogo
    (`nutmeg`, `duelist`, `earthquake`…), que derriban sin alargar nada: ahora «cae derribado».
- **Revisión independiente** (Regla E, 26 sep): confirmó el A/B y la ausencia de estados colgados; sus
  hallazgos —la premisa de `INJURY`/`DEATH`, cuatro tests rotos, el disparador inerte, la descripción al
  revés y los tests que faltaban (bloqueo, eventos que no se emiten)— están todos aplicados arriba.

## Lo que queda anotado, sin arreglar

- **Concordancia de número en las descripciones**: «los rivales adyacentes cae derribado» (`earthquake`).
  Ya estaba roto con la plantilla anterior; las plantillas no tienen forma plural. Y «lesiona a el rival»
  (`dirty_play`, contracción). Anterior a este paquete.
- **Caño contra Muro** en el mismo duelo: no aplica ya (Muro no se dispara en el regate), pero `nutmeg`
  contra un Muro que entra sí puede dejar a los dos en el suelo. Sin test.
- **El pergamino sale sobre el Muro**, no sobre el que cae, y el derribo no tiene evento propio. Sin
  `visual-review`: es presentación y es el paso 2 del revisor (anticipación y lectura del balón).

## Raíces y Toque: por qué no se convierten con esta regla

- **Toque** (`elf_touch`). Una entrada **fallada ya derriba al que entra** (`ResolveTackle`, rama `else`,
  `KnockedDownTicks / 2`): el acto «se escurre y el otro cae» **ya ocurre**, y es lo que el canal
  `tackleEvasion` hace más probable. Le falta **atribución** —que el pergamino salga en la entrada que
  falla, no en todas— y eso necesita publicar el resultado de la entrada, que es otra primitiva. Añadirle
  un derribo propio duplicaría el que ya hay y cambiaría una habilidad racial (RF-031b): decisión del
  revisor. **LIKELY** que el camino correcto sea publicar el resultado de la entrada.
- **Raíces** (`roots`). El empujón no es un evento (`Bodies.AddTacklePush`), así que no hay suceso sobre
  el que disparar; «el rival rebota» es la primitiva de impulso P1 de `perks-de-cuota-a-acto.md`. Esta
  regla es necesaria para ella pero no suficiente.

## Hermanos

- [BM-B](./BM-B.md): las resoluciones publicadas antes de tirarse no miran a sus participantes
  (`charge`, `bull_rush`, `duelist`, `own_third_anchor`, `nutmeg`, `ankle_bite`). La asimetría del regate
  —se corta si cae el conductor, pero `nutmeg` sigue dando el balón al defensor derribado— es de allí.
