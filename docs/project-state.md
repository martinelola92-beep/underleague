# Estado del proyecto

Editado a mano, no generado (V2/V3 de la auditoría de organización, `docs/analisis/auditoria-organizacion-v2.md`).
Se actualiza al cerrar un hito o una ADR. Contiene solo decisiones vigentes, sistemas activos,
bloqueantes y trabajo en curso — nada que ya se pueda derivar de `git log` o de `docs/decisiones/`.

PC (Steam), premium, sin online. **Estado (8 sep 2026): fases 0 y 1 cerradas; fase 2 implementada y medida —bucle de run completo, tres jefes con modificadores, economía y `--full-runs` con tres políticas automáticas—, con la curva de puertas de la ADR 0033 en verde y cuatro decisiones abiertas antes de darla por cerrada (`docs/balance/fase2-resultados.md` §7). Pantalla de Equipo funcionando en Godot; build jugable exportable a Windows desde WSL (`tools/export-windows.sh`). Primera partida del revisor sobre esa build íntegramente procesada: veinte anotaciones (AW-A..AW-T en `docs/pendientes.md`), todas cerradas — intercepción del disparo tick a tick (`docs/plan-intercepcion-disparo.md`), techo de la línea defensiva, persecución del balón como precondición dura, reposicionamiento durante el balón muerto — con cuatro ADR de banda derivadas (0081-0084) y AV-B cerrada (ADR 0085: el valor de un perk se lee al horizonte y la run no lo nota). AT-A cerrada (ADR 0086). **Paquete AY (9 sep, decisión del revisor): ningún perk es negativo** —la rama `else` no hace nada (ADR 0088), la muerte solo es consecuencia de una entrada y nunca del saque (regla del cargador), el valor de un perk se mide contra su control emparejado (ADR 0087: `rowDeviation` 19 → 7, ningún perk por debajo de −7) y las puertas de fase 1 arrancan en 50 (45-55). Medido el paquete completo: `runWinRate` 18,4 / 17,8 (la muerte en la entrada cuesta ~3 puntos; AY-B, se ajusta tras AZ) con `deathsPerRun` en banda. Segunda partida del revisor (AZ, `docs/plan-segunda-partida.md`): tandas 1 y 2 cerradas —el saque ya no «va y vuelve», solo ante el portero se tira, la falta señalada reanuda con saque de falta, el árbitro señala el 80 % (ADR 0090) y la clínica cura la leve—; tanda 3 (física del pase, ADR 0091: pase al pie, intercepción con geometría, el pasillo puntúa, pase en profundidad más hondo y el balón que se para donde cae) implementada y medida; las razas recalibradas para esa física (ADR 0092: sesgos de suma cero, abanico 21,8 → 6,4). Con las razas nuevas los tiros (8,1) y `human_random` vuelven a banda; las décimas que quedaban son bandas por ADR 0093 (`ballThirdMaxShare` ≤ 52, `betterTeamWinRate` 70-90, `grimhold_guns` incoherente 15-40, peldaño plano de 3 puntos en la escalera de jefes). Puertas en verde. Tanda 4 (AZ-F): la sustitución forzada es parte del estado inicial del partido (ADR 0094, sin motor reanudable), con ventana en `/Game` y el rival sustituyendo también. **Paquete AZ cerrado.** Con él cerrado, `runWinRate` valía 14,5 y el 92 % de las derrotas eran un jefe: la run llegaba a cada jefe **un nivel por debajo** del que mide la puerta de la ADR 0033 (fila nueva `levelAtBossActN`: 3,79 / 5,80 / 6,37 contra 5 / 6 / 7) y las muertes habían subido a 2,62 sin decisión. **ADR 0095** (`lethalChance` 1.950/1.500 → 1.200/900, `matchExperience` 100 → 140) cierra AY-B y pone `runWinRate` **en banda por primera vez: 22,17 ± 0,60** con 4.800 runs (2.400 × 2 semillas), el suelo a 3,6 errores típicos con `deathsPerRun` 1,96 / 1,85 y las 43 puertas en verde. **ADR 0096** aplica la tercera palanca de la ADR 0055 (la liga paga oro en vez de elección, la contextual no gasta un slot en un común, los sumideros suben): ganar sin pasar por el mercado cae de 18,3 a **10,7** y los maestros comprados suben de 31 % a **45 %**, sin sacar de banda nada que estuviera dentro (`runWinRate` 23,3 / 22,0). **ADR 0098** corrige el instrumento de esa métrica —el control «sin mercado» compraba cuando el mapa lo obligaba a entrar— y con ello **retira la contradicción** que la ADR 0096 anunciaba entre la ADR 0033 y la ADR 0055: no existía. La cifra real de ganar sin comprar es **7,83** y a la banda le faltan 2,8 puntos; la palanca que queda es que no comprar es en parte una estrategia (122 de oro sin gastar, la mitad de muertes, más veteranía). **ADR 0099 y 0100** (decisión del revisor, mismo paquete): la clínica pasa a tener tres servicios —por pieza, la plantilla entera a tarifa plana y el matasanos, que cobra el 40 % y puede matar con su porcentaje a la vista— y el nodo de evento deja de pagar 1-3 de oro para ser una **carta con opciones** en `data/events/`, con las familias del oro parado y de carne por ventaja. Medido: `runWinRate` 25,3 / 27,0, cartas resueltas 0,38 por run y `brokeMarketRunShare` dentro de banda en las dos semillas por primera vez. **ADR 0097** (decisión del revisor): la inscripción deja de ser un nodo y el hueco de plantilla se compra en el mercado —un tipo de nodo menos y guardado v2—, con `runWinRate` 25,3 y `brokeMarketRunShare` de 38 a 10,8, en el suelo de su banda por primera vez.

**Campo de siete filas (14 sep 2026, decisión del revisor, BA-C):** 16×7 en vez de 16×6 —con seis filas el centro geométrico cae *entre* dos filas y no existe fila central—, con guardado v4. Añadir la fila cuesta ~1,0 tiro y ~0,45 goles por partido y **ninguna palanca local lo recupera**, así que la **ADR 0109** recalibra la banda de tiros a la geometría vigente (8-16 → 7-15) en vez de tocar el motor, y deja escrito que ensanchar la formación o las zonas de acción destruye la profundidad de colocación. Nueve auditorías de la IA de jugadores (`docs/auditoria-ia-jugadores-1..9.md`) dejan **un solo cambio aplicado**, la **ADR 0110**: `Shoot` del defensa 77 → 154 y del centrocampista 188 → 237, porque con 77 un defensa colocado arriba nunca remataba —perdía contra su propio `ShortPass` de 500— y jugar fuera de posición costaba dos tercios del ataque (100/48/38 → 100/68/55). Rechazados y documentados: `ChaseBall pen` en todas las dosis (degrada la diferenciación de builds), el desfase de fase entre equipos (no replica entre semillas) y la histéresis. Queda **abierto** el papel del centrocampista: dispara el 6,5 % de los tiros siendo el 43 % de los jugadores de campo.
---

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
