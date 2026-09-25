# Estado del proyecto

Editado a mano, no generado (V2/V3 de la auditoría de organización, `docs/analisis/auditoria-organizacion-v2.md`).
Se actualiza al cerrar un hito o una ADR. Contiene solo decisiones vigentes, sistemas activos,
bloqueantes y trabajo en curso — nada que ya se pueda derivar de `git log` o de `docs/decisiones/`.

PC (Steam), premium, sin online. **Estado (8 sep 2026): fases 0 y 1 cerradas; fase 2 implementada y medida —bucle de run completo, tres jefes con modificadores, economía y `--full-runs` con tres políticas automáticas—, con la curva de puertas de la ADR 0033 en verde y cuatro decisiones abiertas antes de darla por cerrada (`docs/balance/fase2-resultados.md` §7). Pantalla de Equipo funcionando en Godot; build jugable exportable a Windows desde WSL (`tools/export-windows.sh`). Primera partida del revisor sobre esa build íntegramente procesada: veinte anotaciones (AW-A..AW-T en `docs/pendientes.md`), todas cerradas — intercepción del disparo tick a tick (`docs/plan-intercepcion-disparo.md`), techo de la línea defensiva, persecución del balón como precondición dura, reposicionamiento durante el balón muerto — con cuatro ADR de banda derivadas (0081-0084) y AV-B cerrada (ADR 0085: el valor de un perk se lee al horizonte y la run no lo nota). AT-A cerrada (ADR 0086). **Paquete AY (9 sep, decisión del revisor): ningún perk es negativo** —la rama `else` no hace nada (ADR 0088), la muerte solo es consecuencia de una entrada y nunca del saque (regla del cargador), el valor de un perk se mide contra su control emparejado (ADR 0087: `rowDeviation` 19 → 7, ningún perk por debajo de −7) y las puertas de fase 1 arrancan en 50 (45-55). Medido el paquete completo: `runWinRate` 18,4 / 17,8 (la muerte en la entrada cuesta ~3 puntos; AY-B, se ajusta tras AZ) con `deathsPerRun` en banda. Segunda partida del revisor (AZ, `docs/plan-segunda-partida.md`): tandas 1 y 2 cerradas —el saque ya no «va y vuelve», solo ante el portero se tira, la falta señalada reanuda con saque de falta, el árbitro señala el 80 % (ADR 0090) y la clínica cura la leve—; tanda 3 (física del pase, ADR 0091: pase al pie, intercepción con geometría, el pasillo puntúa, pase en profundidad más hondo y el balón que se para donde cae) implementada y medida; las razas recalibradas para esa física (ADR 0092: sesgos de suma cero, abanico 21,8 → 6,4). Con las razas nuevas los tiros (8,1) y `human_random` vuelven a banda; las décimas que quedaban son bandas por ADR 0093 (`ballThirdMaxShare` ≤ 52, `betterTeamWinRate` 70-90, `grimhold_guns` incoherente 15-40, peldaño plano de 3 puntos en la escalera de jefes). Puertas en verde. Tanda 4 (AZ-F): la sustitución forzada es parte del estado inicial del partido (ADR 0094, sin motor reanudable), con ventana en `/Game` y el rival sustituyendo también. **Paquete AZ cerrado.** Con él cerrado, `runWinRate` valía 14,5 y el 92 % de las derrotas eran un jefe: la run llegaba a cada jefe **un nivel por debajo** del que mide la puerta de la ADR 0033 (fila nueva `levelAtBossActN`: 3,79 / 5,80 / 6,37 contra 5 / 6 / 7) y las muertes habían subido a 2,62 sin decisión. **ADR 0095** (`lethalChance` 1.950/1.500 → 1.200/900, `matchExperience` 100 → 140) cierra AY-B y pone `runWinRate` **en banda por primera vez: 22,17 ± 0,60** con 4.800 runs (2.400 × 2 semillas), el suelo a 3,6 errores típicos con `deathsPerRun` 1,96 / 1,85 y las 43 puertas en verde. **ADR 0096** aplica la tercera palanca de la ADR 0055 (la liga paga oro en vez de elección, la contextual no gasta un slot en un común, los sumideros suben): ganar sin pasar por el mercado cae de 18,3 a **10,7** y los maestros comprados suben de 31 % a **45 %**, sin sacar de banda nada que estuviera dentro (`runWinRate` 23,3 / 22,0). **ADR 0098** corrige el instrumento de esa métrica —el control «sin mercado» compraba cuando el mapa lo obligaba a entrar— y con ello **retira la contradicción** que la ADR 0096 anunciaba entre la ADR 0033 y la ADR 0055: no existía. La cifra real de ganar sin comprar es **7,83** y a la banda le faltan 2,8 puntos; la palanca que queda es que no comprar es en parte una estrategia (122 de oro sin gastar, la mitad de muertes, más veteranía). **ADR 0099 y 0100** (decisión del revisor, mismo paquete): la clínica pasa a tener tres servicios —por pieza, la plantilla entera a tarifa plana y el matasanos, que cobra el 40 % y puede matar con su porcentaje a la vista— y el nodo de evento deja de pagar 1-3 de oro para ser una **carta con opciones** en `data/events/`, con las familias del oro parado y de carne por ventaja. Medido: `runWinRate` 25,3 / 27,0, cartas resueltas 0,38 por run y `brokeMarketRunShare` dentro de banda en las dos semillas por primera vez. **ADR 0097** (decisión del revisor): la inscripción deja de ser un nodo y el hueco de plantilla se compra en el mercado —un tipo de nodo menos y guardado v2—, con `runWinRate` 25,3 y `brokeMarketRunShare` de 38 a 10,8, en el suelo de su banda por primera vez.

**Campo de siete filas (14 sep 2026, decisión del revisor, BA-C):** 16×7 en vez de 16×6 —con seis filas el centro geométrico cae *entre* dos filas y no existe fila central—, con guardado v4. Añadir la fila cuesta ~1,0 tiro y ~0,45 goles por partido y **ninguna palanca local lo recupera**, así que la **ADR 0109** recalibra la banda de tiros a la geometría vigente (8-16 → 7-15) en vez de tocar el motor, y deja escrito que ensanchar la formación o las zonas de acción destruye la profundidad de colocación. Nueve auditorías de la IA de jugadores (`docs/auditoria-ia-jugadores-1..9.md`) dejan **un solo cambio aplicado**, la **ADR 0110**: `Shoot` del defensa 77 → 154 y del centrocampista 188 → 237, porque con 77 un defensa colocado arriba nunca remataba —perdía contra su propio `ShortPass` de 500— y jugar fuera de posición costaba dos tercios del ataque (100/48/38 → 100/68/55). Rechazados y documentados: `ChaseBall pen` en todas las dosis (degrada la diferenciación de builds), el desfase de fase entre equipos (no replica entre semillas) y la histéresis. Queda **abierto** el papel del centrocampista: dispara el 6,5 % de los tiros siendo el 43 % de los jugadores de campo.
---

## El enfriamiento de perk, y la primera reducción de `MATCH_START` (25 sep 2026)

Ejecuta la ADR 0149. **Encargo del revisor**: *«debes reducir los perks de match start»*, tras decidir que
el catálogo de actos **sustituye** a los 102 actuales, que la economía se queda compitiendo por un slot,
que el sub-100 se permite con las cinco condiciones de la biblia §1.5, y que el **segundo balón se olvida**.

**Primitiva nueva: el enfriamiento (RF-069c).** `LimitDefinition` gana `CooldownTicks`; el dato lo declara
en **segundos** (`limit: { "cooldownSeconds": N }`) y el cargador lo pasa a ticks una sola vez;
`PerkSubscription` recuerda el tick de su última activación y la descripción generada lo dice sola
(RT-035). Es lo que permite colgar un perk de un evento **frecuente** —el pase ocurre ~100 veces por
partido— sin convertirlo en ruido.

**Primera reducción: `MATCH_START` 48 → 42.** De los 48, **18 son legítimos** —12 de conducta, 5 de estado
y 1 de economía: una disposición permanente no es un suceso y tiene que estar puesta antes— y **30 eran
número invisible**. Se han movido los seis que tenían un evento de fútbol poco frecuente donde caer:
`bulwark_stance`, `own_third_anchor` y `duelist` a **`TACKLE`**; `elf_touch` —la racial élfica— a
**`TACKLE` con scope `opponent`**, que es el instante en que le entran; `forward_line` a **`SHOT`**;
`flank_specialist` a **`DRIBBLE_ATTEMPTED`**. Todos pasan de `duration: match` a `duration: play`.

**Quedan 24 números**, y ya no están bloqueados: **16** pueden moverse al pase o a la intercepción **ahora
que el enfriamiento existe**, y **8 son constantes permanentes** (atributos y escalares de rasgo) que
alimentan la decisión o la geometría — a esas RF-069b no les pide cambiar de disparador, les pide
acompañarse de un acto.

**Cinco tests actualizados**, todos los que afirmaban «este perk está puesto en el pitido inicial», y uno
de ellos —`DescriptionTests`— falló **porque la descripción generada cambió sola** de «Al empezar el
partido» a «Al entrar»: RT-035 funcionando. 1.226 tests en verde, 241 ficheros de `/data` válidos.

**Lo que NO se ha hecho y hay que decirlo**: ninguna puerta (`Category=Gate`) ni un solo lote de
`/Balance`. El revisor aplazó el balance expresamente —*«ya lo balancearemos después»*— pero eso no es lo
mismo que no medirlo nunca. Seis perks han pasado de bono permanente a bono de jugada: es un recorte real
de su valor y está sin medir.

**Y el diagnóstico que lo ordena todo**, del mismo día: el cartelito de perk sobre la cabeza **ya existía**
(`MatchFlashView` → `MomentMark` → `DrawMarks`) y lo que falta es **qué enseñar** — entre cero y dos
activaciones por partido fuera de los primeros seis segundos, en tres de seis partidos ninguna.

---

## ADR 0149 — El catálogo se mide por lo que se ve (25 sep 2026)

**Decisión del revisor, y reescribe un requisito: `docs/requisitos.md` sube a v0.10.** Viene de tres
auditorías encadenadas del sistema de perks —[13B](./analisis/perks-auditoria-potencial-visual.md),
[13C](./analisis/perks-auditoria-situaciones.md)— y de una corrección suya que hay que dejar escrita porque
tumbó dos marcos seguidos:

> *No quiero un simulador estadístico con una pequeña capa de caos. Quiero lo contrario: un roguelike
> deportivo caótico donde las estadísticas respaldan y diferencian ese caos.*

**El hecho que lo ordena todo** *(MEDIDO, censo de los 102 ficheros de `data/perks/`)*: **70,6 % de los
perks no producen ni un suceso ni un cambio observable de conducta**; 14,7 % cambian conducta, 14,7 %
producen un suceso. **El canal de atribución, en cambio, SÍ existe** y afirmé lo contrario: `MatchFlashView` →
`MomentMark` → `MatchPitchView3D.DrawMarks` pintan un pergamino con el nombre del perk sobre la cabeza del
jugador, 1 s, apilado. Lo que no hay es **qué enseñar**: *(MEDIDO con `BroadcastCapture.FindPerkBurst`,
seis partidos)* un partido produce **entre cero y dos** activaciones fuera de los primeros seis segundos —en
tres de los seis, **cero**— porque **48 de los 102 perks se cuelgan de `MATCH_START`**. El catálogo **no incumplía RF-069:
lo cumplía** —exigía el 60 % de «modificadores numéricos condicionados»—, así que ninguna auditoría de
catálogo podía arreglarlo.

**Qué decide la ADR**: RF-069 gana un eje de **visibilidad** (Acto ≥ 50 % / Conducta ≥ 25 % / Soporte
≤ 25 %) ortogonal al de potencia (60/30/10, que se conserva), con la regla que traduce el encargo —**ningún
perk tiene efectos exclusivamente de soporte**: la estadística se pega al acto y decide si sale bien o
mal— y **enfriamiento obligatorio** para el grado Acto. Cierra **BK-A**. **No se toca ni un número ni un
fichero de `/data`**: *«no te ciñas a los números, ya lo balancearemos después»*.

**Diseño derivado, listo para ejecutar**: [`perks-de-cuota-a-acto.md`](./analisis/perks-de-cuota-a-acto.md).
Los 72 invisibles **no son 72 decisiones: son siete canales** —entrada 15, tiro 9, carnicería 8,
intercepción 7, parada/evasión 6, pase 5, regate 4— más 6 de economía que quedan exentos. Y **siete actos se
pueden escribir hoy sin una línea de C#** (caño, lectura, a bocajarro, plancha, pies de plomo, falso muerto,
árbitro sobornado), ninguno escrito.

**El hallazgo que abarata la atribución** *(MEDIDO)*: `Detail` ya es el canal de matiz del motor
(`post`/`crossbar`, `severe`/`minor`, `held`/`parried`) y **las dos tablas de presentación ya conmutan por
él** —`MatchEventSounds.PoolsFor` y `MatchMomentView.BaseClassification` con cláusulas `when detail == …`—.
Nombrar un acto es **añadir casos, no sistemas**.

**Lo que queda decidido por el revisor y lo que no**: autorizado sobreescribir RF-069 y no ceñirse a los
números. **Sin decidir**: el **segundo balón** (255 líneas de motor suponen uno solo) y si un perk de
economía debe ocupar un slot de perk.

**Riesgo anotado y sin medir**: un catálogo con ≥ 50 % de actos es un partido con más sucesos por minuto, y
la gramática de momentos tiene **cola de una plaza y caducidad de 1,5 s**. El techo de saturación es la
**primera** medición de la conversión, antes de construir nada.

---

## ADR 0148 — El enfriamiento de entrada es disputa, no física (25 sep 2026)

Primer mecanismo **aislado** de la violencia que la ADR 0147 publicó rota. Los enfriamientos de entrada
corrían durante las reanudaciones —el **24,3 %** del partido— y `OffBallTackleCooldownTicks` son 400 ticks:
se recargaban gratis. Ahora sólo corren con el balón en juego.

| | semilla 1 | semilla 7 |
|---|---|---|
| `foulsPerMatch` | 7,96 → **7,09** | 6,05 → **5,56** |
| `offBallTacklesPerMatch` | 4,62 → **3,80** | 3,57 → **3,12** |
| `injuriesPerMatch` | 0,82 → **0,78** | 0,84 → **0,79** |

**Recupera un tercio de la brecha de faltas.** El resto sigue sin explicación medida.

**La primera versión estaba mal y la revisión independiente la tumbó**, y la lección vale más que el
cambio: preguntaba `_phase == MatchPhase.OpenPlay`, que **excluye la turba** (`MobGoldenGoal`, juego real,
9,25 % de los fotogramas). Cada jugador se habría quedado con **una entrada para toda la prórroga**,
volviendo el tramo menos violento justo el que **RF-055d** convierte en la ventana de las builds de
violencia. Y el «30 % del partido es pausa» que justificaba la ADR tenía **el mismo error dentro**: contaba
la turba como pausa. El argumento y el bug eran la misma confusión. Además inventaba **una tercera forma de
preguntar si el balón está en juego** teniendo el repositorio ya dos — la pregunta 7 de `CLAUDE.md`.

**Descartado por el camino**: `Shielding` no es el canal dominante de la violencia (r = 0,008 con n=200),
después de haber sido el candidato número uno. Instrumento nuevo: `CarrierCensusTests`.

**Puertas: 4 rojas contra 6.** Salen `elf_out_of_zone` y `FullRunGateTests…NeverAllOfThem` — pero esta
última **como LIKELY, no como mérito**: la run pasa de ganarse en 8 de 16 semillas a 12 de 16, así que
puede estar volviendo a verde porque el juego es más fácil, que es el mismo mecanismo que la ADR 0147 anotó
al romperla.

**Abierta [BJ-A](./pendientes/BJ-A.md)** con lo que esta ADR no decide: los otros tres enfriamientos que el
principio implica, congelar contra tasa reducida (nunca comparadas), que la regla es **invisible para el
jugador**, y la asimetría nueva —vuelves de la pausa descansado pero incapaz de entrar—.

**Decisión del revisor anotada**: *«no te vuelvas loco con builds concretas; los perks y objetos no están
cerrados todavía»*. `orc_violence` cae 1,64 con este cambio y se registra sin convertirlo en argumento de
diseño.

---

## ARRANQUE DE LA SESIÓN SIGUIENTE (escrito el 25 sep 2026)

