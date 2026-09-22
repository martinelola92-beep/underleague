# BE-A — El centrocampista nunca entra a su marcado sin balón (el comentario dice una cosa y el código otra)

Estado: **primera mitad CERRADA (22 sep 2026)** — el instrumento está arreglado y medido (ADR 0125 **D1**).
La segunda mitad (**D2**, bono por puesto, y **D3**, los tres roles de campo) sigue **abierta** y ahora
tiene números con los que decidirse. Encontrada el 20 sep 2026 durante la auditoría de identidad
(`docs/analisis/auditoria-identidad-generador-de-historias.md`), leyendo `Utility.cs` — **no jugando**.

## Síntoma

`Sim/Engine/Utility.cs`: el comentario documenta el caso como **«Defensa y centrocampista»** y la guarda
del código es `role is Position.Defender`. El centrocampista queda fuera.

## Lo que se sabía cuando se abrió la ficha

La hipótesis 3 («es inocuo: el centrocampista apenas dispararía la entrada») era la que más discriminaba y
la que había que medir primero. **Medida: no es inocuo, es lo contrario.** Y por el camino apareció un
error de medida más grave que el propio síntoma, que es lo que cierra esta primera mitad.

## Medido (500 partidos, semilla 1, conjunto de referencia, 22 sep 2026)

Línea base tomada en HEAD limpio antes de tocar nada; reproduce exactamente las cifras con las que se
escribió la ADR 0125 (`tacklesPerMatch` 12,01 · `injuriesPerMatch` 0,81).

| | `tacklesPerMatch` | `offBallTacklesPerMatch` | `injuriesPerMatch` | `foulsPerMatch` |
|---|---|---|---|---|
| base (mezcladas, HEAD) | **12,01** | — | 0,81 | 7,50 |
| tras separar (D1) | **6,92** | **5,09** | 0,81 | 7,50 |
| `tackleMarkTargetBonus` 0 | 7,78 | **0,98** | 0,70 | 4,87 |

**La semilla 1 es el extremo del rango, y todo lo de arriba son sus cifras.** Lo señaló la revisión
independiente y se reprodujo (500 partidos por semilla; en `reference.json` la semilla genera **también las
plantillas**, así que esto es dispersión por plantilla, que es la población que la banda debe cubrir):

| semilla | `tacklesPerMatch` | `offBallTacklesPerMatch` | `injuriesPerMatch` | suma (la métrica vieja) |
|---|---|---|---|---|
| **1** | **6,92** | **5,09** | **0,81** | 12,01 |
| 2 | 7,50 | 2,49 | 0,49 | 9,99 |
| 3 | 9,24 | 0,99 | 0,43 | 10,23 |
| 5 | 10,42 | 1,00 | 0,54 | 11,42 |

Tres consecuencias, y ninguna es menor: **(a)** `offBallTacklesPerMatch` varía **5x** entre plantillas
(0,99 → 5,09), así que fijarle banda con una sola semilla sería fijarla sobre el peor caso —el precedente
del proyecto, la ADR 0082, usó tres—; **(b)** el «0,81 contra el techo de 0,90» con el que la ADR 0125
describe la restricción que manda es **también** el extremo (el rango real es 0,43-0,81); **(c)** separar
**casi dobla la dispersión** de la métrica con banda: la mezclada recorría 9,99-12,01 (rango 2,02) y la
estrecha recorre 6,92-10,42 (rango 3,50) sobre una banda de anchura 8. Ese es un coste real del cambio.

**CONFIRMED — la separación no mueve el juego.** Las otras 24 filas de `summary.csv` son idénticas a la
línea base, ninguna cambia de estado y la única movida es `tacklesPerMatch`, por definición. La revisión
independiente lo comprobó más fuerte, partido a partido: en los 500, **todas** las columnas de
`matches.csv` son idénticas (marcador, ganador, ticks, faltas, tarjetas, lesiones, tercios, criterio) y
`base.tackles == split.tackles + split.offBallTackles` **exacto**, igual en los 60 jugadores de
`players.csv`. Si se hubiera desplazado una sola tirada, algún marcador habría cambiado. No desplaza ni
una: es contabilidad.

