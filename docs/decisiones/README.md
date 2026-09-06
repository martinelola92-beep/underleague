# Registro de decisiones (ADR)

Una decisión por fichero, numerada, nunca se borra: si cambia, se escribe una nueva que la sustituye y la antigua pasa a `Sustituida por NNNN`. Plantilla en `0000-plantilla.md`.

Cuándo escribir un ADR: cambio de librería o stack, frontera entre proyectos, cambio de un rango de `balance.md` (RT-057), nuevo tipo de efecto en el catálogo de perks, cualquier excepción a las "reglas sin excepción" de `CLAUDE.md` (que, por definición, requiere reescribirlas).

| Nº | Decisión | Estado |
|---|---|---|
| [0001](0001-godot-dotnet-csharp.md) | Godot 4.6 .NET con C# | Aceptada |
| [0002](0002-sim-independiente-de-godot.md) | `/Sim` como librería pura sin Godot | Aceptada |
| [0003](0003-ncalc-para-condiciones.md) | NCalc para condiciones de perks | Aceptada |
| [0004](0004-rng-propio-pcg32.md) | RNG propio PCG32 con flujos derivados | Aceptada |
| [0005](0005-ia-de-utilidad-sin-ml.md) | IA de utilidad ponderada, sin aprendizaje automático | Aceptada |
| [0006](0006-sin-ecs.md) | Sin ECS | Aceptada |
| [0007](0007-punto-fijo-aplazado.md) | Aritmética entera + float en posiciones; punto fijo aplazado | Aceptada |
| [0008](0008-net10-lts.md) | .NET 10 LTS como SDK y objetivo; `/Game` se confirma en fase 1 | Aceptada |
| [0009](0009-identificadores-en-ingles.md) | Identificadores en inglés, documentación en español | Aceptada |
| [0010](0010-rango-empates-reglamentario.md) | Rango de empates al final del reglamentario en RT-056 | **Propuesta: decisión del revisor** |
| [0012](0012-buildswindifferently-normalizada.md) | `buildsWinDifferently` normalizada contra la referencia de la raza | Propuesta |
| [0020](0020-cuerpos-y-ocupacion.md) | Cuerpos con volumen, separación blanda y empuje | Aceptada, pendiente de implementar |
| [0021](0021-adyacencia-estatica-y-proximidad.md) | Vínculos direccionales resueltos antes del partido y proximidad dinámica | Aceptada, pendiente de implementar |
| [0022](0022-roles-y-ocupacion-de-espacio.md) | Comportamiento sin balón por estado táctico, `FindSpace` y `PressCarrier` | Aceptada, pendiente de implementar |
| [0023](0023-perks-exclusivos-de-raza.md) | Perks universales por defecto y núcleo exclusivo por raza | Aceptada, pendiente de implementar |
| [0024](0024-etiquetas-de-estilo-individuales.md) | Etiqueta de estilo individual con sesgo racial (elfos mayormente `Fine`, pero existe el elfo `Brute`) | Aceptada, pendiente de implementar |
| [0025](0025-generacion-de-atributos.md) | Generación de atributos por raza, posición, rareza y estilo, con baremos por posición | Aceptada, con tensión abierta en el eje de rareza |
| [0026](0026-habilidades-raciales.md) | Habilidades raciales como perks de equipo | Aceptada, diseño pendiente del visto bueno del revisor |
| [0027](0027-rareza-frente-a-nivel.md) | Los legendarios son netamente superiores; común de nivel 8 ≈ legendario de nivel 2 | Aceptada. **Modifica RF-024** |
| [0028](0028-zona-de-accion.md) | La correa pasa de radio duro a zona de acción con forma por posición, tamaño por atributo y disciplina por raza | Aceptada. **Modifica RF-042 y RT-095** |
| [0029](0029-visualizacion-de-la-zona.md) | Zona y margen dibujados al colocar, mapa de cobertura del equipo y vínculos visibles | Aceptada. **Modifica RF-045** |
| [0030](0030-acciones-de-ataque-y-bloqueo.md) | Pase corto y largo como decisiones distintas, regate y tiro según el jugador, bloqueo sin balón y árbitro adelantado | Aceptada. **Matiza RF-057** |
| [0031](0031-correa-fuera-del-presupuesto.md) | La correa sale del presupuesto: su valor marginal medido es negativo | Aceptada. **Modifica ADR 0025 y 0028** |
| [0032](0032-metricas-de-comparacion-de-builds.md) | RT-055 y la progresión se miden contra una referencia equipada, no contra una plantilla desnuda | **Propuesta: decisión del revisor** |
| [0033](0033-los-jefes-como-puertas-de-build.md) | Los jefes son puertas de calidad de build y definen la curva de exigencia de la run | Aceptada. **Sustituye la 2.ª mitad de `scalingRewardsGoodBuilds`** |
| [0034](0034-alinear-con-lesion-grave.md) | Alinear a un lesionado grave es posible y su precio es el riesgo de muerte; falta decidir el coste inmediato | **Propuesta: decisión del revisor** |
| [0035](0035-escala-de-valores-por-canal.md) | Cada canal de probabilidad declara su propio escalón: un `+5` vale 13x en un canal y 1,06x en otro | **Retirada** por la ADR 0050 P1 (implementada). Resolvió D-30 mientras estuvo vigente |
| [0036](0036-que-aporta-el-equipamiento.md) | El objeto sube atributos según su rareza; el perk cambia reglas; el consumible es puntual | Aceptada |
| [0037](0037-la-economia-como-dificultad.md) | La escasez de oro es la palanca de dificultad; se mide enfrentando tres políticas de compra | Aceptada |
| [0038](0038-el-precio-como-palanca-de-balance.md) | Lo bueno se encarece en vez de debilitarse; y donde no hay precio, se hace menos frecuente | Aceptada |
| [0039](0039-legendarios-como-personajes.md) | Tres rarezas generables; los legendarios son personajes únicos que se desbloquean ganando divisiones | Aceptada. **Modifica RF-023 y RF-127** |
| [0040](0040-densidad-de-build-por-acto.md) | La curva de puertas se mide con la build que cabe en ese acto, no con la plantilla terminada | Aceptada. **Corrige la medición de la ADR 0033** |
| [0041](0041-lesiones-relativas-al-nivel.md) | La lesión se mide contra el rival: con niveles altos el sistema de desgaste se apagaba solo | Aceptada |
| [0042](0042-el-precio-eficiente-anula-la-decision.md) | Un mercado perfectamente tasado no premia el criterio: la ADR 0037 y la 0038 se anulan entre sí | **Propuesta: decisión del revisor** |
| [0043](0043-trampolin-y-desgaste-por-acto.md) | Cada acto tiene su función: taller, gestión y examen; el jefe es trampolín además de barrera | Aceptada. **Revisa la curva de la ADR 0033** |
| [0044](0044-escala-de-oro.md) | El oro se cuenta en decenas: la tensión económica exige que el jugador pueda hacer la cuenta | Aceptada |
| [0045](0045-por-que-el-desgaste-no-muerde.md) | El desgaste no mata porque siempre hay banquillo: plantilla más corta y perks letales | Aceptada, resuelta en la 0046 |
| [0046](0046-plantilla-corta-nodo-de-inscripcion-y-perks-letales.md) | Plantilla de diez, nodo de inscripción con coste creciente y perks letales escasos y telegrafiados | Aceptada. **Modifica RF-020 y RF-011** |
| [0047](0047-matar-al-rival.md) | Un perk letal propio no puede matar: el rival nunca entra herido y la lesión saca del campo | Sustituida por la 0048 |
| [0048](0048-morir-estando-sano.md) | Un jugador sano puede morir; el azar se sostiene con anticipación, evitación, reducción y recuperación | Aceptada. **Modifica RF-093** |
| [0049](0049-dos-opciones-de-recompensa.md) | La liga ofrece 1 de 2; élite 1 de 3 y jefe 2 de 3. Devuelve peso al mercado | Aceptada. **Modifica RF-071** |
| [0050](0050-fundamentos-matematicos.md) | Perks multiplicativos sobre cuotas, dos tiradas promediadas, curva de nivel más agresiva y límites únicos | Aceptada. **P1, P2 y P4 implementadas**; P3 en suspenso (ADR 0057). **Retira la ADR 0035** |
| [0051](0051-arcos-de-build-y-profundidad.md) | Perks maestros que exigen y excluyen, y profundidad nativa por acto | Aceptada |
| [0052](0052-la-agencia-esta-en-la-formacion.md) | El indicador de riesgo no reduce muertes con formación fija; y recortar opciones eliminó la decisión en vez de moverla | **Propuesta: decisión del revisor** |
| [0053](0053-mapa-de-cuatro-carriles.md) | El mapa se abre a cuatro carriles con divergencia y reconvergencia; los mercados dejan de ser cuello de botella | Aceptada |
| [0054](0054-banda-del-mejor-equipo.md) | `betterTeamWinRate` pasa de 65-80 a 70-88: la banda de fase 0 medía un motor que diluía la habilidad | Aceptada e **implementada** con la P1: medido 79,52, sin acercarse al techo |
| [0055](0055-el-mercado-es-imprescindible.md) | Ganar sin pasar por el mercado debe quedar por debajo del 5%: los maestros y el equipamiento solo se compran | Aceptada |
| [0056](0056-separacion-entre-perfiles.md) | Build buena al 60% en partidos de los actos 2 y 3; build mala por debajo del 2% de completar la run | Aceptada. P1 aplicada y **los cuatro objetivos siguen sin alcanzarse** (§26.7); P3 en suspenso |
| [0057](0057-el-suelo-sin-build.md) | Un equipo sin build gana el 14,5%: la palanca es el peso de la build, no el oro. La P3 queda en suspenso | Aceptada. Suspende la P3; **su palanca queda falsificada** por la medición de la P1 (§26.6) |
| [0058](0058-la-exigencia-la-pone-el-rival.md) | El techo de la escala depende de la rareza y la capa de build del rival crece con el acto | Aceptada e implementada (§27); **su propio criterio de falsificación se cumple**: el hueco no se abre |
| [0059](0059-separar-perfiles-pide-una-palanca-que-discrimine.md) | Primero el instrumento, luego el diagnóstico, y sólo entonces la palanca | Aceptada; puntos 1, 2 y 4 ejecutados, el **punto 3 falsificado** por la medición del 2 |
| [0060](0060-el-castigo-tiene-recorrido-donde-el-premio-no-lo-tiene.md) | El perk mal puesto lo paga el equipo y el techo de la rareza no acota el castigo | Aceptada e implementada (§28). **Primer paquete que abre el hueco entre perfiles**: 6,27 → 10,03 |
| [0061](0061-el-peso-de-los-atributos-no-es-un-grado-de-libertad.md) | El peso de los atributos frente a la build no discrimina entre perfiles: es el mismo número que la fuerza del rival | Aceptada. **Falsifica AL-B** con la medición; ningún número de balance se mueve |
| [0062](0062-recalibrar-la-cadena-de-pases.md) | `buildsWinDifferently_passChain` pasa de 1,30 a 1,11: se medía contra un canal que la escala de cuotas ya no puede saturar | Aceptada e implementada |
| [0063](0063-el-premio-lo-cobra-el-rival-el-castigo-no.md) | El premio del catálogo lo cobra también la oposición y el castigo no: por eso el castigo separa perfiles y su espejo es un mando de dificultad | Aceptada. **Falsifica AM-A como palanca**; ningún número de balance se mueve |
| [0064](0064-perder-un-partido-ordinario-cuesta-lo-mismo-a-las-tres-builds.md) | Encarecer la derrota ordinaria no separa perfiles: las tres builds pierden 4 partidos por run. La run es el **producto de las tres puertas de jefe** | Aceptada. **Falsifica la tercera salida de la ADR 0063**; ningún número de balance se mueve |
| [0065](0065-la-puerta-amplifica-el-hueco-no-lo-crea.md) | La calibración del jefe mueve a los dos perfiles a la vez: la puerta amplifica el hueco, no lo crea. Los objetivos 4 y 5 no son alcanzables a la vez ni revisando la tabla ni con una cuarta puerta | Aceptada. **Falsifica las dos salidas de AO-A**; ningún número de balance se mueve |
| [0066](0066-la-metrica-del-acto-mide-partidos-ordinarios.md) | `winRateAct{n}` mide partidos ordinarios, como su ADR dice; la cifra con jefe se publica al lado | Aceptada (decisión del revisor) e implementada. **Cierra AO-D**; el objetivo 1 pasa a estar alcanzado en el acto 2 |
| [0067](0067-la-dureza-de-un-acto-no-es-una-cuota.md) | La dureza del acto 1 se vigila con la tasa de derrota en partidos ordinarios (`ordinaryDefeatRateAct1 ≤ 30`), no con la cuota de derrotas de run | Aceptada e implementada. **Cierra AP-B** |
| [0068](0068-el-premio-que-la-run-enciende.md) | El contador (RF-070) es el único premio que la oposición no puede cobrar, y cubre el 45% de la separación que la frontera pide | Aceptada. **Primera medición en nueve paquetes en la que la razón de cuotas sube**; ningún número de balance se mueve |
| [0069](0069-el-eje-se-gasta-donde-la-tabla-no-mira.md) | El eje de acumulación sube al techo de rareza en seis perks; la tabla de la ADR 0033 sólo ve dos de ellos y el jefe final no puede recalibrarse, así que la dosis la pone el catálogo | Aceptada e implementada. **Run 17,00 → 19,42 con el suelo en 10,58**; ningún jefe se toca |

