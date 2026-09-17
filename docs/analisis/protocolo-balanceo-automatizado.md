# Protocolo operativo de balanceo automatizado de perks

**Fecha:** 17 sep 2026, **revisado** el mismo día tras una revisión crítica del propio protocolo (§0.1).
**Estado:** diseño, sin código ni `/data` tocados. Responde al encargo de cambiar el objetivo del piloto
C1/Cazagoles: de "cerrar un perk" a "diseñar el sistema que balancea cualquier perk del catálogo". Los
resultados de Cazagoles (17/24/48%, `docs/analisis/c1-piloto-cazagoles-diseno.md` §4-§7) se usan aquí
como **evidencia de calibración** del sistema — tamaños de muestra, umbrales de exposición, forma de la
relación valor→efecto —, no como decisión definitiva de ese perk. Ancla y Cazagoles siguen sin cerrarse.

## 0. Cómo se lee este documento

Sigue el orden del encargo (seis fases del protocolo, medición rápida, niveles de evidencia,
autogeneración de conclusiones, estrategia de búsqueda, tipos de perk, métricas, criterios de parada,
sobreajuste, local/sistémico, catálogo completo, automatización, multiagente) y cierra con las cuatro
preguntas operacionales (A-D) y la pregunta de velocidad. Cada umbral numérico está marcado
**[MEDIDO]** (viene de un experimento de esta sesión — y, tras la revisión, se precisa si mide *tiempo*
o *suficiencia estadística*, que no son lo mismo, §5.5), **[DERIVADO]** (se calcula de una medición ya
hecha), **[CONVENCIÓN ESTÁNDAR, NO CALIBRADA AL PROYECTO]** (una fórmula o multiplicador estadístico
habitual, razonable pero no verificado contra el ruido propio del motor — nuevo tras la revisión, distinto
de una asunción inventada) o **[ASUNCIÓN — PENDIENTE DE CALIBRAR]** (no hay evidencia suficiente todavía;
se dice qué falta y, tras la revisión, si 2-3 mediciones bastan o el hueco puede no reducirse nunca a un
número único). Regla F del proyecto: nada se escribe como certeza sin la evidencia que lo sostiene.

### 0.1 Qué cambió en la revisión del 17 sep 2026

Revisión crítica pedida explícitamente antes de implementar tooling, sin nuevos experimentos (todo lo de
abajo sale de releer lo ya medido o de inspeccionar `data/perks/*.json` con un script, no de simular
nada nuevo):

- **§5.5 (nueva)**: separa límite de tiempo, cantidad de muestra, potencia estadística y suficiencia de
  evidencia — el borrador original los mezclaba bajo la misma etiqueta `[MEDIDO]`. Añade una comprobación
  de potencia real (`error_estándar`) al bucle de Tuning/Validation.
- **§5.4**: los tres suelos `[ASUNCIÓN]` se revisan al alza (más conservadores) y se corrige qué hace el
  sistema mientras no están calibrados — nunca un estado terminal con la confianza de un umbral medido.
  Se precisa cuáles necesitan de verdad más perks (exposición discreta, sí) y cuál puede no tener nunca un
  número único (fidelidad de diseño).
- **§3/§3.1 (nueva)**: la taxonomía se verificó contra los 94 perks reales (`EffectType`, `ProbabilityKind`,
  `TraitScalarKind`, `Limit`, `AccumulatesAcrossMatches`, perks con más de un efecto) — se corrigen tres
  afirmaciones que no encajaban con el dato real (`addCounter` no siempre escala a run; `modifyAttribute`
  no tiene una métrica única; 10 de 13 `TraitScalarKind` no tienen ningún perk real hoy) y se añade la
  matriz de cobertura.
- **§6.1/§6.4/§9**: nuevo estado `INSUFFICIENT_EVIDENCE` (umbral sin calibrar) distinto de
  `INSUFFICIENT_EXPOSURE` (umbral medido); `BALANCED` se reescribe como conjunción explícita de las siete
  condiciones del encargo, no como "pasó RT-056"; Tuning pasa a ser opcional (perks sin parámetro numérico
  van de Screening a Validation cualitativa directamente); nuevo estado `RUN_LEVEL` para perks de
  contador/campaña (no es un fallo, es otro instrumento).
- **§9.1 (nueva)**: presupuesto de lote completo (checkpoint/reanudación, circuito de seguridad si escala
  una fracción alta de perks), que el borrador original no tenía — solo había límites por perk.
- **§11.1 (nueva)**: hace explícito que el camino crítico es cero agentes; retira una cifra optimista
  ("70-80% sin incidencias") que no estaba medida.
- **§13**: de seis a nueve piezas pendientes, con el paso de "confirmación de métrica" (nuevo, de la
  matriz de cobertura) y la distinción `Limit`-vs-exposición como huecos explícitos.

---

## 1. Inspección del sistema multiagente existente

### 1.1 Qué es

`~/ai-orchestrator` (binario `ai`, Python 3.12 puro) es un **router de tareas de programación en lenguaje
natural**: clasifica una tarea (`TRIVIAL`..`ARCHITECTURAL` × `feature/bugfix/refactor/tests/docs/chore/
review/question`), la reparte en un pipeline fijo —`architect` (opcional) → `implement` → `tests` →
`review` (preferentemente de un proveedor distinto al implementador) → `debug` (solo si algo falla,
acotado por `max_loops`, 1-3 según nivel) → `final_review` (opcional, modelo fuerte)— y entrega un
informe. Cinco adaptadores de modelo (`claude`, `codex`, `opencode`, `agy`, `mock`), enrutados por coste
(`local/free/quota/low/paid`), privacidad (`may_train` con allowlist explícita por repositorio) y tier de
capacidad. **Nunca commitea ni publica**: aplicado en tres capas independientes (`AI_WORKFLOW.md` §1),
deja el árbol modificado + `SUMMARY.md`, con snapshot de git antes/después en `refs/ai/runs/<id>/`.

