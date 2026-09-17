# Protocolo operativo de balanceo automatizado de perks

**Fecha:** 17 sep 2026. **Estado:** diseño, sin código ni `/data` tocados. Responde al encargo de cambiar
el objetivo del piloto C1/Cazagoles: de "cerrar un perk" a "diseñar el sistema que balancea cualquier
perk del catálogo". Los resultados de Cazagoles (17/24/48%, `docs/analisis/c1-piloto-cazagoles-diseno.md`
§4-§7) se usan aquí como **evidencia de calibración** del sistema — tamaños de muestra, umbrales de
exposición, forma de la relación valor→efecto —, no como decisión definitiva de ese perk. Ancla y
Cazagoles siguen sin cerrarse.

## 0. Cómo se lee este documento

Sigue el orden del encargo (seis fases del protocolo, medición rápida, niveles de evidencia,
autogeneración de conclusiones, estrategia de búsqueda, tipos de perk, métricas, criterios de parada,
sobreajuste, local/sistémico, catálogo completo, automatización, multiagente) y cierra con las cuatro
preguntas operacionales (A-D) y la pregunta de velocidad. Cada umbral numérico está marcado
**[MEDIDO]** (viene de un experimento de esta sesión), **[DERIVADO]** (se calcula de una medición ya
hecha) o **[ASUNCIÓN — PENDIENTE DE CALIBRAR]** (no hay evidencia suficiente todavía; se dice qué falta).
Regla F del proyecto: nada se escribe como certeza sin la evidencia que lo sostiene.

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
| `modifyProbability` | 54 | el `ProbabilityKind` que toca (`Foul/Card/Injury/Injure/SevereInjury/Pass/Intercept/Dribble/Tackle/ShotOnTarget/Save/TackleEvasion/InterceptEvasion`) decide la familia de métricas (§4) |
| `addCounter` | 23 | efecto de campaña/run, no de partido suelto — normalmente escala a §8 (sistémico) |
| `modifyLeash`, `modifyZoneShape`, `shiftHome` | 5+1+4 | geometría de zona de acción → `ballThirdMaxShare`, distribución espacial |
| `modifyAttribute` | 5 | atributo base → cualquier resolución que lo use, indirecto |
| `immunity` | 5 | cancela un tipo de suceso → tasa de ese suceso |
| `modifyTraitScalar` | 4 | uno de los 13 escalares (`ShotQualityBonus`, `InjuryChanceBonus`, etc.) → la métrica de ese escalar |
| `cancelEvent` | 4 | `FOUL`/`CARD`/`INJURY` cancelado → tasa de ese evento |
| `modifyMarkBias`, `modifyTackleBias` | 3+2 | lógica de selección de objetivo, no un escalar — behavioral audit, no búsqueda numérica |
| `extraAction` | 3 | repetición de acción → frecuencia de esa acción, ya la cuenta el motor |
| `setState`, `relocate`, `modifyKnockdownTicks`, `modifyExperience`, `injure`, `modifyBias` | 1-2 c/u | casos singulares, se tratan uno a uno |
| `modifyUtility` (C1, nuevo) | 0 (inerte) | acción + zona opcional → histograma de acción restringido a exposición (mismo instrumento de Tanda 0) |

**Categorías de balanceo** (no de tipo de dato, de *qué hay que medir*), derivadas de la tabla:

- **Bonus/malus de utilidad** (`modifyUtility`, y por extensión cualquier `modifyTraitScalar` que alimente
  `Utility.Choose`): categoría de C1/Cazagoles. Métrica primaria = histograma de acción restringido a
  exposición.
- **Probabilidades de resolución** (`modifyProbability`): la mayoría del catálogo hoy (54/119 efectos).
  Métrica primaria = tasa del suceso (`ProbabilityKind`) armado vs control.
- **Escalares de rasgo** (`modifyTraitScalar`): métrica primaria = la métrica que ya existe para ese
  escalar (p. ej. `InjuryChanceBonus` → `injuriesPerMatch`).
- **Geometría/posición** (`modifyZoneShape`, `shiftHome`, `modifyLeash`): métrica primaria =
  `ballThirdMaxShare` + distribución espacial (posición media del portador).
- **Selección de objetivo** (`modifyMarkBias`, `modifyTackleBias`): no numérico — behavioral audit
  (distribución de a quién se marca/entra), no una búsqueda de valor.
