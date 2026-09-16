# 0116. El umbral de "equipar" se recalibra contra su propio error de medición

**Fecha:** 2026-09-16 (corregida dos veces el mismo día, tras dos rondas de `independent-reviewer`)
**Estado:** Aceptada e implementada (`Sim.Tests/Perks/EquipmentImpactTests.cs`); documentación pendiente
de una tercera pasada de confirmación.
**Decisión del revisor** (BA-N, `docs/pendientes/BA-N.md`). **Cambia el umbral de la puerta
`EquippingAGoodBuildIsWorthSeveralPointsOfWinRate`** (RT-057: cambio de rango, exige ADR). No modifica
ninguna ADR de precio de objeto (0038, 0086, 0087) ni ningún valor de `/data`.
**Requisitos:** RT-056, RT-057. Relacionado: ADR 0033 (el escalón "muy buena"), ADR 0038 (precio calculado
de objeto), ADR 0087 (valor de perk medido contra su control — esta puerta ya usa su patrón de
emparejamiento por semilla, ver "Instrumento", más abajo), ADR 0115 (el mecanismo real de por qué esta
puerta se mueve, sin atribuirlo a un commit concreto).

**Nota de corrección (dos rondas)**: la primera versión de esta ADR (escrita antes de pasar por
`independent-reviewer`) atribuía la caída del umbral al crecimiento del catálogo de perks (61→94) — esa
causa quedó **REJECTED** con una medición de seis minutos. La segunda versión (tras la primera revisión)
corrigió esa causa pero introdujo dos afirmaciones nuevas que una segunda revisión también refutó: que el
cierre de la fuga del penalti de BB-B (ADR 0115) causó el movimiento de esta puerta, y que la puerta
necesitaría adoptar comparación emparejada de la ADR 0087 (ya la tiene). El umbral implementado (1,0) no
ha cambiado en ninguna de las dos correcciones — solo el razonamiento que lo sostiene.

## Qué umbral existía

`EquippingAGoodBuildIsWorthSeveralPointsOfWinRate` exigía que equipar a los siete titulares de una build
"buena" con un objeto cada uno (RF-076, mezcla de rarezas del acto 3) subiera la tasa de victoria **al
menos 2,0 puntos** frente al mismo equipo sin objetos, sobre 96 plantillas × 32 partidos × 2 direcciones
= 6.144 partidos por brazo. El propio test documentaba, desde que se fijó (paquete AZ), que el número no
salía de una fórmula: lo medido en ese momento era 3,3 puntos y se eligió 2,0 como valor limpio por debajo
de lo medido, con la única afirmación de que "equipar VALE (varios puntos), no una cifra concreta".

## Por qué deja de ser apropiado — la causa real, no la del catálogo

BA-N registró la misma métrica en tres momentos sucesivos del catálogo de perks y observó una caída
monótona (3,3 → 2,0 → 1,7) que parecía coincidir con el crecimiento de 61 a 94 perks. Esa lectura era
razonable a primera vista, pero **falsa**: el `independent-reviewer` congeló el catálogo de perks (mismo
`/data/perks/` verificado byte a byte en cinco commits distintos) y midió la misma puerta en cada uno:

| commit | qué cambió (nunca perks/objetos) | medido |
|---|---|---|
| `ab129d7` | donde se escribió BA-N | 1,7 |
| `96234de` | fix de build (BB-J) | 2,1 |
| `ad3c472` | revert de la inmunidad del saque de centro | 1,7 |
| `99a22c2` | tras revertir la barrera generalizada de BB-B | 3,0 |
| `f1ce8b3` | HEAD, con esta ADR aplicada | **3,4** |

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
(extrapolar el ratio 2,0/3,3 de un único precedente) que no está registrada en ningún sitio del proyecto
como método válido—, sino por control de falso positivo dado el error de medición conocido.**

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

**Lo que esta puerta protege, con precisión, para que nadie la lea con más alcance del que tiene**: con
umbral 1,0 y error 0,9, detecta del orden de 87 % de las veces que equipar deje de aportar nada, y del
orden de 41 % de las veces que su aporte caiga a la mitad de lo normal. **Protege "equipar sigue haciendo
algo", no el escalón fino de la ADR 0033** ("varios puntos" en plural). Esa afirmación más amplia y más
débil sustituye a la frase de la primera versión ("un efecto real y medible, no ruido"), que era correcta
pero incompleta sin decir cuánto detecta y cuánto no. (Los cuatro porcentajes de esta sección —34 %, 6 %,
87 %, 41 %— salen de un valor verdadero estimado con solo cinco medidas, que tiene su propio error típico
de ~0,35: son órdenes de magnitud para decidir "2,0 era peor que 1,0", no una calibración fina a la
décima.)

## Instrumento: lo que de verdad falta, corregido tras una segunda revisión

**La primera versión de esta sección estaba equivocada.** Decía que la puerta comparaba `bareRate` contra
`equippedRate` como dos poblaciones independientes, sin emparejar semilla a semilla, y proponía adoptar el
patrón de diferencia emparejada de la ADR 0087. Leído el código con cuidado (`EquipmentImpactTests.cs`,
`WinsOf`/`Build`): **la puerta ya hace exactamente eso** — el mismo `rosterSeed` genera la plantilla en los
dos brazos, el mismo rival `reference`, las mismas semillas de partido (`rosterSeed*1000 + m`); lo único
que cambia entre brazos es si se equipa el `Item`. Es el patrón de la ADR 0087 aplicado ya. La sd empírica
de 0,78 (§ arriba) ya incluye ese emparejamiento; no hay margen de mejora ahí.

**Lo que de verdad falta es más simple**: la puerta no calcula ni imprime su propia dispersión. El ~0,9
de error típico vivía solo en un comentario, y confirmarlo con datos reales costó reconstruir cinco
commits con `git worktree` — cuando la propia ejecución ya tiene, por plantilla, la diferencia entre su
brazo equipado y su brazo sin equipar, y podría imprimir la RMS de esas 96 diferencias en el mismo
`_output.WriteLine` que ya usa, igual que `rowDeviation` en la ADR 0087 calcula su dispersión dentro de
una sola ejecución. Eso habría hecho innecesaria la arqueología de cinco commits para esta ADR, y es
justo lo que `docs/pendientes/BB-P.md` pide para las otras cuatro puertas. **No se implementa aquí** —es
una mejora de instrumento, fuera del alcance de un cambio de umbral— pero queda como el ítem concreto de
BB-P, no como "pasar a diferencia emparejada" (que ya existe).

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

- BA-N con decisión aplicada; cierre formal pendiente de una tercera confirmación del
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