Instalado el 16-17 sep 2026 (siete commits en `~/ai-orchestrator`, el más reciente "revisión factual y
reviewer normal en tareas de documentación"), es un proyecto personal reciente, con sus propios tests
(§15 de `AI_WORKFLOW.md`) y sin relación de código con Underleague.

### 1.2 Qué NO es

**No tiene ningún concepto de experimento numérico iterativo.** El único bucle del pipeline
(`_verify_loop`, `aiorch/pipeline.py:631`) es "repite implementar→depurar hasta que los tests/la review
pasen o se agote `max_loops` (1-3)" — un bucle de **corrección de un diff que falla**, no de "prueba un
valor, mide el efecto, decide el siguiente valor". No sabe qué es un lote de `/Balance`, una semilla, una
muestra ni una banda RT-056: eso lo tendría que **inventar** un prompt de `implement`/`debug` cada vez,
sin memoria entre invocaciones ni noción de presupuesto de simulación.

### 1.3 Estado en Underleague

**No está integrado.** No existe `.ai/` en `main` (`git log --all -- .ai` solo aparece dentro de refs
`refs/ai/runs/...`, inalcanzables desde ninguna rama). Hay **un único ensayo real**, el 16 sep 2026: una
tarea trivial ("añade a `docs/entorno.md` un párrafo") con un `.ai/project.toml` que mapea los comandos
genéricos del orquestador (`test`, `build`) a los reales de Underleague (`dotnet build`/`dotnet test
--filter "Category!=Gate"`, copiados de `build-and-test/SKILL.md`). Ese fichero nunca llegó a `main`.

### 1.4 Por qué no se usa el flujo actual

No hay ningún indicio de que haga falta: `CLAUDE.md` ya define su propio principio "10-80-10" con
subagentes propios de Claude Code (`fast-worker`, `deep-reasoner`, `independent-reviewer`, `Explore`,
`.claude/agents/`), ya calibrados para este proyecto (leen `CLAUDE.md`, citan RF/RT, conocen Regla A-F).
`ai-orchestrator` resuelve el mismo principio general (modelo barato implementa, modelo distinto revisa)
pero como una capa **externa y genérica**, sin el contexto de dominio que ya tienen los agentes de
`.claude/`. Enrutar una tarea de Underleague a través de `ai` para que un `claude-sonnet` la implemente
sería, en la práctica, Claude Code invocando Claude Code por un envoltorio de subproceso — sin ganar nada
que el `Agent` tool no dé ya directamente, y perdiendo el contexto de sesión.

### 1.5 Decisión de arquitectura para el sistema de balanceo

**No se reconstruye un router.** El bucle de medición (screening/tuning/validación) es código
determinista (§5), no una tarea para que un LLM "implemente". El sitio donde SÍ hay valor en delegar
razonamiento es exactamente donde ya lo delega este proyecto: `.claude/agents/` de Underleague, no
`ai-orchestrator`, por las razones de 1.4. `ai-orchestrator` queda como una herramienta **externa,
opcional, de segundo orden**, para dos usos que no están en el camino crítico:

- **Construir el propio tooling determinista** (§12) como tarea mecánica de código, si en algún momento
  se prefiere delegarla en vez de escribirla directamente — el propio `ai` ya sabe ejecutar
  `dotnet build`/`dotnet test` en Underleague (§1.3).
- **Un segundo lector de proveedor distinto** en el momento de aceptar un valor final (no en cada
  iteración): el propio router prioriza `prefer_independent_reviewer` con un proveedor distinto al
  implementador — útil como diversidad de modelo adicional al `independent-reviewer` de Claude Code, pero
  opcional y nunca en el bucle de medición, cuyo coste (latencia de LLM, enfriamiento de cuota) es
  exactamente lo que el encargo pide evitar ("no convertir una tarea determinista en una conversación
  entre agentes").

No se usa en esta primera versión del sistema. Se documenta como opción, no como dependencia.

---

## 2. Principios rectores (de los que ya existían + los nuevos de este encargo)

1. **Medir barato antes de decidir** (Regla A, ya aplicada en Tanda 0/C1): código determinista mide;
   la IA interpreta solo donde hace falta juicio.
2. **Dato → interpretación → regla → decisión**, nunca mezclados: un umbral se fija antes de ver el
   resultado que va a evaluar (mismo principio que §4/§7 de C1).
3. **La rapidez viene de una estrategia de muestreo, no de relajar criterios** (instrucción explícita).
4. **No se cierra un perk sin las mismas garantías que ya se exigían a mano**: RT-056 completo,
   determinismo, sin `BuildsWinDifferently` como criterio salvo que el perk esté en una build de
   referencia, `MinPassChainRatio` nunca se toca por este sistema.
5. **Todo lo que se pueda convertir en regla determinista, se convierte.** La IA solo entra donde el
   encargo la pide explícitamente (§11).

---

## 3. Taxonomía de perks (derivada del catálogo real, no inventada)

`Sim/Perks/PerkDefinition.cs` define 19 `EffectType`; `data/perks/*.json` usa hoy 17 de ellos en 94
perks (`grep` sobre el catálogo, 17 sep 2026):

| `EffectType` | usos hoy | qué mide el balanceo |
|---|---|---|
| `modifyProbability` | 54 (34 perks solo, 14 combinados con `addCounter`, resto en otras combinaciones) | el `ProbabilityKind` que toca decide la familia de métricas (§4). **Cobertura real de `ProbabilityKind`**: 11 de los 13 valores tienen al menos un perk (`Tackle` 14, `ShotOnTarget` 9, `Intercept` 7, `Injure` 6, `Dribble` 4, `Pass` 3, `SevereInjury` 3, `TackleEvasion` 3, `Save` 2, `Injury` 2, `InterceptEvasion` 1); **`Foul` y `Card` no tienen ningún perk hoy** — su mapeo a `foulsPerMatch`/tarjetas (§4.2) es teórico, sin verificar contra un dato real |
| `addCounter` | 23 usos, pero **solo 6 perks lo usan solo** (`loan, box_office, ad_machine, local_idol, inheritance, life_insurance`); **14 perks lo combinan con `modifyProbability`** en el mismo perk | corregido en esta revisión (ver abajo): el `addCounter` en solitario es de campaña/run; combinado con `modifyProbability`, el contador es un efecto secundario que se registra pero NO bloquea medir la parte de partido suelto |
| `modifyLeash`, `modifyZoneShape`, `shiftHome` | 5+1+4 (1 perk combina los dos últimos: `deep_run`) | geometría de zona de acción → `ballThirdMaxShare`, distribución espacial |
| `modifyAttribute` | 5 (3 solos, 2 combinados con `addCounter`) | **no hay una única métrica**: depende de qué atributo — `Strength`→entradas/potencia de tiro, `Technique`→calidad de pase/tiro, `Speed`→coberturas/intercepciones, `Stamina`→fatiga, `Leash`→geometría. Corregido en esta revisión: antes decía "indirecto" sin más, ahora exige que la clasificación (§11, `fast-worker`) diga explícitamente qué atributo y por tanto qué fila de esta lista aplica |
| `immunity` | 5 | cancela un tipo de suceso → tasa de ese suceso. Sin parámetro numérico (§6.5) |
| `modifyTraitScalar` | 4, pero **solo 3 de los 13 `TraitScalarKind` declarados tienen un perk real** (`injuryChanceBonus` ×2, `hardTackleBonus` ×1, `shootRangeBonusCells` ×1) | la métrica del escalar donde ya hay precedente (p. ej. `injuryChanceBonus`→`injuriesPerMatch`); para cualquiera de los **10 escalares sin instancia hoy** (`SpeedBonusPercent`, `ShotQualityBonus`, `PassQualityBonus`, `FoulChanceBonus`, `FatigueResistancePercent`, `InjuryResistanceBonus`, `AdjacentTeammateBonusPercent`, `SaveBonusClose`, `SaveBonusFar`, `LeashBonus`) el mapeo es una inferencia razonable, no un dato verificado — un perk nuevo con uno de estos diez pasa primero por confirmar la métrica (§6.5 ampliado) antes de Screening |
| `cancelEvent` | 4 | `FOUL`/`CARD`/`INJURY` cancelado → tasa de ese evento. Sin parámetro numérico |
| `modifyMarkBias`, `modifyTackleBias` | 3+2 | lógica de selección de objetivo, no un escalar — behavioral audit, no búsqueda numérica (§6.5) |
| `modifyBias` | 2 | sesgo del criterio arbitral hacia un equipo (familia "El Árbitro" del catálogo conceptual) → `foulsPerMatch`/tarjetas **por equipo**, no agregado — el agregado de RT-056 no distinguiría el sesgo |
| `extraAction` | 3 | repetición de acción → frecuencia de esa acción, ya la cuenta el motor. Ninguno de los tres declara hoy un valor numérico propio — sin parámetro que buscar |
| `setState`, `relocate`, `modifyKnockdownTicks`, `modifyExperience`, `injure` | 1-2 c/u | casos singulares, se tratan uno a uno, capa común de §4.1 |
| `modifyUtility` (C1, nuevo) | 0 (inerte) | acción + zona opcional → histograma de acción restringido a exposición (mismo instrumento de Tanda 0) |

**Estructuras que no aparecían en el borrador original y sí importan** (verificado con los 94 perks
reales, 17 sep 2026, `python3` sobre `data/perks/*.json` — sin simular nada):

- **27 de 94 perks (29%) tienen más de un efecto.** La categoría de un perk no es "una fila de la tabla",
  es la **unión** de las categorías de sus efectos. La mayoría (14) son el caso fácil de arriba
  (`addCounter` + `modifyProbability`, el contador no compite con la medición). Un caso cruza dos
  categorías de verdad — `unlikely_bulwark` (`modifyLeash` + `modifyProbability`, geometría + probabilidad
  a la vez) — y ahí la clasificación (§11) tiene que decir explícitamente cuál de los dos efectos es el
  parámetro objetivo de la búsqueda (normalmente el que la ficha describe como rasgo distintivo), no
  asumirlo.
- **`elseEffects`: 0 de 94 perks lo usa hoy.** El campo existe en el esquema (`PerkDefinition.ElseEffects`)
  pero ningún perk vivo lo activa — medir las dos ramas de un condicional es un hueco de diseño del
  protocolo, pero no urgente: cero perks lo necesitan hoy.
- **21 de 94 perks tienen `Limit`** (activación acotada, p. ej. una vez por partido). Ya corregido en
  §5.4: un `Limit` puede producir activación baja **por diseño**, no por exposición insuficiente — el
  suelo de exposición debe leerse sobre oportunidades, no sobre activaciones ya limitadas.
- **23 de 94 perks tienen `AccumulatesAcrossMatches = true`** — casi coincide con los `addCounter`, pero
  es el campo correcto para la regla determinista ("¿este perk necesita medición de *run*, no de
  partido?"), no el tipo de efecto: es un booleano explícito en el dato, más fiable que inferirlo del
  `EffectType`.

**Categorías de balanceo** (no de tipo de dato, de *qué hay que medir*), revisadas:

- **Bonus/malus de utilidad** (`modifyUtility`): categoría de C1/Cazagoles. Métrica primaria = histograma
  de acción restringido a exposición. Búsqueda: tripleta anclada (§7).
- **Probabilidades de resolución** (`modifyProbability`, sola o combinada con `addCounter`): la mayoría
  del catálogo hoy. Métrica primaria = tasa del suceso (`ProbabilityKind`) armado vs control. Búsqueda:
  tripleta + detección de meseta (§7).
- **Escalares de rasgo** (`modifyTraitScalar`): métrica primaria = la del escalar, donde ya hay precedente
  (3/13); confirmación de métrica requerida para los otros 10. Búsqueda: bisección lineal.
- **Atributo base** (`modifyAttribute`): métrica primaria según el atributo concreto (tabla de arriba,
  nunca "indirecto" sin más). Búsqueda: bisección sobre el delta.
- **Geometría/posición** (`modifyZoneShape`, `shiftHome`, `modifyLeash`): métrica primaria =
  `ballThirdMaxShare` + distribución espacial. Búsqueda: bisección entera (RT-023, aritmética entera).
- **Selección de objetivo** (`modifyMarkBias`, `modifyTackleBias`): no numérico — behavioral audit, sin
  búsqueda de valor (§6.5).
- **Sesgo arbitral** (`modifyBias`): métrica por equipo, no agregada. Búsqueda: bisección sobre el delta.
- **Sucesos binarios** (`cancelEvent`, `immunity`, la mayoría de `extraAction`): sin parámetro numérico —
  Screening → Validation cualitativa directa (§6.1 revisado), sin pasar por Tuning.
- **Contadores/campaña** (`addCounter` en solitario, y cualquier perk con `AccumulatesAcrossMatches =
  true`): **no es un perk bloqueado ni fallido** — necesita otro instrumento (`FullRunMetrics`, el mismo
  que ya usa `/Balance --full-runs`), fuera del bucle rápido de partido suelto por diseño, no por defecto
  del sistema. Estado propio, `RUN_LEVEL` (§9), distinto de `BLOCKED_INFRA`.
- **Casos singulares**: sin volumen suficiente para categoría propia; capa común de §4.1, sin estrategia
  de búsqueda automática salvo que se acumule más de un perk del mismo tipo.

No se listan categorías de C2/C9/C16 (situación, posesión) porque **no existen todavía como primitiva
del motor** salvo la única cláusula de C2 que ya usa C1 (`Pitch.ZoneOf`, la zona). Un perk que las
necesite entra en el estado `BLOCKED_INFRA` (§9), no en el bucle.

### 3.1 Matriz de cobertura (para demostrar que el catálogo entero tiene una estrategia, no para balancearlo)

| Tipo/combinación | # perks | Estrategia | Métricas | Búsqueda | Estado inicial |
|---|---|---|---|---|---|
| `modifyProbability` solo | 34 | Bono/malus de probabilidad | Tasa del `ProbabilityKind` + universales | Tripleta + meseta | `SCREENING` |
| `modifyProbability` + `addCounter` | 14 | Igual; contador solo se registra | Igual + nota de contador | Igual | `SCREENING` |
| `addCounter` solo (6) / `AccumulatesAcrossMatches` (23) | 6-23 según el corte | Medición de run, no de partido | `FullRunMetrics` correspondientes | Ninguna en este sistema | `RUN_LEVEL` (nuevo, §9) |
| `immunity` | 5 | Cancela un suceso | Tasa del suceso + universales | Ninguna (binario) | `SCREENING` → `VALIDATING` directo |
| `cancelEvent` | 4 | Igual | Igual | Ninguna | `SCREENING` → `VALIDATING` directo |
| `modifyAttribute: Strength/Leash` | 4+0 | Depende de la resolución que enfatiza la ficha (§13.4.1) | `tacklesPerMatch`/`injuriesPerMatch` o `ballThirdMaxShare` | Bisección | `SCREENING` |
| `modifyAttribute: Stamina` | 1 | Rendimiento por fase de partido | **Ninguna existe** (§13.4.1) | — | `NOT_READY` (hueco de tooling, no de dato) |
| `modifyAttribute: Speed` | 0 hoy | Cobertura/posicionamiento | `ballThirdMaxShare`/`saveRate`, sin precedente | Bisección | `SCREENING`, marcado `INFERIDO, SIN CONFIRMAR` |
| `modifyAttribute: Technique` | 0 hoy | Calidad de pase/tiro | `passCompletionRate`/`shotsOnTargetShare`, sin banda | — | `DESIGN_REVIEW` (falta fijar banda) |
| `modifyLeash`/`shiftHome`/`modifyZoneShape` | 5/4/1 | Geometría | `ballThirdMaxShare` + espacial | Bisección entera | `SCREENING` |
| `modifyTraitScalar`: `InjuryResistanceBonus`/`LeashBonus` | 0 hoy | Métrica ya bandeada por categoría | `injuriesPerMatch`/`ballThirdMaxShare` | Bisección lineal | `SCREENING` |
| `modifyTraitScalar`: `AdjacentTeammateBonusPercent` | 0 hoy | Comportamiento tipo C1 | Histograma restringido (behavioral audit) | Tripleta anclada | `SCREENING` |
| `modifyTraitScalar`: `SpeedBonusPercent` | 0 hoy | Igual que `Speed` | Igual, sin precedente | Bisección | `SCREENING`, `INFERIDO` |
| `modifyTraitScalar`: 6 escalares de calidad/fatiga (`ShotQualityBonus`, `PassQualityBonus`, `FoulChanceBonus`, `FatigueResistancePercent`, `SaveBonusClose`, `SaveBonusFar`) | 0 hoy | Sin banda o sin métrica (§13.4.2) | `INFO` sin rango, o ninguna | — | `DESIGN_REVIEW` o `NOT_READY` según el caso (tabla de §13.4.5) |
| `modifyTraitScalar` (3 escalares con precedente real hoy: `injuryChanceBonus`, `hardTackleBonus`, `shootRangeBonusCells`) | 4 | Métrica ya asociada y confirmada | Igual | Bisección lineal | `SCREENING` |
| `modifyMarkBias`/`modifyTackleBias` | 3/2 | Selección de objetivo | Distribución de objetivo (bespoke) | Ninguna | `SCREENING` → `VALIDATING` cualitativo |
| `modifyBias` | 2 | Sesgo arbitral | **Falta la fila agregada en `MatchMetrics`** (§13.4.4) | — | `NOT_READY` (tooling puro, no diseño) |
| `ProbabilityKind.Foul`/`Card` (dentro de `modifyProbability`) | 0 hoy | Tasa de falta/tarjeta | `INFO`, sin banda (§13.4.3) | — | `DESIGN_REVIEW` |
| `extraAction` | 3 | Repetición de acción | Frecuencia + universales | Ninguna (hoy) | `SCREENING` → `VALIDATING` directo |
| Casos singulares (6 tipos, 1-2 c/u) | 7 | Uno a uno | Universales + la del suceso que producen | Depende | Uno a uno |
| `modifyUtility` (C1) | 0 (inerte) | Bonus de utilidad | Histograma restringido a exposición | Tripleta anclada | `BLOCKED_INFRA` hasta que exista un perk real en `/data` |

**Cobertura**: las 94 filas del catálogo caen en alguna fila de esta matriz — ninguna queda sin
estrategia, aunque tras §13.4 varias de ellas son explícitamente `NOT_READY`/`DESIGN_REVIEW` en vez de
`SCREENING` — la cobertura significa "el sistema sabe qué hacer con este perk", no "puede iterarlo solo".

### 3.2 Revisión de consistencia de la matriz (18 sep 2026, sin simular nada)

Comprobación pedida explícitamente antes de tocar tooling — los ocho puntos, uno a uno:

1. **¿Dos métricas distintas miden el mismo concepto sin justificación?** No encontrado.
   `passChainAvgLength` (longitud de cadena) y `passCompletionRate` (% de pases completados) miden cosas
   distintas y ambas se usan donde corresponde (§4.2).
2. **¿Una métrica se usa fuera de su población válida?** Un caso, nuevo en esta revisión:
   `betterTeamWinRate` (fase 0, diferencia de calidad de 20) no aparece en la matriz de perks porque no
   aplica a un perk aislado — no había riesgo, pero queda dicho explícitamente para que nadie la añada
   por analogía.
3. **`Limit` vs. exposición insuficiente**: resuelto en §5.4 (leer sobre oportunidades, no sobre
   activaciones ya limitadas).
4. **`addCounter` según su uso real**: resuelto en §3 (solo/`AccumulatesAcrossMatches` → `RUN_LEVEL`;
   combinado con `modifyProbability` → se mide la parte de partido suelto, el contador se registra aparte).
5. **`modifyAttribute` según el atributo**: resuelto en §13.4.1, incorporado a la tabla de arriba.
6. **`TraitScalarKind` no usados, sin capacidad falsa**: resuelto en §13.4.2 — la tabla marca `NOT_READY`
   donde corresponde en vez de asumir que todos son iterables.
7. **Perks con múltiples efectos, estrategia clara**: §3 ya cubre la unión de categorías y el caso
   cruzado (`unlikely_bulwark`). **Hallazgo nuevo de esta revisión, no cubierto antes**: `pack_mentality`
   (`modifyAttribute: Strength`, `target: withTag:Brute`) no afecta solo al portador — afecta a **todos**
   los jugadores del equipo con la etiqueta `Brute`. El harness de control/armado usado hasta ahora
   (C1/Tanda 0, un solo "portador") no está diseñado para esto: mide un jugador, no un subconjunto. Es un
   **hueco de tooling explícito, no de dato**: hace falta una variante del harness que mida "el subconjunto
   de jugadores con la etiqueta X", antes de que `pack_mentality` (o cualquier otro `target` distinto de
   `owner`/`actor`) pueda entrar en `SCREENING`. Se añade a §13.
8. **Combinación que requiera estrategia especial, explícita**: las dos ya conocidas
   (`unlikely_bulwark`, cruza geometría+probabilidad — §3) más la nueva de arriba (`target` multi-jugador).

Ninguno de los ocho puntos obligó a cambiar la filosofía del protocolo (§0.1/paso 3 del encargo): los
hallazgos son huecos de tooling o de banda, no contradicciones en `Screening→Tuning→Validation`, la
potencia como gate, los dos estados de insuficiencia, las siete condiciones de `BALANCED`, cero agentes
en el camino crítico, la separación local/sistémico, ni los límites de tiempo/iteraciones.

---

## 4. Jerarquía de métricas

### 4.1 Universales (se miden siempre que aplique)

| Métrica | Qué es | Cómo se calcula |
|---|---|---|
| **Exposición** | Fracción de ticks/decisiones donde la condición del perk podría dispararse | Continua (zona/estado): fracción de ticks en la condición, igual que §3.3 de C1. Discreta (evento): fracción de partidos con ≥1 oportunidad de disparo |
| **Activación** | Cuántas veces se disparó de verdad | `MatchReport.PerksSummary` (`PerkActivationSummary`, ya existe) |
| **Efecto directo** | El cambio en lo que el perk toca literalmente | Depende de la categoría (§3): histograma de acción, tasa de suceso, valor del escalar |
| **Efecto de comportamiento** | Si el efecto directo se traduce en algo observable del partido | La métrica RT-056 relacionada (§4.2) |
| **Seguridad** | Las siete métricas obligatorias de RT-056 | `Sim.Analysis.MatchMetrics.Compute` — sin excepción, siempre en Validación |
| **Determinismo** | El mismo seed produce el mismo resultado con el perk equipado | Una comprobación, no una serie: dos ejecuciones de la misma semilla, comparación byte a byte (mismo principio que RT-024) |

### 4.2 Específicas (solo cuando corresponda, según la categoría de §3)

| Perk toca... | Métrica específica |
|---|---|
| `Shoot`/`ShotOnTarget`/`Save` | `shotsPerMatch`, `distanceToGoal` de los tiros del portador (auditoría de calidad, como en C1 §5), `goalsPerMatch` (INFO) |
| `ShortPass`/`LongPass`/`ThroughPass`/`Pass`/`Intercept` | `passChainAvgLength` (del equipo del portador, no vía `BuildsWinDifferently` salvo que el perk esté en una build de referencia), `passCompletionRate` (INFO) |
| `Tackle`/`TackleEvasion`/`Dribble`/`Block` | `tacklesPerMatch`, `foulsPerMatch`/tarjetas (INFO) |
| `Injury`/`Injure`/`SevereInjury`/escalares de lesión | `injuriesPerMatch` como métrica **primaria**, no solo de seguridad |
| Zona/posición (`shiftHome`, `modifyZoneShape`, `modifyUtility` con zona) | `ballThirdMaxShare`, histograma de acción restringido a exposición |
| `modifyMarkBias`/`modifyTackleBias` | Distribución de objetivo (concentración por jugador/etiqueta) — bespoke, no en `MatchMetrics` |
| `addCounter` que alimenta progresión/maestros | Escalar directamente a medición de **run** (`FullRunMetrics`), no de partido — fuera del bucle rápido |

**Regla de selección**: la categoría de §3 decide qué fila de esta tabla se activa. Ningún perk mide las
siete filas; la mayoría mide una o dos específicas más las universales.

---

## 5. Niveles de evidencia — Screening / Tuning / Validation

Nombres tomados del encargo; coinciden en espíritu con "medición barata antes de tocar código" (Regla A)
y con lo que ya hizo este piloto sin llamarlo así (una medición de utilidad barata en C1 §3.1b, un primer
lote pequeño de 20 plantillas en C1 §5, uno grande de 100 después de confirmar exposición).

### 5.1 Screening

**Objetivo**: triaje barato — ¿el perk tiene exposición medible? ¿el efecto es obviamente enorme,
obviamente nulo, o hay que afinar?

- **Muestra inicial**: 1 semilla, 20 plantillas × 2 direcciones = **40 partidos/brazo**
  **[MEDIDO como punto de partida operativo]** — es exactamente la muestra de Tanda 0, suficiente para
  confirmar `own_third_anchor` (activación 40/40, L1=0 informativo) y para detectar el efecto de
  `sweeper_keeper` (L1=0,0013, activación 35/40). **Esto mide que 40 partidos bastaron para ESOS cuatro
  perks y ESAS métricas de histograma** (con miles de ticks por partido de muestra efectiva); no está
  demostrado que basten para cualquier perk o cualquier métrica — ver §5.5, que es la corrección real de
  este apartado tras la revisión del 17 sep 2026.
- **Traza**: solo ON si la categoría necesita histograma de acción (bonus de utilidad, geometría); OFF
  para el resto (más barato, `Sim.Analysis.MatchMetrics` no necesita traza).
- **Presupuesto de tiempo**: **objetivo ≤30 s** por perk. **[MEDIDO, como techo de reloj, no como prueba
  de suficiencia]** — 40 partidos con traza tardaron ~2 s en este piloto (§5 de C1, primer lote) y 400
  partidos sin traza tardaron ~6 s (§7 de C1); 40 partidos sin traza son una fracción de eso. Es un
  circuito de seguridad de tiempo (§5.5 punto 1), no una garantía de que la muestra alcanzada en ese
  tiempo sea estadísticamente suficiente (§5.5 punto 4).
- **Salidas de Screening** (§9 tiene la lista completa de estados; §6.1 tiene las reglas exactas):
  - **Exposición por debajo del suelo** (§5.4) → subir la muestra una vez (×3, a 120 plantillas) y
    repetir Screening; si sigue por debajo, el estado final es `INSUFFICIENT_EVIDENCE` (los tres suelos
    de exposición son `[ASUNCIÓN]` hoy, así que no se puede cerrar con la confianza de
    `INSUFFICIENT_EXPOSURE` — ver §5.4/§6.1/§9, corregido en la revisión del 17 sep 2026).
  - **Efecto ya claramente roto** (una métrica obligatoria de RT-056 ya `OUT` a esta escala, algo que en
    este piloto nunca ocurrió con un solo perk pero es la comprobación barata antes de gastar más
    muestra): saltar a un Validation reducido para confirmar y casi seguro `REJECT`/`SAFETY_LIMIT`.
  - **Efecto indistinguible del cero informativo** (L1 o la métrica de efecto directo por debajo del
    suelo de ruido de §5.4, con exposición ya alta): no es `REJECT` automático — puede ser un perk de
    identidad sin efecto de comportamiento medible por diseño. Se marca `DESIGN_REVIEW` con el hallazgo
    "sin efecto de comportamiento detectado", no se decide solo.
  - **Efecto real, ni roto ni nulo**: `CONTINUE` a Tuning.

### 5.2 Tuning

**Objetivo**: localizar el valor, solo si Screening dijo `CONTINUE` y el perk tiene un parámetro
numérico que buscar (§7 distingue esto de los perks de selección de objetivo, que no pasan por aquí).

- **Muestra**: 2 semillas independientes, 100 plantillas × 2 direcciones = **200 partidos/brazo/semilla**
  (400 partidos/brazo combinando semillas). **[MEDIDO para el tamaño de efecto de Cazagoles]**: es
  exactamente lo que hizo falta en C1 §5 para que `passChain`/`shotsPerMatch` dejaran de ser ruido y la
  monotonía se confirmara en las dos semillas — con 40 partidos/brazo la misma medición no era monótona.
  **Esto es un punto de partida operativo, no una cota estadística general** (§5.5): un perk cuyo efecto
  esperado sea menor que el de Cazagoles, o cuya métrica objetivo tenga una tasa base más baja
  (`injuriesPerMatch`, 0,3-0,9 por partido, es un recuento mucho más pequeño que `shotsPerMatch`,
  7-15) necesitará más muestra para la misma confianza — el harness debe comprobarlo (§5.5), no asumirlo.
- **Presupuesto de tiempo**: **objetivo ≤60 s por candidato**. **[MEDIDO como techo de reloj]**: 400
  partidos/brazo con traza tardaron ~17 s en este piloto (tres candidatos, dos semillas, 2.400 partidos
  totales en 16,8 s). Igual que en Screening, es un techo de tiempo verificado en una máquina y una carga
  de trabajo concretas, no una garantía de que ese tiempo sea siempre suficiente para decidir.
- **Presupuesto por perk en esta fase**: máximo **5 candidatos** o **5 minutos** de reloj, lo que llegue
  antes — igual que el resto de límites de este documento, es un límite duro, no una guía (§9).

### 5.3 Validation

**Objetivo**: confirmar el candidato elegido por Tuning, no volver a buscarlo.

- **Muestra**: reutiliza los datos de Tuning del candidato elegido (no se repite la simulación si ya
  existe); añade la batería completa de RT-056 sobre partido completo (`MatchMetrics.Compute`) si Tuning
  no la calculó ya, y una comprobación de determinismo (una sola vez, no por semilla).
- **Presupuesto de tiempo**: **objetivo ≤30 s incrementales**. **[MEDIDO como techo de reloj]**: la
  batería de RT-056 sobre 800 partidos (2 brazos × 2 semillas × 200) tardó ~6 s en C1 §7.
- **Salida**: `ACCEPT` (con todas las condiciones de §6.4, reescritas tras esta revisión) o el estado de
  rechazo/escalada que corresponda.

### 5.4 Huecos identificados en los niveles de evidencia

Los tres huecos siguen exactamente donde estaban; esta revisión (17 sep 2026) corrige **cómo se comporta
el sistema mientras no están calibrados** (§6 tenía el fallo real: usaba estos números como si decidieran
con la misma confianza que uno medido) y precisa, para cada uno, si de verdad hacen falta perks nuevos o
si el hueco puede acotarse mejor con lo que ya existe.

- **Suelo de exposición discreta (activación por partido).** Tanda 0 dio cuatro puntos: 100%
  (`own_third_anchor`, informativo), 87,5% (`sweeper_keeper`, informativo), 7,5% (`iron_gate`, poca
  confianza) y 5% (`bulwark_stance`, sin información). El verdadero punto de quiebre está entre 7,5% y
  87,5% — un rango de 80 puntos, demasiado ancho para fijar un número con confianza. **Sí hacen falta 1-2
  perks reales más con activación intermedia (20-60%) para estrechar el rango**; no se puede derivar de lo
  ya medido porque no hay ningún punto dentro de ese tramo. **Provisional: 50% de partidos con ≥1
  activación** (subido de un primer borrador de 30%, ver razón abajo) — `[ASUNCIÓN — PENDIENTE DE
  CALIBRAR]`, deliberadamente conservador: como no se sabe si el punto de quiebre real está cerca de 10%
  o cerca de 80%, un suelo alto sesga el sistema hacia escalar de más (pedir calibración de más) en vez
  de aceptar de más una medición sin potencia — el coste de un falso `INSUFFICIENT_EVIDENCE` es repetir
  trabajo; el coste de un falso `ACCEPT` es escribir un valor mal medido en `/data`.
  **Matiz nuevo de esta revisión**: un perk con `Limit` (21 de 94 perks del catálogo hoy, p. ej.
  `iron_gate`, 1 vez/partido) puede tener activación total baja **por diseño**, no por exposición
  insuficiente — el suelo debe leerse sobre **oportunidades de disparo** (cuántas veces se cumplió la
  condición antes de tocar el límite), no sobre activaciones ya limitadas, para no confundir "raro porque
  el límite lo acota" con "raro porque la condición casi nunca se cumple". Con activaciones limitadas, el
  sistema todavía no sabe distinguir las dos causas — es otro hueco de tooling (§13), no solo de dato.
- **Suelo de exposición continua (fracción de ticks).** Un solo punto de calibración: Cazagoles, 35-46%,
  con efecto real y medible. **No se puede derivar un suelo de un solo punto** — 35% podría estar muy por
  encima del límite real o casi rozándolo, y no hay forma de saberlo sin un segundo perk de este tipo.
  Ancla mediría esto, pero el encargo pide explícitamente no cerrar Ancla ahora — **este hueco queda
  aplazado, no resuelto**, hasta que exista una segunda medición continua de exposición (Ancla u otro
  perk de zona/estado, el que llegue primero). Provisional: **25%** (subido de 20% por la misma lógica
  conservadora que el punto anterior) — `[ASUNCIÓN — PENDIENTE DE CALIBRAR, sin fecha, bloqueado por la
  restricción de no cerrar Ancla ahora]`.
- **Suelo de "efecto indistinguible de cero".** `sweeper_keeper` (L1=0,0013) es el efecto real más
  pequeño confirmado; `own_third_anchor` (L1=0,0000, activación completa) es el cero informativo. Un solo
  punto real por debajo del cual no se sabe nada. Provisional: 0,0013 como suelo de "hay efecto" —
  `[ASUNCIÓN]`, mismo remedio que el de exposición discreta (1-2 calibraciones más en el rango 0,0001-
  0,001 antes de confiar en un número exacto).
- **Suelo de "fidelidad de diseño" (cuándo un perk se ha vuelto automatismo).** Distinto de los otros
  tres: no está claro que se reduzca nunca a **un solo número universal**. Cazagoles dio un punto
  cualitativo (7% de cuota de `ShortPass` "queda cerca, sin cruzar"), pero la fidelidad de diseño depende
  de la fantasía de cada ficha (lo que es "demasiado automático" para Cazagoles puede no serlo para un
  perk cuya fantasía sea precisamente "hazlo siempre"). **No se calibra con 2-3 perks cualquiera: hace
  falta más de un perk de la MISMA familia de diseño** (p. ej. el resto de "El Remate" en
  `perks-catalogo-unificado.md`) para saber si el número es transferible o es propio de cada ficha. Se
  deja explícitamente **sin umbral numérico**, con la comprobación cualitativa de §6.3 tal cual está, y
  con la salvedad de que quizá nunca tenga un número único — no se fuerza uno.

**Regla de comportamiento mientras un suelo no está calibrado** (corrección central de esta revisión, ver
§6.1): cruzar un suelo `[ASUNCIÓN]` **nunca** produce directamente un estado terminal con la misma
confianza que un umbral medido. Produce `INSUFFICIENT_EVIDENCE` (nuevo, §9), que dice explícitamente
"el umbral usado no está calibrado" y cuya salida es calibrar (medir 1-2 perks de referencia adicionales,
el remedio de arriba) o escalar a `game-design-review`, nunca inventar una decisión con la confianza de
un umbral real.

### 5.5 Cuatro cosas distintas que esta revisión separa explícitamente

El borrador original marcaba "40 partidos" y "≤30 s" con la misma etiqueta `[MEDIDO]`, como si fueran la
misma afirmación. No lo son, y tratarlas igual es exactamente el riesgo que señala la revisión: "400
partidos tardaron X en Cazagoles" no implica "400 partidos siempre bastan".

1. **Límite de tiempo** (`≤30s`/`≤60s`/`≤30s`): un techo de reloj, verificado en esta máquina con esta
   carga de trabajo (§5.1-5.3). Es un circuito de seguridad ("si tarda más de esto, algo va mal o el perk
   necesita otro instrumento"), no una afirmación estadística.
2. **Cantidad de simulaciones** (40/400 partidos): un punto de partida operativo, elegido porque funcionó
   para los efectos concretos que este piloto midió (Tanda 0, Cazagoles). Es el tamaño con el que
   **empezar**, no el tamaño que **garantiza** una decisión fiable para cualquier perk.
3. **Potencia/exposición necesaria**: depende del **tamaño del efecto esperado** y de la **varianza de la
   métrica**, que cambian por perk. Una métrica de recuento con tasa base baja (`injuriesPerMatch`,
   0,3-0,9/partido) tiene más ruido relativo que una de tasa alta (`shotsPerMatch`, 7-15/partido) para el
   mismo número de partidos — el mismo N no da la misma potencia a las dos.
4. **Suficiencia de evidencia**: una decisión de nivel superior (replica en dos semillas, el efecto tiene
   vía causal, no hay descarte de seguridad) — combina las tres anteriores más la interpretación de §6, no
   se reduce a "se han jugado N partidos".

**Corrección de diseño que se sigue de esto**: la muestra fija de §5.1-5.3 deja de ser el único criterio
de parada. El harness, antes de aceptar un resultado de Tuning/Validation como suficiente, debe comprobar
una condición de potencia explícita y determinista sobre la métrica primaria:

```
error_estándar ≈ sqrt(varianza_observada_por_partido / n_partidos)     (fórmula estándar, no inventada
                                                                         para este proyecto)
SI |delta_observado| < 2 × error_estándar
ENTONCES  el efecto no se distingue de ruido con esta muestra
          → duplicar la muestra (hasta el límite de candidatos/tiempo de §5.2/§9) antes de decidir
SI, agotado el límite, sigue sin distinguirse
ENTONCES  NEEDS_REPLICATION o INSUFFICIENT_EVIDENCE, nunca un ACCEPT/REJECT con esa muestra
```

El multiplicador "2" es la convención estadística habitual (≈ intervalo de confianza del 95% para una
normal) — **no se ha calibrado específicamente contra el ruido de este motor** (a diferencia de 40/400
partidos, que sí vienen de datos propios). Se marca `[CONVENCIÓN ESTÁNDAR, NO CALIBRADA AL PROYECTO]`,
una tercera etiqueta distinta de `[MEDIDO]`/`[ASUNCIÓN]`: es una fórmula genérica razonable, no un número
que se haya inventado para que un resultado concreto encajara.

Esto es lo que faltaba para que el "muestreo adaptativo" del encargo original fuera real: sin esta
comprobación, §5.1-5.3 solo adaptaba la muestra a la **exposición** (¿hay suficientes oportunidades?), no
a la **potencia** (¿el tamaño del efecto frente al ruido de esta métrica concreta ya es distinguible?).
Las dos preguntas son distintas y el borrador original solo respondía la primera.

---

## 6. Motor de decisión — reglas, no intuición

Formato exigido por el encargo: "si ocurre X, Y y Z, aplica la regla R", con el umbral ya fijado antes de
mirar el resultado del experimento siguiente.

### 6.1 Reglas de Screening

**Corrección de esta revisión**: los suelos de §5.4 son `[ASUNCIÓN]`, así que cruzarlos no puede producir
directamente el mismo estado terminal que un umbral medido. Además, Screening ahora clasifica también si
el perk tiene un parámetro numérico que buscar (§6.5/§7): si no lo tiene, **Tuning se salta entero**.

```
SI exposición < suelo_exposición_provisional (§5.4, marcado [ASUNCIÓN])
  Y no se ha repetido ya con muestra ×3
ENTONCES  repetir Screening con ×3 plantillas

SI exposición < suelo_exposición_provisional
  Y ya se repitió con muestra ×3
ENTONCES  estado = INSUFFICIENT_EVIDENCE, motivo = "por debajo de un suelo de exposición sin calibrar"
          (NO INSUFFICIENT_EXPOSURE — ese estado se reserva para cuando el suelo esté medido, §9)

SI alguna métrica obligatoria de RT-056 ya está OUT a escala de Screening
ENTONCES  estado = candidato a SAFETY_LIMIT, ejecutar Validation reducida para confirmar antes de decidir

SI efecto_directo < suelo_de_cero_provisional (§5.4, marcado [ASUNCIÓN])
  Y exposición >= suelo_exposición_provisional (el cero es informativo, no por falta de potencia)
ENTONCES  estado = DESIGN_REVIEW, motivo = "sin efecto de comportamiento detectado"
          (este caso SÍ puede escalar a DESIGN_REVIEW con confianza razonable, porque la exposición alta
          ya descarta la explicación más probable de un cero falso — falta de oportunidad, no de efecto)

SI el perk no tiene un parámetro numérico que buscar (selección de objetivo, binario on/off — §6.5/§7)
ENTONCES  estado = CONTINUE, saltar Tuning, ir directo a Validation cualitativa

EN OTRO CASO
  estado = CONTINUE, pasar a Tuning
```

### 6.2 Reglas de Tuning (para un perk con parámetro numérico, monotonicidad esperada)

Reutiliza el patrón exacto de C1 §4/§6: candidatos anclados en una medición barata (no adivinados),
comprobación de monotonicidad explícita, criterio de descarte definido antes de medir.

```
candidato_inicial = medición barata del hueco mecánico (volcado de utilidad para C1, o la probabilidad
                     base/objetivo para modifyProbability, etc. — específico de la categoría, §3)
tripleta = { bajo = p25 de la distribución medida, central = mediana, alto = p75 }
          (si no hay distribución que anclar —parámetro sin medición barata posible—, ver hueco 6.4)

medir los tres candidatos con el esquema de §5.2

SI, para la métrica primaria, |delta_observado| < 2 x error_estándar (§5.5, comprobación de potencia)
  en alguno de los tres candidatos
ENTONCES  ese candidato concreto no es interpretable todavía — duplicar su muestra (dentro del
          presupuesto de §5.2/§9) antes de usarlo en las reglas de monotonicidad siguientes;
          si tras duplicar sigue sin distinguirse, estado = NEEDS_REPLICATION para ese candidato,
          no se descarta ni se acepta con esa muestra

SI el efecto NO es monótono (bajo <= central <= alto, o al revés si el parámetro es negativo)
  en ninguna de las métricas primarias, en las dos semillas
ENTONCES  estado = NON_MONOTONIC
          NO seguir aplicando bisección
          reescanear con una rejilla más ancha (5 puntos en vez de 3) una sola vez;
          si sigue sin monotonía, estado = DESIGN_REVIEW, motivo = "relación valor-efecto no monótona"

SI el efecto es monótono
  Y ningún candidato cumple la banda objetivo del efecto primario
  Y todos están por debajo
ENTONCES  extrapolar un paso más allá del alto, con el mismo tamaño de paso relativo, una vez;
          si el nuevo candidato tampoco entra en banda, estado = WEAK_EFFECT_CEILING,
          escalar a DESIGN_REVIEW (el parámetro no basta; el diseño necesita revisión, no más número)

SI el efecto es monótono
  Y algún candidato cumple la banda objetivo del efecto primario
  Y ese mismo candidato no dispara ninguna condición de descarte de seguridad (§6.3)
ENTONCES  candidato_elegido = el que cumple banda con el paso más pequeño desde el último candidato
                              válido (mismo principio que "reduce el paso al acercarse", C1 §4 aplicado
                              con una tripleta en vez de bisección continua porque anclar en cuartiles
                              medidos ya da tres puntos informativos sin necesitar más pasos)
          pasar a Validation con ese candidato

SI dos o más candidatos cumplen banda
ENTONCES  elegir el de menor magnitud (principio de mínimo cambio necesario, mismo criterio de diseño
          que "identidad memorable > bonus genéricos" no se decide aquí con el número más grande)
```

### 6.3 Reglas de descarte de seguridad (se aplican en Tuning y en Validation)

```
SI alguna métrica obligatoria de RT-056 sale OUT en el brazo armado
ENTONCES  descartar el candidato, NO ajustar el número para corregirlo en la misma pasada
          (si era el único candidato con el efecto en banda: WEAK_EFFECT_CEILING, no REJECT total del
          perk todavía — puede que otra vía de diseño exista, eso lo decide DESIGN_REVIEW)

SI una métrica sistémica (posesión, tiros, lesiones, distribución global de acciones) se mueve
  Y no tiene una vía causal ya identificada para la categoría de este perk (§4.2)
ENTONCES  estado = SYSTEMIC_REGRESSION, escalar (§8) — no se compensa con otro cambio local

SI el efecto de comportamiento pretendido por la ficha del perk se satura
  (p. ej., la acción alternativa que debería seguir existiendo cae por debajo de un suelo de fidelidad
  de diseño — ver C1 §4.3, "ShortPass no puede acercarse a cero")
ENTONCES  descartar el candidato aunque las métricas numéricas parezcan aceptables,
          motivo = "automatismo sin decisión real", no un número fuera de rango
```

**Hueco explícito, revisado**: el "suelo de fidelidad de diseño" de la última regla se definió en C1 de
forma cualitativa ("lejos de cero", "el que menos margen deja") porque solo hay un perk medido, y (§5.4)
puede que no exista un único número universal — depende de la fantasía de cada ficha, no solo de la
familia. Mientras no haya más de un perk de la misma familia medido, esta regla se aplica **siempre como
juicio cualitativo explícito en el registro** (§10: "¿la acción alternativa sigue siendo una opción real,
sí/no, con qué cifra"), nunca como una comparación automática contra un número — es la única regla de
§6.3 que hoy requiere que quien cierra el perk (independent-reviewer o el humano) lea el dato, no una
regla que el harness pueda aplicar solo.

### 6.4 Reglas de Validation — qué significa `BALANCED`, exactamente

Revisión explícita: `BALANCED` **no puede significar** "se encontró un valor que no rompe nada". El
encargo pide siete condiciones; cada una se traza aquí a un mecanismo concreto ya definido en el
documento, y si algún mecanismo todavía no existe, se dice explícitamente en vez de asumir que el
"no romper nada" ya lo cubre.

```
ACCEPT exige las SIETE, todas, no un subconjunto:

1. El comportamiento/intención esperados                → el candidato quedó dentro de la banda del
   se producen                                             efecto PRIMARIO ya en Tuning (§6.2) — sin
                                                             esto no habría llegado a Validation
2. El efecto está en el rango objetivo definido           → mismo mecanismo que 1: la banda del efecto
                                                             primario, definida antes de medir (§6.2)
3. Las métricas de seguridad están dentro de límites       → las siete de RT-056, IN en armado, control
                                                             y combinado (§5.3/§7 de C1, mecanismo ya
                                                             probado)
4. La evidencia es suficiente                              → la comprobación de potencia de §5.5 pasó
                                                             para la métrica primaria Y para cualquier
                                                             métrica de seguridad que se haya movido —
                                                             NUEVO en esta revisión, antes no era un gate
                                                             explícito de Validation
5. El resultado es reproducible cuando corresponda         → réplica en dos semillas (todas las métricas
                                                             que se usan para decidir, no solo la
                                                             primaria) + determinismo byte a byte
                                                             (una vez, RT-024)
6. No hay señal de automatismo o degeneración              → la regla de fidelidad de diseño de §6.3,
                                                             aplicada como juicio explícito (revisado
                                                             arriba: no es automática todavía)
7. No hay regresión sistémica relevante                    → ninguna métrica sin vía causal documentada
                                                             se ha movido de forma replicada (§6.3/§8)

SI las siete se cumplen
ENTONCES  estado = ACCEPT (candidato listo para independent-reviewer antes de escribir /data — Regla E,
          no lo sustituye este sistema; independent-reviewer recibe las siete condiciones y su evidencia,
          no una conclusión ya cerrada)

SI falla el punto 4 (evidencia insuficiente) en concreto, y no por cruzar un suelo [ASUNCIÓN] sino por no
  alcanzar la potencia estadística de §5.5 con el presupuesto de muestra disponible
ENTONCES  estado = NEEDS_REPLICATION (máximo UNA semilla adicional; si sigue sin potencia con tres
          semillas, DESIGN_REVIEW — el problema ya no es de muestra, es de que el efecto es demasiado
          pequeño o ruidoso para que este sistema lo resuelva solo)

EN CUALQUIER OTRO FALLO (puntos 1, 2, 3, 6 o 7)
  estado = REJECT (si es el candidato concreto el que falla) o el estado de escalada correspondiente ya
  definido en §6.3/§9 (SAFETY_LIMIT, SYSTEMIC_REGRESSION, WEAK_EFFECT_CEILING)
```

**Ningún candidato llega a `ACCEPT` solo por no haber roto RT-056.** Si un candidato pasa el punto 3 pero
no se puede confirmar 1, 2, 4, 5, 6 o 7 (por ejemplo, el efecto primario nunca se confirmó con potencia
suficiente porque Tuning se saltó por error la comprobación de §5.5), el estado correcto no es `ACCEPT`
con una nota — es quedarse en `NEEDS_REPLICATION`/`DESIGN_REVIEW` hasta que la condición que falta se
resuelva.

### 6.5 Hueco explícito: parámetros sin medición barata posible

§6.2 asume que existe una "medición barata del hueco mecánico" para anclar la tripleta (como el volcado
de utilidad de C1 §3.1b). Para `modifyProbability` existe un equivalente directo (la probabilidad base ya
está en `data/ai/weights.json`/`tuning.json`, y el hueco es la diferencia contra el suceso rival). Para
`modifyMarkBias`/`modifyTackleBias` (selección de objetivo) **no hay una "distancia" numérica que anclar
en cuartiles** — son reglas de preferencia, no una escala continua. Estos perks **no entran en el bucle
de búsqueda de §6.2**: pasan de Screening directamente a un Validation cualitativo (behavioral audit:
¿la distribución de a quién se marca/entra parece la que pide la ficha?), sin candidatos numéricos que
comparar. Esto es un hueco de diseño del propio protocolo, no una falta de datos — se resuelve
tratándolos como su propia categoría (ya lo hace §3), no forzándolos al patrón bajo/central/alto.

---

## 7. Estrategia de búsqueda del valor, por tipo de parámetro

| Tipo de parámetro | Relación esperada | Estrategia |
|---|---|---|
| Bono de utilidad (%) — C1 | Monotónica en el % (más % → más se elige la acción), coste no lineal (C1 §5: efecto se acelera con el %) | Tripleta anclada en cuartiles de un volcado de utilidad (§6.2), no bisección continua — con solo tres puntos ya se ve la forma, y una cuarta medición solo se justifica si dos candidatos empatan en banda |
| Probabilidad base/delta (`modifyProbability`) | Monotónica, pero con **saturación** cerca de 0%/100% (la probabilidad ya pasa por `ProbabilityScale.ApplyAveraged`, no lineal) | Tripleta + detección de meseta: si dos candidatos adyacentes dan un efecto estadísticamente indistinguible, no seguir refinando en esa región — la escala ya la comprime el motor |
| Escalar de rasgo (`modifyTraitScalar`) | Monotónica, lineal en la mayoría (suma directa al escalar) | Bisección simple sobre el escalar, banda objetivo = la métrica RT-056 asociada |
| Geometría (`modifyZoneShape`, `shiftHome`, `modifyLeash`) | Monotónica en casillas, pero discreta (RT-023: aritmética entera, no hay "medio paso") | Búsqueda entera por pasos de 1 casilla, tripleta si el rango lo permite (normalmente 0-3 casillas) |
| Selección de objetivo (`modifyMarkBias`, `modifyTackleBias`) | No numérica | Sin búsqueda de valor — behavioral audit directo (§6.5) |
| Contador de run (`addCounter`) | Depende del sistema de progresión que lo consuma, no del partido suelto | Fuera del bucle rápido; escala a medición de run (`FullRunMetrics`, más lenta por diseño — no se le pide velocidad de partido suelto) |
| Casos singulares (`relocate`, `setState`, `extraAction`, `injure`, `modifyBias`, `modifyKnockdownTicks`, `modifyExperience`) | Depende del caso — la mayoría no tienen "cuánto", son on/off o de duración fija | Se tratan uno a uno con la capa común (§4.1); sin estrategia de búsqueda genérica hasta que haya volumen suficiente para una |

**Detección de no-monotonicidad** (aplica a todos los tipos con estrategia numérica): ya está en §6.2 —
si la tripleta no ordena igual que el parámetro, se para de aplicar bisección/interpolación y se
reescanea o se escala a diseño. No se fuerza un ajuste sobre un comportamiento que el propio sistema ya
detectó como impredecible.

---

## 8. Balance local vs sistémico

**Se resuelve en este bucle** (local): el perk solo mueve las métricas que su propia categoría predice
(§4.2), dentro de RT-056, sin tocar ninguna otra columna de forma inesperada. Es el caso de Cazagoles en
C1 §7: `shotsPerMatch`/`ballThirdMaxShare`/`passChainAvgLength` se mueven, con vía causal conocida,
`tacklesPerMatch`/`injuriesPerMatch` no.

**Se escala a revisión sistémica** (`SYSTEMIC_REGRESSION`, §6.3) cuando una métrica **sin vía causal
documentada para esa categoría** se mueve de forma replicada — por ejemplo, un perk de pases que además
cambia `injuriesPerMatch`, o cualquier perk que mueva `betterTeamWinRate` o las puertas de diferenciación
de builds (`BuildsWinDifferently`, `TheThreeDoctrinesBuyDifferently`) sin estar él mismo en esas builds.
**El sistema no "arregla" esto con otro cambio local**: registra el hallazgo, marca el perk
`SYSTEMIC_REGRESSION` y para — la decisión de qué hacer (tocar otra pieza, abrir un ADR, aceptar el
riesgo) es de diseño, igual que BB-P dejó `passChain` como una causa diagnosticada y no la "arregló" desde
este piloto.

---

## 9. Máquina de estados por perk

Revisado (17 sep 2026): separa la exposición **confiable** (umbral medido) de la exposición **bajo un
suelo sin calibrar** (`INSUFFICIENT_EVIDENCE`, nuevo — §5.4/§6.1), y añade `RUN_LEVEL` para los perks de
campaña/contador (§3), que no son un fallo del sistema, son un instrumento distinto.

```
NOT_READY ──(existe en /data, tiene condición/efecto ya soportado por el motor)──> SCREENING
NOT_READY ──(necesita una primitiva que no existe: C2 completo, C9, C16...)──────> BLOCKED_INFRA
NOT_READY ──(addCounter en solitario, o AccumulatesAcrossMatches=true, §3)───────> RUN_LEVEL [terminal
                                                                                    para este sistema —
                                                                                    se mide con
                                                                                    /Balance --full-runs]

SCREENING ──(exposición < suelo MEDIDO, tras remuestrear)────────> INSUFFICIENT_EXPOSURE  [terminal]
SCREENING ──(exposición < suelo [ASUNCIÓN], tras remuestrear)────> INSUFFICIENT_EVIDENCE  [terminal*,
                                                                    nuevo — el umbral usado no está
                                                                    calibrado, no es una conclusión sobre
                                                                    el perk]
SCREENING ──(efecto ya rompe RT-056 a escala pequeña)───────────> SAFETY_LIMIT           [terminal]
SCREENING ──(cero informativo, exposición alta y MEDIDA)────────> DESIGN_REVIEW          [terminal*]
SCREENING ──(sin parámetro numérico: on/off, selección objetivo)─> VALIDATING (cualitativo, sin Tuning)
SCREENING ──(efecto real, con parámetro numérico)───────────────> TUNING

TUNING ──(candidato sin potencia suficiente, §5.5)──────────────> duplicar su muestra, una vez, dentro
                                                                    del presupuesto — no consume un
                                                                    "candidato" nuevo
TUNING ──(no monótono tras reescaneo)───────────────────────────> DESIGN_REVIEW          [terminal*]
TUNING ──(monótono pero techo insuficiente)─────────────────────> WEAK_EFFECT_CEILING ──> DESIGN_REVIEW
TUNING ──(presupuesto de candidatos/tiempo agotado sin ganador)──> NEEDS_REPLICATION o DESIGN_REVIEW
TUNING ──(candidato en banda, sin descarte de seguridad)────────> VALIDATING

VALIDATING ──(las siete condiciones de §6.4, todas)─────────────> BALANCED
VALIDATING ──(falla solo evidencia suficiente, §6.4 punto 4)────> NEEDS_REPLICATION (máx. 1 semilla más)
VALIDATING ──(falla cualquier otra de las siete)────────────────> REJECT                [terminal]
VALIDATING ──(mueve algo sin vía causal, replica)───────────────> SYSTEMIC_REGRESSION     [terminal*]

BALANCED ──(pendiente Regla E, no lo hace este sistema)─────────> listo para independent-reviewer y
                                                                    escritura en /data (fuera de este
                                                                    documento — decisión humana/de
                                                                    revisor, nunca automática)
```

`[terminal*]` = terminal para este sistema, no para el perk: escala a un humano/`game-design-review`
(o, para `INSUFFICIENT_EVIDENCE`, a la calibración de §5.4), que puede reabrir el ciclo con un diseño
distinto o con el umbral ya corregido — nunca reintentando el mismo número sin más información.

**Límites duros por perk, obligatorios** (evita perseguir una métrica indefinidamente, instrucción
explícita):
- Iteraciones de Tuning: **5 candidatos máximo** (duplicar muestra por falta de potencia, §5.5, no cuenta
  como candidato nuevo — es la misma medición, más precisa).
- Cambios de valor por perk: **igual al límite de candidatos**.
- Tiempo por perk: **10 minutos de reloj de harness**, sumando Screening+Tuning+Validation.
  **[DERIVADO de un techo de reloj medido, no de una prueba de que 10 minutos basten siempre — §5.5]**:
  un ciclo completo de tres candidatos × dos semillas de C1 tardó bajo 30 s reales; 10 minutos deja un
  margen de ~20× sobre lo medido para perks más caros (traza, auditoría de comportamiento, duplicado de
  muestra por potencia). Si un perk agota los 10 minutos sin resolver, el estado correcto es
  `NEEDS_REPLICATION`/`DESIGN_REVIEW` (según por qué se agotó), nunca extender el presupuesto en caliente.
- Semillas adicionales por `NEEDS_REPLICATION`: **máximo 1** (total 3 semillas); si sigue limítrofe,
  `DESIGN_REVIEW`, nunca una cuarta.

### 9.1 Presupuesto global (catálogo completo, no un perk suelto)

Hueco del borrador original: definía límites por perk pero ninguno para "balancea los perks del
catálogo" como una sola orden sobre 94 perks. 10 minutos × 94 en el peor caso son ~15,7 horas si todos
agotaran su presupuesto — no es el caso esperado (la mayoría de perks debería resolverse en Screening
puro, segundos, y solo una fracción necesita Tuning completo), pero **no hay medición de qué fracción
real sería**, así que no se fija un número global de tiempo total como si fuera un dato — sería la misma
sobreinterpretación que el punto 1 de esta revisión ya corrigió.

En su lugar, el presupuesto global se define por **comportamiento**, no por una cifra fija:

1. **El lote nunca bloquea en un perk atascado.** Si un perk agota sus 10 minutos, se marca (con el
   estado que corresponda) y el lote sigue con el siguiente — un perk problemático nunca consume el
   presupuesto de los demás.
2. **Checkpoint y reanudación, no una ventana de tiempo fija.** El registro (§10) se escribe por perk, no
   al final del lote entero: quien invoca "balancea los perks del catálogo" puede parar la sesión en
   cualquier momento entre perks y reanudar exactamente donde quedó, sin repetir trabajo ya hecho — es la
   forma correcta de encajar un catálogo de 94 perks en una sesión de duración arbitraria, en vez de
   adivinar cuánto va a tardar el conjunto.
3. **Circuito de seguridad a nivel de lote, no solo por perk**: si una fracción alta de los perks
   procesados en una tanda (**[ASUNCIÓN — sin calibrar]: provisionalmente 1 de cada 5**) termina en
   `DESIGN_REVIEW`/`SYSTEMIC_REGRESSION`/`INSUFFICIENT_EVIDENCE`, el lote se detiene y escala a
   `game-design-review` en vez de seguir procesando perks mecánicamente — muchas escaladas seguidas son
   señal de que algo del propio sistema (una categoría mal mapeada, un suelo mal calibrado) está fallando
   de forma sistemática, no de que el catálogo tenga mala suerte perk a perk.
4. **Condición de abandono explícita**: si un perk concreto no puede resolverse dentro de su presupuesto
   (10 minutos, 5 candidatos, 3 semillas — los tres límites de §9), el sistema **para y escala**, nunca
   seguir probando valores fuera de esos límites. Es la garantía que pide el encargo: "no puedo resolver
   este perk dentro del presupuesto → paro y escalo", aplicada literalmente en tres sitios distintos del
   protocolo (Tuning, Validation, y ahora el lote completo).

---

## 10. Registro / trazabilidad

Un documento (o fila de una tabla estructurada) por perk, con como mínimo:

```
perk_id, valor_inicial, valores_probados[], experimento_usado, muestras{semillas, plantillas, partidos},
métricas_medidas{universales, específicas}, resultado_por_candidato[], motivo_del_cambio,
resultado_del_cambio, motivo_de_aceptación_o_rechazo, estado_final, timestamp, versión_del_protocolo
```

Igual que este piloto documentó cada fase de C1 en el propio fichero de diseño (§3.1b, §5, §6, §7): la
información tiene que bastar para que otro agente retome el proceso sin reconstruir el historial a mano.
**Formato concreto (JSON por perk vs. una fila de CSV vs. un documento Markdown por perk) es una decisión
de implementación, no de este diseño** — se resuelve en la fase de tooling (§12), con el mismo criterio
de "el código encargado de registrar no debería requerir que un agente lea una tabla y copie números a
mano".

---

## 11. Arquitectura multiagente aplicada

Mapeo de roles a lo que **ya existe** en `.claude/agents/` de Underleague, no a `ai-orchestrator` (§1.5):

| Rol del encargo | Quién lo hace | Por qué |
|---|---|---|
| **Orquestador** (identifica perks pendientes, coordina iteraciones) | **Código determinista** (el harness/CLI de §12), no un agente — decidir "qué perk sigue" es una consulta al registro de §10, no una tarea de razonamiento | Es exactamente el tipo de decisión que el encargo pide convertir en regla, no en conversación |
| **Clasificación del perk** (qué categoría de §3, qué métricas de §4 aplican) | **`fast-worker`** (una vez por perk, no por iteración): lee el JSON del perk y el catálogo de `EffectType`, devuelve la categoría y la lista de métricas — barato porque es una sola llamada, no está en el bucle de medición | Es juicio (interpretar qué mide un `ProbabilityKind` en el contexto de la ficha), pero de bajo riesgo y alta frecuencia (94 perks): un modelo barato basta, y CLAUDE.md ya reserva `fast-worker` para "trabajo mecánico con especificación cerrada" |
| **Harness determinista** (ejecuta Screening/Tuning/Validation, calcula métricas, aplica las reglas de §6) | **Código C#** (`Sim.Tests`/`Balance`, extendiendo el patrón ya usado en Tanda 0 y C1), cero llamadas a un agente | El propio encargo: "si una tarea puede resolverse con código, prioriza eso" |
| **Diagnóstico de anomalías** (`NON_MONOTONIC`, `SYSTEMIC_REGRESSION`, resultados inesperados) | **`deep-reasoner`** — se invoca solo cuando el harness marca uno de estos estados, no en cada iteración | Coincide con el uso ya descrito en `CLAUDE.md` ("razonamiento pesado... diagnóstico de divergencias") |
| **Revisión independiente antes de tocar `/data`** (Regla E) | **`independent-reviewer`**, sin cambios respecto a como ya funciona hoy — recibe el problema completo (este documento + el registro de §10 del perk concreto), nunca el razonamiento del harness | Es exactamente para lo que existe: nunca ver "por qué está bien" del implementador |
| **Escalado a diseño** (`DESIGN_REVIEW`) | **Skill `game-design-review`**, invocada por la sesión principal (humano en la conversación, no un agente autónomo) | El propio protocolo distingue "esto es un número" de "esto es una decisión de diseño" — la segunda la toma quien ya la toma hoy |
| **Coordinación entre experimentos en paralelo** (varios perks a la vez) | **`Parallel.For` dentro del harness** (mismo patrón que `/Balance`/`BossGateTests`, cada hilo con su propio `Catalog`), no varios agentes coordinándose | Es paralelismo de simulación, no de razonamiento — usar agentes aquí sería más lento, no más rápido |

**Ningún rol usa `ai-orchestrator`.** Se revisita si en el futuro se quiere una segunda opinión de un
proveedor distinto en el paso de Regla E (§1.5) — opcional, no parte del diseño base.

### 11.1 El camino crítico, explícito (revisión: comprobar que no hay dependencia innecesaria)

El bucle que se ejecuta **por cada candidato, en cada iteración** es:

```
código (genera el candidato) → simulación (harness C#) → reglas de §6 (código) → siguiente candidato o fin
```

**Cero llamadas a un agente dentro de ese bucle.** Ningún estado de §9 hace que Tuning o Validation
esperen a un modelo de lenguaje para decidir si un candidato pasa o no — esa decisión ya está en las
reglas deterministas de §6. Los cuatro agentes de la tabla de arriba entran **fuera** de ese bucle, en
tres momentos concretos y acotados:

1. **Antes** de Screening (clasificación, `fast-worker`): una vez por perk, nunca por candidato.
2. **Cuando** el harness marca un estado de excepción (`NON_MONOTONIC`, `SYSTEMIC_REGRESSION`,
   `DESIGN_REVIEW`, `WEAK_EFFECT_CEILING`, `INSUFFICIENT_EVIDENCE`): `deep-reasoner`, y solo si ocurre —
   en el camino normal (efecto real, monótono, sin regresión) esto no se invoca nunca.
3. **Después** de `BALANCED`, antes de escribir `/data`: `independent-reviewer` (Regla E, obligatoria ya
   hoy para cualquier cambio de `/Sim`/`/data`, con o sin este sistema — no es una dependencia que este
   protocolo añada, es la que ya existe).

**Por qué esto no es una guía sino una comprobación**: la razón por la que el diseño original (aprobado en
la revisión anterior) ya cumplía esto es que cada rol de la tabla llevaba escrito explícitamente "una vez
por perk"/"solo si"/"nunca en cada iteración" — esta subsección lo hace más visible, no cambia ninguna
asignación de rol.

**Coste de coordinación, con la cifra que faltaba marcada como lo que es**: el único agente rutinario por
perk es `fast-worker` (una clasificación). Los demás son de excepción. **Cuántos perks llegan a
`BALANCED` sin ninguna excepción es, hoy, una incógnita** — no hay medición de qué fracción del catálogo
real activaría `deep-reasoner`/`DESIGN_REVIEW`; cualquier cifra optimista ("70-80% sin incidencias") sería
una suposición, no un dato, y se retira del documento por esa razón. Lo que sí se sostiene sin necesitar
esa cifra: en el **peor caso**, el número de llamadas a un agente es como mucho **una clasificación por
perk más una revisión por cada estado de excepción**, nunca una llamada por candidato ni por iteración de
muestreo — la latencia de un agente crece con el número de perks y de excepciones, no con el tamaño de la
muestra de partidos, que es donde este documento sí puede prometer minutos y no horas (§14).

---

## 12. Qué debería automatizarse como tooling (no implementado en esta fase)

1. **Extensión de `Sim.Analysis`**: una función que, dado un `PerkDefinition`, devuelva su categoría
   (§3) y la lista de métricas aplicables (§4) — la parte determinista de la "clasificación", para que
   `fast-worker` solo tenga que confirmar/matizar, no decidir desde cero.
2. **Harness genérico de control/armado**, generalización de `_C1CazagolesExperiment.cs`/
   `_C1CazagolesBalanceLote.cs` (ya borrados, patrón reutilizable): parametrizado por `PerkDefinition`
   candidato, tamaño de muestra, semillas, y si necesita traza — hoy existe el patrón dos veces, hecho a
   mano cada vez.
3. **Implementación de las reglas de §6** como funciones puras C# sobre los `MetricResult`/histogramas
   que ya produce el harness — nada de esto necesita una llamada a un modelo.
4. **Registro estructurado** (§10) — probablemente JSON por perk bajo `docs/balance/perks/<id>.json` o
   una tabla en `docs/balance/`, a decidir en la fase de implementación.
5. **CLI o modo de `/Balance`** (`--auto-balance <perk-id>` o similar) que encadene Screening→Tuning→
   Validation y escriba el registro, sin intervención manual salvo en los puntos de escalada.
6. **Volcado de utilidad automatizado por categoría** (generalización de la búsqueda de episodios de C1
   §3.1b): hoy es un instrumento temporal escrito a mano cada vez; para bonus de utilidad en general hace
   falta una versión parametrizable por acción/zona.
7. **Comprobación de potencia estadística (§5.5)**, nueva tras la revisión: una función que, dada una
   serie de partidos armado/control, calcule la varianza observada de la métrica primaria y decida si la
   muestra actual distingue el efecto del ruido antes de que el motor de decisión (§6) la use — sin esto,
   el "muestreo adaptativo" del encargo original solo se adapta a la exposición, no a la potencia real.

Ninguno de los siete se implementa en esta fase, tal como pide el encargo.

---

## 13. Piezas que faltan antes de poder ejecutar "Balancea los perks del catálogo"

Actualizado tras la revisión del 17 sep 2026 (antes eran seis puntos; se añaden tres, se precisa uno).

1. **Los seis elementos de tooling de §12**, sin excepción — hoy cada pieza se ha escrito a mano, una vez,
   para un solo perk. Incluye ahora, explícitamente, **la comprobación de potencia de §5.5**
   (`error_estándar`, duplicar muestra si no distingue) — no estaba en la lista original porque no
   existía como concepto hasta esta revisión.
2. **Cerrar los huecos de calibración de §5.4/§6.3**, con matices por hueco (no los tres son iguales):
   - Suelo de exposición discreta: 1-2 perks reales con activación 20-60% — sí hace falta medir, no se
     puede derivar de lo ya medido (el rango sin datos es demasiado ancho).
   - Suelo de exposición continua: al menos 1 perk más de zona/estado — bloqueado explícitamente por la
     restricción de no cerrar Ancla ahora; queda aplazado sin fecha.
   - Suelo de fidelidad de diseño: puede que no exista un número único — necesita perks de la MISMA
     familia de diseño, no cualquier perk, y se trata como juicio cualitativo mientras tanto (§6.3).
3. **El hueco de §6.5** (perks de selección de objetivo sin parámetro numérico que buscar) necesita su
   propio protocolo de behavioral audit, todavía no diseñado en detalle.
4. **Paso de "confirmación de métrica" antes de Screening**, nuevo tras la matriz de cobertura de §3.1:
   9 de las 22 filas de la matriz (atributo sin especificar, 10 escalares de `modifyTraitScalar` sin
   precedente real, `Foul`/`Card` sin instancia, sesgo arbitral con métrica por equipo) necesitan que la
   clasificación de `fast-worker` declare explícitamente qué métrica aplica antes de que el harness pueda
   arrancar Screening — hoy solo está dicho en prosa (§3), no como paso obligatorio del flujo.
5. **Formato exacto del registro de §10** — decisión de implementación pendiente.
6. **Decisión explícita de qué agente/skill invocar automáticamente vs. qué requiere que el humano lo
   dispare** — §11.1 acota el camino crítico a cero agentes, pero no se ha validado con un perk real
   corriendo de punta a punta.
7. **Ningún perk nuevo de C1/C2 (los 22 de `perks-catalogo-unificado.md` §3.3) puede entrar todavía**:
   siguen bloqueados por infraestructura (`BLOCKED_INFRA`) salvo Cazagoles/Ancla, que ya tienen el C1 +
   la cláusula de zona construidos (aunque no cerrados). El sistema, cuando exista, debería poder marcar
   esto automáticamente comprobando qué `EffectType`/predicado de condición usa el perk.
8. **La distinción entre `Limit` y exposición insuficiente (§5.4/§3)** no tiene todavía un mecanismo de
   cálculo — el harness necesita contar "oportunidades de disparo antes del límite", que hoy no es un
   dato que `MatchReport`/`PerkActivationSummary` expongan directamente.
9. **El circuito de seguridad de lote (§9.1, "1 de cada 5 perks escala")** es una asunción sin calibrar,
   igual que los suelos de §5.4 — no hay evidencia de qué fracción de escaladas es normal en un catálogo
   sano frente a una señal de que el propio sistema está mal calibrado.
10. **Harness para `target` multi-jugador** (§3.2, punto 7: `pack_mentality` y cualquier perk con
    `target: withTag:...`/`linked...` distinto de `owner`/`actor`): el patrón de control/armado usado
    hasta ahora mide un solo portador; falta la variante que mida un subconjunto de jugadores por
    etiqueta antes de que esos perks puedan entrar en `SCREENING`.
11. **Tasa de victoria de entrada** (§15.3, encontrado al ejecutar el demo de extremo a extremo):
    `tacklesPerMatch` cuenta intentos, no éxitos — un bono a la probabilidad de *ganar* una entrada
    (`ProbabilityKind.Tackle`) necesita una fila agregada de victorias/intentos que hoy no existe en
    `MatchMetrics.Compute` (solo hay contadores por jugador, sin resumen de partido). Sin esto, `Tackle`
    sigue marcado `Ready` en el clasificador pero la métrica que usa es un proxy débil para el mecanismo
    real — hueco de tooling, no de diseño.

## 13.4 Resolución del paso de confirmación de métrica (18 sep 2026)

Metodología: sin simular nada. Se lee dónde usa el motor cada atributo/escalar/`ProbabilityKind`
(`grep` sobre `Sim/Engine/*.cs`), qué fila existe en `Sim/Analysis/MatchMetrics.cs` y si tiene banda o es
`INFO`, y qué perk real de `data/perks/*.json` ya usa cada uno (si alguno).

### 13.4.1 `modifyAttribute`, por atributo

Precedente real hoy: **Strength** (4 perks: `brute_boots`, `comeback_spirit`, `pack_mentality`,
`scar_veteran`) y **Stamina** (1: `iron_lungs`). `Speed`/`Technique`/`Leash`: sin ningún perk real.

**Strength** — toca a la vez `ShootStrengthSlope`/`shot.StrengthFactor` (calidad de tiro),
`tackle.FoulStrengthFactor` (umbral de falta), `tackle` pressure, `dribble` (guardia del defensor),
`block` (resolución), e `injury.RelativeFactor` (severidad de la lesión) — verificado por `grep`, no por
inferencia.
1. Modifica: potencia física en cinco resoluciones a la vez.
2. **No hay una métrica primaria única** — depende de cuál de las cinco resoluciones enfatiza la ficha.
   `PositionOnly`/`Family`/etiquetas del propio perk (dato ya existente, sin medir nada) deciden cuál:
   defensa/contacto → `tacklesPerMatch` (banda 6-14) o `injuriesPerMatch` (banda 0,3-0,9); delantero →
   la calidad del tiro, que **no tiene banda** (`shotsOnTargetShare` es `INFO`, `shotsPerMatch` no
   cambia por calidad, solo por decisión).
3. Secundarias/seguridad: `injuriesPerMatch` siempre (vía severidad, sea cual sea la primaria);
   `foulsPerMatch` (`INFO`, sin banda).
4. Fuente: `MatchReport.Tackles`/`Injuries`, `MatchMetrics.Compute`.
5-6. Baseline/tratamiento: mismo esquema de control emparejado ya usado en C1 — con y sin el delta de
   `Strength`, mismas plantillas y semillas.
7-8. **Exposición, tipo nuevo (ni discreta ni continua, "de resolución")**: fracción de partidos donde el
   portador (o los que cumplan el `target`, ver el hueco de `pack_mentality` abajo) participa en al menos
   una resolución que usa `Strength` — insuficiente si el portador casi nunca entra/tira/bloquea/regatea
   defendido, igual que el matiz de `Limit` de §5.4 pero aplicado a la frecuencia BASE de la acción, no a
   un efecto propio del perk.
9. Efecto: más `Strength` → sube `tackleWinRate`/`foulRate`/severidad de lesión, o sube la calidad de tiro
   (sin banda que lo confirme).
10. **Automático si la ficha apunta a una resolución bandeada (tackle/injury); `DESIGN_REVIEW` si apunta
    a calidad de tiro** (sin banda que fije "cuánto es demasiado").

**Stamina** — `player.FatigueResistancePercent`, decae la velocidad menos con el cansancio; el portero
para mejor tarde en el partido (`decayFactor`).
1-4. **No existe ninguna métrica, ni bandeada ni `INFO`, que capture "rendimiento por fase del
partido"** — verificado: `MatchReport` no trocea nada por tiempo, `MatchMetrics.Compute` no tiene
ninguna fila de "primera/segunda mitad" ni de "últimos N minutos".
10. **`NOT_READY` explícito — hueco de tooling real, no de calibración.** Haría falta una métrica nueva
    (p. ej. goles/paradas en el último tercio del partido comparados con el resto) antes de poder medir
    esto en absoluto. No se inventa una proxy.

**Speed** — movimiento (`SpeedPerTickMilli`), `DribbleSpeedSlope`, `SaveBonusFar` del portero (guardián
lejano), `block.SpeedFactor`. Sin ningún perk real.
2. Candidata sin precedente: `ballThirdMaxShare`/`possessionChanges` (cobertura) o `saveRate` (`INFO`,
   sin banda) para porteros.
10. **Clasificación: `INFERIDO, SIN CONFIRMAR`** (no `NOT_READY`: el mecanismo y la métrica existen,
    solo falta un perk real que confirme que es la sensible). Puede entrar en `SCREENING`, pero el
    resultado se marca en el registro (§10) como primera medición de esta categoría, no con la misma
    confianza que Strength/Stamina.

**Technique** — `PassTechniqueSlope`, `DribbleTechniqueSlope`, `ThroughPassTechniqueSlope`,
`ShootTechniqueSlope`, `InterceptTechniqueFactor`, guardián técnico del portero. Sin ningún perk real.
2. Métrica natural: `passCompletionRate`/`shotsOnTargetShare` — **las dos son `INFO`, sin banda**
   (verificado en `MatchMetrics.cs`, no supuesto).
10. **`NOT_READY` para búsqueda automática de valor — mismo hueco que `ShotQualityBonus`/
    `PassQualityBonus` (§13.4.2).** El sistema puede medir el número (existe el cálculo); no puede decidir
    solo "está en rango" porque RT-056 no define ningún rango para calidad de pase/tiro. Pasa a
    `DESIGN_REVIEW` para que se fije una banda antes de iterar un valor — no se inventa una.

**Leash** (atributo, distinto de `LeashBonus` escalar y de `modifyLeash` efecto, mismo mecanismo de
fondo) — geometría de zona de acción. Sin perk real vía `modifyAttribute`, pero el mecanismo es
idéntico al de `modifyLeash`, que sí tiene precedente.
10. **`READY`**: reutiliza sin cambios la categoría de Geometría ya definida en §3 (`ballThirdMaxShare`,
    banda 0-52).

### 13.4.2 `modifyTraitScalar` — los 10 escalares sin perk real hoy

| Escalar | Dónde se lee (motor) | Métrica | Estado |
|---|---|---|---|
| `InjuryResistanceBonus` | resta a la severidad de lesión | `injuriesPerMatch` (banda 0,3-0,9) | **READY** |
| `LeashBonus` | `Recalculate()`, zona de acción | `ballThirdMaxShare` (banda 0-52) | **READY** |
| `AdjacentTeammateBonusPercent` | origen de `LeaderBonusPercent` (`RecomputeLeaderBonuses`, ya activo hoy vía rasgo) | histograma de acción del portador y sus vecinos (mismo instrumento que C1/Tanda 0) | **READY_VIA_BEHAVIORAL_AUDIT** (no banda RT-056, igual que `modifyUtility`) |
| `SpeedBonusPercent` | movimiento | igual que atributo `Speed` | **INFERIDO, SIN CONFIRMAR** |
| `ShotQualityBonus` | calidad de tiro | `shotsOnTargetShare` (`INFO`) | **NOT_READY** (sin banda) |
| `PassQualityBonus` | calidad de pase | `passCompletionRate` (`INFO`) | **NOT_READY** (sin banda) |
| `FoulChanceBonus` | probabilidad de falta en entrada/bloqueo | `foulsPerMatch` (`INFO`) | **NOT_READY** (sin banda) |
| `FatigueResistancePercent` | igual que `Stamina` | ninguna | **NOT_READY** (sin métrica alguna) |
| `SaveBonusClose` | parada cercana del portero | `saveRate` (`INFO`) | **NOT_READY** (sin banda) |
| `SaveBonusFar` | parada lejana del portero | `saveRate` (`INFO`) | **NOT_READY** (sin banda) |

**Ningún escalar sin precedente genera una capacidad falsa**: la tabla dice explícitamente, por fila, si
el sistema puede iterarlo solo (`READY`), necesita el instrumento de comportamiento en vez de una banda
(`READY_VIA_BEHAVIORAL_AUDIT`), necesita una primera medición sin la confianza de un precedente
(`INFERIDO`), o no puede iterarlo todavía (`NOT_READY`, con el motivo exacto: sin banda o sin métrica).

### 13.4.3 `ProbabilityKind.Foul` / `ProbabilityKind.Card`

Sin ningún perk real hoy. Misma conclusión que `FoulChanceBonus`: la métrica natural
(`foulsPerMatch`/`yellowCardsPerMatch`/`redCardsPerMatch`) es **`INFO` en las tres**, verificado en
`MatchMetrics.cs`. **`NOT_READY` para búsqueda automática de valor** — a diferencia de `Tackle`/`Injury`
(que sí tienen banda porque RT-056 los declaró críticos para "sensación de fútbol"), no existe ningún ADR
que diga qué tasa de faltas/tarjetas es aceptable. Un perk de este tipo necesita `DESIGN_REVIEW` para
fijar una banda antes de que el sistema pueda decidir solo.

### 13.4.4 `modifyBias`

2 perks reales (`diver`, `home_ref`). Sesga el criterio del árbitro por equipo (`ApplyBiasDelta`,
`_bias` clamped -100..100, reportado como `FinalBias` por partido).
**`NOT_READY` — falta la métrica agregada, no solo la banda.** `FinalBias` se guarda por partido en
`matches.csv` pero `MatchMetrics.Compute` no lo resume en ninguna fila de `summary.csv` — verificado,
no hay ninguna línea que lo mencione. Esto es tooling puro y sin ambigüedad de diseño (añadir una fila
`INFO` que resuma `FinalBias`/faltas-por-equipo), no una decisión que necesite `game-design-review`; se
lista aquí como pieza de tooling pendiente (§12), no como hueco de diseño.

### 13.4.5 Síntesis

| Grupo | Estado | Por qué |
|---|---|---|
| `Strength` (resolución bandeada), `Stamina`→no, `Leash`, `InjuryResistanceBonus`, `LeashBonus` | **READY** | banda RT-056 existente y mecanismo verificado |
| `AdjacentTeammateBonusPercent` | **READY_VIA_BEHAVIORAL_AUDIT** | mismo instrumento que C1, sin banda agregada |
| `Speed`, `SpeedBonusPercent` | **INFERIDO, SIN CONFIRMAR** | mecanismo y métrica existen, sin precedente real |
| `Technique`, `ShotQualityBonus`, `PassQualityBonus`, `FoulChanceBonus`, `Foul`, `Card`, `SaveBonusClose/Far` | **NOT_READY (sin banda)** | la métrica existe como `INFO`; hace falta `DESIGN_REVIEW` para fijar un rango antes de iterar |
| `Stamina` (perk directo), `FatigueResistancePercent` | **NOT_READY (sin métrica)** | no existe ningún cálculo de rendimiento por fase de partido |
| `modifyBias` | **NOT_READY (falta tooling)** | la métrica agregada no está escrita, aunque el dato (`FinalBias`) ya existe por partido |

**Hallazgo transversal, no anticipado en la matriz original**: seis de las nueve familias `NOT_READY`
comparten la misma causa — **RT-056 deja deliberadamente sin banda casi todas las métricas de "calidad"**
(pase, tiro, parada, falta/tarjeta), reservando bandas solo para las de "sensación de fútbol" agregada
(posesión, cadena de pases, tiros, resultado, tercio, entradas, lesiones). Esto no es un error de RT-056
—esas métricas se dejaron `INFO` a propósito, `docs/balance.md` lo dice explícitamente para varias de
ellas— pero sí es una limitación real y ya verificada de este sistema: **cualquier perk cuyo efecto
natural sea de calidad, no de cantidad, no puede iterarse solo hasta que alguien fije una banda.** No se
inventa una para destrabarlo.

---

## 14. Las cuatro preguntas operacionales

### A. ¿Qué mide?

Universales siempre (§4.1): exposición, activación, efecto directo, efecto de comportamiento, seguridad
(las siete de RT-056), determinismo (una vez, no por iteración). Específicas según la categoría del perk
(§4.2, derivada del `EffectType`/`ProbabilityKind`/predicado que usa, §3) — nunca todas a la vez.

### B. ¿Cuánto mide?

Puntos de partida operativos, no cotas estadísticas universales (§5.5): Screening arranca en 1 semilla,
40 partidos/brazo; Tuning en 2 semillas, 400 partidos/brazo combinando semillas, máximo 5 candidatos;
Validation reutiliza los datos de Tuning + batería RT-056 completa, máximo 1 semilla adicional si el
resultado es limítrofe. **Antes de aceptar cualquiera de estas muestras como suficiente, el harness
comprueba la potencia real de la métrica primaria** (`|delta| >= 2×error_estándar`, §5.5) y duplica la
muestra si no la alcanza, dentro del presupuesto. Los techos de tiempo (`≤30s`/`≤60s`/`≤30s` por fase) son
circuitos de seguridad de reloj, medidos en esta máquina para esta carga de trabajo — no una garantía de
que ese tiempo sea siempre suficiente. Presupuesto duro por perk: **10 minutos de reloj**, agotado el cual
el sistema para y escala (§9); presupuesto de lote: sin cifra de tiempo total fija, con checkpoint/
reanudación y un circuito de seguridad si escala una fracción alta de perks seguidos (§9.1).

### C. ¿Cómo decide?

Con las reglas deterministas de §6, fijadas antes de ver cada resultado: umbrales de exposición (§5.4,
marcados `[ASUNCIÓN]` donde falta calibración, y que producen `INSUFFICIENT_EVIDENCE` — no un
`INSUFFICIENT_EXPOSURE` con la confianza de un umbral medido — mientras lo estén), la comprobación de
potencia de §5.5, detección de monotonicidad y su ruptura (§6.2/§7), condiciones de descarte de seguridad
y de fidelidad de diseño (§6.3), y las siete condiciones explícitas de `BALANCED` (§6.4: comportamiento
esperado, efecto en rango, seguridad, evidencia suficiente, reproducibilidad, sin automatismo, sin
regresión sistémica — las siete, no un subconjunto). La salida siempre es uno de los estados de §9
(`CONTINUE/ACCEPT/REJECT/INSUFFICIENT_EXPOSURE/INSUFFICIENT_EVIDENCE/NEEDS_REPLICATION/SAFETY_LIMIT/
SYSTEMIC_REGRESSION/DESIGN_REVIEW/NOT_READY/BLOCKED_INFRA/RUN_LEVEL/WEAK_EFFECT_CEILING`), nunca una
conclusión libre.

### D. ¿Cómo itera?

Selecciona el siguiente valor según el tipo de parámetro (§7): tripleta anclada en una medición barata
para bonus/probabilidades, bisección entera para geometría, sin búsqueda numérica para selección de
objetivo. Antes de usar cada candidato en las reglas de monotonicidad, comprueba que tiene potencia
suficiente (§5.5) y duplica su muestra si no la tiene. Reduce el paso al acercarse a la banda objetivo
(elige el candidato válido de menor magnitud si varios cumplen). Valida el candidato elegido con las
siete condiciones explícitas de §6.4. Termina en `BALANCED` (listo para Regla E y escritura en `/data`,
ambas fuera de este sistema), o en cualquiera de los estados terminales de §9 dentro del presupuesto de
5 candidatos/10 minutos — nunca busca indefinidamente, ni a nivel de perk ni a nivel de lote (§9.1).

### ¿Cómo balancea un perk en minutos y no en horas?

Por diseño, no por casualidad: (1) reutiliza simulaciones — Validation no repite lo que Tuning ya midió;
(2) control/tratamiento emparejado sobre las mismas plantillas y semillas, que es lo que hizo posible usar
solo 400 partidos/brazo en vez de miles para el efecto que tenía Cazagoles; (3) todas las métricas de una
categoría salen de la misma tanda de partidos, nunca se relanza la simulación por métrica; (4) early
stopping en cada nivel (§5, §6), incluido el nuevo — duplicar solo cuando la potencia no alcanza (§5.5),
no siempre; (5) descarte temprano en Screening antes de gastar la muestra grande de Tuning; (6)
paralelización ya disponible en el patrón de `/Balance` (`Parallel.For` por índice, un `Catalog` por
hilo); (7) cero llamadas a un modelo de lenguaje dentro del bucle de medición (§11.1) — la latencia de
una API de LLM por iteración sería, ella sola, más lenta que las decenas de miles de partidos que este
piloto ya demostró poder simular en segundos.

**Con la salvedad que esta revisión obliga a hacer explícita**: la medición de C1 (tres candidatos, dos
semillas, Tuning + Validation completos) tardó **bajo 30 segundos de cómputo real** — eso es un hecho
medido, no una proyección. Que **ese mismo tiempo** baste para **cualquier** perk del catálogo es una
extrapolación, no el mismo hecho: perks con métricas de tasa base más baja (`injuriesPerMatch`) o con
`Limit` que reduce la activación pueden necesitar la duplicación de muestra de §5.5 una o más veces antes
de decidir, lo que sigue estando muy por debajo del techo de 10 minutos/perk pero ya no es "bajo 30
segundos" sin más. La afirmación defendible es: **minutos, no horas, con la comprobación de potencia de
§5.5 decidiendo cuánto exactamente dentro de ese margen** — no una promesa de un número fijo de segundos
para los 94 perks del catálogo.

---

## 15. Implementación del tooling mínimo (18 sep 2026)

Tras cerrar §13.4 (§13.4/§3.2, commit `707598a`), se implementó el subconjunto de §12 necesario para
demostrar que el protocolo es ejecutable — sin balancear los 94 perks, sin tocar `/data`, sin cerrar
Cazagoles/Ancla. Todo el código es determinista; ningún agente participa en el camino de medición (§11.1).

### 15.1 Qué se implementó

| Pieza (§12) | Fichero | Prueba |
|---|---|---|
| Selección automática de métricas | `Sim/Analysis/PerkBalanceClassifier.cs` | `PerkBalanceClassifierTests.cs` — 11 casos, incluida cobertura de los 94 perks reales sin excepción |
| Comprobación de potencia (§5.5) | `Sim/Analysis/BalancePowerCheck.cs` | `BalancePowerCheckTests.cs` — 7 casos, reproduce el hallazgo real de C1 (40 insuficiente, 200 suficiente, mismo efecto) |
| Motor de decisión (§6) | `Sim/Analysis/BalanceDecisionRules.cs` | `BalanceDecisionRulesTests.cs` — 19 casos, incluida la comprobación de que "pasar RT-056" no basta para `BALANCED` |
| Búsqueda de valor (§7) | `Sim/Analysis/BalanceValueSearch.cs` | tripleta anclada verificada contra los cuartiles reales de Cazagoles (§3.1b) |
| Registro (§10) | `Sim/Analysis/BalanceRegistry.cs` (serialización pura) + `Sim.Tests/Balance/BalanceRegistryFile.cs` (E/S) | round-trip y reanudación |
| Harness genérico de control/armado (§12, pieza 2) | `Sim.Tests/Balance/PairedBalanceHarness.cs` | ejercitado por el demo de extremo a extremo |
| Demo de extremo a extremo | `Sim.Tests/Balance/EndToEndProtocolDemoTests.cs` | dos pruebas, ver §15.2 |

**Separación deliberada**: `BalanceRegistry` (en `/Sim`) solo serializa texto — RT-012 prohíbe que `/Sim`
toque disco, y el analizador de arquitectura del proyecto (`ArchitectureTests.
NoReferenceToForbiddenFrameworkTypes`) directamente **prohíbe referenciar el tipo** `DateTimeOffset`
dentro de `Underleague.Sim.dll`, no solo llamarlo — se descubrió al compilar (§15.3) y se corrigió
guardando la marca de tiempo como texto ISO-8601, resuelta por quien orquesta, nunca por `/Sim`. La
lectura/escritura de fichero vive en `Sim.Tests/Balance/BalanceRegistryFile.cs` (o, cuando exista el CLI
de §12 punto 5, en `/Balance`).

### 15.2 El demo de extremo a extremo, y lo que demuestra de verdad

`EndToEndProtocolDemoTests` usa un perk de prueba construido en memoria (`demo_tackle_fixture`,
`modifyProbability` sobre `Tackle`, misma condición que `own_third_anchor` ya calibrada en Tanda 0 —
nunca escrito en `/data`) y dos pruebas:

- **`ProtocolTraversesClassificationScreeningTuningAndValidation`**: clasifica el fixture, mide Screening
  (40 partidos/brazo, semilla 1) y **se detiene correctamente en `SAFETY_LIMIT`** — `injuriesPerMatch`
  sale de banda (0,23-0,28 contra el suelo de 0,3) por ruido de muestra pequeña, el mismo fenómeno ya
  medido en C1 §5 con 20 plantillas. Es exactamente uno de los dos resultados que pedía el criterio de
  éxito del encargo ("o detenerse correctamente en uno de los estados de escalado"): el sistema no fuerza
  el resultado ni amplía la muestra a mitad de la prueba para conseguir un `BALANCED` más vistoso.
- **`TuningAndValidationMachineryWorksOnRealSimulatedData`**: ejercita Tuning→Validation de forma
  independiente (mismo fixture, semillas 2-3), con datos reales simulados en cada paso — tripleta,
  monotonicidad, potencia, réplica, determinismo (RT-024) y las siete condiciones de §6.4. Con este
  fixture concreto el resultado es `REJECT` (el delta observado es negativo y la potencia es
  insuficiente, ver §15.3), lo cual es el comportamiento correcto: el motor de decisión no acepta un
  candidato cuyo efecto no está ni siquiera en la dirección esperada.

**Ninguna de las dos pruebas afirma que `demo_tackle_fixture` esté balanceado.** Demuestran que el
mecanismo —clasificar, medir, decidir, registrar— corre de punta a punta sobre partidos reales sin
excepciones, y que se detiene en un estado reconocido en vez de continuar sin evidencia.

### 15.3 Dos hallazgos reales, encontrados al ejecutar el demo (no anticipados en el diseño)

1. **`ProbabilityKind.ShotOnTarget`/`Save`/`Pass`/`Intercept`/`InterceptEvasion`/`Dribble` también
   carecían de banda**, y la primera versión del clasificador (§13.4) solo marcaba `Foul`/`Card` como
   excepción `NotReadyNoBand` — el resto de `ProbabilityBonus` se clasificaba `Ready` sin comprobar que
   su métrica natural fuera realmente una de las siete bandeadas. El error apareció al ejecutar el demo
   sobre datos reales (la fila `RangeMin`/`RangeMax` de `MatchMetrics.Compute` para esas métricas es
   siempre `null`), no al releer el documento — es exactamente el tipo de error que una prueba
   determinista sobre datos reales encuentra y una revisión de prosa no. Corregido en
   `PerkBalanceClassifier.NoBandProbabilities`: solo `Tackle`/`TackleEvasion` (tacklesPerMatch) e
   `Injury`/`Injure`/`SevereInjury` (injuriesPerMatch) quedan `Ready` dentro de `ModifyProbability`.
2. **`tacklesPerMatch` cuenta intentos de entrada, no entradas ganadas** — un bono a la probabilidad de
   *ganar* una entrada ya iniciada no tiene por qué mover el número de entradas *intentadas* (esa
   decisión la toma `Utility.Choose`, no `ModifyProbability`). El demo lo muestra literalmente: los tres
   candidatos (40/60/100) dieron el mismo `tacklesPerMatch` armado/control. **`tacklesPerMatch` es un
   proxy débil para un bono de probabilidad de victoria de la entrada** — la métrica mecánicamente
   sensible sería una tasa de victoria de entrada (`tacklesWon`/`tacklesAttempted`), que hoy **no existe
   como fila agregada en `MatchMetrics.Compute`** (solo hay contadores por jugador, sin resumen de
   partido). Se añade a §13 como pieza de tooling pendiente — no se inventa una tasa ni se cambia el
   fixture para ocultar el hallazgo.

### 15.4 Qué NO se implementó (a propósito, por instrucción explícita)

- El CLI de `/Balance` (§12, punto 5) y el volcado de utilidad automatizado por categoría (punto 6):
  quedan como piezas de implementación futura, no bloquean la demostración de mecanismo.
- La variante de harness multi-objetivo (`pack_mentality` y similares, §3.2 punto 7): sigue pendiente.
- Ninguna categoría `NOT_READY`/`DESIGN_REVIEW` de §13.4 (calidad de tiro/pase, `Stamina`, sesgo
  arbitral) se forzó a "funcionar" para el demo — el fixture elegido es deliberadamente de la única
  categoría ya `Ready` con precedente real (`ProbabilityBonus` sobre `Tackle`).
- No se ha balanceado ningún perk real, no se ha tocado `/data`, no se han cerrado Cazagoles ni Ancla, y
  `MinPassChainRatio` no ha intervenido en ningún punto de esta fase.

Verificado: 802/802 pruebas no-puerta en verde (incluida `ArchitectureTests.
NoReferenceToForbiddenFrameworkTypes`, que detectó y forzó a corregir el uso de `DateTimeOffset` en
`/Sim`) y las 43 puertas sin ninguna nueva roja (las mismas de siempre, documentadas en BB-P).

## 16. Auditoría de readiness del catálogo real (18 sep 2026)

Auditoría estática de los 94 perks de `data/perks/*.json` contra el clasificador (§13.4/§3.1/§3.2),
antes de lanzar el primer `SCREENING` real. Sin simular ningún partido: `Sim.Tests/Analysis/
PerkAuditTests.cs` (`PrintAggregateReport`, `AggregateCountsAreFixedAsRegression`) recorre el catálogo
cargado y clasifica cada perk con `Sim/Analysis/PerkAudit.cs`, nuevo, compuesto sobre
`PerkBalanceClassifier`. El resultado agregado queda fijado como regresión: si cambia, es porque el
catálogo o el clasificador cambiaron, y hay que mirar por qué antes de aceptar el nuevo número.

### 16.1 Resultado agregado

```
Total perks: 94

ReadyForScreening: 25
MultiTarget:       22
DesignReview:      13
NotReady:          27
RunLevel:           7
```

**`NOT_READY` por motivo** (mutuamente excluyentes — cada perk cuenta en uno solo):

```
NeedsCampaignHarness:     17   (AccumulatesAcrossMatches + efecto acompañante escalado por contador)
MissingPrimaryMetric:      7   (Immunity ×3, CancelEvent DEATH, ModifyKnockdownTicks, Relocate)
MissingAggregateMetric:    2   (modifyBias: diver, home_ref)
MultiEffectAttribution:    1   (unlikely_bulwark)
```

**`DESIGN_REVIEW` por motivo**:

```
MissingBand:              11   (calidad de tiro/pase/parada, CancelEvent FOUL/GOAL)
AmbiguousPrimaryMetric:    2   (Strength sin señal estructural: brute_boots, comeback_spirit)
```

### 16.2 Tabla representativa (no las 94 filas — se generan con `dotnet test --filter
FullyQualifiedName~PerkAuditTests.PrintAggregateReport`)

| perk | effect type | target shape | readiness | blocking reason | primary metric |
|---|---|---|---|---|---|
| `own_third_anchor` | ModifyProbability | SingleOwner | **ReadyForScreening** | — | tacklesPerMatch |
| `iron_price` | ModifyTraitScalar | SingleOwner | **ReadyForScreening** | — | injuriesPerMatch |
| `sweeper_keeper` | ModifyLeash | SingleOwner | **ReadyForScreening** | — | ballThirdMaxShare |
| `double_shot` | ExtraAction | SingleOwner | **ReadyForScreening** | — | shotsPerMatch |
| `iron_gate` | CancelEvent | SingleOwner | **ReadyForScreening** | — | injuriesPerMatch |
| `pack_mentality` | ModifyAttribute | Population | **MultiTarget** | — | (ambiguo, además) |
| `pivot_duo` | ModifyProbability | Population | **MultiTarget** | — | tacklesPerMatch |
| `dirty_play` | Injure+SetState | SingleOther | **MultiTarget** | — | — |
| `box_predator` | ModifyProbability | SingleOwner | **DesignReview** | MissingBand | shotsOnTargetShare |
| `brute_boots` | ModifyAttribute | SingleOwner | **DesignReview** | AmbiguousPrimaryMetric | ambiguo (3 opciones) |
| `mob_instigator` | CancelEvent | SingleOwner | **DesignReview** | MissingBand | foulsPerMatch |
| `poacher_instinct` | ModifyProbability+AddCounter | SingleOwner | **NotReady** | NeedsCampaignHarness | shotsOnTargetShare |
| `unlikely_bulwark` | ModifyProbability+ModifyLeash | SingleOwner | **NotReady** | MultiEffectAttribution | tacklesPerMatch |
| `diver` | ModifyBias | SingleOwner | **NotReady** | MissingAggregateMetric | FinalBias/faltas por equipo |
| `roots` | Immunity | SingleOwner | **NotReady** | MissingPrimaryMetric | — |
| `no_dying` | CancelEvent (DEATH) | SingleOwner | **NotReady** | MissingPrimaryMetric | — |
| `loan` | AddCounter | SingleOwner | **RunLevel** | — | FullRunMetrics |
| `quick_learner` | ModifyExperience | SingleOwner | **RunLevel** | — | FullRunMetrics |

### 16.3 Los casos con atención especial que pedía el encargo

- **`ProbabilityBonus` sin banda válida**: comprobación sistemática (`NoPerkIsReadyForScreeningWithoutAKnownBandedOrBehavioralMetric`,
  §16.4), no una lista manual — compara la métrica primaria de cada `ReadyForScreening` contra el
  conjunto de nombres `INFO` de `MatchMetrics`. Ningún perk real cae hoy en `Foul`/`Card`/`ShotOnTarget`/
  `Save`/`Pass`/`Intercept`/`InterceptEvasion`/`Dribble` como *único* efecto (los que lo intentan, como
  `box_predator`/`cold_focus`/`safety_net`/`killing_range`/`forward_line`/`long_range_menace`
  (`shotOnTarget`), `crowd_control`/`fine_touch`/`flank_specialist` (`pass`), quedan correctamente en
  `DESIGN_REVIEW`.
- **`modifyAttribute`**: cada atributo con perk real (`Strength`, `Stamina`) tiene su propio veredicto —
  `Strength` es `AmbiguousPrimaryMetric` (ninguno de los 4 perks reales da una señal estructural, §13.4.1),
  `Stamina` combinado con `AccumulatesAcrossMatches` cae en `NeedsCampaignHarness` (`iron_lungs`,
  `scar_veteran`). No se asume una capacidad por el mero hecho de que exista `TraitScalarKind`/
  `AttributeKind` — verificado perk a perk.
- **`addCounter`**: tres grupos reales, no supuestos — 6 perks en solitario (`RunLevel`), 17 con efecto
  acompañante escalado por contador (`NeedsCampaignHarness`, el harness correcto es
  `Balance/PerkValueRunner.cs`, no el de este protocolo), 0 casos de "depende de modifyProbability sin
  contador" (los 14 `modifyProbability`+`addCounter` reales SIEMPRE usan `UsesCounter` en la parte de
  partido, verificado — no hay ningún caso intermedio hoy).
- **`Limit`**: ya no se confunde con exposición insuficiente (§5.4/§6.1, corregido en la revisión
  anterior) — la auditoría añade `TriggerFrequencyCategory` (estático, por el propio `Trigger`, sin
  simular) y anota, para cada perk con `Limit`, si su disparador ya es raro por diseño (`FOUL`/`INJURY`/
  `GOAL`/`DEATH`) o frecuente (`TACKLE`/`SHOT`/...), sin que eso cambie el veredicto de readiness por sí
  solo — comprobado sistemáticamente (`LimitIsNeverTreatedAsInsufficientExposureByItself`).
- **Multi-target**: 22 perks, no solo `pack_mentality` — desglose real por forma de destinatario:
  `Population` (`Team`/`OpposingTeam`/`WithTag`/`Adjacent`/`AdjacentOpponents`/`Linked`/`LinkedWithTag`,
  la mayoría) y `SingleOther` (`Target`/`Opponent`: `dirty_play`, `iron_studs`) — ninguno tratado como si
  el harness de un solo portador pudiera medirlo.
- **Multi-efecto**: 27 perks con más de un efecto (§3). De ellos, 17 son el caso `addCounter`+
  acompañante (resuelto arriba), 1 (`deep_run`) combina dos efectos de la MISMA categoría (Geometry) y se
  queda `ReadyForScreening` sin ambigüedad, y 1 (`unlikely_bulwark`) combina DOS categorías distintas
  (`ProbabilityBonus`+`Geometry`) — el único caso real de atribución no resuelta, marcado
  `MultiEffectAttribution` en vez de asumir que el primer efecto es "el" parámetro.

### 16.4 Segunda pasada — comprobación de consistencia (bugs encontrados y corregidos)

Ejecutando el clasificador contra los 94 perks reales (no contra ejemplos escogidos a mano) aparecieron
tres errores que la primera versión del clasificador (§13.4, `fd8f2d2`) no tenía cubiertos, corregidos en
esta misma sesión con un test de regresión cada uno:

1. **`Immunity`/`CancelEvent`/`ExtraAction` no eran un bloque uniforme "Ready".** La versión anterior
   asumía que cualquier efecto binario (cancela o repite un suceso) tenía automáticamente una métrica
   válida. Verificado que no: los cuatro `ImmunityKind` (`Push`/`Mourning`/`MinorInjuryPenalty`/
   `MinorInjuryClinicCost`) son desplazamiento físico o coste entre partidos, sin ninguna fila en
   `MatchMetrics` — los 3 perks reales (`half_leg`, `tough_hide`, `roots`) pasan de `Ready` a
   `NotReady(MissingPrimaryMetric)`. `CancelEvent` depende de qué evento cancela (`perk.Trigger`): solo
   `INJURY` (`iron_gate`) tiene banda; `FOUL`/`GOAL` (`mob_instigator`/`hand_of_god`) son INFO
   (`DesignReview`); `DEATH` (`no_dying`) no tiene ninguna fila agregada en absoluto (`NotReady`). Solo
   `ExtraAction` resultó estar bien: sus dos disparadores reales (`SHOT`/`TACKLE`) sí tienen banda.
2. **El catch-all de "caso singular" marcaba `Inferred` (que el motor de decisión trata como listo para
   `SCREENING`) con una métrica placeholder no accionable** ("universales + métrica del suceso", una
   cadena descriptiva, no un nombre de `MatchMetrics`). Afectaba a `hot_blooded` (`ModifyKnockdownTicks`),
   `last_man` (`Relocate`) y `quick_learner` (`ModifyExperience`) — los tres se habrían marcado listos
   sin tener ninguna métrica real que medir. Corregido: el catch-all ahora es `NotReadyNoMetric`, y
   `ModifyExperience` (que ya declaraba en su propio docblock que actúa fuera del partido) se reclasificó
   a `RunLevel`, que es lo que realmente es.
3. **`AccumulatesAcrossMatches` se trataba como un bloque uniforme "RunLevel"**, mezclando la economía
   pura (6 perks, ningún efecto de partido) con perks que sí tienen un mecanismo de partido pero cuya
   magnitud depende de un contador que persiste entre partidos (17 perks — un partido suelto con el
   contador a cero no representa su comportamiento típico a mitad de run). Separados en dos categorías
   distintas (`RunLevelCounter` vs. `AccumulatedStateBonus`/`NeedsCampaignHarness`), con la pista correcta
   de qué instrumento usar en cada caso (`FullRunMetrics` vs. `Balance/PerkValueRunner.cs`).

**Comprobaciones automáticas añadidas** (`Sim.Tests/Analysis/PerkAuditTests.cs`), para que el clasificador
no pueda volver a marcar `READY` un caso estructuralmente no medible sin que un test falle:

- `NoPerkIsReadyForScreeningWithoutAKnownBandedOrBehavioralMetric` — ningún `ReadyForScreening` usa una
  de las 15 métricas `INFO` conocidas de `MatchMetrics`.
- `NoPerkIsReadyForScreeningWithMultiTargetEffects` — ningún `ReadyForScreening` tiene un destinatario
  distinto de `SingleOwner`.
- `NoPerkIsReadyForScreeningWithUnresolvedCrossCategoryEffects` — ningún `ReadyForScreening` mezcla
  categorías sin resolver cuál es el parámetro.
- `NoPerkIsReadyForScreeningWhenItNeedsTheCampaignHarness` — ningún `ReadyForScreening` necesita en
  realidad el harness de campaña.
- `EveryNotReadyEntryHasAConcreteReason` / `EveryDesignReviewEntryHasAConcreteReason` — ningún perk
  bloqueado se queda con un motivo `None` (el encargo: "evitar un sistema que diga NOT_READY sin explicar
  qué falta").
- `ReasonsAreMutuallyExclusivePerEntry` — ningún perk tiene a la vez un motivo de `NOT_READY` y uno de
  `DESIGN_REVIEW`.
- `LimitIsNeverTreatedAsInsufficientExposureByItself` — ningún perk con `Limit` queda bloqueado sin un
  motivo concreto que no sea el propio `Limit`.
- `AggregateCountsAreFixedAsRegression` — los cinco números de §16.1, fijados: si el catálogo o el
  clasificador cambian de forma que mueva alguno, el test falla y obliga a mirar por qué.

### 16.5 Respuesta al criterio de salida de esta fase

**De los 94 perks reales, 25 pueden entrar automáticamente en `SCREENING` hoy** (`ReadyForScreening` —
incluye los que no tienen parámetro numérico y por tanto saltan directo a una `VALIDATING` cualitativa,
§6.1/§6.5: `TargetSelection` y `BinaryEvent` sin valor propio). **69 no, y para cada uno se sabe
exactamente por qué**: 22 por un destinatario que el harness de un solo portador no soporta
(`MultiTarget`), 13 porque la banda o la elección de métrica necesita una decisión de diseño humana
(`DesignReview`), 27 por un hueco de tooling concreto y nombrado (`NotReady`, desglosado en §16.1), y 7
porque el instrumento correcto es `/Balance --full-runs`, no este protocolo (`RunLevel`, que no es un
fallo).

*(Nota: §17 reclasifica dos de estos 25 tras encontrar que eran habilidades raciales automáticas, no
elecciones de portador — el total `ReadyForScreening` final es 24, ver §17.6.)*

---

## 17. Infraestructura de familias enteras: qué significa que READY sea un contrato (18 sep 2026)

Encargo explícito tras cerrar §16: no empezar el `SCREENING` de los 25 `ReadyForScreening` todavía.
Primero, cerrar los bloqueos de **infraestructura** que afectan a familias enteras (no a un perk suelto) y
demostrar con ejecuciones reales — no solo con la etiqueta del clasificador — que `READY_FOR_SCREENING`
significa "puede recorrer el pipeline completo", no "tiene una métrica". Sin tocar `/data`, sin cambiar
ningún valor de perk, sin cerrar Cazagoles ni Ancla, sin tocar `MinPassChainRatio`, sin gameplay, sin
inventar bandas, sin screening masivo — solo infraestructura, clasificación, contratos y tests.

### 17.1 `NeedsCampaignHarness` (17 perks): conectar el harness real, sin duplicar `PerkValueRunner`

El clasificador ya sabía distinguir estos 17 perks (§16.4, punto 3); lo que faltaba era demostrar la
tubería completa `perk real → classifier → harness de campaña correcto → control/treatment → métricas →
decision engine` con al menos un perk real, sin reimplementar el arrastre de contador entre partidos que
`Balance/PerkValueRunner.cs` (ADR 0070/0087) ya tiene resuelto y probado.

- `Sim.Tests/Sim.Tests.csproj` gana una referencia a `Balance.csproj` (sin dependencia circular: `Balance`
  no referencia `Sim.Tests`).
- `Sim.Tests/Balance/CampaignBalanceHarness.cs` (nuevo): envuelve `PerkValueRunner.Run` con un filtro de un
  solo perk (`CampaignMatches` = 8 partidos, el recorrido real del contador según ADR 0070, no un número
  elegido aquí) y convierte la fila resultante en la observación que ya entienden
  `BalancePowerCheck`/`BalanceDecisionRules`: la tasa de victoria emparejada como proporción binomial
  (media *p*, varianza *p(1-p)* — la fórmula estándar, no una aproximación nueva), reutilizando
  `PerkValueRow.PairedValueMilli`/`Wins`/`ControlWins` que la ADR 0087 ya valida.
- `HarnessSelector.SelectHarness(PerkAuditEntry)` (nuevo, `Sim.Tests/Balance/`): dispatcher puro que decide
  `SingleMatch`/`Campaign`/`None` a partir del veredicto de auditoría — quien orquesta el pipeline no
  decide a mano cuándo usar cada harness.
- **Demostración con perk real** (`Sim.Tests/Balance/CampaignBalanceHarnessTests.cs`, `pit_veteran`):
  clasificador → `AccumulatedStateBonus`/`NeedsCampaignHarness` → auditoría → `NotReady(NeedsCampaignHarness)`
  → `HarnessSelector` → `Campaign` → `CampaignBalanceHarness.Run` (160 partidos reales, semilla fija) →
  armado 99 victorias (61,9 %) / control 96 victorias (60,0 %), 25 activaciones → `deltaWinRate = 0,0188`
  → `BalancePowerCheck` (potencia insuficiente a *n*=20 plantillas) → `BalanceDecisionRules` → estado final
  `NeedsReplication`. La tubería completa funciona de punta a punta con datos reales; el veredicto de
  potencia insuficiente es correcto para este tamaño de muestra, no un fallo del harness.
- **No se ha balanceado ni medido en firme ningún valor de `pit_veteran`** — es la demostración de
  conectividad que pedía el encargo, no un cierre de balance.

### 17.2 `FinalBias` (2 perks: `diver`, `home_ref`): existe el agregado, sigue sin banda

`Sim/Analysis/RefereeBiasMetrics.cs` (nuevo): agregado puro sobre `(FinalBias, CarrierTeam)` —
`BiasFavoring` normaliza `MatchEngine.BiasFor` (relativo al equipo 0) al equipo del portador,
`MeanBiasFavoringCarrier`/`VarianceBiasFavoringCarrier` resumen una serie de partidos ya jugados
reutilizando `BalancePowerCheck.SampleVariance`. No duplica ninguna fórmula del motor: `FinalBias` ya
existe por partido en `MatchReport`, esto solo lo normaliza y agrega desde fuera de `/Sim` propiamente
dicho (el fichero vive en `Sim/Analysis/`, sin E/S, pero no toca el cálculo del propio `MatchEngine`).

**Distinción explícita que pedía el encargo — "que exista el agregado" no es "que sea suficiente":**
`PerkBalanceClassifier` reclasifica `ModifyBias` de `NotReadyMissingAggregate` a `NotReadyNoBand` (test
`RefereeBiasAggregateExistsButStillHasNoBand`, `Sim.Tests/Analysis/PerkBalanceClassifierTests.cs`). El
motivo del cambio de nombre es literal: antes no existía ningún cálculo agregado de sesgo; ahora existe,
pero **no hay ninguna banda RT-056 ni ADR** que diga qué rango de sesgo medio a favor del portador es
aceptable — sin ese criterio, marcar `READY` sería inventar una banda, que el encargo prohíbe
explícitamente. `diver`/`home_ref` siguen en `DESIGN_REVIEW` (`MissingBand`), igual que antes, solo que
ahora la razón documentada es la correcta: falta la banda, no falta la métrica.

### 17.3 Multi-target (22 perks): soporte estructural, no por perk, probado con `pack_mentality`

`Sim/Analysis/EffectPopulationResolver.cs` (nuevo): resuelve, **por forma de destinatario** (no por
tabla de casos por perk), qué índices de jugador afecta realmente un efecto sobre una plantilla ya
generada — reutilizando `PlayerDefinition.HasTag`, el mismo primitivo que usa `EffectEngine.ResolveTargets`
en el motor real, sin duplicar la resolución dinámica de pares/adyacencia. Tres estados honestos en vez de
fingir una resolución:

- **`Resolved`**: `Owner`/`Actor` (un destinatario), `Team`/`OpposingTeam` (equipo completo), `WithTag`
  (los de la plantilla con esa etiqueta) — todos resolubles con los datos ya generados de `TeamSetup`.
- **`RequiresLiveMatchState`**: `Adjacent`/`AdjacentWithTag`/`AdjacentOpponents` (adyacencia estática o
  dinámica, ADR 0021/AY) y `Target`/`Opponent` (rival concreto del evento) — dependen de estado del
  partido en vivo; el resolver **no inventa una aproximación**, lo deja marcado.
- **`RequiresLinkResolution`**: `Linked`/`LinkedWithTag` — dependen de `LinkTable` (ADR 0021), no
  reproducida aquí; tampoco se adivina quién está vinculado.

**Guardia explícita contra la atribución falsa que advertía el encargo** ("efecto sobre N actores ≈ efecto
sobre el portador" no debe pasar): `PopulationResolutionResult.AffectedCount` expone cuántos jugadores
*distintos del portador* recibe el efecto, y el test `PackMentalityResolvesToMoreThanTheOwnerOnAtLeastOneRealRoster`
(`Sim.Tests/Balance/EffectPopulationResolverTests.cs`) genera 30 plantillas reales con semillas distintas y
mide el tamaño de la población real de `pack_mentality` (`WithTag:Brute`) en cada una: `2,2,1,0,2,0,1,0,1,
1,1,1,1,0,0,1,1,1,2,1,0,1,1,1,0,0,3,1,0,0` — varía de 0 a 3 según la plantilla generada, nunca un número
fijo asumido. 13 tests en total, cubriendo los tres estados y las formas de destinatario reales del
catálogo.

**Por qué esto no cierra `pack_mentality` como `ReadyForScreening`**: saber *a quién* afecta el efecto no
resuelve la ambigüedad de *qué métrica* mide `Strength` en sí (§16.4, `AmbiguousPrimaryMetric` — ninguno de
los 4 perks reales de `Strength` da una señal estructural para elegir entre tackle/foul/shot/dribble/
block/injury) — son dos huecos distintos y `pack_mentality` los tiene ambos. Queda `MultiTarget`,
correctamente: se resolvió la infraestructura de destinatario, no se inventó el resto.

### 17.4 Los 15 `DesignReview`: auditoría de que son de verdad decisiones de diseño

No se ha intentado hacer auto-tuneable ninguno de los 15 artificialmente. Por familia:

- **`MissingBand` — calidad de tiro/pase/parada (11 perks: `box_predator`, `cold_focus`, `safety_net`,
  `killing_range`, `forward_line`, `long_range_menace`, `crowd_control`, `fine_touch`,
  `flank_specialist` y equivalentes)**: el mecanismo (`ModifyProbability` sobre `ShotOnTarget`/`Pass`/
  `Save`) es perfectamente medible — la fila de `MatchMetrics` existe (`shotsOnTargetShare`,
  `passCompletionRate`, `saveRate`) — pero está declarada `INFO`, sin `RangeMin`/`RangeMax` en
  `docs/balance.md`. Falta una decisión humana: **qué rango de estas métricas es "sano" para el juego**,
  no un dato que el tooling pueda derivar del propio perk.
- **`MissingBand` — `CancelEvent` FOUL/GOAL (`mob_instigator`, `hand_of_god`)**: mecanismo binario claro
  (cancela una falta/gol), pero foulsPerMatch/goalsPerMatch también son `INFO` — falta la misma decisión de
  banda, no un problema de medición.
- **`MissingBand` — `RefereeBias` (`diver`, `home_ref`)**: cubierto en 17.2 — el agregado ya existe, la
  banda de sesgo aceptable no.
- **`AmbiguousPrimaryMetric` — `Strength` (`brute_boots`, `comeback_spirit`)**: cubierto en 17.3 —
  ambigüedad de atribución de la métrica primaria entre 5-6 resoluciones distintas, sin señal estructural
  del propio dato (`positionOnly`, familia) para elegir. Es una pregunta de diseño ("¿qué debería mejorar
  Strength en la práctica?"), no de tooling.

En los cuatro casos, lo que falta es **un criterio que solo puede venir de una decisión de diseño humana**
(un rango objetivo, una prioridad de atribución) — el tooling ya sabe medir el mecanismo; no le falta
instrumento, le falta que alguien decida el número o el criterio. Ninguno se ha marcado `READY` por existir
ya el mecanismo o el agregado.

### 17.5 La prueba fundamental: el contrato de `READY_FOR_SCREENING`

`Sim.Tests/Balance/ReadyContractTests.cs` (nuevo): para **todo** perk que la auditoría marca
`ReadyForScreening`, comprueba con una ejecución real (5 plantillas, semilla fija — no cientos de
partidos) que los diez componentes existen, no solo que la etiqueta del clasificador lo diga:

1. harness válido (`HarnessSelector` da `SingleMatch`)
2. control definido (partidos de control > 0)
3. treatment definido (partidos armados > 0)
4. métrica primaria válida (no vacía, no una de las 15 métricas `INFO`)
5. baseline/control comparable (mismo número de partidos armado y control, mismas plantillas/semillas)
6. criterio de exposición evaluable
7. criterio de exposición insuficiente evaluable
8. criterio de decisión (`BalanceDecisionRules.EvaluateScreening` produce un estado definido)
9. safety metrics aplicables (las 7 métricas obligatorias de RT-056 se calculan sobre estos partidos)
10. estrategia de búsqueda compatible (`BalanceSearchStrategy`: coherente con si el perk tiene o no
    parámetro numérico)

Si falta cualquiera, el test falla con el motivo exacto — `READY_FOR_SCREENING` deja de ser una etiqueta
del clasificador y pasa a ser un contrato verificable, tal como pedía el encargo.

**Esta prueba, ejecutándose contra datos reales, encontró dos bugs de clasificación que la auditoría
puramente estática de §16 no podía ver:**

1. **`iron_gate`** (`CancelEvent`/INJURY, `race=Dwarf`, `tagsRequired=[Dwarf]`): `PairedBalanceHarness`
   generaba siempre plantillas `Race.Human` — nunca podía encontrar un titular elegible de raza Enana. El
   contrato falló con "0 partidos armados". **Corregido**: `PairedBalanceHarness` acepta ahora una
   `Race?` opcional (`RunWithEligibleCarrier` pasa `perk.Race`); la generación usa `race ?? NeutralRace` en
   vez de una raza fija — el mismo criterio que ya usa `PerkValueRunner.Measure`
   (`var race = perk.Race ?? NeutralRace`), no una regla nueva.
2. **`elf_touch`** (y, revisando el mismo patrón, otros 4: `hot_blooded`, `numb`, `quick_learner`,
   `roots` — una habilidad racial automática por raza): su propio `_doc` dice literalmente "se asigna
   automáticamente a toda la plantilla élfica y no ocupa slot" — no es una elección de portador, así que
   el harness de portador único nunca encontrará un "titular elegible" para él, con o sin arreglo de raza.
   **Corregido**: `PerkAudit.Audit` gana un parámetro `Catalog` y una comprobación temprana
   (`perk.Id == catalog.Race(perk.Race).Ability`) que enruta las habilidades raciales a
   `NotReady(RacialAbility)` — exactamente el mismo criterio de exclusión que ya usa
   `Balance/PerkValueRunner.cs` internamente (`if (perk.Id == catalog.Race(race).Ability) skip`); no una
   regla inventada para esta auditoría.

Tras ambos arreglos, los 24 `ReadyForScreening` restantes (25 − 1, porque `elf_touch` pasa a
`RacialAbility`) pasan el contrato completo con ejecuciones reales. `ContractCatchesAKnownBadCaseIfSomeoneMisclassifiesIt`
(prueba negativa sobre `mob_instigator`, un `DesignReview` real) confirma que el contrato también rechaza
correctamente un caso que no debería pasar.

### 17.6 Test con perks reales: el camino de cada estado

`Sim.Tests/Balance/FivePathsDemoTests.cs` (nuevo): sin tocar `/data`, muestra el camino
clasificador → auditoría → harness para un perk real de cada estado, sin ejecutar ningún partido (solo
consulta de datos ya cargados y de las funciones puras de decisión):

| perk | estado final | por qué | harness seleccionado |
|---|---|---|---|
| `own_third_anchor` | `ReadyForScreening` | `ModifyProbability(tackle)` condicionado a zona propia — mecanismo y banda confirmados desde Tanda 0 | `SingleMatch` |
| `pit_veteran` | `NotReady`/`NeedsCampaignHarness` | `AddCounter`+`ModifyProbability(tackle)` escalado por contador que persiste entre partidos | `Campaign` |
| `pack_mentality` | `MultiTarget` | `ModifyAttribute(strength)`, destinatario `WithTag:Brute` — afecta a varios jugadores (§17.3) | `None` |
| `box_predator` | `DesignReview`/`MissingBand` | `ModifyProbability(shotOnTarget)` — métrica natural es `INFO`, sin banda (§17.4) | `None` |
| `unlikely_bulwark` | `NotReady`/`MultiEffectAttribution` | `ModifyProbability(tackle)`+`ModifyLeash` — dos categorías distintas, atribución sin resolver | `None` |

6 tests, todos en verde. `NoDataFileWasReadOrWrittenByThisDemo` confirma explícitamente que la demo no
toca `/data`.

### 17.7 Resultado agregado tras esta fase

Los mismos cinco números de §16.1, con tres cambios, cada uno con su causa exacta: `ReadyForScreening`
baja de 25 a **24** (`elf_touch` sale por ser habilidad racial, §17.5), `RunLevel` baja de 7 a **6**
(`quick_learner` es también habilidad racial de su raza), y `DesignReview` sube de 13 a **15**
(`diver`/`home_ref` se mueven aquí desde `NotReady`/`MissingAggregateMetric` — §17.2: el agregado de sesgo
ya existe, pero sigue faltando la banda, así que el motivo correcto es `MissingBand`, no "no hay
métrica"). El resto de la reclasificación de `NotReady` (§17.5: `roots`/`hot_blooded`/`numb` pasan de
`MissingPrimaryMetric` a `RacialAbility` por el mismo motivo que `elf_touch`/`quick_learner`) mantiene el
total de `NotReady` fijo en 27. Fijado como regresión en `PerkAuditTests.AggregateCountsAreFixedAsRegression`:

```
Total perks: 94

ReadyForScreening: 24
MultiTarget:       22
DesignReview:      15
NotReady:          27
RunLevel:           6
```

**`NOT_READY` por motivo** (mutuamente excluyentes):

```
NeedsCampaignHarness:     17
RacialAbility:             5   (elf_touch, hot_blooded, numb, quick_learner, roots)
MissingPrimaryMetric:      4
MultiEffectAttribution:    1   (unlikely_bulwark)
```

**`DESIGN_REVIEW` por motivo**:

```
MissingBand:              13
AmbiguousPrimaryMetric:    2
```

### 17.8 Qué queda bloqueado y por qué (sin cerrar aquí)

- **22 `MultiTarget`**: la infraestructura de destinatario existe (§17.3), pero el harness de
  control/treatment para una población variable (no un solo portador) sigue sin construirse — es el
  siguiente hueco de tooling, no de clasificación.
- **15 `DesignReview`**: necesitan una decisión humana de banda o atribución (§17.4) — el tooling no puede
  ni debe inventar esa decisión.
- **22 `NotReady(NeedsCampaignHarness)`** (incluye los 17 de §16 más `poacher_instinct`/`scar_veteran`/etc.
  ya contados ahí — el número de §17.7 es 17 tras el ajuste de `RacialAbility`, no ha cambiado el
  mecanismo): el harness de campaña ya está conectado y demostrado (§17.1), pero solo se ha ejecutado
  contra UN perk (`pit_veteran`) como demostración — falta recorrer los 16 restantes, deliberadamente
  fuera de este encargo ("no screening masivo").
- **5 `RacialAbility`**: estructuralmente no medibles con ningún harness de portador (no son una elección
  de slot) — necesitarían, si se quisieran medir, un harness de "toda la plantilla de una raza" distinto,
  no contemplado en este protocolo.
- **4 `MissingPrimaryMetric`** y **1 `MultiEffectAttribution`**: sin cambios respecto a §16 — siguen
  siendo huecos de tooling nombrados, no tocados en esta fase.

---

## 18. Primer screening real del catálogo (19 sep 2026)

Encargo explícito: ejecutar únicamente `READY_FOR_SCREENING → SCREENING → clasificación automática`
sobre los 24 perks reales, con muestreo adaptativo (§5.1/§5.4/§5.5), sin ningún tuning de parámetros
salvo registrar el baseline cuando el screening pida `NEEDS_TUNING`. Restricciones explícitas respetadas:
sin tocar `/data`, sin cerrar Cazagoles/Ancla, sin tocar `MinPassChainRatio`, sin recalibrar ningún
umbral/banda durante la ejecución, sin IA dentro del bucle de medición.

### 18.1 Piezas nuevas

- **`Sim.Tests/Balance/ReadyContract.cs`** (nuevo, extraído de `ReadyContractTests.cs`): el contrato de
  §16.5 (los diez componentes), reutilizable desde el propio screening para re-verificarlo antes de gastar
  ninguna simulación — "no dupliques lógica" aplicado al protocolo mismo.
- **`Sim/Analysis/PrimaryMetricPerMatch.cs`** (nuevo): traduce una métrica de `MatchMetrics` a su valor
  para UN partido (no el agregado del lote) — necesario para calcular varianza por partido y aplicar
  `BalancePowerCheck`. `BallThirdMaxShare`/`ScorelineShare` se calculan con la MISMA fórmula del agregado,
  aplicada a un solo partido; no son métricas nuevas.
- **`Sim/Analysis/BatchEscalation.cs`** (nuevo): el circuito de seguridad de lote de §9.1 punto 3 ("1 de
  cada 5 escala"), con el mínimo de perks procesados antes de comprobar (5) documentado explícitamente
  como interpretación, no como recalibración del umbral 1/5 (que sí es literal del documento).
- **`Sim/Analysis/ScreeningResult.cs`** (nuevo): `ScreeningResult`/`ScreeningCost`/`TuningCandidateInfo`,
  reutilizando `BalanceState` (ningún enum nuevo, instrucción explícita del encargo) — `DisplayState`
  traduce al vocabulario pedido (`SCREENING_PASS`, `SCREENING_NEEDS_TUNING`, `INSUFFICIENT_EXPOSURE`,
  `INSUFFICIENT_EVIDENCE`, `SAFETY_LIMIT`, `DESIGN_ESCALATION`, `BLOCKED_BEFORE_SCREENING`) sin un segundo
  estado paralelo: `NotReady`→`BLOCKED_BEFORE_SCREENING`, `Tuning`→`SCREENING_NEEDS_TUNING`,
  `Validating`→`SCREENING_PASS`, `DesignReview`→`DESIGN_ESCALATION`, y el resto literal.
- **`Sim.Tests/Balance/ScreeningRunner.cs`** (nuevo): el orquestador. `RunPerk` aplica, en orden: (1)
  re-verificación del contrato → `BLOCKED_BEFORE_SCREENING` si falla; (2) exposición discreta (§5.1: 20
  plantillas, remuestreo único a 120 si hace falta) usando `BalanceDecisionRules.EvaluateScreening` como
  única fuente de la decisión "seguir remuestreando vs. terminar" (reutilizado, no reimplementado); (3) si
  hay parámetro numérico, potencia estadística sobre la métrica primaria (`BalancePowerCheck`, con UNA
  duplicación de muestra si no distingue del ruido, §5.5); (4) seguridad (las 7 métricas obligatorias de
  RT-056 sobre el brazo armado) y señal sistémica (mismas 7 métricas, buscando delta con potencia en las
  que NO son la primaria del perk, sin declarar réplica con un solo seed — §8); (5) decisión final vía
  `BalanceDecisionRules.EvaluateScreening` otra vez, esta vez con los datos reales de seguridad/potencia;
  (6) si el resultado es `Tuning`, comprueba que la dirección observada coincide con el signo del efecto
  declarado en el perk — si no coincide, escala a `DesignReview` en vez de aplicar una regla no existente
  en `BalanceDecisionRules` (gap explícito, documentado, no resuelto con una regla inventada).
- **`Sim.Tests/Balance/PairedBalanceHarness.cs`** (modificado): `PairedResult` gana
  `ArmedActivationsPerMatch` (activaciones por partido, no solo el total — necesario para "fracción de
  partidos con ≥1 activación", la métrica de exposición real de §5.4, distinta de "activaciones medias
  por partido") y `TotalSimulatedTicks` (coste real, §18 punto 6).
- **`Sim/Analysis/PerkBalanceClassifier.cs`** (modificado): `GetPrimaryEffect(PerkDefinition)` extraído a
  público — el screening necesita el mismo efecto "objetivo" que ya usa la clasificación, para leer el
  signo de su `Value` sin repetir el criterio de selección.

### 18.2 Diseño explícito: por qué la potencia estadística se comprueba DENTRO de Screening

§9 (la máquina de estados) solo describe la regla de duplicar muestra por potencia dentro de `TUNING`. Se
aplicó aquí, dentro de `SCREENING`, por lo que dice §5.5 en general: *"el harness, antes de aceptar un
resultado de Tuning/Validation como suficiente, debe comprobar una condición de potencia explícita"* — sin
esto, `EvaluateScreening` (que no recibe ningún `primaryEffectMagnitude` en esta ejecución, porque no
existe un suelo de "cero informativo" calibrado para métricas de recuento reales, §5.4) resolvería
CUALQUIER perk con exposición y seguridad suficientes directamente a `Tuning`, sin comprobar si el delta
observado es siquiera distinguible del ruido de muestreo. Es una extensión documentada del protocolo
existente (aplicar una pieza ya construida, `BalancePowerCheck`, en un punto donde el documento no decía
explícitamente "aquí" pero sí decía explícitamente el principio), no una regla inventada nueva.

### 18.3 Ejecución real: la primera prueba se detuvo por diseño

`Sim.Tests/Balance/RealScreeningLot1Tests.cs` ejecutó `ScreeningRunner.RunBatch` sobre los 24 perks reales
(orden alfabético, semilla 1). **El circuito de seguridad de lote (§9.1 punto 3) se disparó tras procesar
solo 5 de los 24**: los cinco primeros perks alfabéticamente terminaron en un estado de escalada
(`INSUFFICIENT_EVIDENCE`/`SAFETY_LIMIT`), 5/5 = 100% frente al umbral de 1/5 = 20% — el lote se detuvo
antes de tocar el sexto perk, exactamente como pide el documento ("el lote se detiene y escala... en vez
de seguir procesando perks mecánicamente").

**Resultado de los cinco perks realmente medidos:**

| perk | estado final | partidos | exposición | delta primario | seguridad | motivo |
|---|---|---|---|---|---|---|
| `back_to_back` | `INSUFFICIENT_EVIDENCE` | 480 | 0,8% | — | OK | exposición muy por debajo del suelo 50% [ASUNCIÓN], incluso tras remuestrear a 120 plantillas (240 partidos totales) |
| `blood_scent` | `SAFETY_LIMIT` | 80 | 100% | — | `injuriesPerMatch`=0,250 (rango 0,30-0,90) | banda RT-056 fuera de rango en el brazo armado, con solo 40 partidos |
| `bloodhound` | `SAFETY_LIMIT` | 80 | 100% | — | `injuriesPerMatch`=0,250 (rango 0,30-0,90) | mismo motivo que `blood_scent` |
| `bulwark_stance` | `INSUFFICIENT_EVIDENCE` | 480 | 5,0% | — | OK | exposición muy por debajo del suelo, tras remuestrear |
| `cannon` | `INSUFFICIENT_EVIDENCE` | 160 | 100% | 0,0000 | OK | delta exactamente cero en `shotsPerMatch` tras duplicar la muestra por potencia — no se distingue del ruido |

**Confirmado ante el usuario, por decisión explícita (no una decisión automática del sistema)**: se
preguntó si anular el circuito y procesar los 24, si subir el mínimo de perks antes de comprobar el
circuito y reintentar, o detenerse aquí y reportar estos cinco como el resultado completo de este primer
intento. **La decisión fue detenerse aquí** — el hallazgo principal de esta fase no es "24 perks
clasificados", es que **el circuito de seguridad funciona y que la calibración actual (los tres suelos
`[ASUNCIÓN]` de §5.4, más el suelo de potencia de §5.5) no aguanta ni cinco perks reales sin escalar**, que
es exactamente la información que el criterio de salida de esta fase pedía conocer.

**Reconfirmado tras el arreglo de §19.1** (19 sep 2026, mismo día): con el bug de portador corregido, se
volvió a ejecutar `RealScreeningLot1Tests` sobre los 24 desde el principio. El circuito se disparó
**en el mismo punto exacto** (5/24, ahora 4/5 = 80% de escalada en vez de 100% — `bloodhound` pasa a
`SCREENING_PASS`, los otros cuatro sin cambio de estado). Se preguntó de nuevo si anular el circuito,
subir el mínimo de comprobación, o detenerse otra vez; **la decisión fue, de nuevo, detenerse aquí** —
mismo criterio conservador que la primera vez, ahora con el añadido de que las causas de las cuatro
escaladas restantes ya están investigadas (§19) y ninguna es un bug de tooling nuevo por encontrar.

### 18.4 Lo que esto confirma y lo que esto revela

**Confirma que el screening está bien conectado** (no es un fallo de tooling):
- `bulwark_stance` midió **5,0% de exposición**, coincidiendo EXACTAMENTE con el 5% medido en Tanda 0
  para este mismo perk (§5.4) — el cálculo de "fracción de partidos con ≥1 activación" es correcto, no
  una aproximación distinta que por casualidad da un número parecido.
- El circuito de lote, el motor de decisión (`BalanceDecisionRules.EvaluateScreening`, reutilizado sin
  cambios) y el power-check (`BalancePowerCheck`, reutilizado sin cambios) funcionan exactamente como
  están documentados — cero excepciones, cero estado indefinido, en las 5 ejecuciones reales.

**Revela dos hallazgos genuinos, ninguno resuelto aquí (instrucción explícita: no recalibrar durante el
screening)**:
1. **Los suelos de exposición `[ASUNCIÓN]` (50% discreta) son probablemente demasiado altos para el
   catálogo real a la escala inicial de Screening** (20-120 plantillas): dos de cinco perks reales
   (`back_to_back` 0,8%, `bulwark_stance` 5,0%) ni se acercan, incluso tras el remuestreo ×6 a 120
   plantillas. Esto no dice que esos perks estén mal — dice que el suelo de calibración de §5.4 necesita
   la segunda/tercera medición real que el propio documento ya pedía ("hacen falta 1-2 perks reales más
   con activación intermedia") antes de que Screening pueda producir algo distinto de
   `INSUFFICIENT_EVIDENCE` para perks de disparo poco frecuente.
2. **Las bandas RT-056 son absolutas (calibradas sobre poblaciones grandes), no comparativas, y aplicarlas
   sobre 40 partidos armados (§5.1, la comprobación barata) tiene un riesgo real de falso positivo por
   varianza de muestra pequeña** — `blood_scent`/`bloodhound` cayeron 0,05 por debajo del suelo de
   `injuriesPerMatch` con un tamaño de muestra que probablemente no tiene potencia para distinguir esa
   diferencia de ruido. Documentado en cada resultado (`ScreeningResult.Notes`), NO resuelto (no se ha
   subido el umbral, no se ha exigido una N mínima distinta para el chequeo de seguridad — eso sería
   recalibrar durante el screening, prohibido explícitamente).
3. **`cannon` (delta exactamente 0,0000 en `shotsPerMatch` sobre 160 partidos)**: posible indicio de que
   `shootRangeBonusCells` no mueve el conteo de tiros (podría mover la distancia/calidad del tiro en su
   lugar) — la métrica primaria elegida por el clasificador podría no ser la correcta para este efecto.
   Anotado como hallazgo a investigar (fuera de alcance de esta fase: no se toca gameplay ni clasificación
   sin evidencia adicional).

### 18.5 Coste real medido (los cinco perks procesados)

```
total wall time (suma por perk):  10 414 ms
total partidos simulados:          1 280
total ticks simulados:         1 725 426
partidos/perk media:                 256,0
partidos/perk mediana:               160,0
partidos/perk p90:                   480,0
early-stop (cualquier motivo):        5/5 (100%)
```

Ningún perk se acercó al presupuesto de 10 minutos/perk (§9) — el más caro (`back_to_back`,
`bulwark_stance`, 480 partidos con remuestreo ×6) tardó una fracción de segundo. **El coste del motor de
balanceo en sí, cuando corre, es barato**; lo caro (si acaso) sería la calibración pendiente de §5.4/§5.5,
no la ejecución.

### 18.6 Qué falta, explícitamente

- Los 19 perks restantes de los 24 no se han medido — el lote se detuvo antes, por decisión humana
  explícita, no por límite de tiempo ni de partidos.
- Ningún perk llegó a `NEEDS_TUNING` en esta ejecución — no hay baseline que registrar todavía.
- La calibración de los suelos de exposición (§5.4) y del multiplicador de potencia (§5.5) sigue
  exactamente donde estaba: sin tocar, con dos hallazgos reales más (`back_to_back`/`bulwark_stance` para
  exposición discreta baja, `blood_scent`/`bloodhound` para el riesgo de falso positivo de seguridad a N
  pequeña) que un futuro trabajo de calibración debería usar como puntos de referencia adicionales.

---

## 19. Forense de los cinco casos escalados (19 sep 2026)

Encargo explícito: determinar la CAUSA de cada una de las cinco escaladas de §18.3 sin recalibrar nada
(ni el suelo de exposición 50%, ni el 20% del circuito de lote, ni las bandas de RT-056, ni el motor de
decisión) y sin continuar automáticamente con los 19 restantes. Lectura de código + cálculo sobre
resultados ya obtenidos; solo se simuló donde hacía falta demostrar una hipótesis concreta.

### 19.1 Hallazgo transversal: los cinco cayeron en el mismo portador — el portero

`Sim.Tests/Balance/ScreeningForensicsTests.CarrierPositionForEachEscalatedPerk` demostró que, ANTES de
tocar nada, **los cinco perks escalados usaban el mismo portador de prueba: el portero** (`Position.
Goalkeeper`, slot 0). Causa, con código exacto:

- Ninguno de los cinco (`back_to_back`, `bulwark_stance`, `blood_scent`, `bloodhound`, `cannon`) declara
  `positionOnly` ni `tagsRequired` en su JSON.
- `PairedBalanceHarness.FindEligible` (usado por `RunWithEligibleCarrier`, que usa `ScreeningRunner`)
  recorría los titulares en orden fijo `i = 0..6` y devolvía el PRIMERO elegible.
- `TeamGenerator.StarterPositions[0]` es **siempre** `Position.Goalkeeper`
  (`Sim/Generation/TeamGenerator.cs`).
- Resultado: cualquier perk sin restricción de posición/etiqueta se medía **siempre** sobre el portero,
  la posición menos representativa posible para mecánicas de entrada (`back_to_back`) o de tiro
  (`cannon`) — nunca sobre un jugador de campo, con o sin buena o mala suerte de muestreo.

**Esto es un bug de tooling confirmado, no un hallazgo sobre los perks ni sobre el catálogo.** Reproducido
con test (`CarrierPositionForEachEscalatedPerk`, capturado en rojo antes del arreglo), corregido en
`PairedBalanceHarness.FindEligible` (prefiere un titular de campo, slots 1-6, y cae al portero solo si
ningún jugador de campo es elegible — no cambia `PerkAssignment.Eligible`, que sigue reflejando la
elegibilidad REAL del juego; solo cambia el desempate arbitrario de qué portador concreto mide el
harness), con regresión (`Assert.NotEqual(Position.Goalkeeper, position)`), verificado sin romper nada
(las 25 pruebas de `ReadyContractTests`, que dependen de la misma función, siguen en verde: 24/24
`ReadyForScreening` reales siguen pasando el contrato con el nuevo criterio de portador).

### 19.2 Impacto real del arreglo — se re-ejecutaron los MISMOS cinco perks, no los 19 restantes

| perk | estado ANTES | estado DESPUÉS | qué cambió |
|---|---|---|---|
| `back_to_back` | `INSUFFICIENT_EVIDENCE` (exposición 0,8%) | `INSUFFICIENT_EVIDENCE` (exposición 9,6%) | exposición sube ×12, sigue muy por debajo del suelo 50% |
| `bulwark_stance` | `INSUFFICIENT_EVIDENCE` (exposición 5,0%) | `INSUFFICIENT_EVIDENCE` (exposición 7,5%) | sin cambio real (ruido de muestreo) — causa raíz no es el portador |
| `blood_scent` | `SAFETY_LIMIT` | `SAFETY_LIMIT` | sin cambio — causa raíz no es el portador |
| `bloodhound` | `SAFETY_LIMIT` | **`SCREENING_PASS`** | el arreglo cambia el estado final: el falso positivo de seguridad desaparece |
| `cannon` | `INSUFFICIENT_EVIDENCE` (delta=0,0000) | `INSUFFICIENT_EVIDENCE` (delta=0,0000) | sin cambio — el defensa tampoco es buena posición para un mecanismo de tiro |

El circuito de lote (§9.1 punto 3) **se seguiría disparando igual** tras el arreglo: 4 de 5 = 80%, todavía
muy por encima del 20% — un perk menos en estado de escalada no cambia la conclusión operativa de §18.3
(la decisión de detenerse ahí sigue siendo válida), pero SÍ demuestra que el arreglo tiene efecto real y
medible, no cosmético.

### 19.3 Caso a caso — los seis criterios pedidos

**`back_to_back`** (trigger=TACKLE, condición dinámica `nearAlly(actor,'Bulwark',2)`):
- Camino de código: `MatchEngine` dispara `EventType.Tackle` en cada intento de entrada del portador →
  `CompiledCondition` evalúa `nearAlly` contra la posición en vivo de los aliados → si hay uno con la
  etiqueta `Bulwark` a ≤2 casillas, se aplica `modifyProbability(tackle, +100%)`.
- Oportunidades reales (con el defensa, tras el arreglo): 41 intentos de entrada en 40 partidos (19/40
  partidos con al menos un intento). De esos 41, **7 activaron** (17,07% de conversión) — la condición SÍ
  se cumple quimestre en la vida real, no es una condición imposible.
- Exposición reportada por `ScreeningRunner` (fracción de PARTIDOS con ≥1 activación): 9,6% — muy por
  debajo del suelo, PERO el mecanismo demostrablemente funciona cuando se dan las condiciones.
- Clasificación: **la baja exposición es dependiente del roster Y de una condición dinámica rara** (family
  "wall": perk de sinergia por diseño, pensado para construirse alrededor de varios `Bulwark` juntos, no
  para un roster neutro generado al azar). No es accidental en el sentido de "debería activarse más y no
  lo hace" — es una perk de nicho que el harness actual mide contra la población equivocada (un roster
  neutro, no uno construido para la sinergia).

**`bulwark_stance`** (trigger=MATCH_START, condición estática `hasTag(owner,'Bulwark')`):
- Camino de código: la condición se evalúa UNA VEZ al empezar el partido, contra las etiquetas del propio
  portador (no dinámica, no depende de nada del partido).
- Confirmado con test (`BulwarkStanceExposureCeilingMatchesTheRaceStyleTagWeightNotTheEligibilityFilter`):
  las activaciones coinciden EXACTAMENTE con los partidos donde el portador ya tenía la etiqueta `Bulwark`
  al generarse (2/40 = 5,0% en ambos casos) — cero margen, la condición decide todo.
- `data/races/human.json`: `styleTagWeights.Bulwark = 6` (de 100). El techo de exposición de este perk,
  con CUALQUIER tamaño de muestra, está en ~6% — no es una cuestión de sample size.
- `perk.tagsRequired = []` (verificado en el JSON), pero `perk.condition` exige la etiqueta `Bulwark` —
  **inconsistencia entre el filtro estructural de elegibilidad y la condición semántica del propio perk**.
  `PerkAssignment.Eligible` (la función REAL del juego, no solo del harness) ofrece este perk a CUALQUIER
  jugador, aunque solo sea útil si ya tiene la etiqueta.
- Clasificación: **dependiente del roster** (ceiling fijado por la probabilidad de estilo de la raza, no
  por el harness) + **posible inconsistencia de datos** (¿debería `tagsRequired` incluir `"Bulwark"`? Es
  una pregunta de diseño de contenido — `perk-authoring`/`game-design-review`, no una decisión de tooling
  y no se decide aquí).

**`blood_scent` + `bloodhound`** (`SAFETY_LIMIT` en `injuriesPerMatch`):
- Estadística exacta (`SafetyLimitStatisticsAreConsistentWithSamplingNoiseAtThisSampleSize`, con el
  portero, antes del arreglo): n=40 armado, media=0,25, varianza=0,1923, error estándar=0,0693; delta
  armado-control=0,0000 (potencia insuficiente, confirmado); el suelo de la banda (0,30) está a **0,72
  errores estándar** de la media armada — muy por debajo del umbral habitual de ~2 para considerar algo
  distinguible del ruido.
- **Hallazgo más fuerte** (`ArmedAndControlInjurySequencesForTargetSelectionPerks`): con el portero, las
  secuencias de lesiones armado y control eran **IDÉNTICAS partido a partido** para ambos perks — no es
  que el efecto sea pequeño, es que el brazo armado y el de control produjeron el MISMO partido. El 0,25
  no es "el efecto del perk", es el nivel base de esa semilla a n=40, indistinguible por definición de la
  ausencia total de efecto.
- Procedencia de la banda 0,30-0,90: **ADR 0082**, calibrada sobre "tres semillas de 500 partidos" (1.500
  partidos de referencia, población de auto-juego neutro). El chequeo de `SAFETY_LIMIT` de Screening la
  aplica aquí sobre 40 partidos armados — una diferencia de escala de casi dos órdenes de magnitud entre
  la población que fijó la banda y la muestra que la evalúa.
- Con el arreglo del portador: `blood_scent` sigue idéntico armado/control (confirmando que su causa NO
  es el portador — un perk de `ModifyTackleBias` no tiene vía causal declarada hacia el total de lesiones
  del partido, cambia A QUIÉN se entra, no CUÁNTAS entradas hay). `bloodhound` deja de ser idéntico
  (0,30 vs 0,25, delta=0,05, TODAVÍA sin potencia suficiente) y su media pasa a caer EXACTAMENTE en el
  borde de la banda (0,30, "IN" por definición de `>=`) — el estado final cambia a `SCREENING_PASS`.
- **Los dos comparten la misma causa mecánica** (confirmado: los dos son `TargetSelection`, `ModifyTackleBias`/
  `ModifyMarkBias`, sin vía causal declarada hacia `injuriesPerMatch`), pero **NO comparten el mismo
  desenlace tras el arreglo** — `blood_scent` sigue en `SAFETY_LIMIT` (con el mismo problema de fondo:
  banda absoluta sobre N pequeña), `bloodhound` pasa a `SCREENING_PASS` porque su ruido de muestreo cayó
  del otro lado de la banda esta vez.
- Clasificación: **safety gate mal calibrado para este tamaño de muestra** — no es que la banda esté mal
  (viene de una ADR con metodología real), es que aplicarla como corte absoluto sobre 40 partidos armados,
  SIN comparar contra el propio control emparejado que el harness ya tiene disponible, puede convertir el
  ruido de la semilla en un `SAFETY_LIMIT` o en un `SCREENING_PASS` según a qué lado caiga por azar.

**`cannon`** (`modifyTraitScalar(shootRangeBonusCells, +3)`, `shotsPerMatch` primaria):
- Auditoría causal REAL (no del `_doc`, del motor): `Sim/Engine/Utility.cs::EvaluateShoot` (ADR 0030 §1)
  usa `ShootRangeBonusCells` para desplazar dónde EMPIEZA la rampa de penalización por distancia — "nadie
  tiene prohibido tirar de lejos, simplemente casi nadie debería querer" (comentario del propio motor). No
  hay corte binario: es una rampa de utilidad, no una puerta. **`shotsPerMatch` SÍ es la métrica
  causalmente correcta** — si el efecto existe, se manifiesta como "Shoot gana la comparación de utilidad
  frente a otras acciones", y eso es justo lo que cuenta `shotsPerMatch`. **No es un bug de selección de
  métrica** (corrección de una hipótesis inicial de este mismo forense, revisada tras leer el motor).
- Ventana de efecto real, con los pesos reales (`data/ai/weights.json`): `shootBaseRangeCells=8`,
  `shootBeyondRangePenaltyPerCell=300`. El bonus de `cannon` (+3) solo cambia algo para un jugador a
  distancia (8, 11] casillas del área — fuera de esa banda estrecha, el resultado es idéntico con o sin
  el perk. En el borde de la banda evita hasta 900 puntos de penalización (frente a un `shootInRangeBonus`
  base de 388) — el efecto, cuando se dispara, es grande; el problema es la probabilidad de disparo.
- Con el defensa (tras el arreglo): la secuencia de tiros SIGUE siendo IDÉNTICA partido a partido, byte a
  byte, en las 40 muestras — el arreglo del portero NO resolvió este caso. Un defensa, por su rol
  (`CoverSpace`/`MarkOpponent`/`Retreat` predominan sobre avanzar a rango de tiro), tampoco pasa
  suficiente tiempo en la banda de distancia 8-11 con el balón en juego como para que la rampa evitada
  llegue a decidir nada.
- Clasificación: **potencia estadística insuficiente para un efecto de ventana estrecha, agravado por una
  posición de portador poco representativa del mecanismo** (un Delantero pasaría mucho más tiempo en esa
  banda) — el harness no controla NI reporta la posición/distancia típica del portador elegible, que es
  el hueco de auditoría real detrás de este caso. No es un fallo del clasificador ni de la métrica.

### 19.4 Auditoría transversal

| perk | fallo | causa raíz | clase de fallo | ¿métrica válida? | ¿exposición válida? | ¿safety válida? | ¿potencia válida? | ¿bug de tooling? | ¿hueco de protocolo? |
|---|---|---|---|---|---|---|---|---|---|
| `back_to_back` | `INSUFFICIENT_EVIDENCE` | condición dinámica rara (sinergia de equipo) sobre roster neutro | `PROTOCOL_GAP` | sí | sí (cálculo correcto), suelo inalcanzable para esta familia | n/a | n/a | sí (portero, corregido; el resto persiste) | sí — sin población de roster "afín" para perks de sinergia |
| `bulwark_stance` | `INSUFFICIENT_EVIDENCE` | ceiling fijado por probabilidad de estilo de raza (6%), no por muestreo | `PROTOCOL_GAP` (+ posible inconsistencia de datos) | sí | sí (cálculo correcto), suelo inalcanzable | n/a | n/a | no | sí — suelo único 50% no sirve para perks tag-gated de baja incidencia |
| `blood_scent` | `SAFETY_LIMIT` | banda absoluta de RT-056 sobre N=40 sin comparar con el control emparejado | `INVALID_SAFETY_DECISION` | sí | sí | **no** (0,72 SE del suelo, ruido) | insuficiente | no | sí — el check de seguridad de Screening no usa el control disponible |
| `bloodhound` | `SAFETY_LIMIT` → `SCREENING_PASS` (tras el arreglo) | mismo mecanismo que `blood_scent`; el portero (bug de tooling) empujó la media por debajo del suelo | `TOOLING_BUG` (confirmado y corregido) | sí | sí | corregida por el arreglo | insuficiente | **sí, corregido** | comparte el mismo hueco de `INVALID_SAFETY_DECISION` que `blood_scent` |
| `cannon` | `INSUFFICIENT_EVIDENCE` | ventana de efecto estrecha (8-11 casillas) + posición de portador poco representativa | `INSUFFICIENT_POWER` | sí (confirmado por auditoría del motor) | sí | n/a | **no** | no (el arreglo del portero no bastó) | sí — el harness no controla/reporta la posición del portador |

Categorías del propio encargo, usadas literalmente donde encajan; ninguna inventada salvo
`INVALID_SAFETY_DECISION` (no existía un nombre en el vocabulario previo para "la regla de seguridad es
correcta en sí misma pero se aplica de una forma que no distingue señal de ruido a esta escala" — se usa
el nombre que el propio encargo propuso, no uno nuevo).

### 19.5 Respuesta a las siete preguntas del encargo

**A. Qué de los 5 es una señal válida**: ninguno de los cinco es, hoy, una señal válida de que el PERK
tenga un problema real. Los cinco reflejan limitaciones del tooling/protocolo, no del contenido.

**B. Qué es insuficiencia de exposición**: `back_to_back` (condición de sinergia rara, población
incorrecta) y `bulwark_stance` (ceiling fijado por probabilidad de raza, no por N). Los dos son
estructuralmente distintos entre sí a pesar de compartir el estado final — uno necesita una población de
roster distinta (sinergia), el otro necesita o bien un suelo distinto para su familia o bien resolver la
inconsistencia `tagsRequired`/`condition`.

**C. Qué es un problema de métrica**: ninguno, tras la auditoría causal. La hipótesis inicial sobre
`cannon` (métrica mal elegida) quedó DESCARTADA con evidencia del propio motor (`EvaluateShoot`) — se
corrige explícitamente en este documento en vez de dejar la hipótesis inicial sin marcar.

**D. Qué es un posible problema de safety/potencia**: `blood_scent`/`bloodhound` (safety gate sin
comparar contra el control disponible, aplicado a N=40 cuando la banda se calibró sobre 1.500 partidos) y
`cannon` (potencia insuficiente para una ventana de efecto estrecha, agravada por la posición del
portador).

**E. Qué cambios de tooling son necesarios**: uno YA hecho (`PairedBalanceHarness.FindEligible`: preferir
jugador de campo sobre portero — confirmado con test, corregido, con regresión, verificado contra las 25
pruebas de `ReadyContractTests` y el resto de la suite). Pendientes, NO implementados aquí (necesitarían
su propio diseño, fuera de alcance de un forense): (1) que el chequeo de `SAFETY_LIMIT` de Screening
compare contra el control emparejado, no solo contra la banda absoluta; (2) que el harness reporte la
posición/rol del portador elegido, para poder distinguir "sin efecto" de "portador poco representativo".

**F. Qué cambios de protocolo serían necesarios, si alguno**: ninguno decidido aquí — se documentan como
huecos con la evidencia que un futuro `game-design-review`/calibración necesitaría: (1) el suelo de
exposición discreta (50%) puede necesitar ser distinto por FAMILIA de perk (sinergia vs. individual), no
un único número para todo el catálogo; (2) el chequeo de seguridad de Screening puede necesitar una N
mínima distinta de la de exposición, o una comparación contra control, antes de producir un
`SAFETY_LIMIT` con la misma confianza que una banda calibrada sobre 1.500 partidos.

**G. Qué evidencia adicional haría falta antes de tocar el circuito**: repetir el lote de 24 CON el
arreglo del portador ya aplicado (no hecho automáticamente aquí, por instrucción explícita) para ver
cuántas de las escaladas restantes eran, también, un artefacto del mismo bug de tooling — sin esa
repetición, no se sabe si el 80% de escalada remanente en estos cinco es representativo de los 19 que
faltan o no.

### 19.6 Significado de `READY_FOR_SCREENING` — revisado con evidencia, no decidido de antemano

Los cinco casos, juntos, muestran que **"existe una métrica" no basta** — pero no todos apuntan al mismo
componente que falta:

- `back_to_back`/`bulwark_stance`: métrica y mecanismo son correctos; lo que falta es que la EXPOSICIÓN
  sea *interpretable* frente a la población que el harness genera (una condición de sinergia de equipo, o
  un ceiling de probabilidad de raza, no es lo mismo que "poca muestra").
- `blood_scent`/`bloodhound`: métrica y mecanismo son correctos; lo que faltaba era que la SEGURIDAD fuera
  *interpretable* al tamaño de muestra usado (con el control disponible, comparar en vez de leer una
  banda absoluta).
- `cannon`: métrica y mecanismo son correctos; lo que faltaba era que la POTENCIA fuera *interpretable*
  frente al portador elegido (una ventana de efecto estrecha necesita saber si el portador siquiera pasa
  por esa ventana).

**Conclusión, con la evidencia de estos cinco casos** (no una decisión tomada antes de mirarlos): el
contrato de `READY_FOR_SCREENING` (§16.5, los diez componentes) verifica que el MECANISMO de medición
existe y se puede ejecutar — pero no verifica que la POBLACIÓN de prueba (el portador, la sinergia de
roster) sea representativa del perk, ni que la SEGURIDAD/POTENCIA sean interpretables al tamaño de
muestra real que Screening va a usar. Ampliar el contrato con estos tres componentes (población
representativa, seguridad comparativa, potencia frente a la ventana real de efecto) es una extensión
razonable sugerida por la evidencia — **no implementada aquí**, por instrucción explícita de no tocar el
clasificador durante este forense.

---

## 20. `SAFETY_LIMIT` comparativo (19 sep 2026): el primero de los dos huecos de §19.5.E, cerrado

Encargo explícito: implementar el chequeo de seguridad comparativo antes de decidir nada sobre el
circuito de lote. RT-056, la banda de `injuriesPerMatch` (0,30-0,90, ADR 0082) y cualquier otro umbral
normativo quedan intactos; `/data` no se toca; el criterio de exposición no cambia. Lo único que cambia
es CÓMO se interpreta un "OUT" del brazo armado.

### 20.1 Diseño

`Sim/Analysis/ComparativeSafetyCheck.cs` (nuevo, puro, sin E/S): dado el valor medio armado, la banda
(sin tocar) y las series por partido de armado/control, devuelve uno de tres veredictos:

- **`InBand`**: el armado no rompe la banda — nada que atribuir.
- **`NotAttributable`**: el armado rompe la banda, pero el CONTROL EMPAREJADO (mismo tamaño de muestra,
  sin el perk) también la rompe —la banda no distingue "con perk" de "sin perk" a esta escala—, o la
  diferencia armado/control no se distingue del ruido de muestreo (reutiliza
  `BalancePowerCheck.HasSufficientPower` tal cual, sin duplicar su lógica).
- **`AttributableViolation`**: el armado rompe la banda, el control está dentro, y la diferencia armado/
  control SÍ se distingue del ruido — hay evidencia real de que el perk empuja la métrica fuera de rango.

`Sim.Tests/Balance/ScreeningRunner.cs` usa este veredicto en vez de la banda absoluta: solo
`AttributableViolation` cuenta como `anyMandatoryMetricOut` para `BalanceDecisionRules.EvaluateScreening`
(sin cambios en esa función). Un `NotAttributable` se registra como nota explicativa, no bloquea nada.

### 20.2 Tests focalizados (ejecutados antes de tocar el harness real, por instrucción)

`Sim.Tests/Analysis/ComparativeSafetyCheckTests.cs` (6 tests, sintéticos, sin simular ningún partido):
mismo nivel base en ambos brazos (incluso si los dos rompen la banda) → `NotAttributable`; ruido de
muestra pequeña sin potencia → `NotAttributable`; control ya fuera de banda → `NotAttributable`; muestra
insuficiente para comparar → `NotAttributable`; separación real y limpia con control sano →
`AttributableViolation`; armado ya en banda → `InBand`. Los seis pasaron a la primera.

`Sim.Tests/Balance/ComparativeSafetyScreeningRegressionTests.cs` (2 tests, con partidos reales):
`blood_scent`/`bloodhound` —el caso que motivó el cambio— ya no producen `SAFETY_LIMIT` a través de
`ScreeningRunner.RunPerk` real, con una nota explícita del motivo.

Verificado en orden, como se pidió: tests focalizados (8/8) → suite completa no-gate (895/895, sin
regresiones) → re-ejecución del lote de 24 desde cero.

### 20.3 Impacto real — tercera ejecución de los mismos cinco perks

| perk | 1ª ejecución (con el bug del portero) | 2ª ejecución (portero corregido) | 3ª ejecución (+ safety comparativo) |
|---|---|---|---|
| `back_to_back` | `INSUFFICIENT_EVIDENCE` (0,8%) | `INSUFFICIENT_EVIDENCE` (9,6%) | `INSUFFICIENT_EVIDENCE` (9,6%, sin cambio) |
| `blood_scent` | `SAFETY_LIMIT` | `SAFETY_LIMIT` | **`SCREENING_PASS`** |
| `bloodhound` | `SAFETY_LIMIT` | `SCREENING_PASS` | `SCREENING_PASS` (sin cambio respecto a la 2ª) |
| `bulwark_stance` | `INSUFFICIENT_EVIDENCE` (5,0%) | `INSUFFICIENT_EVIDENCE` (7,5%) | `INSUFFICIENT_EVIDENCE` (7,5%, sin cambio) |
| `cannon` | `INSUFFICIENT_EVIDENCE` (delta=0) | `INSUFFICIENT_EVIDENCE` (delta=0) | `INSUFFICIENT_EVIDENCE` (delta=0, sin cambio) |

Distribución de estados: `INSUFFICIENT_EVIDENCE`=3, `SCREENING_PASS`=2 (antes 1), `SAFETY_LIMIT`=0 (antes
1). **Tasa de escalada del circuito de lote: 60% (3/5)** — baja desde el 80% de la ejecución anterior,
pero sigue muy por encima del 20% del umbral de §9.1 punto 3. **El circuito se disparó una tercera vez, en
el mismo punto exacto (5/24)** — el chequeo comparativo resolvió el único caso de `SAFETY_LIMIT` que
había, pero no toca los tres `INSUFFICIENT_EVIDENCE` restantes, que son huecos de exposición/potencia
distintos (§19.3), no de seguridad.

### 20.4 Qué queda para decidir (no decidido aquí, por instrucción explícita)

Con el bug de tooling corregido (§19.1) y el primero de los dos huecos de protocolo de §19.5.E cerrado
(seguridad comparativa), lo que queda de los cinco casos ya no es tooling: es exposición estructuralmente
baja para perks de sinergia/tag-gated (`back_to_back`, `bulwark_stance`) y potencia insuficiente para un
efecto de ventana estrecha (`cannon`) — exactamente los huecos de CALIBRACIÓN que §5.4/§5.5 ya señalaban
como pendientes antes de este lote. El umbral del 20% del circuito de lote sigue sin tocarse; la decisión
de anularlo, subir su mínimo de comprobación, o dejar que siga deteniendo el lote en 5/24 sigue siendo del
usuario, no del sistema.

### 20.5 Decisión final de esta fase (19 sep 2026)

**Decisión explícita del usuario, tras ver el 60% de escalada de §20.3**: mantener el circuito del 20%
intacto. No anularlo, no subir su mínimo de comprobación, no procesar los 19 perks restantes todavía.

**Estado resultante**: `Lote detenido en 5/24. Circuito del 20% conservado. No procesados los 19
restantes.`

**Razón registrada** (para que quien retome esto no repita el razonamiento): con los dos problemas de
tooling reales ya corregidos (§19.1 portador, §20.1 seguridad comparativa) y la suite en verde (895/895),
el 60% de escalada que queda ya no es ruido de instrumentación — es el circuito detectando correctamente
que la CALIBRACIÓN del protocolo (suelo de exposición único para toda familia de perk, ausencia de un
suelo de potencia por ventana de efecto) no está lista para el catálogo real, exactamente la función para
la que existe (§9.1 punto 3: "señal de que algo del propio sistema está fallando de forma sistemática, no
de que el catálogo tenga mala suerte"). Cambiar el criterio de parada ahora, solo porque el resultado es
incómodo, sería exactamente lo que §18/§19/§20 llevan evitando desde el principio.

**Siguiente fase, explícitamente NO iniciada aquí**: tratar formalmente los tres huecos de calibración
identificados —exposición de sinergia de equipo (`back_to_back`), exposición limitada por
etiqueta/estilo de raza (`bulwark_stance`), y potencia insuficiente para una ventana de efecto estrecha
(`cannon`)— como una revisión de protocolo propia, sin tocar ningún umbral durante la propia evaluación.
Necesitaría su propio alcance (qué perks/datos de referencia adicionales calibrarían cada suelo, si el
suelo de exposición debe depender de la categoría del perk en vez de ser un único número, si existe ya
suficiente evidencia o hacen falta más perks reales de cada familia) antes de empezar.

---

## 21. Revisión de calibración — estrictamente diagnóstica (19 sep 2026)

Encargo explícito: responder "¿tenemos evidencia suficiente para justificar cambiar algo del protocolo, y
qué experimento mínimo la produciría?" para los tres huecos de §19/§20.4, sin tocar código de producción,
umbrales ni el circuito de lote. Todo lo de esta sección es lectura de código + medición con partidos
reales (`Sim.Tests/Balance/CalibrationDiagnosticsTests.cs`, nuevo, diagnóstico puro — no modifica
`ScreeningRunner`/`ComparativeSafetyCheck`/`PairedBalanceHarness`/ningún umbral).

### 21.1 `back_to_back`/`bulwark_stance` — la población de prueba, no el suelo, es la causa

**Evidencia nueva, medida esta sesión:**

| perk | raza de prueba | peso de estilo de esa raza | exposición medida (40 partidos) |
|---|---|---|---|
| `bulwark_stance` | Human (la usada hoy) | `Bulwark`=6/100 | 5,0% |
| `bulwark_stance` | Dwarf | `Bulwark`=75/100 | **80,0%** |
| `back_to_back` | Human | `Bulwark`=6/100 | 10,0% (7 activaciones) |
| `back_to_back` | Dwarf | `Bulwark`=75/100 | **90,0%** (101 activaciones) |
| `shadow_marker` (comparador interno, mismo patrón: `nearAlly(actor,'Brute',2)`) | Human | `Brute`=10/100 | 22,5% |
| `shadow_marker` | Orc | `Brute`=75/100 | **87,5%** |
| `safety_net` (comparador de forma: `nearAlly(actor,'Defender',3)`, etiqueta de POSICIÓN, siempre presente) | Human | n/a (posición, no estilo) | **72,5%** |

**Lectura**: los tres perks de "sinergia de estilo" pasan de muy por debajo del suelo (5-22,5%) a muy por
encima (80-90%) con el ÚNICO cambio de generar el roster con la raza afín a su estilo. `safety_net` (misma
forma de condición, `nearAlly` en 2-3 casillas, pero con una etiqueta de POSICIÓN que existe siempre)
alcanza 72,5% incluso en un roster Human neutro — **la mecánica de proximidad dinámica en sí no es rara;
lo que es rara es la etiqueta de ESTILO cuando se mide contra la raza equivocada**.

**Instrumentación**: el harness YA puede medir esto correctamente (`PairedBalanceHarness`/
`ScreeningRunner` no necesitan ningún cambio para calcular la exposición sobre un roster de raza
distinta — el parámetro ya existe, `RunWithEligibleCarrier` simplemente usa `perk.Race ?? NeutralRace`, y
`perk.Race` es `null` para toda esta familia). **Lo que falta es una función que, dada la condición NCalc
de un perk, sugiera qué raza (o población compuesta) es la afín** — hoy no existe ese mapeo; es
instrumentación nueva (leer qué etiqueta de estilo aparece en `condition` y consultar
`race.StyleTagWeights`), no un cambio de balance ni de umbral.

**Hallazgo adicional, catalogado, no resuelto**: `Sim.Tests/Balance/CalibrationDiagnosticsTests.
CatalogWideStaticStyleTagConditionPerksAllShareTheSameTagsRequiredGap` confirma que los CINCO perks reales
con condición estática `hasTag(owner/actor, ESTILO)` (`bruised_knuckles`, `brute_boots`, `bulwark_stance`,
`cold_focus`, `fine_touch`) dejan `tagsRequired: []` — sistemático, no un error aislado de
`bulwark_stance`. Lectura más probable: es un patrón de diseño deliberado (RF-068, "el perk consulta
etiquetas"; se deja al jugador la decisión de en quién rinde), no una inconsistencia de datos — pero no se
decide aquí, se deja documentado para `game-design-review` si alguna vez hace falta confirmarlo.

### 21.2 `bulwark_stance` — ¿el 50% puede ser universal?

**Respuesta con la evidencia de arriba: NO como suelo único sobre la población "neutral" por defecto,
SÍ como suelo sobre la población correcta.** El 50% no está mal calibrado en sí — lo que falta es elegir
QUÉ roster mide cada familia de perk antes de aplicar ese suelo. Un perk de sinergia de estilo medido
contra su raza afín supera el 50% con margen (80-90%); medido contra Human (la población "neutral" que
usa el harness hoy para TODO perk sin raza explícita) nunca podría acercarse, sin importar cuánta muestra
se añada — coincide exactamente con la distinción que pedía el encargo: "separar el problema de rareza de
la etiqueta del problema de tamaño de muestra". Es lo segundo lo que NO aplica aquí: más partidos con
Human no acerca nunca `bulwark_stance` al 50%, porque el 6% es un techo de probabilidad, no de muestreo.

### 21.3 `cannon` — la hipótesis de la "ventana rara" queda REFUTADA con datos nuevos

`CannonCarrierTimeInTheEffectiveWindowWithTheBallInPlay` (con traza real, 20 partidos): del tiempo que el
portador tiene el balón en posesión, **48,91% cae exactamente en la ventana de distancia (8,11] casillas**
donde el bono de `cannon` cambiaría algo. Esto CONTRADICE la hipótesis de §19.3 ("el portador pasa poco
tiempo ahí") — el portador SÍ está ahí casi la mitad del tiempo con el balón, y aun así el delta medido
sigue siendo exactamente 0,0000. **Se corrige la hipótesis en vez de dejarla sin marcar**: el problema no
es "nunca llega a la ventana", es algo que ocurre DENTRO de la ventana — posiblemente que la utilidad de
Disparar sigue perdiendo frente a otras acciones incluso con los 900 puntos que evita el bono, o que el
portador (un Defensa, tras el arreglo del §19.1) tiene un perfil de `Technique`/`Strength` que ya penaliza
tanto la utilidad de Disparar que el bono de rango no basta para que compita.

**Estimación analítica de volumen necesario** (misma fórmula de §5.5, ninguna calibrada nueva): con la
varianza real observada de `shotsPerMatch` (6,869 sobre 40 partidos), el número de partidos/brazo
necesario para distinguir un delta candidato del ruido (aproximación de varianzas iguales en los dos
brazos) es:

```
delta candidato   n necesario por brazo (aprox.)
0,10 tiros/partido   ≈ 5.495
0,25 tiros/partido   ≈   879
0,50 tiros/partido   ≈   220
1,00 tiros/partido   ≈    55
```

Los 400 partidos/brazo de Tuning (§5.2) bastarían para un delta real de ~0,4-0,5 tiros/partido, pero NO
para uno de 0,1-0,25 — y el delta observado hoy no es "pequeño", es CERO exacto, lo que apunta más a "el
mecanismo no compite en absoluto en esta configuración" que a "hace falta más muestra".

**Instrumentación disponible pero no usada todavía**: el motor YA tiene un volcado de utilidad por tick
(`MatchReport.UtilityDump`/`UtilityRow`, RT-098, citado en `CLAUDE.md` como instrumento existente del
proyecto) — el experimento mínimo que de verdad respondería "¿por qué nunca gana Disparar dentro de la
ventana?" es volcar esa tabla para los frames donde el portador está en la ventana con el balón, en un
lote pequeño (10-20 partidos), comparando la puntuación de `Shoot` con la de la acción elegida, con y sin
el bono. **No implementado aquí** (RT-098 ya existe; conectarlo a este caso concreto es la extensión de
instrumentación pendiente, no un cambio de balance).

### 21.4 Tabla resumen (el formato pedido)

| Hueco | Evidencia actual | Qué falta medir | Experimento mínimo | Cambio de protocolo justificado |
|---|---|---|---|---|
| `back_to_back`/`bulwark_stance` (exposición de sinergia de estilo) | Exposición sube de 5-22,5% a 72,5-90% con la raza afín o una etiqueta siempre presente — 4 mediciones consistentes (Dwarf/Orc/Human×3 perks) | Un mapeo perk→población afín que hoy no existe; si aplica solo a perks con condición de estilo o también a otras familias | Medir el resto de perks "nearAlly/hasTag(estilo)" del catálogo (7-8 más) contra su raza afín, para confirmar que el patrón se sostiene más allá de 3 casos | **Sí, pero no al suelo del 50%** — el cambio justificado es de INSTRUMENTACIÓN (elegir población de prueba por familia de condición), no de umbral |
| `cannon` (potencia insuficiente, ventana estrecha) | La hipótesis "ventana rara" queda refutada (48,9% del tiempo con balón cae en la ventana); delta sigue siendo exactamente 0 | Qué pasa DENTRO de la ventana — si `Shoot` compite alguna vez en la tabla de utilidad | Volcar `UtilityDump`/RT-098 (ya existente) para los frames en ventana, 10-20 partidos, comparar `Shoot` vs. la acción elegida | **No decidido todavía** — la evidencia actual no distingue "hace falta más muestra" (caro: 220-5.495 partidos/brazo según el delta) de "el mecanismo no compite nunca" (un problema de diseño/motor, no de protocolo) |

### 21.5 Recomendación sobre el PROTOCOLO (no sobre ningún perk individual)

Con la evidencia de esta fase, la recomendación es **doble y asimétrica**:

1. **Para la familia "sinergia de estilo/etiqueta"**: hay evidencia suficiente y consistente (4
   mediciones, 2 razas distintas, 3 perks) para justificar una extensión de INSTRUMENTACIÓN —no de
   umbral— que seleccione la población de prueba por familia de condición del perk en vez de usar siempre
   la raza "neutral". El suelo del 50% no necesita cambiar; necesita aplicarse sobre la población
   correcta. Esto es una propuesta de diseño de tooling, no una decisión tomada aquí.
2. **Para `cannon`/potencia de ventana estrecha**: NO hay evidencia suficiente todavía para proponer un
   cambio de protocolo concreto — falta el experimento de instrumentación de utilidad (§21.3) antes de
   saber si el hueco es de MUESTRA (protocolo) o de MECANISMO (diseño/motor, fuera de este protocolo). Se
   deja explícitamente como pregunta abierta, no como recomendación.

Ningún umbral, banda, o regla de decisión se ha tocado en esta fase. Ningún valor de `/data` se ha
modificado. El circuito del 20% sigue intacto, en el estado de §20.5.

---

## Hermanos

- `docs/analisis/c1-piloto-cazagoles-diseno.md` — la evidencia de calibración completa (§3.1b, §5, §6,
  §7) que sostiene los tamaños de muestra y umbrales `[MEDIDO]`/`[DERIVADO]` de este documento.
- `docs/analisis/tanda-0-histograma-de-accion.md` — el origen de los suelos de activación/exposición.
- `docs/analisis/perks-catalogo-unificado.md` — los 22 perks de C1/C2 que quedan `BLOCKED_INFRA` hasta
  que exista el vocabulario completo.
- `docs/balance.md` — RT-054/055/056, fuente de verdad de bandas y del cálculo compartido
  (`Sim.Analysis.MatchMetrics`) que este protocolo reutiliza sin redefinir.
- `~/ai-orchestrator/AI_WORKFLOW.md` — el sistema multiagente inspeccionado en §1, no adoptado como
  dependencia del bucle de medición.