- **Sucesos cancelables/contadores** (`cancelEvent`, `addCounter`, `immunity`): métrica primaria = tasa
  del suceso; `addCounter` casi siempre es de **run**, no de partido — normalmente se escala a análisis
  sistémico (§9) en vez de resolverse en este bucle.
- **Casos singulares** (`setState`, `relocate`, `modifyKnockdownTicks`, `modifyExperience`, `injure`,
  `extraAction`, `modifyBias`): sin volumen suficiente para una categoría propia; se tratan con la capa
  común (§4 "universales") y sin estrategia de búsqueda automática salvo que se acumule más de un perk
  del mismo tipo.

No se listan categorías de C2/C9/C16 (situación, posesión) porque **no existen todavía como primitiva
del motor** salvo la única cláusula de C2 que ya usa C1 (`Pitch.ZoneOf`, la zona). Un perk que las
necesite entra en el estado `BLOCKED_INFRA` (§9), no en el bucle.

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
  **[MEDIDO]** — es exactamente la muestra de Tanda 0, suficiente para confirmar `own_third_anchor`
  (activación 40/40, L1=0 informativo) y para detectar el efecto de `sweeper_keeper` (L1=0,0013, activación
  35/40).
- **Traza**: solo ON si la categoría necesita histograma de acción (bonus de utilidad, geometría); OFF
  para el resto (más barato, `Sim.Analysis.MatchMetrics` no necesita traza).
- **Presupuesto de tiempo**: **objetivo ≤30 s** por perk. **[DERIVADO]** de que 40 partidos con traza
  tardaron ~2 s en este piloto (§5 de C1, primer lote) y 400 partidos sin traza tardaron ~6 s (§7 de C1);
  40 partidos sin traza son una fracción de eso.
- **Salidas de Screening** (§9 tiene la lista completa de estados):
  - **`INSUFFICIENT_EXPOSURE`**: exposición por debajo del suelo (ver hueco 5.4) → subir la muestra una
    vez (×3, a 120 plantillas) y repetir Screening; si sigue por debajo, `INSUFFICIENT_EXPOSURE` final
    (no se sigue intentando indefinidamente).
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
  (400 partidos/brazo combinando semillas). **[MEDIDO]**: es exactamente lo que hizo falta en C1 §5 para
  que `passChain`/`shotsPerMatch` dejaran de ser ruido y la monotonía se confirmara en las dos semillas —
  con 40 partidos/brazo la misma medición no era monótona.
- **Presupuesto de tiempo**: **objetivo ≤60 s por candidato**. **[MEDIDO]**: 400 partidos/brazo con traza
  tardaron ~17 s en este piloto (tres candidatos, dos semillas, 2.400 partidos totales en 16,8 s).
- **Presupuesto por perk en esta fase**: máximo **5 candidatos** o **5 minutos** de reloj, lo que llegue
  antes — igual que el resto de límites de este documento, es un límite duro, no una guía (§9).

### 5.3 Validation

**Objetivo**: confirmar el candidato elegido por Tuning, no volver a buscarlo.

- **Muestra**: reutiliza los datos de Tuning del candidato elegido (no se repite la simulación si ya
  existe); añade la batería completa de RT-056 sobre partido completo (`MatchMetrics.Compute`) si Tuning
  no la calculó ya, y una comprobación de determinismo (una sola vez, no por semilla).
- **Presupuesto de tiempo**: **objetivo ≤30 s incrementales**. **[MEDIDO]**: la batería de RT-056 sobre
  800 partidos (2 brazos × 2 semillas × 200) tardó ~6 s en C1 §7.
- **Salida**: `ACCEPT` (con todas las condiciones de §6) o el estado de rechazo/escalada que corresponda.

### 5.4 Huecos identificados en los niveles de evidencia

- **Suelo de exposición discreta (activación por partido).** Tanda 0 dio cuatro puntos: 100%
  (`own_third_anchor`, informativo), 87,5% (`sweeper_keeper`, informativo), 7,5% (`iron_gate`, poca
  confianza) y 5% (`bulwark_stance`, sin información). El verdadero punto de quiebre está entre 7,5% y
  87,5% y no hay más calibración. **Provisional: 30% de partidos con ≥1 activación** como suelo de
  `INSUFFICIENT_EXPOSURE` — marcado `[ASUNCIÓN — PENDIENTE DE CALIBRAR]`. Para cerrarlo: repetir Tanda 0
  con dos o tres perks reales de activación intermedia (20-60%) antes de confiar en el número.