> **Prioridad cambiada el 25 sep por el revisor, después de escribir esta sección**: el trabajo siguiente es
> la **conversión del catálogo de perks** de la ADR 0149 —tanda A de
> [`perks-de-cuota-a-acto.md`](./analisis/perks-de-cuota-a-acto.md) §6: los siete actos escribibles hoy, y
> medir la saturación de la gramática de momentos **antes** de construir nada—. La violencia de la ADR 0147
> que describe el resto de esta sección **sigue abierta y sigue siendo válida**, pero va detrás.


**Qué hacer: la violencia del partido que dejó la ADR 0147.** Es lo que el revisor priorizó —*«primero el
balanceo total y luego los balanceos de winrate; el gameplay todavía debe evolucionar»*— y es el único
número del partido pegado a su techo.

**Qué leer, y basta con esto**: la sección de la ADR 0147 en este fichero (más abajo),
`docs/plan-balon-parado-posicional.md` §6 y `docs/decisiones/0147-*.md`. **No hace falta leer BI-D ni BI-H**:
están cerradas.

**El problema, con sus números**: el balón parado posicional dejó el partido un **68 % más violento**
—faltas 4,73 → **7,96**, entradas sin balón 2,45 → **4,62**— y `injuriesPerMatch` en **0,82 contra un techo
de 0,90**. Se publicó roto y anotado a propósito.

**Lo que ya está descartado, no lo repitas**: atribuirlo a la compresión del saque de centro es
**REJECTED** — con `kickoffPushCells` en 1,5 la violencia **no baja, sube** (faltas 8,08, lesiones 0,90
justo en el techo).

**Por dónde empezar, y no es tocando un dial**: instrumentar **`Shielding`**. Es gemelo exacto de
`Dribbling` desde la ADR 0137 —mismo `EnterState(estado, ticks)`, mismo corte por pérdida de balón— y **no
está instrumentado en absoluto**: ni ticks, ni rachas, ni por qué terminan. Es la medición barata que puede
explicar **dos** síntomas a la vez: la violencia y el hallazgo sin dueño que dejó BI-D —**hoy se decide
conducir un 44 % menos** que en el árbol de la ADR 0137 (4,1 contra 7,3 conducciones por partido, las dos
con el parámetro a 0)—. El instrumento a copiar está hecho: `DribbleMeasurementTests` ya clasifica por qué
termina una conducción.

**El mecanismo anticipado y nunca aislado** para la violencia es el **enfriamiento durante el balón
parado** (§6 de la nota de diseño), con tres salidas planteadas y ninguna decidida.

**Dos trampas medidas que te ahorrarán una conclusión falsa:**

1. Los porcentajes **«de 1.200 ticks»** están inflados un 52 %: desde la ADR 0147 un partido dura **1.825,8
   fotogramas** de motor. Un `grep` por `1200` antes de citar ningún porcentaje.
2. **No etiquetes sin el error típico delante.** Con 480 partidos por celda y ocho semillas, el ET de una
   media es ~0,8 puntos y el de un contraste pareado sale de la sd de las diferencias por semilla. El 25 sep
   esto tumbó tres de cuatro conclusiones en la revisión independiente.

**Lo que NO toca ahora**: las puertas de winrate (`elf_out_of_zone`, CAT-J, `orc_violence`) están
**aparcadas por decisión del revisor** hasta que el gameplay deje de moverse.

**Y dos restos baratos**, por si sobra hito: el barrido del denominador `1200`, y `BI-G`
(`tacklesPerMatch` cuenta entradas **intentadas** y se usa como métrica primaria de los perks de entrada,
que sólo deciden las **ganadas**).

---

## GAMEPLAY AI FOUNDATIONS PASS — COMPLETO (24 sep 2026)

**Encargo del revisor.** Plan: `docs/plan-gameplay-ai-foundations.md`. **Informe completo:
`docs/informe-gameplay-ai-foundations.md`** — léelo antes que esta sección si vas a seguir.

Nueve ADR (**0138-0146**), ocho commits, **1.222 tests en verde** y 241 ficheros de `/data` válidos.
**No se ha balanceado nada**, que es lo que el encargo manda: los lotes sólo se usaron como *smoke*.

Qué tiene el motor que antes no tenía: percepción compartida del equipo · proteger y despejar · arranque
coordinado · la altura del balón significando algo, con duelo aéreo, cabezazo y segunda jugada · marcador,
minuto y **orden táctica del jugador** · un portero que decide (tres finales de parada, el receptor
importa, y puede salir) · el cansancio como recurso · el balón parado como jugada con decisión · el censo
de utilidad como instrumento · represalia · **la turba sin árbitro** (RF-055d, prometido desde siempre) ·
y `modifyUtility` alcanzable desde `/data`.

**De quince acciones a diecisiete**, y sólo dos: `Shield` y `Clear`. Todo lo demás salió de contexto,
percepción y utilidad, que es la regla 17 del encargo.

### Lo que la fase siguiente tiene que mirar primero

- **Los tiros han bajado**: `shotsPerMatch` 7,17 (semilla 1) y **5,66** (semilla 7) contra una banda 7-15;
  goles 1,89 / 1,32 contra ~2,4 de antes. Tres causas a la vez, todas queridas.
- **La curva de jefes de la ADR 0033 se cae entera** (`grimhold_guns` 58,1 / 68,3 / 81,4). Es la puerta
  nueva y la que más dice: con menos goles, la economía de la run se desplaza. **Primer sitio al que
  mirar.**
- 43 puertas: **39 verdes, 4 rojas** (HEAD traía 3). Las otras tres ya estaban y tienen ficha (BF-A, BF-B).
- El portero recupera mucho más (37 → 123 en veinte partidos, medido).

### SIGUIENTE PASO CONCRETO (sesión limpia)

**Gameplay AI Balance & Measurement Pass**, que es la fase que el encargo dejó explícitamente separada.
Leer `docs/informe-gameplay-ai-foundations.md` §8 y §9, y la sección de la curva de jefes. El instrumento
nuevo es **`UtilityCensus`** (`SimConfig.Census`): da frecuencia por acción, por puesto y **por motivo de
descarte**, que es la fotografía que el bloque 22 del encargo pide y que las nueve auditorías de IA echaron
de menos.

**No empezar por subir pesos.** El censo distingue «se descarta» de «compite y pierde», y son arreglos
opuestos.

Pendiente aparte, y es `/Game`: **el selector de orden táctica** (sin él la decisión no existe para el
jugador) y pintar el estado `Shielding`. Va en su propio commit — `/Sim` y `/Game` no se mezclan.


## **FASE 1 (memoria) CERRADA** (22 sep 2026)

Las tres memorias construidas **y visibles**, que era la condición que faltaba (*no se construye memoria
que nadie vea*):

- **Carrera del jugador** — sección `CARRERA` en la ficha: «14 partidos · 3 goles · 9 entradas ganadas ·
  2 lesiones causadas · 1 muerte causada». No aparece con `Matches == 0`.
- **Reencuentro con un clan** — en el ojeo: «Yunque Verde · 2.ª vez · ganaste».
- **Par knaveador-víctima** — debajo: «tu Balder Cavaprofundo ha lesionado a su Grok Comecráneos».

Las dos pantallas quedan **enganchadas a la secuencia de capturas** (`--screenshots` y `--tour-rivalry`),
así que se regresionan solas. Capturas en `Game/screenshots/`: `equipo-ficha.png`, `ojeo.png` (primer
encuentro) y `ojeo-reencuentro.png`.

**Medido cinco veces: 43 puertas idénticas a la línea base de HEAD** (`elf_brawler` 48,54 ·
`elf_out_of_zone` 46,04 · `injuries` 1,30), en worktree limpio. Todo el paquete es contabilidad y lectura:
no desplaza ni una tirada.

**Limitación de diseño anotada, no descubierta tarde:** `rivalCredit:` es un **contador, no una bitácora**.
Sabe cuántas veces pasó algo, no cuándo. Fue una decisión de la ADR 0124 (claves libres para no subir
esquema) y basta para F1; **una crónica ordenada en F2 necesitaría otra estructura**.

## ADR 0125 D1 hecha: la entrada sin balón ya tiene métrica propia (22 sep 2026)

`tacklesPerMatch` cuenta desde hoy **solo la entrada al portador** y la entrada al marcado sin balón sale
aparte en **`offBallTacklesPerMatch`** (INFO, sin banda). El motor no cambia: `MatchEngine` ya distinguía
las dos ramas, solo dejaban de distinguirse al agregar. Separado en el informe, por jugador y en
`matches.csv`/`players.csv`/`builds.csv`. **La carrera del jugador NO se separa**: sigue sumando las dos,
porque estrechar lo que una run recuerda (RF-122) es diseño, no instrumento.

**Medido (500 partidos, semilla 1, contra línea base tomada en HEAD limpio el mismo día):**

| | `tacklesPerMatch` | `offBallTacklesPerMatch` | `injuriesPerMatch` | `foulsPerMatch` |
|---|---|---|---|---|
| base (mezcladas) | 12,01 | — | 0,81 | 7,50 |
| separadas | **6,92** | **5,09** | 0,81 | 7,50 |
| `tackleMarkTargetBonus` 0 | 7,78 | 0,98 | 0,70 | 4,87 |

Las otras 24 filas de `summary.csv` **idénticas**, las 43 puertas reproducen la línea base **exactamente**
(`elf_brawler` 48,54 · `elf_out_of_zone` 46,04 · `injuries` 1,30) y, partido a partido, las 500 filas de
`matches.csv` coinciden columna a columna: no desplaza ni una tirada.

**Las cifras de esa tabla son de la semilla 1, que es el extremo.** Con cinco semillas (la semilla genera
también las plantillas) `offBallTacklesPerMatch` recorre **0,99-5,09**, `tacklesPerMatch` **6,92-10,42** e
`injuriesPerMatch` **0,43-0,81**. Dos consecuencias para D2: la banda de la métrica nueva se fija con
varias semillas (precedente ADR 0082, tres), y separar **casi dobla la dispersión** de `tacklesPerMatch`
entre plantillas (rango 2,02 → 3,50 sobre una banda de anchura 8).

**Mientras D2 no llegue, el proyecto supervisa menos violencia que antes**: ~5 sucesos por partido (semilla
1) pasan de una métrica con banda IN/OUT a una `INFO` sin banda y fuera del power-check del screening. Es
lo correcto —la banda de fútbol no debe juzgar un puñetazo— pero el único raíl que queda es
`injuriesPerMatch`.

**Tres cosas que esta sesión midió y que cambian lo que se creía** (detalle y etiquetas de evidencia en
`docs/pendientes/BE-A.md`, enmienda en la propia ADR 0125):

1. **La previsión de ~9,19 era falsa**: el valor separado es **6,92**. El 9,19 era otro experimento (la
   métrica mezclada con la entrada sin balón apagada) y hoy ese mismo experimento da 7,78.
2. **El margen de `tacklesPerMatch` está por el suelo, no por el techo.** 6,92 con suelo 6,00, y la entrada
   sin balón **sustituye** disputas además de sumarse. El «riesgo de techo» de la ADR 0125 se razonó con el
   instrumento mezclado.
3. **`bono 0` no desactiva la entrada sin balón**: con `tackleMarkTargetBonus` en 0 quedan 0,98 por partido.
   La premisa de D3 («un puesto con bono 0 no entra nunca») **exige una descalificación explícita** en el
   código; no sale sola del dato.

Además, con el instrumento corregido el defensa y el centrocampista **disputan el balón casi lo mismo**
(0,67 vs 0,64 por partido-jugador): el 65,3 % de las «entradas» del defensa no eran disputas.

## ADR 0125 D2/D3: la palanca está montada, la calibración es decisión del revisor (22 sep 2026)

`tackleMarkTargetBonus` es ya un **mapa por puesto con signo**, la guarda `IsDefensiveRole` **no existe**
—quién entra sin balón lo decide el dato— y **0 significa «ese puesto no entra nunca»**, comprobado en el
código —con un alcance exacto: **la decisión**, no el motor entero, porque `RepeatTackle` puede fabricar
una entrada sin balón sin mirar el mapa (agujero conocido, **sin evidencia de activación**, con test
propio)—. Publicado con `Defender: 150 · Midfielder: 0 · Forward: 0`: el juego es **byte a byte el de
antes** (comparado columna a columna en dos semillas), las 43 puertas reproducen la línea base exacta y
**1.110 tests** pasan. El cargador rechaza con error explícito los siete modos de dato malo del mapa,
incluidos los dos silenciosos que encontró la revisión —clave numérica (`"3"` era `Forward`) y clave
repetida (apagaba el único puesto activo sin un solo error)—, y el reparto publicado está fijado por test.

**Por qué no se ha abierto de verdad**: se midieron **siete configuraciones** y todas las que consiguen el
reparto pedido rompen algo. El orden correcto (DEF 1,21 · MID 0,23 · FWD 0,14) deja `tacklesPerMatch` en
**6,26 sobre un suelo de 6,00**; arreglarlo requiere separar el contador de enfriamiento —que además
descubre un problema real, ver abajo— y eso cuesta **dos puertas más**. Y el patrón que comparten todas:
repartir la violencia entre los tres puestos **aplana la diferenciación de builds**
(`buildsWinDifferently_injuries` 1,30 → 1,21 → 1,05), que es `especialización significativa` tirando en
contra de la propia ADR. Ningún valor del mapa lo evita.

**Dos hallazgos nuevos, medidos** (detalle en `docs/pendientes/BE-A.md`):

1. **Por qué salía el orden invertido**, que la ADR 0125 midió sin explicar: sin balón, la entrada de un
   delantero ya gana a sus propias alternativas (211 contra 180) y la de un defensa pierde por 180 contra
   la suya. El delantero no necesita bono, necesita que se le **reste** — de ahí que el mapa lleve signo.
2. **El enfriamiento de la entrada es un contador único**: pegar sin balón deja al jugador sin poder
   **disputar** el balón durante 12-36 s. Es el mismo caso que el paquete U ya resolvió para el bloqueo.
   Separarlos sube `tacklesPerMatch` de 6,92 a 7,86 sin tocar un número, **pero hunde la build violenta**
   (`orc_violence` 54,17 contra un mínimo de 58). Problema propio, con precedente propio: merece su ADR.

## ADR 0129 — cada entrada paga su propio enfriamiento (22 sep 2026, Opción 2 del revisor)

`MatchPlayer.TackleCooldown` era **un contador único** para dos acciones distintas: pegarle a quien no
lleva el balón dejaba al jugador **sin poder disputarlo** 12 segundos. Es el mismo defecto que el paquete U
ya arregló para el bloqueo (ADR 0030 §2), aplicado al caso que se quedó fuera.

Separado, y con los dos enfriamientos absorbiendo lo que el arreglo libera (`TackleCooldownTicks` 60 → 90,
`OffBallTackleCooldownTicks` 180 → 280): `tacklesPerMatch` **6,92 → 7,65**, faltas 7,54 (antes 7,50),
lesiones **0,79** (antes 0,81), entradas sin balón 4,55. Las 43 puertas siguen en 3 rojas y
`elf_out_of_zone` pasa a **verde**; `coherentBuildsBeatNone_orc_violence`, que el cambio rompía sin
compensar, vuelve a banda.

**Lo que empeora y no se arregla ahí**: `buildsWinDifferently_injuries` 1,30 → 1,14 (umbral 1,40, ya roja).
Si el contacto es más accesible para todos, distingue menos. **No se recalibra el umbral**: es exactamente
lo que la Opción 3 debe recuperar moviendo la diferenciación al canal de rasgo, y bajar la puerta antes de
medirlo sería ajustarla al resultado.

## BE-A CERRADA: el centrocampista entra a su marcado (23 sep 2026)

**ADR 0133.** `tackleMarkTargetBonus` queda en `Defender 150 · Midfielder −40 · Forward 0` con el
enfriamiento sin balón en 400. El centrocampista entra **con ajuste negativo**, que es la única forma: su
alternativa le gana por 104 puntos, así que cualquier positivo lo **satura** (medido con 40: la misma tasa
que el defensa y las lesiones en 1,07 sobre un techo de 0,90). Ni una línea de `/Sim`: el mecanismo ya
estaba, faltaba el número.

**El defensa va por delante en las cinco plantillas medidas** (DEF 0,24-0,91 · MID 0,09-0,26 entradas sin
balón por partido-jugador), con `injuriesPerMatch` entre 0,47 y 0,81 y ninguna métrica obligatoria cambiando
de estado. En las puertas de build con el instrumento de ocho de la ADR 0131: `orc_violence` y `orc_mob`
**mejoran**, `elf_brawler` mejora, `elf_out_of_zone` empeora 0,8 sobre un error típico de 1,09. Lo que
parecía aplanamiento de identidad de build era **ruido de una semilla**.