| [0070](0070-el-instrumento-media-un-catalogo-que-la-run-no-juega.md) | `--perk-values` mide en campaña de ocho partidos y las veinte builds se reescriben contra el catálogo que la run entrega | Aceptada e implementada. **Paquete de medición**: la run baja de 19,42 a 16,25 y se entrega así |
| [0071](0071-un-techo-que-acotaba-una-copia-y-no-la-linea.md) | El techo de un efecto con contador acota la **línea** `k^maxValue` y distingue el ámbito de equipo | Aceptada e implementada. **Cierra AR-B**; no cuesta nada |
| [0072](0072-un-slot-vale-lo-que-vale-lo-que-va-a-caber-en-el.md) | El listón de la doctrina contextual es el **coste de oportunidad medido** del slot, no una constante; el valor se corrige antes por el ruido de la tabla | Aceptada e implementada. **Cierra AS-A y AJ-D**. Run 16,25 → **20,33**, `contextualAdvantage` −0,25 → **+3,83**; ningún jefe ni banda se toca |
| [0073](0073-la-densidad-por-acto-no-estaba-mal-de-un-lado-solo.md) | La densidad por acto se remide con anclas medidas: los perks se habían ido en el juego, los objetos en el modelo | Aceptada e implementada. **Cierra AS-B**; las doce celdas en banda sin tocar ningún jefe |
| [0074](0074-una-muestra-que-no-resolvia-el-margen-con-el-que-juzgaba.md) | La muestra de la puerta de jefes se **deriva** (640 → 5.120 partidos por celda) y con ella una celda estaba fuera: `the_hunt` se recalibra de quality 46 a 44 | Aceptada e implementada. **Cierra AT-B**; la tabla, las bandas y el margen de ±2,5 no se tocan. Cuesta 1,33 de `contextualAdvantage` |
| [0075](0075-la-frontera-se-ha-movido-y-sigue-sin-caber.md) | La frontera de la ADR 0065 recalculada con la `S` de hoy: los objetivos 4 y 5 siguen sin caber, por 0,68 puntos en vez de 3,29 | Aceptada. Medición: no mueve ningún número. Abre **AU-B** (la puerta del acto 1 ha dejado de discriminar) |
| [0076](0076-la-puerta-del-acto-1-no-mide-lo-que-el-once-lleva.md) | `R₁` nunca se cayó: el 1,008 era un artefacto del banco de 1.200 runs. Con 7.272 vale **1,157** y **baja** si el acto 1 lleva más perks. El banco de cierre se deriva | Aceptada. Medición: no mueve ningún número. **Cierra AU-B en negativo**, corrige `S` a **0,936 ± 0,082** y la frontera a **12,60**; abre **AV-A** y **AV-B** |
| [0077](0077-un-numero-que-se-mide-a-si-mismo-y-por-que-converge.md) | Las tasas de paso que la política cree son un punto fijo: converge en **una** vuelta con `L ≤ 0,03` medido. `Act2GatePassPermille` 439 → **493**; la del acto 1 no se mueve | Aceptada e implementada. **Cierra AU-D**; ningún jefe ni banda se toca, coste no significativo |
| [0078](0078-una-build-al-azar-es-una-build-mala.md) | Una build al azar es una build mala: la banda 40-60 de `randomBuildNearNone` pasa a un techo de ≤ 45, el de las builds malas | Aceptada e implementada. **Cierra AU-A** y el techo de AL-D; el umbral se endurece, no se relaja |

El hueco del 0011 corresponde a una decisión sobre el radio de adyacencia que quedó absorbida por la 0021 antes de aceptarse.
| [0012](0012-buildswindifferently-normalizada.md) | `buildsWinDifferently` normalizada contra la referencia de la raza | **Propuesta: decisión del revisor** |
| [0020](0020-cuerpos-y-ocupacion.md) | Cuerpos con volumen, separación blanda y empuje | Aceptada |
| [0021](0021-adyacencia-estatica-y-proximidad.md) | Adyacencia resuelta antes del partido; proximidad dinámica aparte | Aceptada |
| [0022](0022-roles-y-ocupacion-de-espacio.md) | Comportamiento sin balón: contraste táctico y búsqueda de espacio | Aceptada |