**CONFIRMED — el reparto por puesto que la ADR 0125 midió era casi todo entrada sin balón.** Por
partido-jugador, con el instrumento corregido:

| puesto | al portador | sin balón | % de sus «entradas» que no son disputa |
|---|---|---|---|
| Defensa | 0,67 | **1,27** | **65,3 %** |
| Centrocampista | 0,64 | 0,00 | 0 % |
| Delantero | 0,20 | 0,00 | 0 % |
| Portero | 0,01 | 0,00 | 0 % |

El «DEF 1,95 · MID 0,64 · FWD 0,20» de la ADR 0125 se descompone así. **Disputando el balón, el defensa y
el centrocampista están casi igualados (0,67 vs 0,64)**: lo que separa a los dos puestos hoy no es entrar
más, es que solo uno de ellos puede pegar a quien no lleva el balón.

**CONFIRMED — el bono es una palanca, no un interruptor.** Con `tackleMarkTargetBonus` en 0 la entrada sin
balón **no desaparece**: quedan 0,98 por partido, decididas por utilidad sin ningún bono. `offBall` es la
*intención* con la que se decidió la entrada (`MatchPlayer.TackleOffBall`), y esa intención puede ganar la
comparación por sí sola.

> **Esto contradice la premisa de la ADR 0125 D3** («un puesto con bono 0 no entra nunca»). Si D2 sustituye
> la guarda de rol por el mapa de bonos, un puesto con 0 **seguirá entrando** salvo que se le descalifique
> explícitamente. Es una línea de código, pero hay que escribirla: el dato en 0 no desactiva la acción.

**CONFIRMED — la entrada sin balón sustituye disputas, no solo las suma.** Apagando el bono, las entradas
al portador suben de 6,92 a 7,78 (+0,86): parte del presupuesto de disputa se lo come el golpe. Lo mismo
por el lado del coste: las faltas bajan de 7,50 a 4,87 y las lesiones de 0,81 a 0,70 — es decir, **la
entrada sin balón vale hoy ~0,11 de las 0,81 lesiones por partido**.

**REJECTED — «al separarlas `tacklesPerMatch` baja a ~9,19»** (la previsión anotada en
`docs/project-state.md` y en el `_doc` de `data/sim/tuning.json`). El valor real es **6,92**. El 9,19 del
`_doc` es **otro experimento** —la métrica mezclada con la entrada sin balón apagada, en otro estado del
árbol—, no una separación; hoy ese mismo experimento da 7,78. Descartada como previsión, no como dato.

## Lo que esto le cambia a la segunda mitad (D2/D3)

**LIKELY — el riesgo declarado de la ADR 0125 apunta al lado equivocado de la banda.** La ADR razona sobre
un techo («DEF+MID da 23,86 contra un techo de 14»), pero eso se midió con las dos poblaciones sumadas.
Con el instrumento corregido, abrir la entrada sin balón sube `offBallTacklesPerMatch` —que **no tiene
banda**— y baja `tacklesPerMatch`, que en la plantilla más expuesta (semilla 1) queda en **6,92 con suelo
6,00: 0,92 de margen, por abajo**. No está aislado por un experimento propio: se deduce de la sustitución
medida arriba, y hay que comprobarlo en el lote de D2 —con varias semillas, no con una.

La restricción que la ADR llama «la que manda» —`injuriesPerMatch` en 0,81 con techo 0,90— **no cambia**:
la separación es de medida y las lesiones se tiran igual.

## D2/D3 — la estructura está, la calibración es del revisor (22 sep 2026)

**Hecho y commiteado**: el ajuste de la entrada sin balón es un **mapa por puesto**
(`data/ai/weights.json` → `context.tackleMarkTargetBonus`, con esquema propio y los cuatro puestos
obligatorios), la guarda `IsDefensiveRole` **ya no existe** —quién entra lo decide el dato— y **0 significa
que ese puesto no DECIDE entrar nunca**, comprobado explícitamente en el código porque está medido que un 0
no desactiva nada por sí solo. Con `Defender: 150 · Midfielder: 0 · Forward: 0` el lote sale **byte a byte
idéntico** a antes: es el mismo juego, con la palanca ya montada.

