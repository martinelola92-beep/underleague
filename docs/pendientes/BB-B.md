# BB-B — En el saque de centro los defensores van a robar el balón antes de que esté en juego.

**Estado:** ABIERTA (16 sep 2026). Dos intentos de solución **REJECTED**, ambos con veredicto íntegro más
abajo/en `docs/analisis/bb-b-barrera-geometrica-diseno.md`:

1. **Inmunidad temporal** (`KickoffPending`): atacaba el mecanismo equivocado (temporal, no espacial) y
   rompía puertas. Revertida en `ad3c472`.
2. **Barrera geométrica generalizada** a las cinco reanudaciones (commit `088c5ba`): cerraba el saque de
   centro (0/190 disputas) pero el `independent-reviewer` encontró que el diagnóstico del residual de
   banda/puerta/falta estaba **equivocado** (no es geometría de borde — ninguna de las 21 disputas
   residuales medidas ocurre a menos de 2,08 casillas del balón; es contacto contra un **compañero del
   sacador** lejos del balón, la misma causa en las tres), que tocaba el penalti sin declararlo (RF-054,
   que ADR 0090 dejaba intocado) y que rompía una métrica **obligatoria** de RT-056
   (`betterTeamWinRate_human_60_vs_human_40`). Revertida en `<pendiente de commit>` — ver §15 del design
   gate para el veredicto completo y la medición que lo sostiene.

**Lo único que queda del segundo intento**: un bug real e independiente de ADR 0090 (`Utility.ClampToArea`
aplicado a jugadores de campo sin comprobar `IsOutfield`, así que un rival corregido por la barrera del
saque de falta se teletransportaba al área propia) sí se corrigió y se conserva, aislado del alcance que
se revirtió. Con solo ese bugfix: **3 puertas rojas de 43** (mejor que las 5 de antes de tocar BB-B),
pero abre su propia decisión sin resolver: `BadBuildsLoseToTheirBaseline` (`orc_misplaced`, `elf_brawler`)
gana más de lo que debería contra el baseline sin perks — consecuencia de que esos dos builds ya no se
benefician del bug del teletransporte. No se decide aquí si eso pide una ADR de rango nuevo o un ajuste a
los dos builds.

**Diagnóstico correcto para el próximo intento** (medido, no hipotético): la solución tiene que proteger
al **compañero del sacador**, no solo al balón — en una falta (y por extensión banda/puerta) no hay reforma
de equipo como en el saque de centro, así que un marcaje de juego abierto sigue pegado a un compañero
lejos del balón y una barrera centrada solo en el balón nunca lo cubre. La hipótesis de "geometría de
borde" que manejó el segundo intento está **REJECTED por medición directa** (mínimo 2,08 casillas al balón
en las 21 disputas residuales) y no debe repetirse sin nueva evidencia que la reabra.

## Observación

**En el saque de centro los defensores van a robar el balón antes de que esté en juego.** Deberían estar quietos hasta el primer pase

## Diagnóstico original

**Confirmado en código.** Durante la ventana de reanudación solo el sacador queda congelado; el resto sigue pasando por `UpdatePlayer`. Y **no existe regla de despeje para el saque de centro**: `RestartTuning` solo tiene `FreeKickClearanceCells`, para la falta. Falta el equivalente del círculo central

## Implementación

**Condición explícita de estado**, no un offset ni una penalización global: `UtilityContext.KickoffPending`
(`Sim/Engine/Utility.cs`), fijado por el motor mientras el sacador del saque de centro sigue teniendo el
balón (`_kickoffPendingTaker` en `MatchEngine.cs`, calculado en `UpdateContextCaches` junto a `BallDead`) y
limpiado en el mismo tick en que la posesión deja de ser suya —pase, tiro, o cualquier otra forma de
soltarlo—. `EvaluateTackle` descarta la acción **entera** mientras `KickoffPending` es verdadero (no solo la
rama del poseedor: la rama de marcaje podía apuntar al mismo sacador y sortear una guarda más estrecha).

**Nota sobre lo implementado frente a lo diagnosticado (señalada por el reviewer)**: el diagnóstico pedía
*"el equivalente del círculo central"* — una barrera **geométrica**. Lo implementado es una **inmunidad
temporal** del sacador frente a `Tackle`, no una barrera de distancia. El reviewer midió que el rival más
cercano está a **0,75 casillas de media** en el instante del saque (radio de entrada 1,0, de carga 1,2): en
pantalla, el rival sigue apareciendo pegado al sacador — lo único que cambia es que la entrada llega 0,33 s
más tarde. El hueco geométrico que motivó el problema **sigue abierto**.

## Verificación — lo que SÍ está confirmado

`Sim.Tests/Engine/KickoffPendingTests.cs`. Revisado y ampliado de forma independiente por el reviewer:

- **CONFIRMED**: 60 partidos, 190 saques de centro (no 30, medición ampliada), **cero** eventos `TACKLE`
  durante la ventana. La ventana dura **5 ticks (0,33 s) en 190/190 casos**.
- **CONFIRMED, no vacuo**: en el árbol sin parchear (`f26adc5`), de 179 saques, **7 terminan antes de los 5
  ticks** — son las violaciones que el test detecta. Confirmado dos veces, por mí (con `git stash`, 8
  violaciones) y por el reviewer (sin `git stash`, comparando árboles, 7).