**Se pudo cerrar ahora por dos cosas que no eran de BE-A**: la **ADR 0129** (cada entrada paga su propio
enfriamiento) dio el margen de `tacklesPerMatch`, y la **ADR 0131** (las puertas de build promedian ocho
plantillas) deshizo el falso aplanamiento.

**ADR 0132, de propina y con el mismo origen**: `IsInActivePlay` era pura geometría y no sabía si el balón
estaba en juego, así que durante una reanudación al que iba a sacar **le entraban y le cargaban** mientras
esperaba (RF-057 lo prohíbe: con el balón muerto no hay jugada activa). Arreglado donde vive la regla, así
que acota a la vez la entrada sin balón y el bloqueo. Devuelve 0,04 de presupuesto de lesión.

**Lo que queda abierto**: [BF-C](pendientes/BF-C.md) — el delantero sigue en 0 porque pega sin balón por un
hueco, no por identidad: su `Tackle` (211) ya gana a su `MarkOpponent` (180). Abrirlo cuesta 0,07 de
lesiones por un defecto. Y [BF-A](pendientes/BF-A.md), el único rojo verdadero de las puertas.

## La Opción 3 se implementó, se midió y se descartó (22 sep 2026)

El ajuste por puesto pasando por `ActionMultiplier(Tackle)`, con el valor publicado bajando a 110, dejaba
**las 43 puertas en verde** en la semilla 1. **No se ha commiteado**, y el motivo está medido en
`docs/pendientes/BE-A.md`:

- **la premisa era falsa**: el carácter ya diferenciaba (elfo 0,087 · humano 1,063 · orco **2,476**
  entradas sin balón por defensa-partido, *antes* del cambio: 28 a 1), porque el multiplicador de rasgo ya
  multiplica el `Tackle` base;
- **todo el efecto cabía en 29 sucesos**: los defensas elfos pasaban de 29 entradas sin balón a **0** y el
  resto se movía ~1 %. No era diferenciar por carácter, era apagar una raza;
- **las 43 verdes eran suerte de semilla**: con las 8 bases de `CatJSeedDispersionTests`,
  `buildsWinDifferently_injuries` da media 1,29 antes y 1,29 después, y sigue fallando en 5 de 8;
- **rompía la invariante de la ADR 0105 §3**: agresivo + sucio acumula ×200 y 110 × 200 / 100 = 220, por
  encima de `tackleBallCarrierBonus` (195). El cargador valida el valor publicado, no el efectivo;
- y el repositorio **ya tenía la forma legible** para lo mismo: `BlockAggressiveBonus` /
  `BlockBruteTagBonus`, bonos con nombre en `/data` condicionados a un rasgo.

**Aviso que arrastra a la ADR 0129**: sus mejoras de puerta (`elf_out_of_zone` a verde, `orc_violence`
recuperada) también están medidas **en la semilla 1**. La dispersión entre plantillas de estas métricas de
build es grande (sd 0,18-0,23 en un umbral de 1,40); cualquier lectura futura de la curva de puertas de
build debería usar tres semillas o las ocho de CAT-J, no una.

## ADR 0136 — «Centrar» está dentro, y los tiros desde la línea de fondo se quedan en la mitad (23 sep 2026)

Paso **2c** de la ADR 0135, intercalado por decisión del revisor **antes** del paso 3 y con motivo medido:
el ángulo en la utilidad de `Shoot` es la vía (B) de [BA-E](pendientes/BA-E.md) y sola pone seis puertas en
rojo, porque le quita el tiro al delantero **sin darle nada a cambio**.

**Qué es**: un pase **alto** al área a un compañero con mejor apertura a portería que el pasador, que lo
**remata de primeras sin controlarlo**. El remate pasa por `LaunchShot` con los mismos términos y los
factores **invertidos** (fuerza 18 / técnica 4, contra 4 / 14 del tiro): el tiro es *colocar* y el remate es
*llegar y empujarla*. Y la intercepción del pase pasa de círculo en el plano a **esfera**, que es lo que
hace que el centro exista: a mitad de vuelo el balón va por encima del radio.

**Medido, 10.000 partidos × 2 semillas:**

| | baseline | con el centro |
|---|---|---|
| centros por partido | 0 | **2,13 / 2,33** |
| tiros sin ángulo (apertura < 0,5) | 25,77 / 29,00 % | **14,31 / 14,86 %** |
| tiros desde la línea de fondo | 23,30 / 26,53 % | **13,04 / 13,43 %** |
| apertura media | 69,11 / 65,59 | **78,52 / 77,35** |
| goles por partido | 2,73 / 2,35 | 2,46 / 2,07 |

Es del mismo orden que la palanca **(A)** de BA-E, que la ADR 0111 rechazó por poner **ocho** puertas en
rojo. Ésta no pone ninguna: **41 de 43**, y las dos rojas son `orc_misplaced` 45,05 (BF-B) y `elf_brawler`
47,01 (BF-A), en valores indistinguibles de los que sus fichas documentan.

**El experimento salió separable, y eso es la mitad del valor del paquete.** Con el centro apagado por dato
(`crossTargetGoalDistanceCells = 0`) el árbol es **byte a byte HEAD**: 0 de 10.000 partidos difieren. Ocurre
porque los dos defectos latentes que se arreglaron por el camino **se cancelan por construcción**. Así que
todo lo medido arriba es del centro y de nada más, sin argumentarlo.

**Dos defectos latentes encontrados y arreglados en la causa:**
1. `FlightArc`/`FlightTargetZ` los escribía sólo `LaunchShot` y nadie los limpiaba, así que **el primer pase
   después de una parada heredaba la comba del tiro parado**. Inerte mientras nada leía `_ball.Z`; con la
   esfera habría dado pases rasos **ininterceptables**.
2. `MatchPlayer.ActionCount` estaba clavado a `(int)PlayerAction.Block + 1`, o sea «la última acción más
   uno» — una trampa que salta justo al hacer lo que el propio enum manda (añadir al final, RT-097).
   Añadir `Cross` reventó 234 tests con `IndexOutOfRange`.

**Y un test replanteado, no regenerado**: `RecoveryExtraActionTests` fijaba una **huella exacta del flujo de
RNG** y llevaba cinco regeneraciones, tres el mismo día. Su propio comentario decía que a la cuarta tocaba
replantearlo. Ahora afirma la **regla** —sin perks nadie entra dos veces en el mismo tick— y bandas, no la
huella.

**Lo que queda abierto y es lo primero de la próxima sesión:**
- **−0,27 goles por partido**, coste real y consistente. **Aparcado por decisión del revisor**: «ya
  miraremos más adelante si la precisión debe ponderar más».
- **El paso 3 de la ADR 0135** (ángulo y oclusión en la puntería *y* en la utilidad de `Shoot`, portero
  después). Ahora sí tiene alternativa que ofrecer. **Hay que REMEDIR la vía (B)**, no dar por buenas sus
  seis puertas rojas de cuando se midió sola.
- `crossMarkedTargetPenalty` se publica en **0** a propósito (el precio de rematar marcado ya lo cobra
  `shot.pressurePenalty` en la resolución); queda como palanca si algún día interesa.


## Siguiente paso concreto (sesión limpia, 23 sep 2026)

**El paso 3 de la [ADR 0135](decisiones/0135-el-balon-tiene-altura.md), «la portería disponible»**, que es
lo que el centro acaba de desbloquear. Por dónde empezar: `docs/plan-altura-del-balon.md` §6 (el paso 3
ampliado) y §7 (por qué el orden importaba), y la enmienda 2 de la ADR 0135. **Orden obligatorio dentro del
paso: ángulo y oclusión primero, portero después** —el portero introduce un bucle de realimentación y
hundiría `saveRate`—. Y lo primero que hay que hacer es **remedir la vía (B)** con el centro dentro: sus
seis puertas rojas son de cuando se midió sola, sin alternativa que ofrecerle al delantero.

Lleva `game-design-review` propio (lo pide la enmienda 2) porque toca la IA de utilidad, y hay que vigilar
`shotsPerMatch`, `passChainAvgLength`, `possessionChanges` y **sobre todo las puertas de build**: la ADR
0110 calibró los pesos de `Shoot` del defensa y del centrocampista.

### Después del paso 3

**[BF-C](pendientes/BF-C.md): qué hace un delantero cuando su equipo no tiene el balón.** Es lo único que
queda para poder abrirle la entrada al marcado, y no es calibración: hoy su mejor acción fuera de posesión
es pegar. La medida barata que discrimina ya existe (`ActionHistogramTests`, el histograma de acción por
puesto). Después, [BF-A](pendientes/BF-A.md): `elf_brawler`, una build mala a propósito, gana el 46,6 %
contra su referencia.

### Contexto anterior, por si hace falta

Antes de tocar la entrada sin balón otra vez: **decidir si hace falta**. Con 28 a 1 entre orco y elfo, la
diferenciación por carácter ya existe; la pregunta abierta es si se quiere **más**, y si la forma debe ser
un bono con nombre al estilo `BlockAggressiveBonus` —legible en la ficha del jugador— en vez de un
multiplicador oculto. Eso es `game-design-review`, no calibración. Y lo que falta para abrir el mapa al
centrocampista y al delantero sigue siendo darles algo mejor que hacer sin balón, más un enfriamiento
**por puesto** si se quiere que el defensa pegue más que ellos.

Contexto: `docs/pendientes/BE-A.md` (las tres opciones con sus números), las **dos enmiendas** al final de
la ADR 0125 y la **ADR 0129**. Los CSV de las configuraciones medidas están en `out/D2-*`.

**Decisiones del revisor pendientes de ejecutar** (ADR escritas, nada implementado): **0126** (clanes
canónicos: ~110-120 jugadores escritos), **0127** (elenco de fichajes por raza: ~300-325), **0128**
(legendario como premio, **bloqueada** por el perfil entre runs, que no existe). La **0125 está
implementada** (D1 métrica separada, D2/D3 mapa por puesto y guarda por dato) y la **0129** también. Las
dos primeras tocan balance y van con lote.

**Línea base de puertas vigente**: `elf_brawler` 48,54 · `elf_out_of_zone` 46,04 · `injuries` 1,30.
Medida cinco veces idéntica el 22 sep en worktree limpio, la última ya con la ADR 0125 D1 dentro.
**Cualquier paquete de contabilidad debe reproducirla exactamente**; si se mueve, ha desplazado el consumo
de RNG.

**Línea base de `/Balance` vigente** (500 partidos, semilla 1, `reference.json`): `tacklesPerMatch` **6,92**
· `offBallTacklesPerMatch` **5,09** · `injuriesPerMatch` 0,81 · `foulsPerMatch` 7,50. **Ninguna lectura de
`tacklesPerMatch` anterior al 22 sep 2026 es comparable con esta**: mezclaba dos poblaciones.

## F1 (memoria) — mitad de `/Sim` CERRADA (22 sep 2026)

**ADR 0124** y su enmienda. `RunPlayer.Career` (registro tipado, vocabulario cerrado que nunca topa ni se
reinicia, a diferencia de `Counters`), guardado **v4 → v5**, atribución de lesión y de muerte con `Kill`
recibiendo al matador **sin valor por defecto**, `PlayerDeathDetail.KillerPlayerId` —que **puede ser un
jugador del rival**, el caso que da nombre al juego—, `RivalHistory` como lectura pura sobre `RunState`
(cero esquema) y los contadores nuevos saliendo por `players.csv`.

**Medido: las 43 puertas idénticas a la línea base de HEAD en los tres lotes** (48,54 / 46,04 / 1,30). El
cambio es contabilidad pura y no desplaza ni una tirada. 1.080 tests, RT-024 incluido.

La **revisión independiente encontró cuatro fallos reales** que se corrigieron antes de commitear; el más
grave era que el matador **rival** no se persistía en ningún sitio, así que la ADR no cumplía su propio
título. También faltaba `game-design-review` (Regla B): tres reglas de juego quedan decididas en la
enmienda, y una de ellas importa porque **RF-125 pone un umbral de 30 lesiones por run**.

**BE-B cerrada (22 sep)**: los pares (causante, víctima) se guardan en contadores de clave libre
—`rivalCredit:<clan>:<índice>:<propio>:<hecho>`—, sin subir esquema y en las dos direcciones. La revisión
añadió la exclusión explícita del nodo de jefe, que se descartaba por accidente de los rangos de id.

**`clanId` queda absorbido por la ADR 0126**, que va más allá: clanes canónicos con plantilla escrita.

Fichas nuevas de la revisión: `BE-B` (`injure` con `target: actor`), `BE-C` (`MatchResolution` y
`defeatTick`), `BE-D` (RF-125 subcontable).

## Plan de evolución vigente (21 sep 2026): `docs/plan-evolucion-knavall.md`

Ocho fases ordenadas por impacto/coste, derivadas de la ADR 0123 y la 0122. **F1 memoria** (historial de
jugador + `RivalHistory` de lectura pura + `clanId`) y **F2 atribución** (`SAVE`, cartel de perk con su
efecto, nombres de rivales, crónica conectada, dorsales estables) son las de mayor impacto y **ninguna de
las seis decisiones abiertas las bloquea**: ese es el argumento para empezar por ahí. Siguen **F3
intención** (abrir `modifyUtility`, rasgos nuevos en `/data`; único riesgo alto de la primera mitad: es un
recalibrado y `injuriesPerMatch` está en 0,81 con techo 0,90), **F4 verdad** (la pasada sistemática sobre
los nueve casos de divergencia declarado/implementado), **F5 espectáculo** (el hueco real es el momento de
**remontada**) y **F6 re-skin**, adelantable. **F7 turba** y **F8 memoria entre runs** van al final y están
*gated*.

Regla dura de F1: **no se construye memoria que nadie vea** — cada pieza sale con su superficie mínima.
Criterio de F1: las 43 puertas **idénticas** a la línea base; si se mueven, ha tocado el consumo de RNG.

## Dirección vigente: **ADR 0123 — Knavall: Rise to Rule** (21 sep 2026)

ADR paraguas y documento de dirección. Separa **decisiones de producto** (nombre y lenguaje del mundo —
*to knav* / *knaved* / *Knavall* —, premisa del torneo que otorga el derecho a gobernar, bucle de partido
automático de ~90 s, `SIMULACIÓN → EVENTOS → PRESENTATION DIRECTOR`, comentarista de texto con voz
selectiva ES/EN, meta que desbloquea posibilidades y nunca estadísticas) de **aspiraciones** con su señal
de verificación. **El renombrado a Knavall está decidido y NO ejecutado**: es mecánico (1.691 apariciones
en 411 ficheros; las 23 de `/data` son títulos de esquema, ningún id), cabe en un commit aislado.

Corrige el estado del que partía el plan propuesto: el `PresentationDirector` **ya existe** y la ADR 0119
ya separó simulación y presentación (`MatchScreen` es hoy el modo depuración, F3; la pantalla real es
`BroadcastScreen`); el catálogo son **102** perks, no 94; y la run completa ya existe y está medida, así
que lo que falta del vertical slice **no son partidos sino el rival recurrente** — que cuesta cero
cambios de esquema. Falta el momento de **remontada**, que no existe.

Seis decisiones pendientes del revisor listadas al final de la ADR.

## Auditoría de identidad (20 sep 2026) — ¿es el juego un generador de historias?

`docs/analisis/auditoria-identidad-generador-de-historias.md`. Auditoría de **producto**, no de balance:
seis auditorías en paralelo más un lote propio de 500 partidos. **No se ha cambiado ni una línea del juego.**

**Tesis, con tres medidas que convergen: el juego ya produce los sucesos; lo que no produce es atribución.**
El **67,8 %** de los partidos tiene un suceso excepcional (lesión 53 %, turba 27,6 %, roja 12 %,
incomparecencia 4 %) — la oferta no es el cuello de botella. Pero **14 de los 26 `EventType` no llegan nunca
al jugador** (pase, entrada, recuperación, intercepción y **parada** incluidos), el cartel de perk lleva el
nombre y nunca el efecto (0,9 por partido, 65 % de partidos sin ninguno), y **`RunPlayer` no persiste ni un
hecho** de lo que el motor ya calcula en `PlayerMatchStats`. **Corolario: no añadir sucesos raros nuevos
hasta que los existentes se lean.**

**Dos piezas construidas y desconectadas.** (1) `modifyUtility` (C1) está en el motor, pilotada y medida al
24 % con RT-056 en verde, y **no está en el enum de `perks.schema.json`**: ningún dato puede usarla.
(2) `MatchLogView` (crónica RF-121) existe y no se usa en `ReportScreen`.