- **Suelo de exposición continua (fracción de ticks).** Un solo punto de calibración: Cazagoles, 35-46%,
  con efecto real y medible. No hay un punto que haya fallado por debajo de eso con este tipo de métrica.
  **Provisional: 20%** como suelo — **[ASUNCIÓN — PENDIENTE DE CALIBRAR]**, con el mismo remedio (medir
  Ancla, que por diseño tendrá una exposición distinta al estar atado al tercio propio en vez del rival).
- **Suelo de "efecto indistinguible de cero".** Se usa aquí `sweeper_keeper` (L1=0,0013) como el efecto
  real más pequeño confirmado y `own_third_anchor` (L1=0,0000 con activación completa) como el cero
  informativo. El hueco entre 0 y 0,0013 no está calibrado con más de un punto — **[ASUNCIÓN]**: se toma
  0,0013 como suelo de "hay efecto" hasta tener una tercera calibración.

---

## 6. Motor de decisión — reglas, no intuición

Formato exigido por el encargo: "si ocurre X, Y y Z, aplica la regla R", con el umbral ya fijado antes de
mirar el resultado del experimento siguiente.

### 6.1 Reglas de Screening

```
SI exposición < suelo_exposición (§5.4)
  Y no se ha repetido ya con muestra ×3
ENTONCES  repetir Screening con ×3 plantillas

SI exposición < suelo_exposición
  Y ya se repitió con muestra ×3
ENTONCES  estado = INSUFFICIENT_EXPOSURE, parar

SI alguna métrica obligatoria de RT-056 ya está OUT a escala de Screening
ENTONCES  estado = candidato a SAFETY_LIMIT, ejecutar Validation reducida para confirmar antes de decidir

SI efecto_directo < suelo_de_cero (§5.4)
  Y exposición >= suelo_exposición (el cero es informativo, no por falta de potencia)
ENTONCES  estado = DESIGN_REVIEW, motivo = "sin efecto de comportamiento detectado"

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

**Hueco explícito**: el "suelo de fidelidad de diseño" de la última regla se definió en C1 de forma
cualitativa ("lejos de cero", "el que menos margen deja") porque solo hay un perk medido. No hay todavía
un número (p. ej. "por debajo de 10% de cuota, se descarta") que se pueda aplicar sin mirar el perk en
cuestión — **[ASUNCIÓN — PENDIENTE DE CALIBRAR]**: hace falta medir 2-3 perks más de esta misma familia
(el resto de "El Remate": Sangre fría; y algún perk de "El Bloque Bajo") para saber si 7% (lo que dio 48%
en Cazagoles) es ya demasiado poco en general o es específico de esa ficha.

### 6.4 Reglas de Validation

```
SI las siete métricas de RT-056 son IN en el armado, en las dos semillas y en el combinado
  Y todo movimiento armado-vs-control tiene vía causal documentada y replica en las dos semillas
  Y la comprobación de determinismo es idéntica byte a byte
  Y ninguna condición de descarte de §6.3 se dispara
ENTONCES  estado = ACCEPT (candidato listo para independent-reviewer antes de escribir /data — Regla E,
          no lo sustituye este sistema)

EN CUALQUIER OTRO CASO
  estado = REJECT si el candidato específico falla,
  o NEEDS_REPLICATION si el resultado es limítrofe y una tercera semilla podría resolver la duda
  (máximo UNA semilla adicional; si sigue limítrofe con tres semillas, DESIGN_REVIEW, no una cuarta)
```

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

```
NOT_READY ──(existe en /data, tiene condición/efecto ya soportado por el motor)──> SCREENING
NOT_READY ──(necesita una primitiva que no existe: C2 completo, C9, C16...)──────> BLOCKED_INFRA

SCREENING ──(exposición insuficiente incluso tras remuestrear)──> INSUFFICIENT_EXPOSURE  [terminal]
SCREENING ──(efecto ya rompe RT-056 a escala pequeña)───────────> SAFETY_LIMIT           [terminal]
SCREENING ──(cero informativo, exposición alta)─────────────────> DESIGN_REVIEW          [terminal*]
SCREENING ──(efecto real, no numérico — selección de objetivo)──> VALIDATING (cualitativo)
SCREENING ──(efecto real, numérico)─────────────────────────────> TUNING

