# 0116. El umbral de "equipar" se recalibra contra su propio error de medición

**Fecha:** 2026-09-16 (corregida cuatro veces el mismo día, tras cuatro rondas de `independent-reviewer`)
**Estado:** Aceptada e implementada (`Sim.Tests/Perks/EquipmentImpactTests.cs`); documentación pendiente
de una quinta pasada de confirmación.

**Aviso sobre el mensaje del commit `f1ce8b3`**: ese mensaje, ya publicado y sin editar (no se reescribe
historia publicada), conserva la redacción de la primera versión de esta ADR — atribuye la caída al
catálogo de perks, cita "1,7 medido" (en ese árbol la puerta mide 3,4; el número no se remidió al
escribir el mensaje) y deriva el umbral por un ratio inventado. **Esta ADR manda sobre ese mensaje.** Si
buscas por qué el umbral es 1,0 con `git log`/`git blame` y llegas primero al mensaje de `f1ce8b3`, la
respuesta correcta está aquí, no ahí.
**Decisión del revisor** (BA-N, `docs/pendientes/BA-N.md`). **Cambia el umbral de la puerta
`EquippingAGoodBuildIsWorthSeveralPointsOfWinRate`** (RT-057: cambio de rango, exige ADR). No modifica
ninguna ADR de precio de objeto (0038, 0086, 0087) ni ningún valor de `/data`.
**Requisitos:** RT-056, RT-057. Relacionado: ADR 0033 (el escalón "muy buena"), ADR 0038 (precio calculado
de objeto), ADR 0087 (valor de perk medido contra su control — esta puerta ya usa su patrón de
emparejamiento por semilla, ver "Instrumento", más abajo), ADR 0115 (el mecanismo real de por qué esta
puerta se mueve, sin atribuirlo a un commit concreto).

**Nota de corrección (cuatro rondas)**: la primera versión de esta ADR (escrita antes de pasar por
`independent-reviewer`) atribuía la caída del umbral al crecimiento del catálogo de perks (61→94) — esa
causa quedó **REJECTED** con una medición de seis minutos. La segunda versión corrigió esa causa pero
introdujo dos afirmaciones nuevas que una segunda revisión también refutó: que el cierre de la fuga del
penalti de BB-B (ADR 0115) causó el movimiento de esta puerta, y que la puerta necesitaría adoptar
comparación emparejada de la ADR 0087 (ya la tiene). La tercera revisión encontró, con `git log -G` sobre
la propia línea del umbral (no de memoria): que el "3,3 medido" que esta ADR atribuía a la elección de
2,0 pertenece en realidad a la elección **anterior** (3,0, con 24 plantillas en vez de 96 — corregido en
"Qué umbral existía"); que un comentario histórico del test seguía citando "768 partidos por brazo" con
el mismo error de no contar la vuelta que ya se había corregido dos líneas más abajo para otro número; y
que la sección "Instrumento" y `docs/pendientes/BB-P.md` prescribían un estadístico incorrecto (la RMS de
unas diferencias que el código ni siquiera calcula, en vez de `sd/√n`) para la mejora de instrumento que
sí falta. La cuarta revisión encontró, verificado de nuevo con `git show` sobre cada commit citado (no de memoria):
que la tabla de "Qué umbral existía" emparejaba el valor inicial (`a91c020`, umbral 5,0) con la medida
"5,4→4,7" que en realidad pertenece a un commit distinto (`b241611`, que solo subió `Rosters` de 8 a 24
sin tocar el umbral — invisible para un `git log -G` sobre la línea del `Assert`, que es como se había
reconstruido la tabla); que "el umbral ha bajado cuatro veces" era un error de cuenta repetido en cuatro
sitios (esta ADR dos veces, `docs/pendientes/BA-N.md`, `docs/project-state.md`) — son tres bajadas desde
un valor inicial que no es una bajada; y que esta ADR nunca comparó en coste la alternativa de subir
`Rosters` (el propio precedente del proyecto en las dos bajadas anteriores) frente a bajar el umbral,
antes de elegir la segunda. El umbral implementado (1,0) no ha cambiado en ninguna de las cuatro
correcciones — solo el razonamiento que lo sostiene.