**Hallazgos nuevos del patrón «el texto promete lo que el código no hace»** (eran cinco, esta auditoría
añade nueve): **la turba no existe** —`CheckEndConditions:3102` hace cuatro cosas y ningún cambio de regla,
en el 27,6 % de los partidos—; **el portero no puede salir del área** (`GoalkeeperLeftArea` nunca puede ser
`true`, el rasgo `Rusher` no puede cumplir su nombre); **`kamikaze` e `iron_price` son ventaja pura vendida
como sacrificio** (CONFIRMED en `ResolveInjury:2545`: `injuryChanceBonus` es lo que le hacen a la víctima,
y no existe término de autolesión — afecta a PD-1, que se apoya en ellos); `numb` concede inmunidad al luto
(RF-104 sin implementar); prótesis y vínculos no existen; `AERIAL_DUEL` nunca se emite; 13 plantillas de
acontecimiento sin versión `en` (ADR 0009).

**Los dos primeros pasos recomendados**: persistir `PlayerMatchStats` en `RunPlayer` (desbloquea RF-122,
hoy imposible aunque se implemente) y meter `modifyUtility` en el esquema con un perk que lo use.

**Cinco preguntas para el revisor** en §20, y la primera es la de fondo: **`CLAUDE.md` dice que el desgaste
es el recurso central de la *run* y `economy.json` lo implementa por *acto*** (`healsRoster` en el jefe, con
`_doc` que lo declara deliberado). Las dos no pueden ser verdad.

Ficha nueva: `docs/pendientes/BE-A.md` (el centrocampista nunca entra a su marcado sin balón).

**Decisión del revisor sobre la auditoría (20 sep 2026): ADR 0122.** «Underleague no necesita más
emergencias, ni más sinergias, ni más contenido: necesita **convertir su simulación en memoria**.» Tres
fases y no se empieza una sin la anterior: **Fase 1** (1A historial individual persistente + 1B **un**
perk conductual con `modifyUtility`) → **Fase 2** (que el partido cuente lo que ya ocurre: `SAVE`,
`TACKLE`, `RECOVERY`, crónica, cartel de perk con su efecto, dorsales) → **Fase 3** (turba, prótesis,
nemesis). **No se toca el catálogo de 102 perks.** El criterio de aceptación es conductual, no de balance:
*«¿puedo reconocer en el campo qué tipo de jugador es?»* y *«¿me estoy acordando de mis jugadores?»*.

**SIGUIENTE PASO CONCRETO (sesión limpia): Fase 1A.** Leer `docs/plan-fase-memoria-y-atribucion.md`
entero y `docs/decisiones/0122-*.md`. Empezar por `architecture-review` sobre la única decisión de
frontera abierta: si el historial va en un registro nuevo de `RunPlayer` o reutiliza `Counters` (el plan
argumenta que reutilizarlo lo haría rehén de las reglas de perk). Dato que el plan ya verificó y que
cambia el tamaño del trabajo: de los seis campos que pide el revisor, **lesiones causadas y muertes
causadas no las atribuye el motor hoy** — `Kill(victim, detail)` no recibe al matador, y su única llamada
está dentro de `ResolveInjury`, que sí conoce al `tackler`. Sube `CurrentSchemaVersion` 4 → 5. Criterio
duro: **1A no debe mover ninguna de las 43 puertas**; si las mueve, ha tocado el consumo de RNG.

## Trabajo en curso (19 sep 2026)

**UI del partido: fase E hecha (19 sep 2026) — la pantalla de Partido es ya la retransmisión con voz de
pregón**, con marcadores de posición (`docs/ui/README.md` §11). **ADR 0119** (arquitectura): momentos, niveles,
pausa y política de velocidad son una vista pura de `/Sim` (`MatchMomentView`, 17 tests, revisada por
`independent-reviewer`: corregida la muerte por reincidencia partida en dos momentos, CONFIRMED 37/44); el ritmo
real vive en `/Game` (`PresentationDirector`); el residuo sale del estado, no del director. **ADR 0120**
(decisión del revisor): registra lo que la dirección contradice —RA-021/025, RF-050/115/116, RA-020/022, ADR
0112/0114, UI-010/011/013, log bajo el campo— con nota en cada requisito. F3 alterna con el modo depuración (la
`MatchScreen` de siempre). Verificado con capturas de partidos reales (`retrans-*.png`, bandeja con sustitución
real incluida); el encuadre coincide al píxel con la captura de referencia. **Pendiente**: C13 (nivel de un
suceso anulado) y P2 (visibilidad de la build) a `game-design-review`; gestos de cámara; 16:9; capturas de roja y
muerte (no aparecen en 12 semillas). **Siguiente paso de UI: partida del revisor sobre la retransmisión** (build
de Windows, `tools/export-windows.sh`) para validar ritmo y legibilidad a x1/x4/x16 antes de tocar tiempos.

**Anotaciones de la partida del 19 sep: serie BC en `docs/pendientes/`.** Resueltas BC-B (el límite de usos
de un perk se consume al activarse: `double_shot` y `charge` se encadenaban hasta 5 veces), BC-E (la
sustitución elegida en `/Game` fallaba o se perdía si el rival había sustituido antes) y BC-F (los dorsales
se renumeraban al sustituir); revisadas por `independent-reviewer`, 43 puertas idénticas a la línea base.
**Abiertas y pendientes de `game-design-review`:** BC-A (goleador en campo rival al sacar, hermano de BB-C),
BC-C (`double_shot` no cambia el resultado), BC-D (`last_man` no cambia la jugada), BC-G (balón suelto en el
córner, reabre BB-G) y BC-H (el aviso de alineación incompleta es falso). La elección de sustituto SÍ cambia
el partido (BB-F ampliada); la ventana sigue sin decir la posición del que sale.

**Protocolo de balanceo automatizado: fase metodológica CERRADA.** `docs/analisis/protocolo-balanceo-automatizado.md`
(§17–§34). El cribado corrió sobre el catálogo entero y su veredicto es que **ningún perk tiene hoy una
señal de tuning real y accionable**: 19 `INSUFFICIENT_EVIDENCE`, 4 `SCREENING_PASS` vacíos (pasan por no
tener parámetro numérico) y 1 `NEEDS_TUNING` falso (`high_line`, medido con una métrica que va al revés).
El lote de 24 sigue detenido en 5/24 por su propio circuito de seguridad, que **no se ha tocado**.
Instrumentos nuevos: `TerritorialBalance` (métrica territorial con dirección, sin banda y sin cablear a
nada) y `DirectionCheck`. Informe de decisión por perk: `docs/analisis/informe-decision-catalogo.md`.

**Catálogo cuadrado por raza y rasgo (decisión del revisor).** 102 perks. Las cinco razas con **2 perks
opcionales** cada una (la habilidad racial no cuenta); `Brute`, `Fine`, `Cold` y `Neutral` con **3**.
`Bulwark` se queda en **2**: el tercero rompe la puerta de economía (BB-U). Regla nueva del revisor, ya
auditada sobre los 102: **un perk no puede tener dos condiciones de raza o rasgo** — `unlikely_bulwark`
era el único infractor y se sustituyó por `duelist`. `Dwarf`, `Elf` y `Undead` reciben un 5 % de `Neutral`
(tenían 0 %) para que los perks de ese estilo no fueran inalcanzables.

**La descripción de un perk se lee en cuatro secciones** (RT-035, decisión del revisor): qué hace, sin
título · `Condición:` · `Límite:` · `Riesgo:`. Las tres últimas solo aparecen si el perk las tiene, y qué
merece sección propia se decidió contando perks, no a ojo. Verificado con captura en Equipo y en Mercado.

**Tooltip de composición (BB-J, RF-012d)**: la ficha de jugador y el Mercado dicen cuántos de la etiqueta
que un perk cuenta lleva ya la plantilla ("Bruto: 0 de 3"). `pack_mentality` NO se ha arreglado: la
previsibilidad queda resuelta, el diseño del perk sigue abierto.

**Línea base de puertas remedida (22 sep 2026, worktree limpio en HEAD):** las 3 rojas son
`badBuildsLoseToNone_elf_brawler` **48,54**, `badBuildsLoseToNone_elf_out_of_zone` **46,04** y
`buildsWinDifferently_injuries` **1,30** — no las que citaba el párrafo de abajo (`passChain`, e `injuries`
en 1,20), que quedó desactualizado. Tardan **7 m 02 s**.

**Puertas: 3 rojas, y ahora se sabe por qué.** `CAT-J` deja de ser una incógnita: medidas sobre ocho
semillas, las tres métricas fallan en la **mayoría** de ellas, así que son balance real y no ruido —lo
contrario que la puerta de equipamiento, que sí lo era (**ADR 0118**: promedia ocho semillas y sube el
umbral de 1,0 a 1,5)—. El siguiente hilo es `buildsWinDifferently_injuries` (1,20 contra 1,40, cuatro
errores típicos por debajo): las builds física y técnica se distinguen menos de lo que el diseño pide.
Las 43 puertas tardan ahora **7 m 05 s**, no los 5 m 32 s que cita `CLAUDE.md`.

**Patrón encontrado cinco veces en un día: el texto promete lo que el dato o el código no hacen.**
`steamroller` (BB-Q, resuelta), `shadow`, los falsos maestros (BB-R), el generador de referencias neutras
(BB-S) y `CaptureRunner` (BA-L2, resuelta). Los cinco salieron mirando datos, ninguno jugando. Merece una
pasada sistemática, no cinco arreglos sueltos.

## Trabajo en curso (16 sep 2026)

- **Tanda 1 y 2 de perks completas** (94 perks, siete primitivas nuevas de tanda 2 y seis de tanda 1).
  ADR 0112 (aviso de perk activado), ADR 0113 (oro de perk como inversión, no premio), ADR 0114
  (cámara con cuatro estados). Ver `docs/analisis/perks-catalogo-unificado.md`.
- **Paquete BB en curso**: trece anotaciones de la partida del revisor sobre gameplay, en
  `docs/pendientes/`. BB-M resuelto (no tocaba la ADR 0048). **BB-G resuelta (ADR 0117, 16 sep
  2026)**: `chaseBallLooseBonus` 250→410 (`data/ai/weights.json`) porque con balón suelto y quieto
  `ChaseBall` perdía contra `CoverSpace`/`FindSpace` (hueco medido 152 puntos); episodios de balón
  parado ≥15 ticks bajan de 107 a 32 en 200 partidos (−70 %), 43 puertas 4 rojas → 3. Un rechazo
  inicial se corrigió tras `independent-reviewer`: la puerta que parecía empeorar no sobrevivía a
  una segunda semilla (patrón BB-P). **BB-C resuelta** (`ResetPositions` ya no teletransporta al
  goleador que sigue celebrando; revisión encontró y corrigió un alcance sin pedir —incluía
  `KnockedDown`—, un umbral de test flojo y una afirmación de "sin relación causal" con las puertas
  que la remedición emparejada no sostenía). **BA-K resuelta** (`/Game`: el render ya no desliza un
  salto de varias casillas entre dos ticks —cortinilla de opacidad en 3D, corte de posición en las
  dos vistas—, verificado con captura). **BB-A y BB-L resueltas** como consecuencia de las dos
  anteriores. **BB-D analizada**: la pausa dramática que pedía no necesita tocar RT-020, se resuelve
  entera en `/Game`; queda como mejora de presentación independiente, no bloqueaba a las demás.
  **BB-B resuelta (ADR 0115, 16 sep 2026)**, tras dos intentos rechazados (inmunidad temporal;
  barrera geométrica con tres fallos — fuga al penalti, sin techo de duración, métrica de
  aceptación mal planteada). El tercer intento corrige los tres, verificado por el
  `independent-reviewer` en dos rondas: 33→0 disputas reales contra el sacador en 200 partidos,
  penalti intacto, lote de `/Balance` sin movimiento fuera del ruido. 4 puertas rojas de 43
  (mejor que las 5 de antes), ninguna atribuible a la barrera — todas son builds concretos que
  cruzan el margen de su puerta cuando cualquier cambio real de `/Sim` desplaza el consumo de RNG
  (mismo patrón tres veces esta sesión; candidato a revisar el margen de esas puertas, fuera de
  BB-B). Decisión de rango pendiente y aparte: `BadBuildsLoseToTheirBaseline`. **Hallazgos nuevos,
  ajenos a BB-B, sin diagnosticar**: BB-N (el saque de córner no ocurre nunca en la muestra
  medida) y BB-O (un jugador fuera del campo puede conservar el balón y congelar el partido, hasta
  el 62 % de un partido medido, ~1 de cada 400).
  **BA-N cerrada (ADR 0116, 16 sep 2026)**: umbral de `EquippingAGoodBuildIsWorthSeveralPointsOfWinRate`
  bajado de 2,0 a 1,0 (la tercera bajada desde el valor inicial de la puerta: 5,0→3,0→2,0→1,0). No por el
  catálogo de 94 perks (hipótesis REJECTED tras seis rondas de `independent-reviewer`), sino porque
  el umbral anterior tenía ~34 % de falso positivo contra su propio error de muestreo (~0,9 puntos).
  Cerrada por decisión del orquestador tras seis rondas de revisión (el umbral se reprodujo correcto
  las siete veces; solo la narrativa histórica fallaba, cada vez más acotada), con deuda documentada
  en `docs/pendientes/BA-N.md` en vez de una séptima ronda.
  **BB-P medida y diagnosticada (17 sep 2026, seis semillas por métrica + bisección en worktrees)**:
  de las puertas rojas que dejó BB-B, `CoherentBuildsBeatTheirBaseline`/`orc_violence` y
  `BadBuildsLoseToTheirBaseline` confirman ruido de muestreo puro (sd empírico 2,0-2,5, coincide con
  el 2,3 teórico ya documentado; las medias caen dentro de un sd de su umbral) — mismo patrón que la
  puerta de equipar, ningún umbral se toca. **`BuildsWinDifferently`/`passChain` NO es ruido, y su
  causa ya está aislada**: cayó de 1,233 (ADR 0062, 6 sep) a ~1,08 por al menos tres decisiones de
  diseño independientes, todas anteriores a este ciclo (BA-N→BB-G→BB-C→BA-K) — el 61 % de la caída es
  la ADR 0088 (9 sep, quitar los `elseEffects`) quitándole a `orc_violence` el malus `pass −50` de
  `brute_boots` que antes acortaba su propia cadena; el resto se reparte entre la física del pase
  (ADR 0091/0092, con recuperación parcial) y dos cambios de balance posteriores (ADR 0109, 0110).
  Ninguna causa es un bug: son decisiones ya tomadas y documentadas que nadie volvió a comprobar
  contra este umbral concreto. No se toca `MinPassChainRatio`: la decisión de recalibrarlo (o de
  restaurar el hueco por otra vía) queda para `game-design-review`, no para esta ficha. Queda sin
  medir `TheThreeDoctrinesBuyDifferently` (coste por semilla mucho mayor, `FullRunGateTests`) — no
  está roja ahora mismo. Sin resolver: la inconsistencia calculado/medido de precio de objeto (ADR
  0038 vs ADR 0087) que BA-N no tocó, y el
  pase de `game-design-review` (pendiente, Regla B) sobre si el poder de detección de la puerta
  recalibrada (~41 % ante una caída a la mitad) es aceptable para el escalón que nombra la ADR 0033.
- **Auditoría de organización de trabajo** (V1→V2→V3, decisión del revisor 16 sep 2026): en migración.
  Ver `docs/analisis/auditoria-organizacion-v2.md` para el razonamiento completo.
- **Tanda 0 del catálogo conceptual, hecha (17 sep 2026)**: el histograma de acción elegida por
  jugador/partido contra el control emparejado de la ADR 0087, prerrequisito de C1
  (`modifyUtility`) antes de tocar el motor (`docs/analisis/tanda-0-histograma-de-accion.md`,
  `Sim.Tests/Analysis/ActionHistogramTests.cs`). Ningún peso, perk ni tope tocado. Calibrado contra
  cuatro perks ya existentes: confirma que `modifyProbability` no mueve el histograma
  (`own_third_anchor`, L1=0 con activación completa) y que el instrumento distingue un efecto real
  pequeño de un cero (`sweeper_keeper`, L1=0,0013) — con el matiz de que `bulwark_stance` activó en
  solo 2/40 partidos, así que su propio L1=0 no prueba nada por baja potencia (mismo límite AT-C que
  ya documentó la ADR 0087). Pendiente, no hecho aquí: aplicar el instrumento a un candidato real de
  C1 (Tanda 2, `Cazagoles`/`Ancla`) — parada explícita hasta revisar este resultado.

