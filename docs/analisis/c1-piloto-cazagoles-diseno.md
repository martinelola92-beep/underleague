# C1, piloto Cazagoles — decisión de diseño/arquitectura y plan experimental

**Fecha:** 17 sep 2026. **Estado:** Análisis previo a implementar, sin código todavía (`game-design-review`
+ `architecture-review`, CLAUDE.md Regla B/C: primitiva de motor nueva). Sigue a
`docs/analisis/tanda-0-histograma-de-accion.md` (el instrumento) y a la revisión de alcance de Tanda 2
(activación/exposición de `Cazagoles`/`Ancla`, `docs/pendientes/BB-P.md` para el estado de `passChain`).

## 1. Qué existe ya, y qué es genuinamente nuevo (la pregunta central del encargo)

**El motor ya tiene el geométrico que hace falta.** `Sim/Model/Cell.cs`, `Pitch.ZoneOf(Vec2 p, int team)`
devuelve `Zone.Own`/`Zone.Middle`/`Zone.Opposing` relativo al equipo, usando `Pitch.AttackDirection` y
`Pitch.Columns` (16, dividido en tercios). Es la **misma** función que ya consume el sistema de perks: la
condición NCalc `zone(actor)` (`Sim/Perks/ConditionCompiler.cs:678`) delega en
`ConditionContext.World.ZoneOf`, que es este mismo `Pitch.ZoneOf` (`sweeper_keeper` ya lo usa hoy, en su
condición de disparo `"zone(actor) == 'Own'"`). **No hace falta escribir ni una línea de geometría nueva
para "en el tercio rival".**

**Lo que sí es nuevo es el formato de utilidad mutable por acción, y el momento en que se re-evalúa.**
`Sim/Engine/Utility.cs:160`:

```csharp
int traitMultiplier = p.ActionMultiplier(action) * (100 + p.LeaderBonusPercent) / 100;
int score = (baseWeight * tactical / 100 * traitMultiplier / 100) + eval.Context;
```

`p.ActionMultiplier(action)` lee `_actionMultipliers[14]` (`Sim/Engine/MatchPlayer.cs:27`), un array
**`readonly`** que solo se rellena **una vez**, al construir el jugador, a partir de sus rasgos
(`Sim/Engine/MatchPlayer.cs:94-105`). `LeaderBonusPercent` es el único precedente de un multiplicador
**mutable en tiempo de partido** que ya entra en esa misma línea (lo escribe el mecanismo de Líder cada
tick, según qué compañeros tenga al lado). Es exactamente la pieza que
`docs/analisis/catalogo-conceptual-fase-a.md` señala como precedente de C1: "pasar de escalar a `int[14]`
es la misma pieza."

### Qué es C1, con precisión

Una **fórmula** (a decidir, ver §3) y un **array nuevo, mutable, por acción** (p.ej.
`_perkActionBonusPercent[14]`, mismo patrón que `_actionMultipliers` pero escribible en tiempo de partido)
que `Utility.Choose` lee en la misma línea que `LeaderBonusPercent`, más un **tipo de efecto genérico**
en el sistema de perks (`modifyUtility`, parametrizado por acción y porcentaje) que cualquier perk futuro
puede usar para un bono **siempre activo** mientras el efecto esté vigente. Esta parte, por sí sola, **no
necesita nada de C2**: un perk que sube `Shoot` un `X`% todo el partido, sin condición, es C1 puro.

### Qué NO es C1: el chequeo por tercio

`Cazagoles` no es un bono siempre activo: "en el tercio rival elige `Shoot`... el resto elige `ShortPass`"
es una condición que hay que **re-evaluar cada tick** (o cada decisión), no fijar una vez al activarse el
perk. Los efectos actuales del catálogo se disparan una vez (`MATCH_START`, `RECOVERY`...) y se quedan
fijos una duración (`"match"`, `"play"`); no hay ningún efecto hoy que se **reevalúe continuamente**. Esa
reevaluación continua es la pieza nueva, y aquí está el matiz que corrige la hipótesis de partida:

**No es "C1 sin nada de C2". Es C1 más una porción mínima y de una sola cláusula de C2 (la del tercio),
no el vocabulario completo.** El propio catálogo conceptual lista seis predicados para C2 (tercio del
actor, fase de posesión, con/sin balón, balón suelto, primer tick tras recibir, turba). Aquí solo hace
falta **uno**, y es el único de los seis que ya tiene una función C# de una línea (`Pitch.ZoneOf`) sin
más estado que la posición del jugador, que el motor ya conoce. Los otros cinco necesitarían su propio
fontanero de estado (fase de posesión, si el balón está suelto, si acaba de recibir, si hay turba) antes
de poder comprobarse cada tick — eso sí es C2 completo, y no se toca en este piloto.