**Lo que esta corrección deja escrito, y que las versiones anteriores no decían**: el umbral ha bajado
**tres veces** desde el 5,0 inicial (5,0 → 3,0 → 2,0 → 1,0, tres bajadas, no cuatro — el primer valor de
la lista es el punto de partida del test, no una bajada; tabla completa en "Qué umbral existía"), y **la
última bajada (2,0 → 1,0, esta ADR) es la única que no vino acompañada de más muestra** — las dos
anteriores (8→24, 24→96 plantillas) sí la tuvieron. Hoy, por las propias palabras de esta ADR, la puerta
protege "que equipar sigue haciendo algo", no el escalón fino que nombra la ADR 0033. Es un debilitamiento
real de esa garantía de diseño, no solo una recalibración de instrumento — y el paso por
`game-design-review` sobre si ese nivel de cobertura es aceptable no se hizo antes de aceptar esta ADR
(queda como pregunta abierta en `docs/pendientes/BB-P.md`, punto 5, pero la decisión de facto — "sí, es
aceptable" — ya está en producción).

## Qué umbral existía

`EquippingAGoodBuildIsWorthSeveralPointsOfWinRate` exigía que equipar a los siete titulares de una build
"buena" con un objeto cada uno (RF-076, mezcla de rarezas del acto 3) subiera la tasa de victoria **al
menos 2,0 puntos** frente al mismo equipo sin objetos, sobre 96 plantillas × 32 partidos × 2 direcciones
= 6.144 partidos por brazo. **El umbral ya se había bajado dos veces antes de esta ADR** (verificado con
`git log -G` sobre la línea del `Assert`, no de memoria — ese comando ve los cuatro commits que tocaron el
umbral, pero no ve `b241611`, que cambió la muestra sin tocar el umbral; por eso la fila inicial de abajo
no lleva una medida al lado, para no repetir el error de emparejar mal dos commits distintos que ya se
cometió y se corrigió en las otras dos filas):

| commit | `Rosters` | umbral | nota |
|---|---|---|---|
| `a91c020` (4 sep) | 8 | 5,0 | valor inicial del test, no una bajada; su propio comentario dice que es "deliberadamente bajo (la mitad de lo que se mide hoy)" — medido ~10, no un número que se haya intentado precisar aquí |
| `76ce1c4` (4 sep) | 24 | 3,0 | bajada 1; medido dan **3,3** |
| `54c6b38` (9 sep, ADR 0090) | 96 | 2,0 | bajada 2; medido **3,7** con 96 plantillas (remedido en worktree, quinta ronda) — el 3,0 que una versión anterior de esta fila citaba es la medida del instrumento **anterior** (24 plantillas, remedido igual: exactamente 3,0), tomada por error de la fila de `99a22c2` de la tabla de abajo |
| `f1ce8b3` (esta ADR) | 96 | 1,0 | bajada 3; ver más abajo — misma muestra que la bajada anterior, no una muestra mayor |

El "3,3" que una versión anterior de esta ADR citaba como la medida que llevó a elegir 2,0 pertenece en
realidad al paso anterior (24 plantillas, umbral 3,0). Una corrección posterior sustituyó ese número por
"3,0, ya con 96 plantillas" — el mismo error, un número distinto: el 3,0 vuelve a ser una medida del
instrumento de 24 plantillas (`git show 54c6b38 -- Sim.Tests/Perks/EquipmentImpactTests.cs` ya lo decía
en su propio comentario histórico, líneas 36-38 del fichero vigente: "el umbral de 3,0 quedaba dentro
del ruido (medido 3,0 justo tras la tanda 2)", refiriéndose a la etapa de 24 plantillas). El medido real
que llevó a elegir 2,0, con la muestra ya en 96 plantillas, es **3,7** (remedido, quinta ronda, worktree
sobre `54c6b38`). El test documentaba, desde que se fijó (paquete AZ), que el número no salía de una
fórmula, con la única afirmación de que "equipar VALE (varios puntos), no una cifra concreta".

## Por qué deja de ser apropiado — la causa real, no la del catálogo

BA-N registró la misma métrica en tres momentos sucesivos del catálogo de perks, **siempre con 96
plantillas** (fallaba <2,0 → 2,0 → 1,7 — ver la serie completa en `docs/pendientes/BA-N.md`), y esa caída
parecía coincidir con el crecimiento de 61 a 94 perks. Esa lectura era razonable a primera vista, pero
**falsa**: el `independent-reviewer` congeló el catálogo de perks (mismo `/data/perks/` verificado byte a
byte en cinco commits distintos, todos con 96 plantillas) y midió la misma puerta en cada uno:

| commit | qué cambió (nunca perks/objetos) | medido |
|---|---|---|
| `ab129d7` | donde se escribió BA-N | 1,7 |
| `96234de` | fix de build (BB-J) | 2,1 |
| `ad3c472` | revert de la inmunidad del saque de centro | 1,7 |
| `99a22c2` | tras revertir la barrera generalizada de BB-B | 3,0 |
| `f1ce8b3` | primer commit con el umbral en 1,0 | **3,4** |

**El número se mueve 1,7 puntos sin que cambie un solo perk ni un solo objeto.** La causa real —ya
documentada en la ADR 0115 al hablar de las puertas de build que se movían por el mismo motivo— es que
cualquier cambio en `/Sim` (aunque no toque el catálogo) resortea el consumo de RNG de los 6.144 partidos
del brazo, y esta puerta mide una diferencia de dos tasas con un error típico de **~0,9 puntos** —el mismo
número que el propio test ya citaba para su tamaño de muestra—. Con media 2,38 y desviación muestral 0,78
sobre esas cinco medidas, el error empírico coincide con el teórico: **el rango 1,7–3,4 es lo que este
instrumento produce por muestreo, no una tendencia real del catálogo**.

**Conclusión de la hipótesis (Regla F): "el catálogo de 94 perks diluye la aportación marginal de
equipar" pasa de LIKELY a REJECTED como explicación de esta caída concreta.** Sigue viva como mecanismo
teórico plausible (más perks fuertes SÍ podrían diluir una contribución marginal fija), pero **sin
evidencia de activación**: nunca se ha observado por encima del ruido de este instrumento, y no hay
ninguna medición que aísle catálogo de código para confirmarla.

## Qué representa ahora el umbral, y por qué es 1,0

**No por una fórmula de calibración —no existe ninguna, y la primera versión de esta ADR inventó una
(extrapolar un ratio de un precedente que además emparejaba mal dos episodios de calibración distintos,
corregido arriba) que no está registrada en ningún sitio del proyecto como método válido—, sino por
control de falso positivo dado el error de medición conocido.**

Con el valor verdadero de esta puerta estimado en ~2,4 (media de las cinco medidas de arriba) y un error
típico de ~0,9:

| umbral | probabilidad de salir rojo por puro muestreo, sin que nada haya cambiado |
|---|---|
| 2,0 (el anterior) | **~34 %** |
| 1,0 (este) | **~6 %** |
| 0,4 (la otra lectura descartada en la primera versión) | ~1-2 % |

**El umbral de 2,0 tenía una probabilidad de una entre tres de salir rojo en cualquier commit que no
tocara ni objetos ni perks** — es, literalmente, la definición de CLAUDE.md de "un test que falla por mala
suerte": era un umbral mal puesto para su propio ruido, y esa fragilidad es lo que produjo tanto BA-M como
BA-N. 1,0 lo baja a un nivel razonable sin acercarse tanto a cero que dejara de significar nada (0,4 queda
dentro de medio error típico y no distinguiría "equipar no aporta nada" de ruido puro).

**Alternativa no elegida, y por qué se anota en vez de compararse en coste aquí**: las dos bajadas
anteriores (8→24, 24→96 plantillas) subieron la muestra en vez de bajar el umbral — el precedente propio
del proyecto es "arregla el instrumento", no "baja el listón". Subir `Rosters` otra vez dividiría el
error típico (∝ 1/√`Rosters`), pero no lo suficiente para igualar el falso positivo de 1,0: a 384
plantillas (×4 la muestra de hoy) el error baja a ~0,45 y el falso positivo del umbral 2,0 sería del
orden del **19 %** (Φ((2,0−2,4)/0,45), corregido en la quinta ronda — una versión anterior de este
párrafo decía "del orden del 6 %", una cifra que no sale de esa cuenta), lejos todavía del 6 % que da
el umbral 1,0 hoy. Igualar ese 6 % subiendo solo la muestra exigiría del orden de **1.175 plantillas**
(×12 la muestra de hoy), no ×4. Era una alternativa real que esta ADR no llegó a costear en tiempo de
ejecución antes de decidir bajar el umbral, y con el número correcto sale más cara de lo que la primera
redacción de este párrafo daba a entender — si acaso, refuerza la elección de bajar el umbral en vez de
subir la muestra, no la debilita. Se elige bajar el umbral porque es el cambio mínimo que BA-N pedía
(Opción B, sin tocar el instrumento), no porque se haya demostrado más barato que subir la muestra —
queda anotado en `docs/pendientes/BB-P.md` como la opción que de verdad se descarta sin costear, no
como una idea menor.

**Lo que esta puerta protege, con precisión, para que nadie la lea con más alcance del que tiene**: con
umbral 1,0 y error 0,9, detecta del orden de 87 % de las veces que equipar deje de aportar nada, y del
orden de 41 % de las veces que su aporte caiga a la mitad de lo normal. **Protege "equipar sigue haciendo
algo", no el escalón fino de la ADR 0033** ("varios puntos" en plural). Esa afirmación más amplia y más
débil sustituye a la frase de la primera versión ("un efecto real y medible, no ruido"), que era correcta
pero incompleta sin decir cuánto detecta y cuánto no. (Los cuatro porcentajes de esta sección —34 %, 6 %,
87 %, 41 %— salen de un valor verdadero estimado con solo cinco medidas, que tiene su propio error típico
de ~0,35: son órdenes de magnitud para decidir "2,0 era peor que 1,0", no una calibración fina a la
décima.)

## Instrumento: lo que de verdad falta, corregido tras la segunda y la tercera revisión

**La primera versión de esta sección estaba equivocada.** Decía que la puerta comparaba `bareRate` contra
`equippedRate` como dos poblaciones independientes, sin emparejar semilla a semilla, y proponía adoptar el
patrón de diferencia emparejada de la ADR 0087. Leído el código con cuidado (`EquipmentImpactTests.cs`,
`WinsOf`/`Build`): **la puerta ya hace exactamente eso** — el mismo `rosterSeed` genera la plantilla en los
dos brazos, el mismo rival `reference`, las mismas semillas de partido (`rosterSeed*1000 + m`); lo único
que cambia entre brazos es si se equipa el `Item`. Es el patrón de la ADR 0087 aplicado ya. La sd empírica
de 0,78 (§ arriba) ya incluye ese emparejamiento; no hay margen de mejora ahí.

**Lo que de verdad falta, corregido otra vez (tercera revisión) porque la segunda versión de esta sección
también se equivocaba en el estadístico y en lo que el código ya hace**: el ~0,9 de error típico vivía
solo en un comentario, y confirmarlo con datos reales costó reconstruir cinco commits con `git worktree`,
en vez de leerse de la propia ejecución — pero `WinsOf(bool equipped)` hoy **no calcula las 96 diferencias
por plantilla**, devuelve un único entero acumulado (`wins`) sobre las 96 plantillas y las dos direcciones;
esas diferencias no existen en el código actual y habría que cambiar `WinsOf` para conservarlas, no es "un
`_output.WriteLine` más". Y el estadístico correcto **no es la RMS** de esas diferencias —eso mediría algo
del orden de 9 puntos, no 0,9, porque mezcla la dispersión con la propia magnitud del efecto— sino la
**desviación típica de las 96 diferencias, dividida por √96** (el error típico de su media). Queda como el
ítem concreto de `docs/pendientes/BB-P.md`, con el estadístico correcto, no como "pasar a diferencia
emparejada" (que ya existe) ni como una RMS que daría un número diez veces mayor y llevaría a bajar la
puerta otra vez sin necesidad.

## Lo que esta ADR explícitamente NO resuelve

**La inconsistencia entre el valor calculado (ADR 0038) y el valor medido de un objeto sigue sin
resolverse.** El propio `docs/analisis/builds-analisis-sistemico.md` §9 demostró que la fórmula calculada
no predice el valor medido pieza a pieza (`+10 fuerza` mide 2, `+10 resistencia` mide 61; la suma de
atributos no predice nada: `+30` puede valer 0). Esta ADR no toca `data/economy/item-values.json`, no
toca la fórmula de la ADR 0038, y no decide cuál de las dos tablas debería mandar cuando difieren — esa
pregunta, que BA-N dejó explícitamente abierta, sigue abierta.

## Lo que se mide

`dotnet test Sim.Tests --filter "FullyQualifiedName~EquippingAGoodBuildIsWorthSeveralPointsOfWinRate"`
(commit `f1ce8b3`, el que aplicó el cambio de umbral): verde, **3,4 puntos medidos** — ya estaba verde con
el umbral de 2,0 antes de bajarlo a 1,0 (margen de 1,4 puntos en el momento exacto del cambio). Este número
es una fotografía de un árbol concreto, no una constante: **vuelve a ejecutar el test para ver el valor
vigente**, no confíes en la cifra de este párrafo más allá de para entender la magnitud del margen en el
momento del cambio — anclar un número medido a un hash que deja de ser HEAD es precisamente el error que
motivó esta corrección. **Este cambio no arregló ninguna puerta roja existente; endureció el criterio de
falso positivo de una puerta que, en el momento de aplicarlo, ya pasaba.** Es la corrección correcta
igualmente: el umbral de 2,0 seguía teniendo del orden de 34 % de probabilidad de volver a salir rojo por
azar en el próximo cambio de `/Sim`, con o sin relación con objetos o perks.

43 puertas (una invocación, sobre el mismo commit): sin regresión atribuible a este cambio — las 4 rojas
existentes (`TheThreeDoctrinesBuyDifferently`, `CoherentBuildsBeatTheirBaseline`/`orc_violence`,
`BadBuildsLoseToTheirBaseline`/`elf_out_of_zone`, `NoGateMetricIsOutOfRange`) **podrían compartir el mismo
patrón de instrumento** (puertas de una sola semilla cerca de su margen, sensibles a cambios de `/Sim`
ajenos a su contenido) pero eso todavía no está medido para ninguna de las cuatro — ver
`docs/pendientes/BB-P.md`, que las trata como candidatas a comprobar, no como casos ya confirmados.

## Consecuencias

- BA-N con decisión aplicada; cierre formal pendiente de una quinta confirmación del
  `independent-reviewer` sobre esta corrección.
- El escalón "muy buena" de la ADR 0033 sigue existiendo; la puerta que lo vigila detecta su desaparición
  total con buena fiabilidad (~87 %) y una degradación parcial con fiabilidad limitada (~41 %) — queda
  anotado para que nadie la lea como una garantía fina.
- **ADR 0115 gana un dato que no tenía anotado**: a lo largo de los commits de esa sesión, esta puerta se
  movió de 1,7 a 3,4 sin que ninguno tocara un objeto ni un perk — sin que el movimiento se pueda atribuir
  a uno de ellos en concreto (una primera redacción de este punto sí lo atribuía al cierre de la fuga del
  penalti; una segunda revisión lo desmintió con la propia tabla de commits y quedó corregido en la 0115).
- `docs/pendientes/BA-M.md` contenía la premisa que originó este error ("la métrica es determinista, así
  que no es ruido" — el determinismo garantiza que la misma build da el mismo número, no que el muestreo
  de 6.144 partidos no tenga varianza) y una acusación a un commit que, a la luz de esto, no se sostiene.
  **Corregida en el mismo commit que esta ADR** (`50c2bb1`), no queda pendiente aparte.
- Abierto `docs/pendientes/BB-P.md`: calibración de las puertas estadísticas de un solo partido/semilla
  contra su error típico, con esta puerta como caso confirmado y las 4 rojas actuales como candidatas sin
  medir todavía. Incluye el ítem de instrumento (dispersión autoinformada) que esta sección de arriba no
  implementa.