## ADR 0134 — el once efectivo, y lo que destapó (23 sep 2026)

Paquete de la familia **BA-I + BB-F + BC-H** (el revisor pidió tratarlos como una sola tarea). No eran tres
defectos de interfaz: faltaba un concepto. `/Sim` decidía quién juega dentro de `RunLineup.Build` y **todos
los consumidores de fuera lo volvían a derivar de `state.Lineup`**, que es la intención guardada. De ahí
salían los tres síntomas y **dos incumplimientos**: RF-002d (jugar en inferioridad es una decisión legítima
que era imposible mientras hubiera banquillo, con un aviso que además mentía) y RF-012c/RF-012d + condición 3
de la ADR 0048 (el jugador de relleno jugaba **sin indicador de riesgo de muerte**).

**Lo que más importa de todo el paquete no estaba en el encargo:** `RunLineup.Build` **tiraba la colocación
del jugador** y reasignaba las siete casillas por rol en cada partido. Como el indicador de riesgo sí leía
las casillas guardadas, **mover fichas movía el número y no movía el partido**. Lo destapó un test que ya
existía y que era vacío sin saberlo —pasaba en verde ejerciendo una colocación ilegal, el portero fuera de
su área—. Arreglado (`RunLineup.PlaceOutfield`); queda abierto en [BG-B] medir las formaciones que ahora sí
son posibles, que el balance de fase 1 **nunca ha visto**, y el jefe que empuja casillas fuera de `Effective`.

**Decisión del revisor a mitad de paquete**: la lesión **leve** ya no saca del campo si el jugador no quiere.
La ventana pasa a tener tres respuestas —sustituir, dejar el hueco, seguir jugando—. El precio viaja
calculado desde `/Sim/Run` (`PlayOn.After`) porque la penalización de RF-091 es **lineal** y aplicarla dentro
del motor compondría multiplicativamente: con una sola lesión previa, un atributo de 99 daba 71 en vez de 69.

**Medido**: huella de determinismo `209287DE9EDC1BB0` **intacta** (ningún partido de estado inicial fijo se
mueve); 43 puertas con las **mismas 4 rojas** de la línea base (`orc_misplaced` 45,18 y rareza 43,75
idénticas), salvo la de doctrinas −0,12 → −0,11, que es consecuencia directa de respetar la colocación —al
no poder jugar un titular, los demás **conservan su casilla** en vez de recolocarse el equipo entero—.
**LIKELY, no CONFIRMED**: no se aisló revirtiendo solo ese cambio.

**La revisión independiente encontró tres fallos de programa con reproducción**, arreglados con test cada
uno: «que siga jugando» reventaba con el suplente que ya había entrado; dos decisiones sobre el mismo
jugador componían `After1 + After2 − base`; y el rechazo reabría su ventana. Y una cosa mayor: **quien se
queda tocado multiplica por ocho su probabilidad de morir** (`hurtInThisMatch`, rama inalcanzable hasta
ahora) y la bandeja anunciaba solo el −15 %. Se le ha puesto el número, y **el revisor cierra la
pregunta de diseño (23 sep)**: *«el player sabe que dejar un lesionado sube sus posibilidades de morir, es
su decisión»*. El multiplicador no es un defecto que corregir sino el contenido de la decisión; la única
condición era que se supiera antes de elegir, y con `PlayOnRisk` en la bandeja se sabe. **BA-I y BB-F
quedan cerradas; BC-H conserva su mitad de antes del partido.** Deuda escrita, no tapada: (E) no pasó por
`game-design-review` y el canje **no está medido** —ni puede estarlo hoy, porque la política automática
nunca lo ejerce, el mismo hueco que CAT-C tiene con el consumible manual—.

**BB-K, BB-N y BB-O, medidos los tres (23 sep 2026).** Entraron por `gameplay-debug` con una sola sonda de
4 000 partidos que midió los tres síntomas a la vez. **No tienen causa común**: la sospecha de que "los
tres hablan de cosas atascadas en el campo" resultó falsa, y cada uno vive en una parte distinta del
motor.

- **[BB-O] CERRADA.** Causa CONFIRMED y reproducida tick a tick: el sacador de una reanudación se elige una
  vez y **no se revalida**, así que uno lesionado durante la cuenta atrás recibía igualmente la posesión
  —fuera del campo— y congelaba el partido hasta el final (semilla 144 del árbol del 16 sep: 740 de 1 200
  fotogramas sin un solo evento). La hipótesis que el propio fichero daba por principal —que el camino de
  lesión no soltaba el balón— era **falsa**: los tres caminos de salida sí lo sueltan, desde el commit
  inicial. Arreglado con la revalidación del sacador, un invariante en `UpdateBall` y una guardia en
  `WalkRestartTaker`. Medido **inerte** en HEAD (lote de 10 000 partidos byte a byte idéntico al
  baseline). **La revisión independiente reprodujo el bug y el arreglo en HEAD** forzando el disparador
  (326-1 185 fotogramas congelados → 0), retiró del diff un cambio en `ParkBall` que era código muerto,
  encontró vivo el hermano de `WalkRestartTaker` —arreglado en el mismo paquete— y halló que la guardia
  cierra de paso un **segundo fallo**: a un expulsado le borraba el `SentOff` y dejaba pasar una
  sustitución que la ADR 0094 prohíbe.

  **Lo que no se consiguió, y consta**: no hay test que dispare el caso de forma natural. La hipótesis de
  que lo mantenía en cero la barrera de BB-B quedó **REJECTED** —con `restartClearanceCells` a 0, en 8 000
  partidos y 123 134 reanudaciones el sacador es retirado 0 veces—. Queda **LIKELY** que lo desactivara la
  ADR 0134 E, por la que la lesión leve ya no saca del campo, que es justo lo que retiraba al sacador en el
  caso del 16 sep.

  De la revisión sale además **[BH-A]**, pendiente nuevo: el motor **no tiene ninguna defensa contra el
  silencio**. BB-O se arregló por su camino, pero cualquier otro estado que no progrese sigue pudiendo
  congelar un partido 49 s sin que nadie se entere. La opción barata es una puerta de test, sin tocar
  `/Sim`.
- **[BB-N] causa CONFIRMED, decisión del revisor pendiente.** El córner no es imposible: es **1 de cada
  3 290** llegadas a la línea de fondo (1 en 2 000 partidos, contra 3 289 saques de puerta). La lógica que
  decide córner o saque de puerta es correcta; lo que falta son las dos vías por las que un **defensor**
  manda el balón fuera por su propio fondo: el tiro en vuelo se salta `CheckOutOfBounds` por completo y
  siempre resuelve saque de puerta, y un bloqueo defensivo deja el balón **parado**. Hay que decidir entre
  modelarlo (ADR) o retirar `RestartKind.Corner` para que el código no prometa una regla que no ocurre.
- **[BB-K] causa CONFIRMED, arreglo diseñado y sin implementar.** 12 160 episodios de oscilación en 2 000
  partidos (6 por partido, 0,8 s de media), el **98,3 % durante `CoverSpace`** y con un compañero a 0,14
  casillas persiguiendo **el mismo punto**. `CoverSpace` es la única acción de colocación frecuente que no
  mira a los compañeros: `OfferSupport` y `FindSpace` ya penalizan el apiñamiento. Se descartaron por
  medición dos hipótesis atractivas: que sólo oscilara el cuerpo con el destino fijo (sólo el 18,5 % de los
  episodios), y que la geometría amplificara el movimiento del balón (amplificación mediana **0,25**: en
  realidad amortigua). La nota de diseño con las diez preguntas está en el fichero; recomienda repartir el
  punto de cobertura por id ascendente, como ya hace `Marking`. **Es un hito aparte**: cambia
  comportamiento defensivo real y pide baseline y las 43 puertas.

## La familia BE cerrada (23 sep 2026)

Las cinco fichas que el `independent-reviewer` dejó abiertas sobre la ADR 0124. **Cuatro comparten
patrón**, y verlo junto cambió tres de los cuatro arreglos: *un contador que cuenta cosas que no debería,
porque no filtra por equipo, por parte del partido o por tipo de nodo*. Ninguna se veía jugando: las
encontró leyendo código.

- **[BE-B] cerrada.** `InjuriesCaused` comparaba equipos con nadie: un perk `injure` con `target: "actor"`
  —que el cargador admite— habría acreditado al portador por lesionar a un compañero, y desde la ADR 0124
  eso **se guarda** y RF-125 pone un logro encima. Arreglado filtrando **al acreditar y no al resolver**,
  que deja diseñable un perk de daño amigo sin contaminar el logro. Se **descartó** prohibir
  `target: "actor"`: el fallo era de contabilidad, y cerrar esa puerta habría quitado una vía de diseño
  legítima en un juego cuya identidad es la carnicería administrada. **La ficha no recogía el hermano**:
  `DeathsCaused` tenía el mismo descuido, y también está arreglado.
- **[BE-C] a medias, y con una conclusión falsa que hubo que retirar.** El síntoma 2 (`PlayedTicks` sin
  filtro de equipo) está arreglado: era inocuo **por accidente** —los rangos de id no se solapan— y habría
  fallado en silencio el día que se estrecharan. El síntoma 1 **sigue abierto**: se llegó a cerrar con un
  argumento estructural —«los del campo son un subconjunto de los disponibles, así que no hay ventana
  posterior a `defeatTick`»— que la revisión independiente **refutó con un experimento**. `CanStart` deja
  salir al lesionado grave marcado (RF-093 vía 1) y `IsAvailable` no lo cuenta: puede haber **siete en el
  campo con cinco disponibles**, y entonces `defeatTick` se fija mientras el partido sigue. La ventana
  existe; es rara y de gravedad baja. Sí se arregló de paso que `CheckForfeit` llevaba **tres** `5`
  literales sin atar a `RunRules.MinimumAvailablePlayers`: ahora hay constante y test, con su
  documentación diciendo que es condición **necesaria y no suficiente**.

  **Lo que más conviene recordar de este paquete no es el arreglo, es el fallo de método**: esa conclusión
  se escribió como teorema **dentro de `/Sim`**, donde un lector futuro ya no la comprobaría, a partir de
  un razonamiento que nadie había verificado. Regla F. Lo que un comentario afirme como estructural, o lo
  demuestra un test, o se escribe como *«medido, no demostrado»*.
- **[BE-D] resuelta sin decisión nueva: la tenía RF-125b.** Exige que el progreso de un logro se pueda
  perseguir «de forma deliberada», y un contador que **baja** al vender un jugador no cumple eso. Así que
  el contador de RF-125 es **del club** y vive en la run, no en la plantilla. Segundo motivo que la ficha
  apuntaba sin rematar: un contador de run recoge también las lesiones de las cartas de evento, que
  `RunCareer` deja fuera por diseño. No se implementa: RF-125 depende del perfil entre runs (gate 4 de la
  ADR 0123).
- **[BE-F] cerrada la lectura, en cuatro consumidores** (uno más de los dos que la ficha nombraba: la
  revisión encontró `StandardRunSystems.OpponentFor` escribiendo la exclusión a mano por cuarta vez). `NodeKinds.IsCatalogRivalMatch` separa «¿aquí se juega?» de «¿enfrente hay
  un clan del catálogo?», y los dos sitios que excluían `Boss` a mano ahora lo preguntan por su nombre.
  Pasó por `architecture-review`, que añadió una precisión que la ficha no hacía: **mitiga la clase de
  error, no la elimina** —quien escriba `IsMatch` por costumbre sigue teniendo el bug— y eliminarla exigiría
  renombrar `IsMatch` en 28 sitios, doce en `/Game`, rompiendo la regla de no mezclar proyectos.
  **Sigue abierto el dato**: el nodo de jefe guarda el `opponentId` fantasma, y quitarlo obliga a mover el
  cursor de `MapGenerator`, que regeneraría todos los mapas de todas las semillas.
- **[BE-E] reducida.** El diseño ya lo cerró la ADR 0128; la incoherencia de dato se documenta en vez de
  quitarse, porque el esquema y el cargador exigen cuatro valores para **todos** los `*ByRarity` y quitar
  uno sólo aquí cambiaría una incoherencia pequeña por una irregularidad estructural. **Queda abierto** lo
  de fondo: el legendario tiene 5 slots de perk y ese techo de build es inalcanzable mientras la ADR 0128
  siga bloqueada.

**Todo el paquete es inerte**: 1 140 tests en verde y **byte a byte idéntico** al baseline en los dos
lotes, el de 10 000 partidos y el de 500 runs completas (`summary.csv`, `runs.csv`, `runs-nomarket.csv`).
Era lo esperado —ningún dato de hoy ejercita ninguno de los caminos— y es justo lo que lo hace seguro.

**La revisión independiente cambió el paquete en cinco sitios**, y conviene leerla entera antes de tocar
nada de esta familia: refutó el argumento de BE-C (arriba); encontró que los dos `if` de BE-B **no tenían
ningún test** y se podían borrar con la suite en verde (ahora hay cuatro, y tres fallan sin el arreglo);
que la rama nueva de `ApplyRivalCredits` tampoco estaba cubierta (ahora sí, con nodo `Boss`); que la ficha
BE-B se cerraba declarando una acción **no hecha** —el mensaje de `PerkLoader`, ya corregido—; y que el
`_doc` que BE-E añadía **contradecía al esquema**, además de descubrir que ese mismo `_doc` cita cifras
obsoletas de los campos que documenta.

Y un hallazgo que no era del paquete pero lo destapó: **[BH-B]**, el nodo de jefe se presenta en el mapa y
en el ojeo con el **nombre de un clan de liga** (9 de 9 semillas medidas). La auditoría de BE-F no lo vio
porque buscó por `IsMatch` y estos dos sitios leen `OpponentId` directamente — *el grep equivocado para la
pregunta que se estaba haciendo*. Es `/Game`, así que va aparte y pide `visual-review`.

## En curso: la altura del balón y el rechace (ADR 0135, 23 sep 2026)

**El revisor decide modelar el córner**, y por el camino algo más grande: *«el balón debe tener físicas lo
más fieles posibles a la realidad y el juego tiene que sentirse real»*, y *«igual es el momento de darle
altura al balón, esto puede ser importante a la hora de decidir cómo se intercepta»*. Elegidas, con las
alternativas delante: **física real con `z` y gravedad** (frente a altura derivada sin estado), **portería
con alto y ancho y puntería dispersa** (frente a declarar sólo la altura), y **alcance esférico** (frente a
cilíndrico, más legible pero menos fiel).

Todo está en **[ADR 0135](./decisiones/0135-el-balon-tiene-altura.md)** y en
**`docs/plan-altura-del-balon.md`**, que lleva las diez preguntas de `game-design-review`, el veredicto de
`architecture-review` y el troceado en seis pasos. **Leer los dos antes de seguir.**

Lo que hizo falta saber antes de diseñar nada, medido: 5,69 tiros a puerta y 3,18 paradas por partido, y
**cero rechaces** —el bloqueo deja el balón con velocidad cero y la parada siempre da posesión al portero—,
que es la causa de que [BB-N] midiera 1 córner en 2 000 partidos. Y el balón se dibuja con altura
constante, así que en un render 3D **todos los tiros son rasos**. El tercer hecho es el que hace atractiva
la decisión: la ficha del enano ya dice «**Bajos**» y el motor no lo sabe, porque `bodyRadius` es un radio
horizontal para separar cuerpos.

**Dos decisiones de arquitectura** que conviene no volver a discutir: la altura es **del balón, no del
espacio** (nada de `Vec3` global: el 99 % del motor no necesita altura, y el patrón del repo es añadir un
escalar al actor, no una dimensión al mundo); y las magnitudes verticales van en **milésimas enteras** en
`tuning`, como `shotSpeedCellsPerTickMilli`, con la altura en `float` igual que la X y la Y.

**Paso 1 hecho** (`Ball.Z`/`VelocityZ`, gravedad, bote, y la recogida medida **por barrido** del segmento
recorrido en vez de contra el punto final). RT-024 **en verde**, que era la razón de ser del paso. Se
predijo que sería inerte y **no lo fue**: el barrido cambia 7 153 partidos de 10 000 porque desplaza el RNG
—y además es un cambio de regla pequeño y deliberado, «se coge el balón si pasó cerca, no sólo si acabó
cerca»—, pero **ninguna métrica se sale de banda**. Conviene no confundir las dos cosas: de aquí en
adelante **todos** los pasos moverán todas las semillas, y el recuento de partidos distintos no es una
medida del efecto. Baseline previo guardado en `out/baseline-altura/`.

