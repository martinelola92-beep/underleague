# BB-K — Dos jugadores del mismo equipo que quieren la misma casilla «bailan y parpadean»

**Estado:** Abierta — **causa CONFIRMED (23 sep 2026)**, arreglo pendiente de `game-design-review`

## Observación

**Dos jugadores del mismo equipo que quieren la misma casilla «bailan y parpadean»** (primera partida del
revisor). Anotado entonces como "ya pasa poco, pendiente de reproducir".

## Medición (23 sep 2026, 2000 partidos de referencia, semillas 1..2000)

Un "baile" se define como una racha de **≥ 4 inversiones de rumbo consecutivas** (el desplazamiento del
fotograma invierte el signo respecto al anterior, con ambos módulos > 0,02 casillas).

- **12 160 episodios** en 2000 partidos = **6,1 por partido**; largo medio **11,8 fotogramas** (0,8 s a
  15 ticks/s), máximo medido **48** (3,2 s).
- **144 018 fotogramas en baile** en total.
- **98,3 %** tienen un compañero a **menos de 1 casilla** (típico medido: **0,14 casillas**, es decir
  prácticamente encima).
- **94,3 %** tienen el **destino compartido** con ese compañero (distancia entre destinos < 0,5; el valor
  típico es **0,03-0,08**, o sea literalmente el mismo punto).
- **98,3 % de los episodios ocurren durante `CoverSpace`** (11 948 de 12 160). El resto es residual:
  `FindSpace` 107, `ShortPass` 45, `MarkOpponent` 33, todo lo demás ≤ 11.
- Los protagonistas son siempre **los dos defensas del mismo equipo** (ids 1-2 y 101-102 en el
  emparejamiento de referencia).

## Hipótesis

- **H1 — el destino se mantiene fijo y lo único que oscila es el cuerpo**, empujado por
  `BodySeparation`. **REJECTED**: sólo **2 256 de 12 160** episodios (18,5 %) tienen el destino estable
  (deriva < 0,25 casillas). En la mayoría el destino se mueve 2,0-2,7 casillas durante el episodio.
- **H2 — la geometría de `Zone.SegmentEntry` amplifica el movimiento del balón**: un giro pequeño de la
  recta balón→portería desplazaría mucho el punto de entrada en la zona. **REJECTED por medición
  directa**: el cociente deriva(destino)/deriva(balón) tiene **mediana 0,25 y p90 0,47** sobre 12 035
  episodios. El destino se mueve **cuatro veces menos** que el balón: `SegmentEntry` **amortigua**, no
  amplifica.
- **H3 — CONFIRMED. `EvaluateCover` no penaliza el apiñamiento, así que dos defensas con zonas
  solapadas calculan literalmente el mismo punto de cobertura y se quedan encima el uno del otro.**

  La cadena, entera y con cita:

  1. `EvaluateCover` (`Sim/Engine/Utility.cs:815-849`) fija el destino en
     `p.Zone.SegmentEntry(from, ownGoal, p.EffectiveHome, direction)`: el punto por el que la recta
     **balón → portería propia** entra en la zona del jugador. **No mira a ningún compañero.** Es la única
     acción de colocación frecuente que no lo hace: `OfferSupport` tiene `SupportCrowdedPenalty`
     (`Utility.cs:686-699`) y `FindSpace` tiene `FindSpaceCrowdedPenalty` (`:774`).
  2. Dos defensas con zonas solapadas reciben de esa fórmula **el mismo punto** (medido: 0,03-0,08 de
     separación entre destinos) y convergen sobre él hasta quedar a ~0,14 casillas.
  3. Una vez encima, `BodySeparation` (`Sim/Engine/BodySeparation.cs:87-207`) los empuja, pero el tope es
     `maxPushPerTickMilli: 60` = **0,060 casillas/tick**, mientras el paso de reaproximación es
     **0,131-0,159 casillas/tick** (`movement` en `data/sim/tuning.json`). **Se vuelven a juntar 2,5 veces
     más rápido de lo que la separación los aparta.**
  4. El empuje **no entra en `Velocity`** (`BodySeparation.cs:183-188`, deliberado) ni realimenta la
     decisión, así que la utilidad del tick siguiente no sabe que le han empujado y vuelve a mandarlo al
     mismo punto. No hay histéresis: cada jugador reelige desde cero cada `decisionIntervalTicks: 2`
     (`MatchEngine.cs:857-861`).

  El balón se mueve (3-7 casillas durante un episodio típico), los dos defensas siguen juntos el mismo
  punto de cobertura, y el "parpadeo" que ve el revisor es el empuje de separación entre dos cuerpos
  solapados que nunca llegan a separarse.

**Nota de confusión detectada y anotada**: se midió que el 98,7 % de los bailes son entre ids de **paridad
distinta** (deciden en ticks alternos por `(_tick + player.Id) % 2`, lo que sugeriría contrafase
estructural). **No se puede concluir nada de ese dato**: los dos compañeros de la misma posición llevan
siempre ids consecutivos por cómo se generan los equipos, así que la paridad distinta está garantizada por
construcción, no por el mecanismo. Queda como hipótesis **sin aislar**.

## Arreglo candidato

El repositorio ya tiene **dos precedentes propios** para esto, y la regla del proyecto es mirarlos antes de
inventar una abstracción:

- la reserva `taken[]` de `Marking.AssignTeam` (`Sim/Engine/Marking.cs:46-88`), que reparte rivales por id
  ascendente sin que dos defensores se queden con el mismo;
- la exclusión dura del **perseguidor designado** (`Utility.cs:592-596`), un solo jugador por equipo puede
  elegir `ChaseBall`.

