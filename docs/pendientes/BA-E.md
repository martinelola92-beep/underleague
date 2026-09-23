# BA-E — Goles sin ángulo.

**Estado:** **Medida y atacada con éxito parcial (23 sep 2026)** — la
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