**Paso 2 hecho**: la portería gana alto y ancho (1,0 de semiancho y 0,70 de alto, proporción ~1:3), el tiro
apunta a un punto **disperso** dentro de ella en vez de a su centro exacto, el vuelo describe una parábola,
y un tiro desviado puede irse **por encima del larguero** —vía que no existía—. La traza graba la altura.
La dispersión sale de la **calidad** del disparo, que ya integra técnica, distancia y presión, para no
abrir un segundo camino que pudiera contradecir al primero. La tirada de dentro/fuera **no se tocó**: se
consideró derivarla de la geometría y se descartó por no rehacer la calibración del 70,5 % en el mismo paso
que introduce la altura.

Medido: cambian **todos** los partidos y el agregado no se mueve (`goalsPerMatch` 2,79 → 2,81,
`possessionChanges` 21,75 → 21,66, ninguna fuera de banda). Era lo previsto — con el portero midiendo su
alcance en el plano, la altura todavía no decide nada.

**Paso 2b hecho** (decisión del revisor sobre la marcha): **el marco es físico**. Los postes no lo eran
—«poste» sólo salía en comentarios— y la altura, atada a la calidad, llegaba con máximo 0,453 contra un
larguero a 0,70: el **0,00 %** se le acercaba, o sea que el larguero habría sido decorativo. Van juntas
porque sin marco, desatar la altura la haría gratis. La puntería pasa a ser **intención + error** —un
delantero malo también *quiere* meterla por la escuadra, lo que le distingue es si lo consigue— y el marco
rechaza con evento propio `SHOT_POST`. Medido: **1,44 % de los disparos al marco**, uno cada nueve
partidos, `goalsPerMatch` 2,79 → 2,73, ninguna métrica fuera de banda.

El primer intento del marco estuvo mal y queda escrito por qué: acotar el punto de mira y preguntar después
concentra en el borde **toda** la masa de los tiros que se habrían ido, o sea palo garantizado — 18,5 % de
los disparos y los goles cayendo a 1,27.

---

## El paso 3a, hecho y medido — y NO enviado (23 sep 2026)

**Está implementado, probado, documentado y commiteado, pero va APAGADO por dato**
(`minAimApertureCenti: 100`, que hace el divisor exactamente 1, y `offTargetAperturePenalty: 0`).
Verificado con `cmp`: el build publicado es **byte a byte** HEAD anterior en 20 000 partidos de dos
semillas. Encenderlo son dos números.

**Lo que consigue encendido**, que es real y era el objetivo de [BA-E]: la ventaja de conversión del tiro
sin ángulo —convertía **mejor** que la media— muere, de 1,42 / 1,31 a **1,09 / 1,03**, y sin tocar la IA
(`passChainAvgLength` no se mueve). Coste: `goalsPerMatch` 2,46 → 2,20 y `shotsOnTargetShare` 72 → 66.

**Lo que impide enviarlo**: cuatro puertas rojas sobre las dos del baseline. Tres se despejaron —dos son
preexistentes ([BF-A] ya lo dice de `elf_brawler`), una es ruido (0,03 sobre el tope con error típico
0,92), y `bossGate_grimhold_guns` se arregla **sin tocar la banda**, ablandando el jefe (`quality` 31 → 27)
como hicieron las ADR 0049, 0050 P2 y 0074—. Queda **`buildsWinDifferently_injuries`**, de ≥1,10 a
**1,04**, fuera en 7 de 8 semillas. La ADR 0131 calibró ese umbral midiendo media 1,29 y dejó escrito que
**no es un rango que se arregle bajándolo otra vez**. Hay lectura alternativa anotada (sd 0,17 pone el
borde a ~1 error típico). **Decisión del revisor, pendiente.**

### Lo que la revisión independiente refutó, y hay que leer antes de retomarlo

1. **El efecto NO lo produce la trigonometría.** El barrido movía la penalización dejando el suelo siempre
   activo, así que nunca midió el término solo. Medido: **penalización sola → ventaja 0,978** (89 % del
   recorrido); geometría sola → 1,235 (19 %). **Esto es la vía (C) de BA-E con rampa**, la que el revisor
   declinó el 14 sep.
2. **Por tanto el `game-design-review` que se hizo no cubre la mecánica que actúa** (se hizo sobre la
   división). Encenderla pide uno propio.
3. **La geometría no puede hablar**, y eso es estructural: [BH-C](./pendientes/BH-C.md), ficha nueva. El
   recorte al marco amortigua **toda** mecánica de puntería —cualquier término que ensanche el error se
   manifiesta como palos, no como fallos—. **Conviene resolverla antes del paso 3c**, que concentra masa
   justo en la línea del poste.

### Por dónde empezar la siguiente sesión

**La recomendación es atacar [BH-C] antes que decidir sobre el umbral de lesiones**: con el recorte
arreglado —que la mira cruda decida dentro/fuera, o sea el «error angular completo», alternativa 1 de §10
del plan— la geometría pasa a ser portante y puede que la penalización plana no haga falta. El precio es
que rehace la calibración del 70,5 % de la ADR 0050 P2, así que necesita su propio paso.

Leer, en este orden: `docs/pendientes/BA-E.md`, `docs/pendientes/BH-C.md`,
`docs/plan-altura-del-balon.md` §10–§11.ter, y la enmienda 3 de la ADR 0135.

**Deuda anotada del paso**: faltan tests de la división trigonométrica a nivel de `OnTargetAim` con RNG
fijo, del penalti, del remate de centro, y uno que fije los valores publicados de `tuning.json` (hoy no
los cubre ninguno: los cuatro tests los sobrescriben). Y dos afirmaciones que se escribieron como
comprobadas y no lo están: que el penalti no paga apertura, y que el remate «llega de frente por
definición» —`EvaluateCross` sólo exige apertura **mejor**, no de frente—.

---

## Después del paso 3a: lo que ya estaba en cola

*(Lo de abajo es el planteamiento del paso 3 tal y como se escribió **antes** de medirlo, y se conserva
porque el 3b y el 3c siguen vivos. Dos avisos al leerlo: la autorización para avanzar con puertas en rojo
**ya caducó** —era «hasta implementar centrar», y el centro está dentro desde la ADR 0136—, y el orden
«ángulo primero, portero después» sigue en pie pero ahora con [BH-C] por delante.)*

El revisor amplía el paso 3 (enmienda 2 de la ADR 0135): el **ángulo** del tirador, los **rivales que
tapan** y la **colocación del portero** deben entrar en la puntería **y en la decisión de disparar**. Y
añade el diagnóstico que lo ata todo:

> «el delantero se posiciona en la línea de fondo creyendo que es el mejor sitio cuando en realidad no lo
> es. También forzará que centre balones buscando compañeros rematadores en mejor posición.»

**Eso ya está fichado y medido en [BA-E]**, y el aviso es serio: **la palanca elegida es la vía (B) de esa
ficha y ya se midió — pone SEIS puertas en rojo** y acorta la cadena de pases. La vía (A) está RECHAZADA
(ADR 0111) con ocho puertas en rojo, y dejó dicho que *la corrección buena tendrá que ser **local** al
delantero en zona de remate*.

**Por eso el orden es: primero «centrar», después el ángulo.** La (B) rompía porque le quitaba el tiro al
delantero **sin darle nada a cambio**: sin ángulo y sin alternativa, la jugada se muere y la cadena se
acorta. Con el centro, la transforma. Y es local por definición, que es lo que la ADR 0111 pedía.

**Qué es «centrar»** (especificación del revisor, §7 de `docs/plan-altura-del-balon.md`): un **pase alto**
a un compañero cerca del área, que **remata sin controlar** —de primeras, no recibe y luego dispara—, y
apoyado en **fuerza** en vez de técnica para distinguirlo del tiro. Eso le da identidad propia: el tiro es
colocar, el remate es llegar y empujarla, y hay criaturas para cada cosa.

**Reabre una exclusión a conciencia**: los pases siguen **rasos** por decisión explícita de la ADR 0135, y
un centro es un pase alto. Es el primer pase con altura del motor y necesita su propia medición.

### Autorización expresa del revisor sobre las puertas

> «las puertas están rojas pero hay que ignorarlas hasta implementar acción "centrar"»

**Se puede avanzar con puertas en rojo** mientras el sistema esté a medias: el ángulo sin el centro es un
sistema incompleto y medirlo contra las puertas sería medir un estado que nadie quiere enviar. Vuelven a
ser criterio de parada **cuando el centro esté dentro**, no antes. Dos avisos para no confundirse leyendo
el rojo:

- **Cuatro puertas ya estaban rojas antes de toda esta familia** y son de [BF-B] (`orc_misplaced` 45,18,
  rareza 43,75, doctrinas), comprobado contra árbol limpio. No son de aquí.
- **La (B) hay que remedirla con el centro dentro.** Las seis rojas son de la (B) *sola*; repetirla sin la
  alternativa sería repetir un experimento cuyo resultado ya se conoce.

### Y el resto del paso 3, cuando el centro esté

El alcance de portero y defensas pasa a **esférico**. **Orden obligatorio: ángulo y oclusión primero, el
portero después** — el portero introduce un bucle (el tirador reacciona a él y él ya reacciona al balón) y
si todos los tiros van al lado contrario, `saveRate` se desploma. Queda sin verificar, dicho como tal: la explicación natural de por qué los goles no se movieron en el paso
2 sería que la dispersión no saca al balón del alcance del portero (`save.reachCells` 0,9 contra semiancho
1,0). **No está comprobada** —la sonda que se escribió medía el final del vuelo, que para un tiro parado no
es la línea de gol—, y es justo lo que el paso 3 tiene que resolver.

**Y antes de dar el paso 4 (el rechace), releer §3.ter**: esto reabre la **ADR 0117**
(`chaseBallLooseBonus` se calibró con balones que recorren 1,25 casillas) y despierta
**[BC-G]** (el balón muerto en el córner, tres mecanismos CONFIRMED sin arreglar). La métrica que más va a
empujar es **`possessionChanges`**, hoy en 21,75 con techo 28.

---

**También pendiente: el arreglo de [BB-K]**, que ya tiene diagnóstico y nota de diseño y sólo le falta
implementar y medir (`balance-measure` con baseline del mismo árbol y las 43 puertas). Antes de tocarlo,
leer la nota de diseño del propio fichero: la recomendación es repartir el punto de cobertura por id
ascendente y el riesgo a vigilar es que el segundo defensa abandone el centro y abra pasillo.

**Una decisión espera al revisor**: el **apartado E** de la ADR 0134 con el ×8 letal a la vista (leer «Lo
que la revisión independiente dejó abierto»). Y una pregunta menor que salió de BB-O y no se coló en su
arreglo: **¿debería poder sacar una reanudación un jugador derribado o celebrando?** Hoy puede; cambiarlo
mueve 3 partidos de cada 10 000.

Después, por este orden: [BG-B] medir formaciones no-2-3-1 y el jefe que empuja casillas, [BC-H] el
escenario de captura con un titular no disponible y luego el hueco deliberado antes del partido, [BG-C]
los tres contratos, [BG-A] la experiencia de carta.

---

## El audio suena por primera vez (23 sep 2026)

El revisor dejó **57 ficheros** en `Game/audio` (efectos, dos músicas y un ambiente) y el paquete los pone
a sonar. Todo es presentación: **ni una línea de `/Sim` ni de `/data`**, y por eso no lleva lote de
`/Balance` —el disparador de `balance-measure` es «¿puede alterar una métrica cuantificable?», y esto no
puede: no toca una sola tirada—.

**Lo que ya había** era el `AudioManager` (pools por carpeta, bolsa barajada, variación de tono) y la capa
de momentos, escritos el día anterior contra carpetas vacías. **Lo que faltaba era todo lo demás.**

### Tres cosas nuevas

1. **Infraestructura**: `Game/default_bus_layout.tres` con tres buses (`SFX`, `Music` a −6 dB, `Ambience` a
   −12 dB) —antes no existía ninguno y todo caía en `Master`, sin control separado—; el bucle activado en
   los `.import` de música y ambiente (es un ajuste de importación, el código no puede ponerlo); y el
   indicador **`--audio-trace`**, que escribe una línea por reproducción para poder **demostrar qué suena
   sin altavoz** —en WSL no hay tarjeta de sonido y las capturas corren con `--audio-driver Dummy`—.
2. **Música y ambiente** (`Game/Ui/ScreenAudio.cs`, aplicado desde `Nav.Go`): menú antes de empezar y al
   morir, una única música **continua** para todo el rato entre partidos —el gestor no la reinicia al
   cambiar de pantalla, porque para el jugador mapa, mercado y equipo son el mismo sitio— y **silencio
   musical en el partido**, con el estadio en bucle por debajo. Ahí la banda sonora son los eventos.
3. **La capa de campo** (`Game/Match/MatchEventSounds.cs`), que es la que faltaba de verdad: cuelga de los
   **eventos crudos** —balón, golpeo, entrada, caída, hueso— y no de los momentos. Usa el mismo patrón de
   puntero que los gestos de tiro (lista precalculada en `BindPlayback`, resincronizada en cada salto), así
   que un evento suena una sola vez aunque el director congele encima. **Solo a ×1**, misma regla que los
   gestos (`docs/ui/README` §6, ampliada): a ×16 serían dieciséis golpes por segundo.

**Por qué dos capas y no una**: un momento llega cuando se *presenta*, y solo hay 8,6 por partido. Un pase
o una entrada no son momentos y son casi todo lo que pasa. Con una sola capa el campo sonaría ocho veces
por partido y estaría mudo el resto. Donde las dos tienen algo que decir no se repiten: el hueso cruje en
la casilla (campo) y el grito llega con el sello (retransmisión), y el desfase natural entre suceso y
presentación las ordena sin que nadie sincronice nada. La tabla de momentos pasa a devolver **varios
pools** para que un gol sea la red *y* la grada, y una muerte el jadeo *y* el cuerno.

### Medido

- **Traza del recorrido de retransmisión**: 24 reproducciones, las dos capas presentes —silbato ×6,
  `ball_hit` ×4, `grunt` ×3, `pain` ×2, `fall` ×2, gol + grada, `cheer`, `boo`, `crush`, `kick`— y
  **ningún `drop`**: los ocho reproductores sobran. 18 pools cargados; el único que se pide y está vacío es
  `sfx/referee/horn`, que se anuncia una vez y no rompe nada.
- **La música y el ambiente no los ejercita el arnés de capturas** (navega con `Nav.Suppressed`), así que
  se comprobaron aparte, en headless con la traza: `music/menu` al abrir el inicio y **un solo**
  `music/map` en todo el recorrido `--tour` pese a pasar por mapa, ojeo y equipo — que es exactamente la
  propiedad que se quería (no reiniciar la pista al cambiar de pantalla).
- **El audio no mueve un píxel** [CONFIRMED]: el recorrido se corrió con el paquete (A, B) y **con los
  cambios de audio apartados** (C, con `git stash` y los dos ficheros nuevos fuera del árbol). A-vs-C da
  exactamente lo mismo que A-vs-B: **0,00 % en seis capturas** y 1,5-3,3 % en las cuatro que avanzan con
  reloj real —que varían igual entre dos pasadas idénticas—. El ruido es del arnés, no del cambio.
- **Hallazgo lateral, y no es de este paquete**: contra las capturas **comprometidas** la diferencia llega
  al **70 %** (`retrans-tiro`) y al 39 % (`retrans-lesion`). Los PNG de HEAD son de `3d46624`, **anterior a
  tres commits de `/Sim`** que cambian el tiro (altura del balón, marco físico, centro). **La red de
  regresión visual lleva estancada desde entonces**: se regeneran en este paquete, en un commit aparte,
  para que la próxima comparación signifique algo.

### Lo que queda

- **Pools vacíos**, que suenan a nada y no rompen nada: `combat/tackle`, `combat/heavy_hit`,
  `combat/medium_hit`, `combat/light_hit`, `football/net`, `referee/horn`. Los dos primeros son los que más
  se notarían —hoy la entrada se apoya en `players/grunt`—. Cuando haya ficheros es **una línea de la
  tabla**, no un cambio de diseño.
- **Nadie llama a `SetBusVolumeLinear`**: no hay pantalla de ajustes. Los tres buses están para eso.
- **27 MB de WAV sin comprimir** entran en el repositorio con este paquete (los aplausos de la grada pesan
  3 MB cada uno, 17 s en estéreo). Es **peso de repositorio, no de build**: el importador los pasa a QOA y
  los 57 ficheros viajan como ~6 MB dentro del `.pck` de 9 MB (comprobado en la build de Windows, las 57
  rutas están dentro). Pasar los largos a OGG quitaría ~20 MB del árbol y casi nada del juego, así que la
  pregunta es solo de comodidad del repositorio — y **es material del revisor, no se recodifica sin
  preguntar**.