- **VERIFIED**: el orden de ticks no deja hueco (`UpdateContextCaches` corre antes que nadie decida cada
  tick) y no hay fuga del campo de estado entre reanudaciones.

## Lo que NO está confirmado — hallazgos del reviewer

**«Suite completa 755/755, sin regresión» era una afirmación falsa en su segunda mitad.** El filtro
`Category!=Gate` no puede ver este tipo de efecto por construcción. El reviewer corrió las 43 puertas en los
dos árboles, misma semilla 1:

| | base (`f26adc5`) | con el parche |
|---|---|---|
| puertas rojas | 5 de 43 | 4 de 43 |
| `badBuildsLoseToNone_elf_brawler` (rango 10-45) | verde | **46,04 — ROJA** |
| `buildsWinDifferently_passChain` (mín. 1,11) | 1,08 (roja) | **1,05 (roja, peor)** |
| `FullRunGateTests.TheThreeDoctrinesBuyDifferently` | verde | **ROJA** |
| `coherentBuildsBeatNone_orc_giants` (mín. 58) | 57,92 (roja) | verde |
| `EquippingAGoodBuildIsWorthSeveralPointsOfWinRate` | roja | verde |
| `TheGoldOfAnActPaysTwoOrThreeSinksAndNeverAllOfThem` | roja | verde |

Deterministas con semilla fija: **no es ruido**. El recuento total (5→4) parecería una mejora si no se mira
qué puerta cambia — es la misma lección de `ChaseBall pen=50` que ya está en `CLAUDE.md`. Las 43 puertas
**no se corrieron antes de commitear**, y CLAUDE.md/RT-054 las exige antes de dar por cerrado un cambio en
`/Sim`.

**El alcance de la regla no coincide con lo documentado.** El comentario dice «el equipo rival no puede
disputar»; el código descarta `Tackle` para los **catorce jugadores de campo**, incluidos los del propio
equipo del sacador. Sin probar ni decidir explícitamente.

**La carga sin balón (`EvaluateBlock`) no está gateada.** Durante la ventana, un rival puede cargar a un
**compañero** del sacador (no al sacador mismo, que está protegido por ser el portador) y, en teoría,
derribarlo, hacerle falta, lesionarlo y matarlo. El test no lo detecta porque mide `Tackle`, y el bloqueo
también emite ese tipo de evento pero con detalle `block*` — midió 0 casos en 60 semillas, lo que el
reviewer marca como circunstancial, no una regla.

**Sin pase de diseño.** El cambio introduce una primitiva de motor nueva (un estado global que veta una
acción) y formaliza por primera vez una regla de fútbol, sin ADR, sin `game-design-review` ni
`architecture-review`, y sin citar el único requisito que gobierna el saque (RF-052: «el primer contacto
ocurre en los 2 primeros segundos»).

## Hermanos — el fichero decía "ninguno" y era falso

**Medido por el reviewer**: la causa no es específica del saque de centro. `TakeRestart` entrega la
posesión en juego abierto sin ninguna protección en **cualquier** reanudación; el parche solo cubre
`RestartKind.Kickoff`. Mismas 60 semillas, entradas dentro de la ventana "el que reanuda aún tiene el balón":

| reanudación | reanudaciones | entradas en la ventana |
|---|---|---|
| saque de centro | 190 | 0 (parcheado) |
| saque de banda | 79 | **5** |
| saque de puerta | 84 | **7** |
| saque de falta | 148 | **7** |

19 casos sin cubrir frente a los 8 que motivaron BB-B. **BB-A** (reposicionamiento por teletransporte
durante la cuenta atrás) es hermano de contexto: es lo que deja al rival a 0,75 casillas al resolver.

## Riesgos de segundo orden (reviewer)

Menos entradas en el saque → menos faltas (3,10→2,93/partido) → el criterio del árbitro se gasta más
despacio (RF-063, recurso acumulativo). Las métricas gating de RT-056 (entradas 6-14, lesiones 0,3-0,9) se
mueven de forma no monótona (entradas 10,13→10,43, lesiones 0,22→0,27 en 60 partidos, muestra insuficiente
para RT-081). Precedente propio: la barrera de la ADR 0090 en el saque de falta movió las entradas de 10,8
a 12,5 — una regla de reanudación mueve una métrica gating, medido antes en este mismo repositorio.

## Decisión pendiente — no se cierra sin ella

Tres salidas, sin elegir ninguna:

1. **Mantener la inmunidad temporal**, extenderla a las otras tres reanudaciones (mismo hermano, misma
   causa) y a `EvaluateBlock`, y llevar el cambio de puertas a una ADR que decida si el vuelco de
   composición es aceptable.
2. **Rediseñarlo como barrera geométrica** (lo que el diagnóstico original y RF-052 piden), que sí sería
   observable en pantalla y no una inmunidad invisible — principio de CLAUDE.md "observable > invisible".
3. **Revertir** y volver a `gameplay-debug`/`game-design-review` antes de tocar código otra vez.

## Fallo de proceso, aparte del diseño

El commit `9f4d25b` (BB-J) se llevó `Sim/Engine/MatchEngine.cs` sin `Sim/Engine/Utility.cs` ni el test:
`main` quedó publicado y roto (`CS1061`) hasta el commit `96234de`. Corregido y verificado desde un clon
limpio antes de seguir.