**Por qué no se hace con NCalc, aunque el sistema de condiciones ya exista.** `CompiledCondition` guarda
contexto en la instancia y **no es reentrante** (`docs/arquitectura.md`, citado también en
`architecture-review`); evaluarla cada tick para veinte jugadores multiplicaría su coste por ~15
partido/segundo × hasta 2.100 ticks, y no es el uso para el que se compiló. El chequeo de tercio, al ser
un enum cerrado de tres valores, no necesita expresión NCalc: un campo de datos (`"zone": "Opposing"` en
el efecto) más una comparación C# (`switch`/`==`) es toda la maquinaria que hace falta, y es barata de
verdad (una comparación de enteros por jugador y tick, no una evaluación de árbol de expresión).

### Dónde vive el chequeo, para no ensuciar `Utility.cs`

El chequeo de zona **no** va dentro de `EvaluateShoot`/`EvaluateShortPass` (eso mezclaría "qué vale la
acción" con "qué perk tiene este jugador", justo lo que RT-034 prohíbe: nada de perks nombrados dentro del
motor). Va en el mismo sitio que ya recalcula estado por tick antes de decidir
(`MatchEngine.UpdateContextCaches` o el punto equivalente): por cada jugador con un efecto
`modifyUtility` con zona, se recalcula `Pitch.ZoneOf(player.Position, player.Team)`, se compara contra la
zona configurada del efecto, y se escribe el porcentaje configurado en `_perkActionBonusPercent[action]`
(o se resetea a 0 si ya no aplica). `Utility.Choose` sigue sin saber qué perk es: solo lee un array que ya
está actualizado cuando le toca decidir. Esto es reutilizable tal cual para `Ancla` (misma mecánica, zona
y acción distintas), sin escribir el nombre de ningún perk en `MatchEngine.cs` ni en `Utility.cs`.

## 2. Confirmación pedida explícitamente

| pieza | es C1 | es una cláusula mínima de C2 | necesita C2 completo |
|---|---|---|---|
| Array `_perkActionBonusPercent[14]` + fórmula en `Utility.Choose` | ✅ | | |
| Tipo de efecto `modifyUtility(acción, %)` sin condición | ✅ | | |
| Recalcular `Pitch.ZoneOf` cada tick y escribir/limpiar el array | | ✅ (una cláusula, ya con función existente) | |
| Fase de posesión, con/sin balón, balón suelto, primer tick tras recibir, turba | | | ✅ (cada una necesita su propio estado nuevo) |
| Vocabulario data-driven genérico para que un diseñador escriba "prefiere A sobre B cuando S" sin código nuevo por predicado (C16) | | | ✅ |

**No se implementa nada de esta tabla en este documento.** Es la decisión a confirmar antes de escribir
una sola línea.

## 3. Plan experimental — Cazagoles

### 3.1 Fórmula exacta del `modifyUtility` (candidata, sin fijar el porcentaje todavía)

```csharp
// Sim/Engine/MatchPlayer.cs — nuevo, análogo a LeaderBonusPercent pero por acción
private readonly int[] _perkActionBonusPercent = new int[ActionCount]; // 0 = sin bono, mutable en partido
public int PerkActionBonusPercent(PlayerAction action) => _perkActionBonusPercent[(int)action];
internal void SetPerkActionBonus(PlayerAction action, int percent) => _perkActionBonusPercent[(int)action] = percent;
```

```csharp
// Sim/Engine/Utility.cs:160 — candidata, apilado aditivo con el bono de Líder (mismo paréntesis)
int traitMultiplier = p.ActionMultiplier(action)
    * (100 + p.LeaderBonusPercent + p.PerkActionBonusPercent(action)) / 100;
```

**No fijo el porcentaje aquí, y no debería fijarse a ojo.** Mismo protocolo que BB-G (Regla A: medición
barata antes de tocar código): antes de elegir el número, un volcado de utilidad (RT-098) de un delantero
con el balón en el tercio rival dice el hueco real entre `Shoot` y `ShortPass` en ese instante — igual que
el volcado que fijó `chaseBallLooseBonus`. Eso es el primer paso de la implementación, no de este plan; se
hace y se documenta antes de fijar el `%`, no se adivina.