---

## Modelos 3D: maqueta con humanos, y el balón deja de ir raso (23 sep 2026)

Dos encargos del revisor sobre la misma pantalla, y **ningún cambio en `/Sim` ni en `/data`**.

### El balón tenía altura y nadie la leía — [BI-B], CERRADA en la vista 3D

Reportado jugando la build: *«no se ve el arco del balón. Lo veo raso siempre»*. La causa se aisló **con un
grep**, sin tocar código: `MatchTrace.BallHeightAt` tenía **cero consumidores** en todo el repositorio. La
ADR 0135 dio altura al vuelo en `/Sim`, la traza la guardaba, y `MatchPitchView3D` colocaba el balón en una
constante. Nada falló en verde porque esa ADR se midió con métricas de `/Balance`, que no necesitan render.

Arreglado: la vista lee la altura e **interpola entre los dos ticks con el mismo `Alpha`** que la posición
(un arco, no una escalera de 15 escalones por segundo), y el balón en vuelo lleva **sombra en el césped**
que se encoge con la altura — sin una referencia fija en el suelo, subir el balón en una cámara en tres
cuartos es indistinguible de alejarlo. Verificado en captura: `retrans-modelos.png`, balón a **1,394
casillas** de alto, con la sombra debajo. La vista **2D de depuración se queda raso a propósito**: es la
pantalla que el revisor llamó desfasada.

**Patrón para vigilar**, no anécdota: *un dato nuevo en `/Sim` sin consumidor en `/Game`*. Merece un grep
de consumidores cada vez que una ADR añade estado a la traza.

### Maqueta de modelos humanoides (solo humanos)

Material: **Universal Animation Library 2 de Quaternius, CC0** (`Game/models/`, 8 MB, con su licencia al
lado), maniquí de 1,829 de alto con 43 animaciones. Es **material provisional** y la regla 10 sigue en pie:
no se produce arte hasta cerrar el diseño de la fase 2. Existe para **decidir mirándolo**.

`Game/Ui/PlayerModel.cs` cuelga el modelo **del mismo nodo que ya movía la cápsula**, así que hereda
posición y no se enteran ni el anillo, ni el dorsal, ni la sangre, ni la cámara; a la cápsula se le quita la
malla y se queda de hueso. Escala medida del propio modelo contra la altura que manda la raza, giro hacia
donde se va, `Walk_Carry_Loop` con la velocidad de la traza y `LayToIdle` congelado en su primer fotograma
—que es el cuerpo en el suelo— para el derribado, en vez de tumbar el hueso noventa grados. Si el fichero
no está, `TryCreate` devuelve `null` y el jugador se queda con su cápsula: la maqueta no puede romper nada.

Captura nueva `retrans-modelos.png` (club humano, la pantalla de **pregón**, no la de depuración) y
`partido-3d-razas-color.png`, que además es la primera imagen en color de las cinco razas juntas.

**Lo aprendido en la primera captura, que es el valor de la maqueta:** un humanoide a la altura de la raza
**se lee peor que la cápsula** a la distancia de cámara actual —el cuerpo es delgado y el campo entero cabe
en 1.280 px—, y los modelos salían en **T-pose** porque nadie había llamado a la postura. Lo segundo está
arreglado; lo primero es la decisión que la maqueta pone sobre la mesa: **o la cámara se acerca, o las
criaturas son más corpulentas que un humano de proporciones reales**. Las proporciones de raza ya lo dicen
(humano 12 de ancho por 17 de alto: un cuerpo mucho más recio que un maniquí de 1,83 por 0,5).

### Segundo pack: el de fútbol (23 sep 2026, mismo día)

El revisor dejó después un **Soccer Game Pack de Mixamo** (`C:\Users\urban\Downloads\NewAnimations`) y
la maqueta pasa a usarlo: personaje `X Bot` con malla y **15 clips de fútbol de verdad** en
`Game/models/soccer/` — trote, carrera, chut, remate de cabeza, entrada, trompicón, cuerpo en el suelo,
levantarse, recepción, saque de banda, penalti y cuatro de portero.

**Los clips se aplican sin reorientar nada, y eso se midió antes de escribir el código** con una sonda
nueva (`Scenes/SondaModelo.tscn`, `Game/Screens/ModelProbe.cs`): personaje y clips traen los **mismos 65
huesos** `mixamorig_*` y las pistas apuntan a `Skeleton3D:mixamorig_…`, que es exactamente la jerarquía del
personaje. Se cargan una vez en una `AnimationLibrary` compartida y cada jugador la monta. Si no hubieran
coincidido, habría hecho falta reescribir las rutas o reorientar con `SkeletonProfileHumanoid`.

**La sonda se queda** porque el patrón se repite: dos veces con el pack anterior se perdió una ronda de
capturas de diez minutos por suponer lo que trae un fichero importado (el importador de glTF **quita el
sufijo `_Loop`** de los nombres; un modelo sin animación arrancada se queda en T).

Mapeo por estado de la traza (`StateAt`, el modelo no decide nada, RT-014): quieto → espera de campo o
**de portero** según el puesto · moviéndose → trote o carrera según la velocidad real, con el ritmo
escalado · disparando o pasando → chut · entrando → entrada · derribado o lesionado → trompicón y, cuando
su propio reloj acaba, cuerpo en el suelo en bucle.

**Lo que aún falta**: no hay **celebración** en el pack (se queda en la espera) y la **estirada del
portero** no está enganchada porque depende de un evento —una parada— y no del estado del jugador, que es
lo único que la maqueta mira. El pack de Quaternius se conserva **sin usar**: trae un juego `Zombie_*`
completo que le vendrá bien a los no-muertos.

**Y la pregunta de skins, contestada al revisor**: Mixamo tiene un **auto-rigger** que engancha cualquier
malla humanoide a este mismo esqueleto, así que el personaje es sustituible sin tocar código —la tabla de
clips es lo único que sabe de ficheros—. Fuentes CC0 que encajan con las razas: KayKit (esqueletos, orcos),
Quaternius, Kenney. **Aviso de licencia**: el material de Mixamo se usa dentro de un juego pero no se
redistribuye como asset; para enviar en Steam, CC0 es el camino sin ataduras y esto es un marcador de
posición.

---

## ADR 0137 — Conducir es un compromiso (23 sep 2026, decisión del revisor)

El revisor, jugando: *«el balón no va en sus pies»* y después *«debería existir conducción con duración
porque si no el regate tampoco entra en juego»*. **Tenía razón, y se midió antes de tocar nada**: el regate
ocurría **0,85 veces por partido** (contra 8,6 tiros, 12,5 entradas, ~43 pases) y **no estaba instrumentado
en ninguna parte** — ni métrica en `/Balance`, ni campo en el informe, y `MatchLogView` lo omite. La
instrumentación nueva es `Sim.Tests/Balance/DribbleMeasurementTests.cs`.

**El cambio es pequeño porque el patrón ya existía**: `PlayerState.Dribbling` ya movía al portador con el
balón: lo que faltaba era la **duración**. Se entraba con `EnterState(Dribbling, 0)` y, siendo un estado de
decisión, el portador volvía a decidir cada 2 ticks, donde la utilidad prefería el pase. Ahora entra con
`dribble.driveTicks` y la puerta de decisión respeta el contador —**comprobado inerte** para los otros dos
estados de decisión, que entran con 0 en las dieciocho llamadas del motor— con un invariante nuevo:
**conducir exige llevar el balón**, y si te lo quitan el estado se corta en el acto.

**Medido en seis configuraciones y cinco pasadas de las 43 puertas** (tabla completa en la ADR):

| | base | enviado (`driveTicks` 12) |
|---|---|---|
| regates intentados por partido | 0,85 | **2,69** |
| ticks conduciendo | 3,31 % | **9,02 %** |
| duración de una conducción | 5,45 t (0,36 s) | **15,4 t (1,03 s)** |
| puertas rojas | 2 | **3** |

**El fútbol no se mueve**: lote de 2.000 × 2 semillas contra línea base propia, ninguna métrica fuera de
banda y ninguna cambia de estado (`possessionChanges` 21,70 · `passChainAvgLength` 1,99 · `shotsPerMatch`
8,61 · `injuriesPerMatch` 0,72). 1.175 tests en verde, determinismo incluido.

**Tres cosas medidas que refutan ideas propias**, y valen más que el cambio:

1. **La duración proporcional a la técnica no arregla nada.** Parecía la solución elegante —que la
   conducción *diferenciara* en vez de aplanar— y da **5 rojas** en vez de 3. El término se queda en `/data`
   con cuota 0, inerte y comprobado cifra por cifra.
2. **«El mejor equipo gana menos» no replica**: la semilla 1 decía −4,50 y la semilla 7 dice +1,80. REJECTED.
3. **La hipótesis del derribo del duelo es falsa**: bajar `lostKnockdownTicks` de 6 a 3 deja
   `orc_violence` en **57,16, idéntico**. No participa.

**Queda abierto para el revisor**: `coherentBuildsBeatNone_orc_violence` (57,16 contra mínimo 58) rompe en
**las cinco dosis**, así que es la mecánica y no la calibración, y **sin explicación medida todavía**; y
`badBuildsLoseToNone_elf_out_of_zone` (46,04 contra techo 45). A cambio, la conducción **arregla**
`orc_misplaced` y mejora `elf_brawler` (47,01 → 45,55). Decisión: compensar de otro modo o mover esas dos
bandas con los datos de la ADR (RT-057).

**Aviso que vale más que este paquete**: varias puertas están **a distancia de ruido** de su umbral
—doctrinas falló por 0,09, `ordinaryDefeatRateAct1` por 0,04, `orc_violence` por 0,06 en una dosis—. Leer
una sola pasada como «esto rompe X» es sobreinterpretar.

## Tres defectos reportados jugando, arreglados y medidos (24 sep 2026)

El revisor probó la build y reportó tres cosas. Las tres están cerradas, con ficha, enmienda de ADR y test
permanente; ninguna se arregló antes de medir.

| ficha | síntoma | causa real | estado |
|---|---|---|---|
| [BI-E](./pendientes/BI-E.md) | ping-pong de cabezazos en el centro del campo | el cabezazo conservaba la altura y empujaba hacia ARRIBA, y los dos equipos se cancelaban | cerrada · cadena 80 → 2 |
| [BC-A](./pendientes/BC-A.md) | el goleador se queda en campo rival mientras el rival saca de centro | `ResetPositions` teletransportaba, y BB-C había tenido que eximir al que celebra | cerrada · 85,7 % → **0,0 %** |
| [BI-F](./pendientes/BI-F.md) | el saque de puerta se lo cabecean de vuelta en la bota del portero | el duelo aéreo se disputaba también **en la subida**, contra lo que la propia ADR 0139 §4 decía | cerrada · vuelo truncado 1,07 → 1,40 |

**En BI-F la hipótesis del revisor sobre el mecanismo era la equivocada y el síntoma que describía era
exacto.** Decía «el portero no le da suficiente vuelo»; medido, el portero sí levanta el balón (mediana
1,33). Lo que pasaba es que nadie le dejaba despegar. Es el caso de manual para la Regla A: la hipótesis
natural habría llevado a subir un número que ya estaba bien.

### El reloj del partido se separa del tick del motor

Decisión del revisor, textual: *«en todas las paradas de juego debemos dar tiempo para que los jugadores se
reposicionen de manera natural, sin teletransportes. No me importa que se alargue el tiempo de gameplay (el
reloj del partido seguiría parado)»*. Enmienda a la **ADR 0143**.

**Consecuencia medida**: el tiempo muerto ya no se descuenta de `regulationTicks`, así que un partido
contiene **más fútbol real**. Lote de 2.000 partidos contra línea base propia: `shotsPerMatch` 8,30 →
**9,91** (IN), `goalsPerMatch` 2,14 → **2,55**, `possessionChanges` 22,64 → **24,81** (IN).

**Y una métrica se sale, y se deja salida a propósito: `tacklesPerMatch` 6,86 → 5,69**, por debajo de su
suelo RT-056 de 6,00. No es ruido (falla en las semillas 1 y 13, pasa en la 7) y **no la causa ninguna de
las piezas que uno esperaría**. Aislado apagando cada cambio, 2.000 partidos cada uno:

| configuración | `tacklesPerMatch` |
|---|---|
| línea base | **6,86** |
| todo puesto | **5,69** |
| sin el duelo aéreo sólo-en-bajada | 5,50 |
| sin vaciar el área del saque de puerta | 5,68 |
| **sin parar el reloj** | **4,68** |

Lo causa **la reposición andando en sí**, y la explicación es legible: un equipo que se recoloca de verdad
tras un gol **presiona menos** que uno teletransportado a casa que luego deriva hacia el balón durante la
cuenta atrás. Es el comportamiento pedido, con su precio.

**Tres cosas que el revisor tiene que decidir, las tres con número y ninguna tocada:**

1. **`tacklesPerMatch` fuera de banda.** O se acepta un fútbol con menos entradas y se mueve el suelo con
   datos (RT-057), o se decide que la reposición debe dejar a alguien adelantado. Mover el suelo para poner
   la puerta en verde sin decidirlo sería exactamente lo que se retiró del despeje en este mismo trabajo.
2. **El partido dura ~115 s de reloj de pared** contra los **60-90 s** de `docs/requisitos.md`. Lo autorizó
   expresamente, así que no se ha tocado; la palanca es `regulationTicks`, y bajarla es decisión de
   requisitos.
3. **Abrir el paquete de [BI-G](./pendientes/BI-G.md)** (métrica de entradas ganadas con banda RT-056 + ADR).

### Dos trampas que costaron caro, escritas para no repetirlas

- **Partir una función en dos perdió una guarda.** `ResetPositions` → `PlaceEveryoneHome` /
  `SendEveryoneHome` se dejó por el camino `!player.OnPitch`, lo que colocaba en el campo, **desde el tick
  0**, al suplente de una sustitución programada (ADR 0094). El partido divergía entero y cuatro tests de
  run se caían con «la sustitución no es legal». Antes de dar con ello se gastaron **dos hipótesis
  razonadas y ninguna medida**, las dos REJECTED. Lo resolvió leer el diff buscando *qué cambia el partido
  en el tick 0*, que era la única clase de causa compatible con el síntoma.
- **Una métrica puede quedarse con una premisa caducada.** `ballThirdMaxShare` contaba el balón parado a
  propósito «porque durante las reanudaciones el reloj sigue». Al parar el reloj, la métrica leía una
  **parada** como territorio: 40,94 → 52,74, fuera de banda, sin que el juego se hubiera concentrado en
  ningún tercio. Arreglado el instrumento, **ninguna métrica queda fuera**.

### Hallazgo nuevo, abierto: [BI-G](./pendientes/BI-G.md)

Al remedir se destapó que **el protocolo de balance mide los perks de entrada con una métrica que no puede
responder**: `tacklesPerMatch` cuenta entradas **intentadas** (`_report.Tackles++` es incondicional) y
`PerkBalanceClassifier` la usa como métrica primaria de `ProbabilityKind.Tackle`, que sólo decide las
**ganadas**. Medido: idéntica —10,10— con el parámetro en 5, 15, 30, 40 y 60, y lo mismo a 300 plantillas
que a 60. El test que lo cubría estaba en verde **por un empate**, no por una demostración. Arreglarlo pide
llevar `TacklesWon` a `MatchSummary`, una banda RT-056 y su ADR: no cabía en un arreglo de reanudación.

Es el tercer caso en dos días del mismo patrón (con BC-D y la ADR 0146): *el texto promete lo que el dato o
el código no hacen*.

### Las puertas, como foto

**7 rojas contra las 5 que ya traía `main`** (la curva de jefes, tres de `BuildGateTests` y
`RaceBalanceTests`). De las cinco heredadas, **cuatro mejoran** —`undead_none` 62,38 → 60,58,
`elf_out_of_zone` vuelve a banda, `elf_brawler` 47,47 → 45,18, `orc_misplaced` 46,90 → 45,10— y **la nueva
es una sola métrica**, `tacklesPerMatch`, que arrastra sus dos tests. Está explicada y aislada arriba, y se
deja roja a propósito: es la decisión 1.

Ojo con una herencia: `RaceBalanceTests` **no estaba roja en la foto del pass** y sí lo está en `main` desde
el arreglo de BI-E — anotado ahí como LIKELY, sin experimento que lo aísle.

## El balón parado como fase posicional — diseño cerrado, implementación pendiente (24 sep 2026)