> **El alcance de ese «nunca» es la decisión, no el motor entero** *(lo encontró la revisión independiente;
> mecanismo real, **sin evidencia de activación**)*. `MatchEngine.RepeatTackle` —el efecto `extraAction` de
> «Embestida»/«Arrollador»— no decide: ejecuta, y marca la entrada como sin balón sin consultar el mapa, así
> que un delantero con ajuste 0 y uno de esos perks **sí** produce entradas sin balón. Hoy no ocurre en
> ningún lote (ningún no-defensa lleva `charge` ni `steamroller` en `/data`, y el conjunto de referencia no
> lleva perks) y **no es una regresión** —con la guarda vieja el agujero era idéntico—, pero estaba escrito
> como cerrado en cuatro sitios y no lo era. Ahora lo fija un test
> (`RepeatTackleIgnoresThePerPositionMapAndIsTheKnownGapOfTheZeroRule`) y va con la decisión abierta de
> `extraAction`, que resulta ser **el mismo problema visto dos veces**: por la métrica y por la regla.

**El cargador es la última línea, no el esquema** (RT-032). Rechaza con `DataException`: el formato viejo
(entero plano), un puesto que falta, un puesto desconocido, una **clave numérica** —`"3"` sería `Forward` y
se colaba en silencio—, una **clave repetida** —apagaba el único puesto activo sin un solo error—, un
ajuste que llega a `tackleBallCarrierBonus`, y un portero con un valor distinto de 0, que sería un dato
inerte con aspecto de hacer algo. Los siete casos tienen test, y el reparto publicado también: abrir un
puesto tiene que ser deliberado, no un descuido que aparezca siete minutos después en las puertas.

**No hecho**: abrir de verdad los tres puestos. No porque falte trabajo, sino porque **las tres
calibraciones que lo consiguen rompen algo**, y elegir cuál se rompe es decisión del revisor.

### Lo que dice la tabla de utilidad, antes de medir nada

Sin balón, la entrada compite contra la mejor alternativa del propio puesto (`Base × Tactical / 100`):

| puesto | `Tackle` | mejor alternativa | hueco que el ajuste debe cubrir |
|---|---|---|---|
| Defensa | 420 | `MarkOpponent` 600 | **+180** |
| Centrocampista | 346 | `MarkOpponent` 450 | **+104** |
| Delantero | 211 | `MarkOpponent` 180 | **−31** (ya gana sin ajuste) |

**Esto explica el «orden invertido» que la ADR 0125 midió sin explicar.** Con el bono plano de 150 el
margen efectivo era −30 para el defensa, +46 para el centrocampista y **+181** para el delantero: al abrir
el rol salía FWD 2,29 y MID 2,74 contra DEF 1,64 porque las alternativas del delantero son malas, no
porque entre mucho. **Consecuencia: para que el defensa quede por delante, el mapa tiene que poder
RESTAR.** El «bono» es un ajuste con signo, y así está implementado.

### Lo medido (500 partidos por configuración; DEF/MID/FWD = ajuste, entradas sin balón por partido-jugador)

| configuración | `tacklesPerMatch` | sin balón | `injuries` | reparto | veredicto |
|---|---|---|---|---|---|
| hoy (150 / 0 / 0) | 6,92 | 5,09 | 0,81 | DEF 1,27 · MID 0 · FWD 0 | el centrocampista no existe en la fase sin balón |
| 190 / −40 / −221, enfr. 280 | **6,26** | 6,50 | 0,85 | DEF 1,21 · MID 0,23 · FWD 0,14 | orden correcto, pero el **suelo de 6,00 a 0,26** |
| + contador de enfriamiento separado, enfr. 550, entrada al portador 120 | 7,31 | 5,36 | **0,80** | DEF 0,99 · MID 0,19 · FWD 0,13 | las dos bandas cómodas… y **rompe la identidad de las builds** |