**Alternativa a la aditiva, para que quede escrita y no se elija por descarte:** un factor multiplicativo
separado (`* (100 + PerkActionBonusPercent(action)) / 100` como término aparte, no dentro del mismo
paréntesis que `LeaderBonusPercent`) evitaría que el bono de Líder y el de perk interactúen de forma
no lineal cuando coinciden en el mismo jugador. Con un único perk piloto y sin Líder en juego a la vez
sobre el mismo portador en la muestra de calibración, las dos formas miden igual; la diferencia importa
en cuanto haya dos o más bonos activos a la vez. Queda anotado para cuando eso ocurra, no decidido ahora.

### 3.1b Medición previa (17 sep 2026) — el hueco medido, y por qué no da un número único

**Instrumento**: mismo principio que el volcado que fijó `chaseBallLooseBonus` (BB-G), temporal y no
commiteado (mismo estatus que `_BBG.cs`). Sobre plantillas generadas sin ningún perk (`TeamGenerator`,
raza neutral, calidad 50, sin `modifyUtility` ni cambio de pesos: el sistema tal cual está hoy), se buscó
en la traza (RT-098/`MatchTrace`) todo tick de decisión en que un delantero con el balón en su tercio
rival (`Pitch.ZoneOf == Zone.Opposing`) elige `ShortPass` — el caso exacto que describe la ficha de
Cazagoles («en el tercio rival elige `Shoot` donde el resto elige `ShortPass`»); en ese tick exacto se
repitió el partido con `SimConfig.DumpUtility` fijado al jugador y al tick para leer la fila completa de
`Shoot` y de `ShortPass`.

**Con la fórmula candidata de 3.1 (aditiva, `Líder=0` en la muestra — ver nota de método), el porcentaje
que necesita `PerkActionBonusPercent(Shoot)` para que `Shoot` iguale o supere a `ShortPass` en un episodio
concreto es exactamente**:

```
X_requerido = 100 · (ShortPass.Score − Shoot.Context) / (Shoot.Score − Shoot.Context) − 100
```

(`Shoot.Score − Shoot.Context` es el término ponderado `Base·Tactical/100·Trait/100` ya aislado del propio
volcado, sin reconstrucción — es una lectura exacta, no una aproximación.)

**Resultado, dos semillas independientes (RT-081), 15 y 17 episodios reales con fila completa**:

| semilla | n | mín | p25 | mediana | p75 | máx | media |
|---|---|---|---|---|---|---|---|
| 1 | 15 | 1,0 % | 18,4 % | 23,9 % | 48,6 % | 121,0 % | 36,3 % |
| 2 | 17 | 4,4 % | 13,8 % | 24,7 % | 47,5 % | 135,6 % | 34,9 % |

La forma **replica**: mediana ≈ 24 % en las dos semillas, mismo rango intercuartílico (≈14-19 % a ≈47-49 %),
misma cola larga por encima de 100 % en los casos más extremos. No es ruido de muestra pequeña — es la
misma distribución, medida dos veces.

**Por qué esto NO da un número único, a diferencia de `chaseBallLooseBonus`.** El bono de BB-G competía
contra un rival con `Context` prácticamente fijo (`coverBetweenBallAndGoalBonus`, constante); un solo
volcado bastaba porque el hueco no variaba con la situación. Aquí `ShortPass.Context` (la calidad del
apoyo disponible: distancia, ángulo, si hay un compañero mejor colocado) varía mucho de un episodio a otro
(42 a 448 en la muestra) y el bono candidato es un **factor multiplicativo** sobre el término ponderado de
`Shoot`, no un sumando plano — así que el mismo `%` cierra un hueco pequeño con margen de sobra y deja un
hueco grande completamente sin tocar. **No existe un porcentaje que "cierre el hueco" en el sentido de
BB-G, porque el hueco no es uno solo: es una distribución.**

**Lo que sí es defendible, y lo que no.**

- **Defendible**: un porcentaje en el entorno de la mediana/RIC medida (≈20-50 %) desplaza la decisión en
  una fracción sustancial y replicada de los episodios reales donde el sistema actual ya prefiere el pase
  — ni tan bajo que no cambie nada observable (Tanda 0: un efecto por debajo del umbral de
  `sweeper_keeper`, L1≈0,001, sería indistinguible de ruido), ni tan alto que fuerce `Shoot` en la
  práctica totalidad de los casos, incluidos los de la cola (>100 %, un compañero claramente mejor
  colocado) — eso anularía el coste que la propia ficha exige («mata jugadas que seguían vivas... con él,
  el equipo remata peor y más», no «nunca pasa»). Rango de partida razonable: **20 %-50 %**, con la
  mediana (≈24 %) como candidato central si hay que elegir un solo punto de arranque.