Lo más barato y más en la línea del motor es **dar a `CoverSpace` la penalización de apiñamiento que ya
tienen `OfferSupport` y `FindSpace`**. Pero hay un matiz medido que importa: esas dos penalizaciones se
calculan sobre la **posición actual** del compañero (`TeammatesNear`, `Utility.cs:1067-1085`), **no sobre
su destino**, así que sólo castigan *después* de haberse juntado — que es exactamente la firma de una
oscilación. Penalizar por **destino** (o repartir el punto de cobertura por id ascendente, como
`Marking`) ataca la causa; penalizar por posición sólo cambia la forma del baile.

Es un cambio de pesos de IA y de una acción del motor: **`game-design-review` primero, luego
`balance-measure` con baseline** (RT-096, ADR 0033).

## Nota de diseño (`game-design-review`, 23 sep 2026)

**No es una mecánica nueva: es cerrar una inconsistencia.** De las tres acciones de colocación,
`OfferSupport` y `FindSpace` ya miran a los compañeros; `CoverSpace` —la más usada en defensa, el 98,3 %
de los bailes— no. No hay que inventar una regla, hay que aplicar la que ya existe donde falta.

1. **Qué experimenta el jugador.** Dos defensas pegados temblando, 6 veces por partido, 0,8 s de media y
   hasta 3,2 s. Parece un error gráfico. Y por debajo hay una consecuencia real de juego: **dos jugadores
   ocupan el sitio de uno**, así que media defensa desaparece y el otro lado queda abierto.
2. **Qué decisión toma hoy con esto.** Ninguna: es un artefacto. Pero la pantalla de Equipo ya le enseña
   un `CoverageMap` con huecos y solapes (`Sim/Placement/PlacementView.cs:28-58`), o sea que **el juego le
   promete una cobertura que el motor no respeta**.
3. **Qué decisión debería tomar.** Que su colocación decida quién cubre qué, y que colocar dos defensas
   con zonas solapadas se note como lo que es —un jugador desperdiciado— y no como un temblor.
4. **Qué regla representa.** La cobertura de zona de `docs/simulacion.md` §2.2 y, de refilón, RF-012d: hoy
   el equipo defiende peor de lo que su colocación anuncia, y eso no es previsible mirando la pantalla.
5. **Sistemas.** Sólo `/Sim/Engine/Utility.cs` (`EvaluateCover`) y un peso en `/data/ai/weights.json`.
   Nada de `/Game`: no hay lógica repartida que pueda contradecirse (RT-014).
6. **Alternativas.**
   - **A — penalizar por posición actual**, como las dos hermanas. Barata y consistente, pero ya está
     medido que eso sólo castiga *después* de juntarse: cambiaría la forma del baile, no lo quitaría.
   - **B — penalizar por destino**: si el punto de cobertura coincide con el de un compañero, penaliza.
     Ataca la causa.
   - **C — repartir el punto por id ascendente**, calcado de `Marking.AssignTeam` (`Marking.cs:46-88`):
     el primero se queda el punto, el siguiente coge el mejor que quede. Precedente directo en el motor y
     determinista por construcción (RT-041).
   - **D — histéresis / inercia**, lo que ya propone [BC-G](./BC-G.md). Quita el temblor pero no el
     solapamiento: dos defensas seguirían cubriendo el mismo punto, sólo que quietos.
   - **E — separarlos en `/Game`**. Rechazada: esconde el síntoma y deja intacta la pérdida de cobertura.
7. **Trade-off.** Si dos defensas ya no pueden cubrir el mismo punto, el segundo hace otra cosa. Eso
   **debilita el doblaje** en los casos donde doblar sí tenía sentido. Es un coste legible y deseable: la
   colocación pasa a importar.
8. **Cómo cambia las estrategias.** Colocar con zonas solapadas deja de ser gratis, y el `CoverageMap` de
   la pantalla de Equipo pasa a significar algo. Es una sinergia con una herramienta que ya existe.
9. **Degeneración posible.** La de verdad: una penalización fuerte puede empujar al segundo defensa fuera
   del centro y abrir un pasillo por el medio. Hay que mirarlo en `goalsPerMatch`, `ballThirdMaxShare` y
   las puertas de build, no sólo en el contador de bailes.
10. **Cómo se demuestra.** (a) la sonda de oscilación de este fichero —12 160 episodios— tiene que bajar
    de forma clara; (b) lote de `/Balance` contra baseline del mismo árbol, mirando **todas** las métricas
    (lección de `ChaseBall pen=50`); (c) las 43 puertas.

**Recomendación: C, con B como respaldo.** Es la que tiene precedente propio en el motor, la que es
determinista sin esfuerzo y la única que ataca el solapamiento en vez del temblor.

**No se implementa en el mismo hito que [BB-O](./BB-O.md)**: cambia comportamiento defensivo real y pide
su propio baseline y sus 43 puertas. Es un hito aparte.

## Hermanos

- [BC-G](./BC-G.md) — misma familia, ya medida: ciclo `ChaseBall`/`Retreat` **sin histéresis**, equilibrio
  a ~2,3 casillas, avanza y retrocede cada 2-4 ticks (semilla 216, id 104). Allí ya se propone "inercia de
  `ChaseBall` mientras ya se persigue".
- AW-S (`docs/pendientes.md`, cerrada) — la decisión explícita de **no** poner histéresis en esta pasada,
  con la referencia de que gfootball exige que un aspirante sea 20 % mejor para relevar al titular. La
  deuda está documentada en `Utility.cs:584-590`.
- [BF-C](./BF-C.md) — también sobre alternativas de utilidad mal ordenadas fuera de posesión.