El defensa está **saturado**: entre 80 y 150 de ajuste su tasa no se mueve (1,26-1,27). Su palanca no es el
ajuste sino el enfriamiento, que es **global**: no se puede bajar al defensa sin bajar a todos. Por eso
abrir los otros dos puestos es una **redistribución**, no una suma, y por eso hace falta un tercer número.

### El hallazgo que hay debajo, y su precio *(CONFIRMED, aislado)*

**`MatchPlayer.TackleCooldown` es un contador único**: una entrada sin balón lo pone a
`OffBallTackleCooldownTicks` (hoy 180 ticks; en las pruebas hasta 550) y `Utility` descarta con él
**cualquier** entrada, también la del portador. O sea: **pegarle a quien no lleva el balón deja al jugador
sin poder disputarlo durante 12-36 segundos.** Es exactamente el caso que el proyecto ya resolvió para el
bloqueo en el paquete U —contadores separados, *«disputar el balón deja de pagar por haber cargado»*— y
explica por qué al dar papel al centrocampista caían las disputas: no es un coste de diseño, es un
contador compartido.

Separarlos funciona: con el dato de hoy, `tacklesPerMatch` sube de **6,92 a 7,86** sin tocar ningún número.
**Pero tiene un precio medido, y es el que bloquea el paquete**: con el contador separado y el dato de hoy,
la puerta `coherentBuildsBeatNone_orc_violence` cae a **54,17** (mínimo 58) — la build violenta deja de
ganar lo que debe. Aislado: el mapa por puesto **no** la rompe (pasa con D2/D3 puro), el contador separado
**sí**, con el dato intacto.

### Estado de las 43 puertas en cada rama

| | rojas | cuáles |
|---|---|---|
| línea base (HEAD) | 3 | `elf_brawler` 48,54 · `elf_out_of_zone` 46,04 · `injuries` 1,30 |
| D2/D3 puro (190 / −40 / −221, enfr. 280) | 3 | `orc_misplaced` 46,88 **(nueva)** · `elf_brawler` 46,25 (mejora) · `injuries` 1,21 (empeora) |
| + contador separado + enfr. 550 / 120 | 5 | las anteriores más `orc_violence` 56,88 y `TheThreeDoctrinesBuyDifferently` |

**El patrón que las tres comparten**: repartir la violencia entre los tres puestos **aplana la
diferenciación de builds**. `buildsWinDifferently_injuries` baja de 1,30 a 1,21 con D2/D3 puro y a 1,05 con
el contador separado: cuanto más universal es pegar, menos distingue a un equipo violento. Es
`especialización significativa > combinaciones arbitrarias` tirando en contra de la ADR 0125 D2, y no lo
arregla ningún valor del mapa —lo probé en siete configuraciones—.

### Decisión del revisor (22 sep 2026): **Opción 2 y luego Opción 3**

La 2 está hecha: **ADR 0129**, cada entrada paga su propio enfriamiento. Las disputas del balón dejan de
estar bloqueadas por los golpes (6,92 → 7,65 con los dos enfriamientos recalibrados a 90/280), las faltas y
las lesiones quedan donde estaban (7,54 contra 7,50 · 0,79 contra 0,81) y `orc_violence` vuelve a verde.
Las 43 puertas bajan de 3 rojas a 3, con `elf_out_of_zone` pasando a verde. `buildsWinDifferently_injuries`
baja a 1,14 y **no se recalibra ahí**: es justo lo que la Opción 3 debe recuperar.

### Las tres decisiones, tal como se dejaron escritas

1. **Aceptar el aplanamiento** y publicar D2/D3 con el orden correcto, cambiando `orc_misplaced` por
   `elf_out_of_zone` en la lista de puertas rojas conocidas y `buildsWinDifferently_injuries` de 1,30 a
   1,21. Es la lectura literal de la ADR 0125.