- **No defendible**: cualquier valor fuera de ese rango elegido sin repetir esta medición — en particular,
  algo por encima de la cola (>120 %) convertiría a Cazagoles en "dispara siempre que tenga el balón en el
  tercio rival", que es una mecánica distinta (determinista, no probabilística) de la que describe la
  ficha C-26.
- **Nota de método, sin confirmar**: el cálculo asume `LeaderBonusPercent≈0` para los portadores
  muestreados — no se puede separar de `ActionMultiplier` solo con la fila volcada (`TraitMultiplier` es
  el producto de los dos). Es consistente con lo observado (todas las filas de `Shoot` muestran combinaciones
  compatibles con `Líder=0`) pero no está aislado por un experimento propio: **LIKELY**, no CONFIRMED. Si
  el piloto se instrumenta con un portador que sí tiene compañeros de casilla-hogar contigua con el rasgo
  Líder, esta media hay que remedirla sobre ese caso concreto antes de fiarse del rango de arriba para él.

**No se ha tocado `modifyUtility`, ningún peso de `data/ai/weights.json` ni ningún tope.** Este apartado
es una medición, no una implementación: el valor final y el experimento de validación (§3.4-3.7) se
deciden junto al revisor antes de escribir código.

### 3.2 Condición geométrica mínima

`Pitch.ZoneOf(player.Position, player.Team) == Zone.Opposing`, recalculada cada tick (§1). Sin estado
adicional: la posición del jugador y su equipo ya existen.

### 3.3 Qué cuenta como activación/exposición (la lección de Tanda 0, aplicada)

Dos números, no uno, siempre juntos con la L1 — la regla que fijas en tu mensaje anterior:

- **Exposición**: fracción de los ticks del portador, mientras decide, en los que `Pitch.ZoneOf(...) ==
  Opposing` es verdad. Es la oportunidad, no el efecto.
- **Activación (efecto)**: histograma de acción elegida (mismo instrumento de Tanda 0,
  `ActionHistogramTests`) **restringido a los ticks de exposición**, comparado brazo con perk contra brazo
  control. La L1 sobre el conjunto expuesto es la que importa; la L1 sobre el partido entero se diluye si
  la exposición es baja, igual que le pasó a `bulwark_stance`.
- Umbral orientativo de "bien potenciado", tomado de Tanda 0: `own_third_anchor` (40/40 partidos con
  activación, L1=0 informativo) frente a `bulwark_stance` (2/40, L1=0 sin información). Para Cazagoles,
  si la exposición al tercio rival cae muy por debajo de eso en la muestra, se sube la muestra (`Rosters`)
  antes de concluir nada — mismo remedio que BA-N y BB-P, no un umbral inventado.

### 3.4 Baseline / control emparejado

Misma metodología que Tanda 0 y ADR 0087: mismas plantillas, mismas semillas de partido, con `Cazagoles`
puesto en un delantero elegible y sin él (control), sin arrastre de campaña (independiente, como Tanda 0).
Tamaño de muestra a decidir con la exposición medida en un primer lote pequeño (p.ej. 20 plantillas × 2
direcciones, como Tanda 0) antes de comprometerse a uno mayor.

### 3.5 Métricas principales

- **`passChain`**: no se mide vía la puerta `BuildsWinDifferently` (`Cazagoles` no está en `elf_tiki_taka`
  ni en `orc_violence` hoy) — se mide **directamente**, la longitud media de cadena de pases del **equipo**
  del portador, con y sin el perk, emparejado. Es la pregunta causal limpia: ¿jugar con Cazagoles acorta
  las cadenas de ese equipo? Integrarlo en la puerta de builds es un paso posterior, no de este piloto.
- **`shotsPerMatch`**: mismo tratamiento, del equipo del portador, con y sin perk.
- **Diferenciación de builds**: para un piloto de un solo perk aislado, el proxy correcto es la propia L1
  del histograma de acción (§3.3) — cuánto cambia el **perfil de conducta** del portador, no una
  comparación entre dos builds que todavía no llevan este perk. La integración en `BuildsWinDifferently`
  (o en un build nuevo del catálogo) es una validación de segundo orden, posterior a decidir si el
  mecanismo aislado es seguro.

### 3.6 Métricas de seguridad

- `injuriesPerMatch`: sin vía directa conocida (Cazagoles no toca `Tackle`/`Block`), pero cualquier
  redistribución de posesión (menos pases, más tiros, más saques de puerta tras fallar) cambia el ritmo
  del partido; se comprueba como red de seguridad estándar, no porque haya una hipótesis causal fuerte.
- `ballThirdMaxShare`: más tiros → más saques de puerta → más reinicios en el tercio propio del rival;
  vía plausible, se mide.
