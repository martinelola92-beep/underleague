# BA-E — Goles sin ángulo.

**Estado:** **Abierta. Medida a fondo, NO cerrada, y con dos afirmaciones mías refutadas (23 sep 2026).**
El paso 3a-i de la ADR 0135 se midió entero y **se envía apagado por dato**
(`minAimApertureCenti: 100`, `offTargetAperturePenalty: 0`), así que **en el build publicado la ventaja de
conversión sigue en 1,42 / 1,31 y aquí no hay nada cerrado.**

**Lo que sí quedó establecido**, con baseline byte a byte contra HEAD en 20 000 partidos:

| | apagado (lo publicado) | encendido |
|---|---|---|
| goles sin ángulo / tiros sin ángulo | 1,42 · 1,31 | **1,09 · 1,03** |
| `goalsPerMatch` | 2,46 · 1,71 | 2,20 · 1,56 |
| `shotsOnTargetShare` | 72,1 | 65,8 |

**Refutación 1 — esto SÍ es la vía (C).** Se escribió aquí que no lo era, «porque aquella era una
penalización plana y ésta una identidad trigonométrica». La revisión independiente midió la celda que el
barrido original no midió —el término **sin** la trigonometría— y sale: la penalización sola deja la
ventaja en **0,978**, o sea el **89 %** del recorrido; la trigonometría sola cubre el **19 %**. El trabajo
lo hace `offTargetAperturePenalty * (100 − apertura) / 100`, que es una penalización elegida a mano,
lineal en (1 − cos θ). **Es la vía (C) con rampa en vez de escalón**, y el revisor la declinó el 14 sep.
Como el efecto lo produce esa mecánica y no la geometría, **el `game-design-review` que se hizo (sobre la
división trigonométrica) no cubre la mecánica que de verdad actúa**: hace falta uno propio antes de
encenderla.

**Refutación 2 — la geometría no puede hablar.** `OnTargetAim` acota la mira al marco, así que un tiro cuya
mira cruda se va tres semianchos se recorta al poste y **sigue contando como tiro a puerta**. Multiplicar
el error por 1/apertura no saca el balón de la portería: sólo lo empuja contra el borde (palos 1,12 →
1,91 %). Es un artefacto estructural que afecta a **cualquier** mecánica de precisión futura, no sólo a
ésta — ficha propia en [BH-C](./BH-C.md).

**Por qué se envía apagado.** Encendido pone **cuatro puertas rojas** sobre las dos del baseline. Tres son
preexistentes o ruido; la que manda es `buildsWinDifferently_injuries`, que pasa de ≥1,10 a **1,04** (ocho
semillas: media 1,04, sd 0,17, fuera en 7 de 8). La ADR 0131 calibró ese umbral midiendo media 1,29 y
eligiéndolo para que saltara «con el cociente real en 1,05», y dejó escrito que **no es un rango que se
arregle bajándolo otra vez**. Lectura alternativa disponible y anotada: con sd 0,17 el borde cae a ~1
error típico, así que también se puede leer como banda estrecha frente al ruido de semilla. Las dos
lecturas están sobre la mesa; **decisión del revisor pendiente**.

Sigue pendiente la vía (B), el ángulo en la utilidad de `Shoot` — el paso 3b.

**Estado anterior:** **Medida y atacada con éxito parcial (23 sep 2026)** — la
[ADR 0136](../decisiones/0136-centrar-el-pase-alto-que-se-remata.md) mete **«centrar»** en el motor y
**los tiros desde la línea de fondo se quedan en la mitad**. Con el censo convertido en instrumento del
motor y medido con 10.000 partidos × 2 semillas contra un baseline que es byte a byte HEAD:

| | baseline | con el centro |
|---|---|---|
| tiros sin ángulo (apertura < 0,5) | 25,77 / 29,00 % | **14,31 / 14,86 %** |
| tiros desde la línea de fondo | 23,30 / 26,53 % | **13,04 / 13,43 %** |
| apertura media de los tiros | 69,11 / 65,59 | **78,52 / 77,35** |
| goles por partido | 2,73 / 2,35 | 2,46 / 2,07 |

Es del mismo orden que la palanca **(A)** —32,4 → 12,2 %—, que la ADR 0111 rechazó por poner **ocho
puertas en rojo**; ésta no pone ninguna. **Ojo con comparar contra el 32,4 % histórico**: aquel censo se
midió sobre un motor anterior a los pasos 1, 2 y 2b de la ADR 0135 y con un instrumento ad-hoc; las cifras
de arriba son todas del mismo instrumento y el mismo árbol.

**Lo que queda**: −0,27 goles por partido, coste real y consistente en las dos semillas, **aparcado por
decisión del revisor** («ya miraremos más adelante si la precisión debe ponderar más»). Y la vía (B) —el
ángulo en la utilidad de `Shoot`, paso 3 de la ADR 0135— sigue pendiente y ahora **sí** tiene alternativa
que ofrecer: hay que **remedirla**, no dar por buenas sus seis puertas rojas de cuando se midió sola.