El revisor encargó generalizar la reposición de las reanudaciones. **La nota de diseño está hecha y
aprobada**: `docs/plan-balon-parado-posicional.md`, con las diez preguntas de `game-design-review`, el
análisis de cómo se coloca un equipo de verdad en cada reanudación y la conversión a la cuadrícula
(1 columna ≈ 6,6 m, 1 fila ≈ 9,7 m; la barrera de 9,15 m son **1,4 columnas**).

**Lo decidido por el revisor**: la pausa es **graduada**, no binaria — centro, córner y saque de puerta
largas; falta de tiro media; banda corta pero no cero. Y el motivo es **legibilidad**, no fidelidad: *«dar
tiempo a asimilar que ha pasado algo… viendo el gameplay hay veces que no te enteras qué pasa»*. La
**colocación** sí sigue el fútbol: el saque de banda no recoloca a nadie.

**Hallazgo de dato que sale del análisis**: `restartClearanceCells` vale 2,0 casillas para las cinco
reanudaciones por igual, o sea **13 m en horizontal y 19 m en vertical** — más que la barrera real en los
dos ejes y **seis veces** la distancia de un saque de banda. No representa ninguna regla del fútbol.

### HECHO (24 sep 2026, madrugada) — ADR 0147 implementada y publicada

`tacklesPerMatch` **5,69 fuera → 8,79 IN**, que era lo que motivaba todo, y **ninguna métrica de partido
fuera de banda**. Pausa graduada por tipo, casilla de saque de centro comprimida hacia el medio campo, y
enfriamientos/energía corriendo mientras uno se recoloca.

**Dos cosas publicadas ROTAS y anotadas, a propósito:**

1. **El partido es bastante más violento**: faltas 4,73 → **7,96**, entradas sin balón 2,45 → **4,62**,
   `injuriesPerMatch` 0,55 → **0,82 contra un techo de 0,90**. Atribuí dos tercios a la compresión del saque
   de centro y **lo medí**: con `kickoffPushCells` en 1,5 la violencia **no baja, sube** (faltas 8,08,
   lesiones 0,90 justo en el techo). **Hipótesis REJECTED**; el dial sigue sin encontrar. Por dónde seguir:
   el **enfriamiento en balón parado**, que es el mecanismo anticipado en la nota de diseño y nunca aislado.
2. **`FullRunGateTests…NeverAllOfThem` rota**: 0 → 0,186, y **RF-114k dice que nunca caben los cuatro
   sumideros**. LIKELY: el partido es más caótico (`betterTeamWinRate` 88,29 → 80,78), el jugador gana menos
   y los rerolls salen más baratos. Sin aislar.

Puertas: **6 rojas contra las 7** que traía `main`; se arreglan las dos de `tacklesPerMatch` y mejoran
cuatro heredadas.

## BI-H, segunda pasada: los saques y la parada atrapada (25 sep 2026)

Cierra los tres bloqueos de la primera pasada, y **dos eran falsos**.

- **`gk_catch` no necesitaba nada de `/Sim`**: el evento `Save` ya traía la respuesta en `Detail` —`held`
  es atraparla, `parried`/`corner`/`penalty` es estirarse a despejarla—. La primera pasada lo dio por
  bloqueado **sin haber mirado el `Detail`**.
- **Los saques sí, y en dos piezas.** `RestartKind` se muda de dentro de `MatchEngine` a `MatchPhase.cs` y
  se hace público (es un estado del partido, no un detalle del motor), y se graba en la traza como un byte
  por fotograma. Y se expone el **sacador**: el primer intento disparaba el gesto sobre el dueño del balón,
  y medido resultó que **durante la reanudación el balón no tiene dueño en ningún fotograma** (160f/0,
  180f/0, 209f/0). El motor sí lo sabía (`_restartTaker`). Verificado después: **100 % de los fotogramas de
  reanudación con sacador identificado**.
- **Un fallo de precedencia, demostrado sin ambigüedad**: la recepción estaba antes que los eventos, y una
  parada atrapada *es* el portero haciéndose dueño de un balón en vuelo. `Receive` 36 → **32**, `Catch` 0 →
  **4**. **La suma se conserva**: los cuatro `held` estaban escondidos dentro de las recepciones.

Estado del alcance: recepción, conducción, pase, tiro, cabezazo, despeje alto, parada rechazada y parada
atrapada **se disparan y están medidos**. `throwin` y `penalty` quedan enganchados y **sin evidencia de
activación** —el partido medido no tiene ni saque de banda ni penalti—, que no es lo mismo que «no
funcionan».

Lo que falta es **verlo en movimiento**: si el remate se lee como remate no lo dice ninguna métrica.

---

## BI-H, primera pasada: el balón sale del hueso con el que se juega (25 sep 2026)

Encargo del revisor, ampliado dos veces: la interacción visual con el balón debe cubrir **todo** contacto y
**no sólo los pies** — pies, cuerpo, cabeza y manos. Presentación pura en `/Game`, sin tocar la posición
que decide `/Sim` (RT-014).

**Qué hay hecho**: el ancla deja de ser un offset inventado (`radio × 0,55` en la dirección de carrera) y
pasa a ser **el hueso** con el que se está jugando, leído del `Skeleton3D` **ya animado** —así el punto es
coherente con el clip en curso sin anotar nada en los clips—. Cada parte lleva hueso izquierdo y derecho y
se elige **el más cercano al balón**: eso basta para que el golpeo salga del pie que toca, y cuesta una
comparación de distancias en vez de un sistema de IK. Los gestos (`receive`, `header`, `gk_save`,
`penalty`) salen de los **eventos**, porque `/Sim` no tiene estados de recibir, rematar ni parar.

**Una frontera que estaba a medias**: la vista 3D no recibía los eventos, aunque RT-014 diga literalmente
que *el render consume eventos*. `MatchScreen` ya los tenía y no se los pasaba, y resultó que hay **dos**
pantallas que montan esa vista.

**Lo que destapó la medición, y es la parte que importa**: el primer volcado de `DebugContacts()` dio
**«gestos: NINGUNO»** en un partido entero. Los siete clips estaban enganchados y no se disparaba ninguno.
Tres defectos —una guarda mal colocada, `MatchEvent.Actor` es el **id** y no el índice de la traza, y la
segunda pantalla sin cablear— más un cuarto de diseño: **un gesto nacido de un evento dura un tick**, y el
fotograma siguiente el estado lo cortaba. Una captura no enseña nada de eso: a esa distancia de cámara los
modelos ocupan unos píxeles.

Tras arreglarlo: **`Header` 2 · `Receive` 36 · `Save` 1** por partido, ancla `Feet` 433 · `Hands` 59
fotogramas, y la separación del balón al pie **0,1 casillas** (el objetivo de la captura era ~0,175).

**Bloqueado, y es de `/Sim`**: los **saques** no se pueden enganchar porque `MatchPhase` sólo tiene un
`Restart` genérico y la traza no lleva el tipo de reanudación. El clip `throwin` sigue cargado sin usar, y
`gk_catch` igual (ningún evento distingue atrapar de despejar). Exponer el tipo de reanudación es cambio de
`/Sim` y va en su propio commit.

---

## Decisión del revisor: el balance de winrate espera (25 sep 2026)

> *«Vamos primero a solucionar el balanceo total y luego ya iremos con los balanceos de winrate. El gameplay
> todavía debe evolucionar.»*

**No se retoman las puertas de `BuildGateTests` ni las bandas de winrate** —`elf_out_of_zone`, CAT-J con sus
cuatro métricas, `orc_violence`— hasta que el gameplay deje de moverse. Quedan documentadas con su medición
y su etiqueta, que es justo para lo que sirven: afinar un winrate contra un motor que va a cambiar es medir
el árbol equivocado, y este proyecto ya ha pagado dos veces por eso (la línea base de la ADR 0137 y la foto
de puertas de la ADR 0147).

Lo que **sí** sigue valiendo de ese trabajo es lo que no es winrate: el denominador roto de «1.200 ticks»,
que las conducciones se han encogido 3,6 veces, y que se decide conducir un 44 % menos.

---

## Conducción cerrada: tres dosis, con su error típico delante (25 sep 2026)

El revisor: *«ponte entonces primero a cerrar conducción»*. Se cerró **remidiendo**, no leyendo la ADR: el
balón parado posicional entró entre medias, así que lo que la ADR 0137 dejó «pendiente de decisión» ya no
era medible desde aquel árbol.

**Lección del paquete, y va primero porque es la que se repite**: la primera versión de este cierre midió
las catorce celdas —el acierto— y luego **leyó sus medias como si no tuvieran error**. La revisión
independiente lo tumbó, y tenía razón: existía una medición de coste cero capaz de discriminar (la
dispersión entre semillas, que `CatJSeedDispersionTests` ya hacía) y no se corrió. **Tres de las cuatro
conclusiones no sobrevivieron al error típico.** Regla A, incumplida por el mismo que la cita.

| celda | 0 | **12** | 18 | Δ(0→12) | ET | σ |
|---|---|---|---|---|---|---|
| `badBuildsLoseToNone_elf_out_of_zone` | 42,45 | **45,42** ❌ | 45,86 ❌ | **+2,97** | 0,60 | **+4,9** |
| `coherentBuildsBeatNone_human_counter` | 76,82 | 78,18 | 78,57 | +1,35 | 0,45 | **+3,0** |
| `badBuildsLoseToNone_human_scattered` | 31,48 | 29,90 | 32,14 | **−1,59** | 0,74 | **−2,1** |
| `badBuildsLoseToNone_elf_brawler` | 43,62 | 44,38 | 45,68 ❌ | +0,76 | 0,97 | +0,8 |
| `badBuildsLoseToNone_orc_misplaced` | 44,35 | 44,90 | 45,31 ❌ | +0,55 | 1,11 | +0,5 |
| `coherentBuildsBeatNone_orc_violence` | 57,50 ❌ | 57,24 ❌ | 57,53 ❌ | **−0,26** | 1,00 | −0,3 |

1. **`orc_violence` no es de la conducción — LIKELY, no CONFIRMED.** Plana en las tres dosis (Δ 0→18 =
   +0,03), cuando el mecanismo que la ADR 0137 propuso exigiría que creciera. El contraste **no tiene
   potencia** para rechazar el «~1 punto» que aquella afirmaba; lo que cierra el caso es la planitud más un
   tratamiento mucho mayor que tampoco la movió: la ADR 0147 subió las faltas **+68 %** y la celda pasó de
   57,16 a 57,24. Y no está establecidamente roja en ninguna dosis (57,50 contra 58 son 0,5 ET). Pasa a
   [CAT-J](./pendientes/CAT-J.md).
2. **«Las builds malas pierden menos cuanto más se conduce» — REJECTED, y era el titular.** Sólo
   `elf_out_of_zone` pasa de 2 σ; dos están bajo el ruido y la cuarta va **al revés y significativa**. Entre
   las coherentes suben dos con señal. La conducción mueve celdas en las dos direcciones sin patrón por
   familia; el mecanismo queda **sin demostrar**.
3. **La dosis se queda en 12.** La 18 restaura exactamente la conducción de la ADR 0137 (15,23 t contra
   15,4 t) y su riesgo (35,8 % cortadas), pero sube con señal dos celdas de builds malas (+5,1 σ, +2,4 σ).
   La 0 mata un perk: la conducción **resucita `crowd_control`**, de 1,80 % a 13,15 %.
4. **Un denominador roto en todo el proyecto**: los porcentajes «de 1.200 ticks» se calculan sobre un
   literal, y desde la ADR 0147 un partido dura **1.825,8 fotogramas** de motor. Todo porcentaje así está
   inflado un 52 %. La cuota real de conducción no es 3,84 % sino **2,52 %**, contra el 9,02 % de la ADR
   0137: se ha encogido **3,6 veces**.
5. **Y no es porque la corten**: el 75,7 % de las conducciones agota su contador con el balón en los pies.
   Son dos caídas — hoy se **decide conducir un 44 % menos** (4,1 contra 7,3 por partido, las dos con el
   parámetro a 0: mismo ajuste, distinto árbol) y las rachas ya no se **reencadenan**.

**No se ha tocado `/Sim` ni `/data`**: el paquete es instrumentación en `/Sim.Tests`, los tres volcados en
`docs/balance/bi-d/` y documentación.

**Queda abierto, y es del revisor**: `elf_out_of_zone` = 45,42 contra un techo de 45, causa CONFIRMED y
mecanismo sin demostrar. **No se movió la banda** (precedente CAT-J, y moverla abriría la puerta a la dosis
18). La pregunta es de diseño: *¿puede una vía de ganar que no consulta los perks erosionar el principio de
que construir mal sale peor que no construir?* — y se le entrega con el efecto que aguanta el muestreo
(+2,97, 4,9 σ), no con la distancia al techo (0,42, que con ET 0,64 es ruido con signo).

**Y el hallazgo grande que no tiene dueño**: por qué se decide conducir un 44 % menos que hace dos días. El
hermano a mirar primero es **`Shielding`**, gemelo exacto de `Dribbling` desde la ADR 0137, **sin ninguna
instrumentación**, y que compite por las mismas decisiones del portador.

---

### Siguiente paso concreto (sesión limpia)

**Decidir las dos de arriba con el revisor.** [BI-D](./pendientes/BI-D.md) ya está **cerrada** (25 sep,
sección de arriba), así que lo que queda de la cola es [BI-H](./pendientes/BI-H.md): la interacción visual
con el balón, que **debe cubrir también tiro, parada y saques** —todo lo que implique contacto— y, desde el
25 sep, **anclarse a la parte del cuerpo que toca**: pies, cuerpo, cabeza y manos, no sólo los pies.

Y ojo a un dato de BI-D que **le afecta directamente**: la conducción es hoy el **2,52 %** de los ticks del
partido, no el 9,02 % que midió la ADR 0137. Sigue habiendo poco que lucir conduciendo, y ahora se sabe que
son dos caídas (se decide conducir un 44 % menos y las rachas ya no se reencadenan) y que subir la dosis
**no** es la salida.

*(La descripción del paquete que sigue quedó cumplida; se deja por su contexto.)*

**Implementar el paquete.** Arrancar leyendo sólo `docs/plan-balon-parado-posicional.md` (se basta) y
`docs/decisiones/0143-el-balon-parado-es-una-jugada.md`, que es la ADR que se enmienda. Orden:

1. **`architecture-review` antes de escribir código** — es primitiva de motor y las plantillas de
   colocación van en `/data` por RT-031, así que hay frontera que revisar.
2. **ADR que enmienda RF-050, RF-053 y RF-054.** Son requisitos funcionales, no rangos de balance, y lo que
   se ha decidido los contradice: RF-053 dice que las reanudaciones son instantáneas y no paran el reloj, y
   RF-054 que sólo paran el partido el penalti y la roja. Sin esa ADR no se toca `docs/requisitos.md`
   (RT-057).
3. Implementar, medir `tacklesPerMatch` (que es lo que motivó todo: 5,69 contra suelo 6,00) y **vigilar el
   presupuesto de reloj de pared**: estimado ~135 s, y el que más pesa es el **saque de puerta** (~7 por
   partido × pausa larga ≈ 315 ticks), no el córner.
4. Arreglar de paso que durante la espera no corran energía ni enfriamientos, con el principio que lo
   ordena: **lo físico sigue el reloj de pared, lo que es disputa sigue el reloj del partido**. Ojo al
   precio: abarata el cansancio (ADR 0142) y la nota de diseño deja tres salidas planteadas, ninguna
   decidida.

**Después, y en este orden**: [BI-D](./pendientes/BI-D.md) (alargar la conducción, **ya aprobado por el
revisor**) y luego [BI-H](./pendientes/BI-H.md) (la interacción visual jugador-balón, encargo en cola, con
el alcance ampliado el 25 sep a **pies, cuerpo, cabeza y manos según el tipo de contacto**). Ese orden no es
casual: BI-H es pulir cómo se ve jugar el balón, y hoy lo tiene dueño el 32,3 % del partido en posesiones de
0,33 s — no hay casi nada que lucir hasta que BI-D esté.

**Y dos decisiones de balance siguen esperando**, las dos con número: si `regulationTicks` baja para
devolver el partido a los 60-90 s de RF-050, y si se abre [BI-G](./pendientes/BI-G.md).

---

### Siguiente paso concreto (sesión limpia)

Leer `docs/decisiones/0137-conduccion-con-duracion.md` y `docs/pendientes/BI-D.md`. Decidir las dos puertas
abiertas. Si se quiere entender `orc_violence` antes de decidir, el camino es `gameplay-debug` sobre el
escenario de esa puerta (`BuildGateTests`), porque la causa **no** es el derribo del duelo.