2. **Separar el contador de enfriamiento** (su propio ADR: es una regla de motor, tiene precedente en el
   paquete U y arregla un problema real) y **entonces** recalibrar la curva de puertas de builds, que es
   lo que se rompe.
3. **Cambiar el canal**: que el ajuste sin balón entre en el **multiplicador de rasgo** en vez de sumarse
   después, para que «los más agresivos» —que es lo que la ADR pedía— sea lo que diferencie, y no el
   puesto. No está medido; es el único camino que no aplana la identidad *por construcción*.

Mi recomendación, si sirve: **la 2 y luego la 3**. La 1 compra el reparto por puesto pagando identidad de
build, que es justo lo que el proyecto dice que vale más.

## Lo que sigue abierto

- **El síntoma original sigue vivo, pero ya no por el código**: la guarda `IsDefensiveRole` ha
  desaparecido y quién entra lo decide el dato; lo que mantiene fuera al centrocampista es que su ajuste
  vale 0, y cambiarlo es la decisión de arriba. El comentario y el código ya no se contradicen, que era la
  mitad del hallazgo.
- El papel del centrocampista (`docs/project-state.md`: dispara el 6,5 % de los tiros siendo el 43 % de los
  jugadores de campo) sigue sin cerrar. Ahora se sabe que en la fase sin balón tampoco disputa menos que el
  defensa: disputa lo mismo y pega cero.
- `offBallTacklesPerMatch` nace **sin banda** a propósito (D1). Con 5,09 medido y la distribución de arriba,
  fijarla es una ADR posterior — y no antes de D2, que es justo lo que va a mover esa distribución.
- **La carrera del jugador NO cambia de significado.** La primera versión de este paquete la estrechaba
  con la métrica; la revisión independiente señaló que eso es una decisión de diseño sobre lo que una run
  recuerda de un jugador (RF-122) tomada en una ficha de pendientes, y que además dejaba un único entero
  del guardado v5 con **dos definiciones dentro** y nada que las distinguiera. **Corregido**:
  `RunPlayer.WithCareerFrom` suma las dos entradas, exactamente como antes. D1 separa el **instrumento**,
  no la memoria. Si algún día la carrera debe distinguirlas, es `game-design-review` más versión de
  guardado, y va con D2.
- `PerkBalanceClassifier` sigue apuntando `Tackle`/`TackleEvasion` a `tacklesPerMatch`. **Es correcto y
  ahora mejor**: el canal de perk de `tackle` multiplica la tirada de victoria, que solo existe cuando hay
  balón que disputar, así que la métrica estrecha mide ese efecto sin diluirlo en 5,09 sucesos ajenos.
- **DECISIÓN ABIERTA, la que deja esta mitad: `extraAction` sobre `TACKLE`/`RECOVERY`.** El clasificador
  los da por `Ready` con `tacklesPerMatch` porque «la repetición es, literalmente, un `tacklesPerMatch`
  más». Desde D1 eso **solo vale si la repetición alcanza al portador**, y `MatchEngine.RepeatTackle` marca
  la entrada como sin balón salvo que el nuevo objetivo lleve el balón él mismo, así que **en el caso común
  cae en la métrica sin banda**. Afecta a dos perks reales del catálogo: `charge` (TACKLE) y `steamroller`
  (RECOVERY). *(CONFIRMED: el test de la primitiva —`ExtraActionOnTackleRepeatsARealOffBallTackle…`— mide
  dos repeticiones y las dos salen en `OffBallTackles`.)*
  **No se ha tocado la clasificación**, a propósito: pasarla a `AmbiguousPrimaryMetric` cambiaría qué perks
  son medibles y `PerkAuditTests` fija ese inventario con cuentas exactas auditadas por el revisor —es una
  decisión de protocolo (RT-057), no un arreglo—. Va con D2, que es cuando `offBallTacklesPerMatch`
  recibirá banda.
