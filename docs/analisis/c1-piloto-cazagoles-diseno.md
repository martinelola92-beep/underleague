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

## Hermanos

- `docs/analisis/tanda-0-histograma-de-accion.md` — el instrumento que este plan reutiliza.
- `docs/analisis/catalogo-conceptual-fase-a.md` — el catálogo conceptual completo, candidato C-26.
- `docs/pendientes/BB-P.md` — estado actual de `passChain` (por debajo de su umbral por causas ya
  diagnosticadas y ajenas a este piloto); el piloto mide un efecto adicional sobre el mismo canal, no
  sustituye ese diagnóstico.