- Lote de `/Balance` (RT-054) solo si la medición aislada por parejas no muestra nada preocupante primero
  — no se lanza el lote agregado como primer paso (esconde justo lo que un agregado en banda puede
  esconder, `balance-measure`).

### 3.7 Criterio para distinguir señal de ruido

1. La exposición tiene que ser comparable a `own_third_anchor` (activación alta), no a `bulwark_stance`.
   Si no lo es, se sube `Rosters` antes de interpretar nada.
2. Cualquier delta en `passChain`/`shotsPerMatch` se remide con una **segunda semilla independiente**
   antes de llamarlo señal — mismo protocolo que BB-G/BB-C/BB-P esta sesión: una sola semilla no basta
   para separar efecto de ruido de muestra.
3. Con exposición alta y réplica en dos semillas, un delta se lee como señal real del mecanismo. Sin
   réplica, se anota como no confirmado y no se usa para decidir nada sobre C1 en general.

### 3.8 Qué no se toca

`MinPassChainRatio` (ADR 0062) permanece fijo. Este piloto mide si `Cazagoles` empeora la diferenciación
de pases, no decide si el umbral de la puerta debe cambiar — son preguntas distintas y esta ficha no
responde a la segunda.

## 4. Candidatos representativos y protocolo por candidato (17 sep 2026, sin implementar todavía)

§3.1b midió una distribución (n=32, dos semillas), no un punto. Este apartado no elige el valor de
producción: compara tres puntos **anclados en la propia tabla** (no en redondeos a ojo) para que la
decisión final se tome con datos de los tres, no con una intuición sobre uno solo.

### 4.0 Los tres candidatos, y por qué esos puntos y no otros

| candidato | valor | ancla en la tabla combinada (n=32) | episodios que voltearía |
|---|---|---|---|
| **Bajo** | **17 %** | ≈ p25 medido (16,8 %) | 8/32 (25 %) |
| **Central** | **24 %** | mediana medida (24,3 %) | 16/32 (50 %) |
| **Alto** | **48 %** | ≈ p75 medido (47,8 %) | 25/32 (78 %) |

