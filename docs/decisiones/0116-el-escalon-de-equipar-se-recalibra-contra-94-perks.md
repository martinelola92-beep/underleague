# 0116. El umbral de "equipar" se recalibra contra su propio error de medición

**Fecha:** 2026-09-16 (corregida el mismo día, tras `independent-reviewer`)
**Estado:** Aceptada e implementada (`Sim.Tests/Perks/EquipmentImpactTests.cs`)
**Decisión del revisor** (BA-N, `docs/pendientes/BA-N.md`). **Cambia el umbral de la puerta
`EquippingAGoodBuildIsWorthSeveralPointsOfWinRate`** (RT-057: cambio de rango, exige ADR). No modifica
ninguna ADR de precio de objeto (0038, 0086, 0087) ni ningún valor de `/data`.
**Requisitos:** RT-056, RT-057. Relacionado: ADR 0033 (el escalón "muy buena"), ADR 0038 (precio calculado
de objeto), ADR 0087 (valor de perk medido contra su control, patrón que esta puerta no usa —ver
"Instrumento", más abajo), ADR 0115 (el mecanismo real de por qué esta puerta se mueve).

**Nota de corrección**: la primera versión de esta ADR (escrita antes de pasar por
`independent-reviewer`) atribuía la caída del umbral al crecimiento del catálogo de perks (61→94). Esa
causa quedó **REJECTED** por el propio revisor con una medición de seis minutos que yo no había hecho
antes de escribir la ADR. El umbral que se implementó (1,0) resultó ser correcto, pero por un motivo
distinto del que se escribió entonces. Esta versión sustituye la causa y el razonamiento; no cambia el
número.

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
umbral 1,0 y error 0,9, detecta ~87 % de las veces que equipar deje de aportar nada, y solo ~41 % de las
veces que su aporte caiga a la mitad de lo normal. **Protege "equipar sigue haciendo algo", no el escalón
fino de la ADR 0033** ("varios puntos" en plural). Esa afirmación más amplia y más débil sustituye a la
frase de la primera versión ("un efecto real y medible, no ruido"), que era correcta pero incompleta sin
decir cuánto detecta y cuánto no.

## Instrumento: por qué esta puerta es más ruidosa de lo necesario, sin arreglarlo aquí

El proyecto ya tiene un patrón mejor para este tipo de medida: la ADR 0087 mide el valor de un perk como
**diferencia emparejada contra su control** (misma plantilla, misma semilla, con y sin el perk), que
cancela buena parte de la varianza entre plantillas en vez de comparar dos poblaciones agregadas. Esta
puerta compara `bareRate` contra `equippedRate` como dos tasas independientes sobre 96 plantillas
distintas por brazo, sin emparejar semilla a semilla. Es probable que ese cambio de diseño del instrumento
bajara el error típico muy por debajo de 0,9 sin tocar el número de plantillas. **No se hace en esta ADR**
—sería una primitiva de medición nueva, fuera del alcance de un cambio de umbral— pero queda anotado como
la mejora real pendiente, y como la pregunta ("¿existe ya una convención del repositorio para esto?") que
debería haberse hecho antes de aceptar un umbral basado en una comparación no emparejada.

## Lo que esta ADR explícitamente NO resuelve

**La inconsistencia entre el valor calculado (ADR 0038) y el valor medido de un objeto sigue sin
resolverse.** El propio `docs/analisis/builds-analisis-sistemico.md` §9 demostró que la fórmula calculada
no predice el valor medido pieza a pieza (`+10 fuerza` mide 2, `+10 resistencia` mide 61; la suma de
atributos no predice nada: `+30` puede valer 0). Esta ADR no toca `data/economy/item-values.json`, no
toca la fórmula de la ADR 0038, y no decide cuál de las dos tablas debería mandar cuando difieren — esa
pregunta, que BA-N dejó explícitamente abierta, sigue abierta.

## Lo que se mide

`dotnet test Sim.Tests --filter "FullyQualifiedName~EquippingAGoodBuildIsWorthSeveralPointsOfWinRate"` en
HEAD (`f1ce8b3`): verde, **3,4 puntos medidos** — ya estaba verde con el umbral de 2,0 antes de bajarlo a
1,0 (margen de 1,4 puntos en el momento exacto del cambio). **Este cambio no arregló ninguna puerta roja
existente; endureció el criterio de falso positivo de una puerta que, en el momento de aplicarlo, ya
pasaba.** Es la corrección correcta igualmente: el umbral de 2,0 seguía teniendo ~34 % de probabilidad de
volver a salir rojo por azar en el próximo cambio de `/Sim`, con o sin relación con objetos o perks.

43 puertas (una invocación, sobre HEAD): sin regresión atribuible a este cambio — las 4 rojas existentes
(`TheThreeDoctrinesBuyDifferently`, `CoherentBuildsBeatTheirBaseline`/`orc_violence`,
`BadBuildsLoseToTheirBaseline`/`elf_out_of_zone`, `NoGateMetricIsOutOfRange`) comparten exactamente el
mismo mecanismo causal que motivó esta ADR (puertas de una sola semilla cerca de su margen, movidas por
cambios de `/Sim` ajenos a su contenido) y no se tocan aquí — ver `docs/pendientes/BB-P.md`.

## Consecuencias

- BA-N cerrada.
- El escalón "muy buena" de la ADR 0033 sigue existiendo; la puerta que lo vigila detecta su desaparición
  total con buena fiabilidad (~87 %) y una degradación parcial con fiabilidad limitada (~41 %) — queda
  anotado para que nadie la lea como una garantía fina.
- **ADR 0115 gana un efecto de segundo orden que no tenía anotado**: cerrar la fuga del penalti (§17 de
  esa ADR) movió esta puerta de 1,7 a 3,4. Queda registrado aquí y referenciado desde la 0115.
- `docs/pendientes/BA-M.md` contiene la premisa que originó este error ("la métrica es determinista, así
  que no es ruido" — el determinismo garantiza que la misma build da el mismo número, no que el muestreo
  de 6.144 partidos no tenga varianza) y una acusación a un commit que, a la luz de esto, no se sostiene
  (diferencia de 0,4 errores típicos). Pendiente de corrección aparte.
- Abierto `docs/pendientes/BB-P.md`: calibración de las puertas estadísticas de un solo partido/semilla
  contra su error típico, con esta puerta y las 4 rojas actuales como primeros casos.