- **Arreglado de paso, porque D1 lo rompía en silencio**: `AttributeBiasChannelTests` medía la exposición
  del disparador `TACKLE` de un defensa con `stats.Tackles`. El evento TACKLE lo publican las dos entradas,
  así que la exposición es la suma de los dos contadores; leer solo uno se dejaba dos tercios fuera.

## Lo que la partición NO cubre (revisión independiente, corrige lo que este paquete afirmaba)

La primera redacción decía que la partición es completa —«ninguna resolución se pierde ni se cuenta dos
veces»—. **Es falso tal como estaba escrito**: es una partición de los **eventos TACKLE emitidos**, no de
todas las resoluciones de entrada. El reparto de contadores lo decide `carrierHasBall` (el **resultado**),
mientras que la base de falta y la sanción las decide `offBall` (la **intención** con la que se decidió, 3
ticks antes). Son ejes distintos y dejan dos casillas, **ninguna de las dos introducida por D1**:

- **Hueco** (`carrierHasBall == false && offBall == false`): entrada al portador cuyo objetivo soltó el
  balón mientras duraba `Tackling`. No cuenta en ningún contador y no emite evento TACKLE, pero **sí tira
  la falta y puede lesionar**. Es comportamiento deliberado desde el paquete E (`MatchEngine` lo comenta),
  pero vive solo en un comentario: **falta un test que lo fije y una medida de cuántas son**.
- **Solape** (`offBall == true && carrierHasBall == true`): entrada decidida contra un marcado que recibe
  el balón dentro de esa ventana. Cuenta en `tacklesPerMatch` y puede ganar el balón, pero se juzgó con
  `offBallFoulBase` y se sanciona como entrada sin balón. O sea: **`tacklesPerMatch` contiene una
  submuestra resuelta bajo otra regla de falta**, y no es observable desde los eventos (el `Detail` del
  FOUL es el mismo). *No medido.* **Importa para D2**: si D2 abre la entrada sin balón a los tres roles, el
  solape crece con ella y alimenta `tacklesPerMatch`, enmascarando en parte el riesgo de suelo de arriba.

## Otras dos que la revisión dejó anotadas

- **`injuriesPerMatch` sigue mezclando las dos poblaciones, y está bien que lo haga.** El argumento de D1
  —«medir dos poblaciones con una regla hacía que cualquier apertura saliera fuera de banda por
  construcción»— se aplicaría igual a las lesiones (~14 % vienen hoy de la entrada sin balón) y a las
  faltas (~35 %). La diferencia es qué mide cada banda: `tacklesPerMatch` mide **fútbol** (disputar el
  balón) y por eso un golpe no le pertenece; `injuriesPerMatch` mide **daño**, y una lesión es una lesión
  venga de donde venga. Por eso se separa una y no la otra. `foulsPerMatch` es INFO y no juzga nada.
- **El hermano ya resuelto es `Blocks`** (ADR 0030 §2): el bloqueo sin balón lleva desde entonces fuera de
  `report.Tackles`, por exactamente esta razón. D1 no inventa un patrón, aplica el que ya existía.
- **Para D2, una pregunta de diseño que D1 solo deja al descubierto**: la entrada sin balón no es visible
  para el jugador en ninguna parte —no está en las entradas de la carrera, no está en `TacklesWon`, no
  aparece en la ficha—, solo deja rastro difuso en faltas y lesiones. D2 quiere convertirla en la identidad
  de un puesto («tú no pasas de aquí, puñetazo»); una identidad que no se puede ver en ningún número de la
  plantilla es un modificador invisible, que es justo lo que el principio rector del proyecto pone último.

## Hermanos

- `docs/decisiones/0125-entrada-sin-balon-metrica-propia-y-bono-por-puesto.md` — D1 implementada; D2/D3 no.
- `docs/decisiones/0105-*.md` — la que introdujo la entrada sin balón y la sumó a la misma banda.
- `docs/balance.md` §RT-056 — la definición nueva y el aviso de que ninguna lectura anterior es comparable.
- `docs/analisis/tanda-0-histograma-de-accion.md` — el instrumento de la verificación 3 de la ficha original.