TUNING ──(no monótono tras reescaneo)───────────────────────────> DESIGN_REVIEW          [terminal*]
TUNING ──(monótono pero techo insuficiente)─────────────────────> WEAK_EFFECT_CEILING ──> DESIGN_REVIEW
TUNING ──(presupuesto de candidatos/tiempo agotado sin ganador)──> NEEDS_REPLICATION o DESIGN_REVIEW
TUNING ──(candidato en banda, sin descarte de seguridad)────────> VALIDATING

VALIDATING ──(las siete de RT-056 IN, réplica, determinismo)────> BALANCED
VALIDATING ──(limítrofe)─────────────────────────────────────────> NEEDS_REPLICATION (máx. 1 semilla más)
VALIDATING ──(falla)─────────────────────────────────────────────> REJECT                [terminal]
VALIDATING ──(mueve algo sin vía causal, replica)───────────────> SYSTEMIC_REGRESSION     [terminal*]

BALANCED ──(pendiente Regla E, no lo hace este sistema)─────────> listo para independent-reviewer y
                                                                    escritura en /data (fuera de este
                                                                    documento — decisión humana/de
                                                                    revisor, nunca automática)
```

`[terminal*]` = terminal para este sistema, no para el perk: escala a un humano/`game-design-review`, que
puede reabrir el ciclo con un diseño distinto (no con el mismo número).

**Límites duros, obligatorios** (evita perseguir una métrica indefinidamente, instrucción explícita):
- Iteraciones de Tuning: **5 candidatos máximo**.
- Cambios de valor por perk: **igual al límite de candidatos** (no hay un cambio "gratis" fuera de ese
  conteo).
- Tiempo por perk: **10 minutos de reloj de harness**, sumando Screening+Tuning+Validation.
  **[DERIVADO]** de que un ciclo completo de tres candidatos × dos semillas de C1 (Tuning+Validation
  completo) tardó bajo 30 s reales — 10 minutos deja un margen de ~20× sobre lo medido, para perks más
  caros de medir (los que necesitan traza, o categorías con auditoría de comportamiento adicional).
- Semillas adicionales por `NEEDS_REPLICATION`: **máximo 1** (total 3 semillas); si sigue limítrofe,
  `DESIGN_REVIEW`, nunca una cuarta.

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

**Coste de coordinación vigilado explícitamente**: el único agente que se invoca por perk de forma
rutinaria es `fast-worker` (clasificación, una vez); `deep-reasoner`/`independent-reviewer`/
`game-design-review` solo se invocan en estados de excepción (`NON_MONOTONIC`, `SYSTEMIC_REGRESSION`,
`DESIGN_REVIEW`, `ACCEPT`→escritura en `/data`), que en un catálogo sano deberían ser la minoría. Para
los 94 perks ya vivos, si (optimista) el 70-80% llega a `BALANCED`/`ACCEPT` sin incidencias, la carga de
agentes es ~94 clasificaciones + ~20-30 diagnósticos/revisiones — minutos de latencia de LLM en total,
frente a las decenas de minutos que costaría el propio muestreo del harness sumado en todo el catálogo.

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

Ninguno de los seis se implementa en esta fase, tal como pide el encargo.

---

## 13. Piezas que faltan antes de poder ejecutar "Balancea los perks del catálogo"

1. **Los seis elementos de tooling de §12**, sin excepción — hoy cada pieza se ha escrito a mano, una vez,
   para un solo perk.
2. **Cerrar los tres huecos de calibración de §5.4/§6.3** (suelo de exposición discreta, suelo de
   exposición continua, suelo de fidelidad de diseño) con 2-3 perks reales más, antes de confiar los
   umbrales `INSUFFICIENT_EXPOSURE`/`DESIGN_REVIEW` a un sistema sin supervisión.
3. **El hueco de §6.5** (perks de selección de objetivo sin parámetro numérico que buscar) necesita su
   propio protocolo de behavioral audit, todavía no diseñado en detalle — aquí solo se dice que existen y
   que no entran en la búsqueda de valor.
4. **Formato exacto del registro de §10** — decisión de implementación pendiente.
5. **Decisión explícita de qué agente/skill invocar automáticamente vs. qué requiere que el humano lo
   dispare** — este documento propone `fast-worker` automático y el resto bajo demanda, pero no se ha
   validado con un perk real corriendo de punta a punta.
6. **Ningún perk nuevo de C1/C2 (los 22 de `perks-catalogo-unificado.md` §3.3) puede entrar todavía**:
   siguen bloqueados por infraestructura (`BLOCKED_INFRA`) salvo Cazagoles/Ancla, que ya tienen el C1 +
   la cláusula de zona construidos (aunque no cerrados). El sistema, cuando exista, debería poder marcar
   esto automáticamente comprobando qué `EffectType`/predicado de condición usa el perk.

---

## 14. Las cuatro preguntas operacionales

### A. ¿Qué mide?

Universales siempre (§4.1): exposición, activación, efecto directo, efecto de comportamiento, seguridad
(las siete de RT-056), determinismo (una vez, no por iteración). Específicas según la categoría del perk
(§4.2, derivada del `EffectType`/`ProbabilityKind`/predicado que usa, §3) — nunca todas a la vez.

### B. ¿Cuánto mide?

Screening: 1 semilla, 40 partidos/brazo, ≤30 s. Tuning: 2 semillas, 400 partidos/brazo combinando
semillas, ≤60 s/candidato, máximo 5 candidatos. Validation: reutiliza los datos de Tuning + batería RT-056
completa, ≤30 s incrementales, máximo 1 semilla adicional si el resultado es limítrofe. Presupuesto duro
por perk: **10 minutos de reloj**, agotado el cual el sistema para y escala en vez de seguir midiendo
(§9). Todos los tamaños de muestra son valores **[MEDIDO]** de Tanda 0 y C1, no estimaciones a ojo.

### C. ¿Cómo decide?

Con las reglas deterministas de §6, fijadas antes de ver cada resultado: umbrales de exposición (§5.4,
marcados como provisionales donde falta calibración), detección de monotonicidad y su ruptura (§6.2/§7),
condiciones de descarte de seguridad y de fidelidad de diseño (§6.3), condiciones de aceptación en
Validation (§6.4). La salida siempre es uno de los estados de §9
(`CONTINUE/ACCEPT/REJECT/INSUFFICIENT_EXPOSURE/NEEDS_REPLICATION/SAFETY_LIMIT/SYSTEMIC_REGRESSION/
DESIGN_REVIEW/NOT_READY/BLOCKED_INFRA/WEAK_EFFECT_CEILING`), nunca una conclusión libre.

### D. ¿Cómo itera?

Selecciona el siguiente valor según el tipo de parámetro (§7): tripleta anclada en una medición barata
para bonus/probabilidades, bisección entera para geometría, sin búsqueda numérica para selección de
objetivo. Reduce el paso al acercarse a la banda objetivo (elige el candidato válido de menor magnitud
si varios cumplen). Valida el candidato elegido con la batería completa de §5.3/§6.4. Termina en
`BALANCED` (listo para Regla E y escritura en `/data`, ambas fuera de este sistema), o en cualquiera de
los estados terminales de §9 dentro del presupuesto de 5 candidatos/10 minutos — nunca busca
indefinidamente.

### ¿Cómo balancea un perk en minutos y no en horas?

Por diseño, no por casualidad: (1) reutiliza simulaciones — Validation no repite lo que Tuning ya midió;
(2) control/tratamiento emparejado sobre las mismas plantillas y semillas, que es lo que hizo posible usar
solo 400 partidos/brazo en vez de miles; (3) todas las métricas de una categoría salen de la misma tanda
de partidos, nunca se relanza la simulación por métrica; (4) early stopping en cada nivel (§5, §6);
(5) descarte temprano en Screening antes de gastar la muestra grande de Tuning; (6) paralelización ya
disponible en el patrón de `/Balance` (`Parallel.For` por índice, un `Catalog` por hilo); (7) cero
llamadas a un modelo de lenguaje dentro del bucle de medición (§11) — la latencia de una API de LLM por
iteración sería, ella sola, más lenta que las decenas de miles de partidos que este piloto ya demostró
poder simular en segundos. La medición de C1 (tres candidatos, dos semillas, Tuning + Validation
completos) tardó **bajo 30 segundos de cómputo real** — la cifra que sostiene que "minutos, no horas" no
es una aspiración, es lo que ya se midió esta sesión.

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