**Estado anterior:** Abierta — **con la pieza que faltaba ya implementada (23 sep 2026)**: la
[ADR 0136](../decisiones/0136-centrar-el-pase-alto-que-se-remata.md) mete **«centrar»** en el motor —un
pase alto al área que un compañero con mejor apertura remata sin controlar, apoyado en fuerza—, y con ella
el **censo de apertura de esta ficha deja de ser un script y pasa a ser instrumento del motor**
(`shotAperture`, `lowApertureShotShare`, `lowApertureGoalShare`, `bylineShotShare`), medido en el disparo
con el mismo umbral (apertura < 0,5) con el que se midió el problema. **Pendiente del lote**: hasta que
esté, no se puede decir si el 32,4 % baja. La vía (B) —el ángulo en la utilidad de `Shoot`— se **remide**
después, con el centro dentro; sus seis puertas rojas son de cuando se midió sola.

**Estado anterior:** Abierta — **con vía nueva desde el 23 sep 2026**: la ADR 0135 y la acción «centrar» del
§7 de `docs/plan-altura-del-balon.md` atacan esto por donde la ADR 0111 dejó dicho que había que
atacarlo —**local al delantero en zona de remate**, no global—. La vía (B) vuelve a estar sobre la mesa
**pero sólo con el centro implementado**: sola ya se midió y pone seis puertas en rojo.

## Observación

**Goles sin ángulo.** «El delantero está en la línea de fondo y tira a portería sin ángulo. Es muy irreal. El delantero tiende demasiado a ir a la línea de fondo»

## Análisis / estado actual

**Abierta, medida y con informe: `docs/ba-e-goles-sin-angulo.md`.** El diagnóstico de esta fila era correcto y ahora tiene cifras: **32,4 % de los tiros** salen con apertura < 0,5 y producen el **37,1 % de los goles** —convierten **mejor** que la media—, y el **30,1 %** se tiran a menos de una casilla de la línea de gol. Tres palancas medidas: (A) `FindSpace` midiendo el avance hacia la **portería** en vez de hacia la banda es la mejor corrección geométrica —sin ángulo 32,4 → **12,2 %**— y rompe cadena (1,90) y lesiones (0,94); (B) penalizar el ángulo en la **utilidad** de `Shoot` sube los tiros a 8,33 pero acorta la cadena y pone 6 puertas en rojo; (C) meter el ángulo en la **resolución** del tiro no toca ni tiros ni cadena y a peso 1500 quita la **ventaja** de conversión (ratio 1,14 → 1,04), a cambio de `goalsPerMatch` 2,30 → 2,10 y de `betterTeamWinRate`. **Decisión del revisor (14 sep): no aplicar C, no tocar A todavía, auditar antes la banda de la cadena.** Hecho: `docs/auditoria-cadena-de-pases.md`. Resultado que cambia el plan: **A está bloqueada por DOS bandas, no por una**. La de la cadena es heredada del primer commit y este informe recomienda revisarla por sus propios méritos —deja fuera al 33,4 % de los partidos ya hoy—, pero **revisarla no desbloquea A**, que también rompe `injuriesPerMatch` (0,94 contra 0,90), banda medida en la ADR 0082 y que protege el recurso central del juego. La causa es la misma: con A el delantero deja la esquina vacía y se queda donde hay gente, así que hay más duelos. Hecho, y el resultado cierra la vía: el coste de violencia **sí** se puede devolver (`tackleDistanceMaxCells` 1,0 → 0,9 deja lesiones en 0,88 y conserva la corrección espacial: tiros sin ángulo 32,4 → **13,0 %** y desde la línea de fondo 30,1 → **14,2 %** en las dos semillas), pero al pasar las 43 puertas se ve que **el problema no era el precio sino A**: A sola pone **ocho** puertas en rojo —`coherentBuildsBeatNone_orc_violence` 55,83 contra 58, dos `badBuildsLoseToNone`, `randomBuildLosesToNone_human_random` 44,17 y la curva de jefes— y con el precio pagado siguen cinco. **A queda RECHAZADA por coste** (ADR 0111): cambiar la regla de desmarque **global** aplana el juego de colocación, que es la misma lección que dejó el ensanchado de zonas. La corrección buena tendrá que ser **local** al delantero en zona de remate. **C sigue disponible** como corrección parcial y segura

## Hermanos

- `docs/plan-altura-del-balon.md` §7 y **[ADR 0135](../decisiones/0135-el-balon-tiene-altura.md)** — el
  revisor llega al mismo diagnóstico jugando («el delantero se posiciona en la línea de fondo creyendo que
  es el mejor sitio cuando en realidad no lo es») y propone la pieza que faltaba: **centrar**. La (B) de
  esta ficha rompía porque le quitaba el tiro al delantero sin darle nada a cambio; con el centro, la
  jugada no se muere, se transforma.
- [BF-C](./BF-C.md) — el delantero fuera de posesión, la otra mitad de sus malas alternativas.