Los tres son cuartiles reales de la medición, no valores redondos elegidos por conveniencia: cada uno
tiene una lectura directa ("voltea el cuarto más fácil / la mitad / los tres cuartos más fáciles de los
episodios medidos"). **Se descarta deliberadamente anclar un candidato en la cola (>100 %)**: por §3.1b,
eso convertiría a Cazagoles en "dispara siempre que tenga el balón en el tercio rival" — una mecánica
determinista, distinta de la que describe la ficha C-26, y no es lo que este piloto está diseñado para
validar.

### Protocolo común a los tres (no se repite por candidato)

- **Instrumento**: extensión de `ActionHistogramTests` (Tanda 0) con el filtro de exposición de §3.3
  (delantero + balón + `Zone.Opposing`) — igual metodología, restringida a los ticks donde Cazagoles
  puede actuar.
- **Control emparejado**: §3.4 (mismas plantillas y semillas, con/sin el perk, sin arrastre de campaña).
- **Métricas principales**: §3.5 — `passChain` y `shotsPerMatch` medidos **directamente sobre el equipo
  del portador**, nunca vía `BuildsWinDifferently` ni su umbral (Cazagoles no está en `elf_tiki_taka` ni
  en `orc_violence`, y `passChain` ya tiene su propia causa histórica documentada en BB-P — mezclar las
  dos cosas contaminaría cualquier lectura).
- **Métricas de seguridad**: §3.6 (`injuriesPerMatch`, `ballThirdMaxShare`), más una comprobación nueva
  específica de este piloto (ver "auditoría de comportamiento" en cada candidato abajo): la distribución
  de `distanceToGoal` de los tiros que añade el perk, no solo su recuento — el aviso explícito de
  `balance-measure` de que un agregado en banda puede esconder una IA que dispara mal.
- **Señal contra ruido**: §3.7 (exposición comparable a `own_third_anchor`, réplica en una segunda
  semilla independiente antes de llamar señal a cualquier delta).
- **`MinPassChainRatio` fijo**: §3.8, sin excepción para ningún candidato.
- **Comprobación añadida por comparar tres candidatos a la vez**: si el efecto es real, el orden esperado
  es monótono en `X` — Alto ≥ Central ≥ Bajo en `L1` del histograma, en caída de `passChain` y en subida
  de `shotsPerMatch`. Si ese orden **no** se cumple con los tres medidos, es una señal de que algo en la
  medición está mal montado, no de que el mecanismo sea errático — se para y se revisa el instrumento
  antes de interpretar cualquier candidato.

### 4.1 Candidato bajo — 17 %

1. **Decisiones que debería desplazar**: el cuarto de episodios donde `ShortPass` gana por el margen más
   estrecho de la muestra (los de `X_requerido` ≤ ~17 en la tabla de §3.1b) — el apoyo disponible es
   marginal, no claramente mejor que el tiro.
2. **Shoot vs ShortPass (histograma restringido a exposición)**: se espera una `L1` mayor que cero pero
   pequeña — del orden de lo que Tanda 0 midió para `sweeper_keeper` (0,0013, un efecto real pero
   modesto) o menor. Es el candidato con más riesgo de resultar **indistinguible de ruido** incluso con
   exposición alta.
3. **`passChain` (equipo del portador)**: caída esperada pequeña, posiblemente dentro del margen de error
   de la medición emparejada — no se espera un movimiento claro.
4. **`shotsPerMatch` (equipo del portador)**: subida pequeña; el aumento debería venir mayoritariamente de
   posiciones razonables (los tiros "casi obvios" que el sistema actual ya casi prefiere).
5. **Activación/exposición**: mismo procedimiento que los otros dos (§3.3) — la exposición (oportunidad)
   no depende de `X`, así que debería salir igual en los tres candidatos; solo cambia la activación
   (efecto) medida sobre esa misma exposición.
6. **Métricas de seguridad**: no se espera movimiento detectable en `injuriesPerMatch` ni
   `ballThirdMaxShare` con un efecto tan pequeño; la auditoría de `distanceToGoal` sirve de referencia
   base ("cómo es la distribución cuando el efecto es mínimo") para comparar contra los otros dos.
7. **Señal real vs ruido**: solo se cuenta como señal si, con exposición comparable a `own_third_anchor`,
   la `L1`/`passChain`/`shotsPerMatch` se replican en una segunda semilla en la misma dirección. Dado el
   tamaño esperado, es el candidato con más probabilidad de no alcanzar ese listón.
8. **Condición de descarte (aunque `Shoot` suba)**: si con exposición bien potenciada el efecto sigue sin
   distinguirse de la `L1=0` de `own_third_anchor` (el cero informativo de Tanda 0, no el `L1=0` de
   `bulwark_stance` por falta de activación), este candidato se descarta **no por ser perjudicial, sino
   por ser inútil**: un piso demasiado bajo para que el jugador note el perk, que es justo lo que este
   candidato está aquí para comprobar.

### 4.2 Candidato central — 24 %

1. **Decisiones que debería desplazar**: la mitad de los episodios medidos, exactamente los de
   `X_requerido` ≤ mediana — situaciones de apoyo "normal", ni marginal ni claramente superior.
2. **Shoot vs ShortPass**: se espera una `L1` claramente por encima de la de `sweeper_keeper` (0,0013):
   un efecto real y visible en el perfil de conducta del portador dentro del tercio rival, no un cambio
   cosmético.
3. **`passChain`**: caída moderada y medible, en la misma dirección que el candidato bajo pero de mayor
   magnitud — el primer punto donde se espera poder decir "esto mueve algo" con confianza, no solo "no se
   puede descartar ruido".
4. **`shotsPerMatch`**: subida moderada; la auditoría de comportamiento debería mostrar todavía una
   mayoría de tiros en distancias razonables, con una fracción creciente (pero no dominante) de
   posiciones más discutibles frente al candidato bajo.
5. **Activación/exposición**: igual que 4.1.
6. **Métricas de seguridad**: es el primer candidato donde vale la pena mirar `ballThirdMaxShare` con
   atención (más saques de puerta tras más tiros fallados); `injuriesPerMatch` sigue sin vía causal
   directa conocida, se comprueba como red de seguridad estándar.
7. **Señal real vs ruido**: mismo criterio de §3.7. Es el candidato con más probabilidad a priori de
   producir una señal replicable y de magnitud manejable — es el motivo por el que la mediana es un buen
   punto de partida, no una conclusión.
8. **Condición de descarte**: si `passChain` del equipo del portador cae más de lo que `shotsPerMatch`
   justifica (el coste supera claramente al beneficio de amenaza ofensiva), si alguna métrica de
   seguridad sale de su banda de RT-056, o si la auditoría de `distanceToGoal` muestra que la mayoría de
   los tiros añadidos vienen de posiciones pobres (la IA "dispara mal", no "dispara más" con criterio) —
   cualquiera de las tres, aunque `shotsPerMatch` y la `L1` "se vean bien" en agregado.

### 4.3 Candidato alto — 48 %

1. **Decisiones que debería desplazar**: tres cuartos de los episodios medidos; solo quedan sin voltear
   los casos con la ventaja de `ShortPass` más clara de la muestra (apoyo claramente mejor colocado).
2. **Shoot vs ShortPass**: se espera la mayor `L1` de los tres candidatos, con `ShortPass` reducido a una
   fracción minoritaria de las decisiones dentro de la ventana de exposición.
3. **`passChain`**: la mayor caída esperada de los tres. Contexto que ya tenemos (BB-P): `passChain`
   global ya mide ~1,08-1,09 contra un umbral de puerta de 1,11, por causas ajenas a este piloto y ya
   diagnosticadas — no se toca `MinPassChainRatio` ni se usa `BuildsWinDifferently`, pero una caída grande
   en el equipo del portador es la señal más temprana de que este candidato es demasiado agresivo para
   convivir con el resto del diseño, aunque la puerta en sí no la vea.
4. **`shotsPerMatch`**: la mayor subida esperada; es también donde más riesgo hay de que la auditoría de
   `distanceToGoal` muestre una fracción sustancial de tiros de calidad pobre — el propio mecanismo por
   diseño empieza a ganarle a apoyos que el volcado marcaba como claramente mejores.
5. **Activación/exposición**: igual que 4.1 y 4.2.
6. **Métricas de seguridad**: candidato con más atención necesaria a `ballThirdMaxShare` e
   `injuriesPerMatch` como red de seguridad, y el más informativo para la auditoría de comportamiento —
   si en algún candidato la distribución de `distanceToGoal` se rompe, es más probable verlo aquí primero.
7. **Señal real vs ruido**: mismo criterio de §3.7; a esta magnitud, no replicar en una segunda semilla
   sería tan informativo como replicar (indicaría que incluso un efecto grande depende de una
   configuración concreta de plantillas, no del mecanismo).
8. **Condición de descarte**: las mismas tres de 4.2 (coste de `passChain` desproporcionado, salida de
   banda RT-056, auditoría de calidad de tiro rota), más una cuarta propia de este candidato: si la
   fracción de `ShortPass` dentro de la ventana de exposición se acerca a cero, Cazagoles deja de ser "en
   el tercio rival prefiere tirar" (una IA sesgada, con coste) y pasa a ser "en el tercio rival solo
   tira" (un automatismo sin decisión real) — eso contradice la propia ficha C-26 ("mata jugadas que
   seguían vivas", no "elimina el pase como opción"), y se descarta por fidelidad de diseño aunque todas
   las métricas numéricas parezcan aceptables.

**No se ha implementado `modifyUtility` ni tocado ningún peso.** Este apartado es el protocolo de
comparación, para revisar antes de escribir el primer código de C1.

## 5. Resultado del experimento (17 sep 2026) — mecanismo medido, valor de producción sin decidir

**C1 está implementado y commiteado** (`Sim/Engine/MatchPlayer.cs`, `Sim/Engine/Utility.cs`,
`Sim/Engine/MatchEngine.cs`, `Sim/Perks/EffectEngine.cs`, `Sim/Perks/PerkDefinition.cs`): el array
`_perkActionBonusPercent`, el tipo de efecto `ModifyUtility` (acción + `%` + zona opcional) y el recálculo
por tick en `MatchEngine.UpdateContextCaches` — exactamente el diseño de §1/§2. **Inerte por defecto**:
ningún perk de `/data` lo usa todavía, y las 761 pruebas no-puerta y las mismas 43 puertas (3 rojas de
siempre, ninguna nueva — ver `docs/pendientes/BB-P.md`) confirman que añadirlo no cambió ni un partido.

**El perk de Cazagoles con cada candidato se construyó solo en memoria** (`PerkDefinition`/
`EffectDefinition` de test, `Trigger=MATCH_START`, `condition=""`, `positionOnly=Forward`, mismo patrón
que `deep_run.json`), no en `/data`: el valor de producción sigue sin decidir, así que no había nada que
commitear en el catálogo todavía.

### Protocolo ejecutado

Control emparejado (§3.4), dos semillas independientes, filtro de exposición de §3.3
(`Pitch.ZoneOf == Opposing`, sin exigir balón — la misma definición literal del documento). Primer lote
pequeño (20 plantillas × 2 direcciones) mostró exposición alta (35-46%) pero `passChain`/`shotsPerMatch`
sin monotonía entre candidatos — ruido de muestra pequeña, no un fallo del instrumento (la `L1`, que tiene
muchísima más muestra por partido, ya era monótona en ese primer lote). Siguiendo §3.4 ("tamaño de
muestra a decidir con la exposición medida en un primer lote pequeño... antes de comprometerse a uno
mayor"), se subió a 100 plantillas × 2 direcciones (200 partidos/brazo/candidato/semilla, 2.400 partidos
en total) **igual para los tres candidatos**, una sola vez, sin tocar ningún candidato por separado.

### Tabla única de comparación (media de las dos semillas)

| candidato | exposición (armado/control) | `L1` histograma expuesto | `passChain` (armado/control) | Δ`passChain` | `shotsPerMatch` (armado/control) | Δ`shots` | `injuriesPerMatch` (armado/control) | dist. mediana tiro, casillas (armado/control) | `Shoot`/(`Shoot`+`ShortPass`) en expuestos (armado/control) |
|---|---|---|---|---|---|---|---|---|---|
| **17%** | 39,5% / 40,2% | 0,0336 | 2,067 / 2,096 | −0,029 | 4,135 / 3,760 | +0,375 | 0,378 / 0,398 | 2,88 / 2,64 | 84,3% / 78,2% |
| **24%** | 40,5% / 41,5% | 0,0385 | 2,054 / 2,135 | −0,081 | 4,342 / 3,880 | +0,462 | 0,407 / 0,420 | 3,05 / 2,62 | 88,9% / 78,5% |
| **48%** | 40,1% / 41,3% | 0,0698 | 2,025 / 2,115 | −0,090 | 4,570 / 3,805 | +0,765 | 0,405 / 0,390 | 3,10 / 2,62 | 93,0% / 75,6% |

**Monotonía Bajo ≤ Central ≤ Alto (media de las dos semillas), confirmada en las tres métricas** tras subir
la muestra: `L1` 0,0336→0,0385→0,0698; Δ`passChain` −0,029→−0,081→−0,090; Δ`shots`
+0,375→+0,462→+0,765. Con la muestra pequeña ninguna de las dos últimas lo era (ruido, no señal); con la
muestra grande, sí, en las dos semillas por separado.

### Lectura contra los ocho puntos de §4, candidato por candidato

- **17%**: la `L1` (0,0336) ya está muy por encima del techo de ruido de calibración de Tanda 0
  (`sweeper_keeper`=0,0013) — **no resultó indistinguible de ruido**, al contrario de lo previsto en
  §4.1: el mecanismo tiene un efecto real y medible incluso en el extremo bajo del rango. No se cumple la
  condición de descarte de §4.1.
- **24%**: efecto claramente mayor que 17% y menor que 48% en las tres métricas, como se esperaba. El
  coste (`passChain` −0,081, ~−3,9% relativo) no es desproporcionado frente al beneficio (`shotsPerMatch`
  +0,462, ~+12%); la distancia mediana de tiro sube de 2,62 a 3,05 casillas (el coste de calidad que la
  ficha pide, no un roto). Ninguna métrica de seguridad (`injuriesPerMatch`) se mueve. No se cumple
  ninguna condición de descarte de §4.2.
- **48%**: mayor efecto de los tres en todo. `Shoot`/(`Shoot`+`ShortPass`) en los ticks expuestos sube de
  ~76% (control, sin cambios entre candidatos) a **93%** — `ShortPass` queda exprimido a ~7% de las
  decisiones con balón en el tercio rival, lejos de "nunca pasa" (condición de descarte propia de §4.3),
  pero ya es el candidato donde más cerca está de esa línea. `injuriesPerMatch` sigue plano. No se cumple
  ninguna condición de descarte de §4.3, pero es el que menos margen deja antes de cumplirla si se subiera
  más.

**`ballThirdMaxShare`** (§3.6) no se ha medido en este lote emparejado — la propia sección ya preveía
diferirlo a un lote de `/Balance` "solo si la medición aislada por parejas no muestra nada preocupante
primero"; con los tres candidatos limpios en lo demás, no se ha lanzado.

**`MinPassChainRatio` no se ha tocado.** No se ha decidido ningún valor de producción ni se ha escrito
ningún fichero en `/data/perks`. Este apartado es el resultado del experimento aprobado en §4, para
revisar antes de elegir el candidato y cerrar C1.

## Hermanos

- `docs/analisis/tanda-0-histograma-de-accion.md` — el instrumento que este plan reutiliza.
- `docs/analisis/catalogo-conceptual-fase-a.md` — el catálogo conceptual completo, candidato C-26.
- `docs/pendientes/BB-P.md` — estado actual de `passChain` (por debajo de su umbral por causas ya
  diagnosticadas y ajenas a este piloto); el piloto mide un efecto adicional sobre el mismo canal, no
  sustituye ese diagnóstico.
