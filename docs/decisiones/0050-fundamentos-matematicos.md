# 0050. Cuatro correcciones a los fundamentos matemáticos

**Fecha:** 2026-09-05
**Estado:** Aceptada. **P2, P4 y P1 implementadas** (`fase2-diseno.md` §22 y §26); **P3 en suspenso** por la ADR 0057
**Sustituye:** la ADR 0035 (escalones por canal), una vez implementada la P1
**Requisitos:** RT-023, RT-056, RF-069, RF-024
**Origen:** `docs/analisis-formulas-roguelike.md`, comparación con Brogue, DCSS, Shattered PD y CDDA

## Contexto

El análisis de cuatro roguelikes consolidados destapó que tres de nuestros fundamentos matemáticos son peores que los suyos, y que **dos de los problemas que llevamos días persiguiendo son consecuencia directa** de esas elecciones, no de la calibración.

## Las cuatro decisiones

### P1 · Los perks multiplican cuotas, no suman puntos

`modifyProbability` deja de sumar puntos porcentuales y pasa a multiplicar las **cuotas**: `odds = p/(1−p)`, `odds' = odds × k`, `p' = odds'/(1+odds')`. Valores permitidos: `k ∈ {1,15 · 1,3 · 1,5 · 2}` y sus inversos.

**Motivo**: con una fórmula aditiva, el mismo incremento vale cosas incomparables según la base — un `+5` multiplica por trece la probabilidad de lesionar (base 0,4%) y no mueve la de pase (base 77%). Ese defecto ya ha producido **dos fallos de balance costosos**: una habilidad racial que valía cuatro veces su presupuesto y una build catalogada como mala que ganaba por llevar tres copias de un perk de intercepción.

La ADR 0035 (escalones por canal) fue el parche; multiplicar cuotas elimina el problema de raíz y **retira esa tabla**. Multiplicar cuotas, y no probabilidad, es lo que hace que funcione igual de bien cerca de 0 que cerca de 1.

**Se hace ahora y no después**: el catálogo de lanzamiento previsto ronda los 150 perks y hoy hay 45. Migrar cuesta el triple si se espera.

### P2 · Dos tiradas promediadas en las resoluciones decisivas

Tiro, parada, entrada y regate pasan de una tirada uniforme al **promedio de dos**, como hace DCSS. No se toca ninguna media; baja la desviación típica en torno a un 30%.

**Motivo, y es el que más nos afecta**: el error de medición de una celda es de ±4 puntos con 640 partidos, y la ventaja de jugar bien que perseguimos ronda los 5-8. Con ese ruido, **la habilidad es indistinguible del azar**. Reducir la varianza hace que el mejor equipo gane más a menudo y que la diferencia entre construir bien y mal se vea con lotes más pequeños.

No se aplica a las resoluciones de alta frecuencia (pase, intercepción) para no alterar el ritmo del partido.

### P3 · Curva de nivel más agresiva

`budgetPerLevel` de **8 a 14**, y `budgetByRarity` de 250/275/300 a **250/287/324** para conservar la relación de la ADR 0027 (común de nivel 8 ≈ raro de nivel 2). El crecimiento del nivel 1 al 8 pasa del **22% al 39%**.

**Motivo**: Shattered Pixel Dungeon crece un 250% a lo largo de su recorrido; nosotros un 22%, y se nota — los perks de acumulación valían 0,2 puntos antes de arreglarlos. Si superar la run debe significar "haber hecho las cosas con cabeza", sobrevivir y subir de nivel tiene que pesar.

### P4 · Un solo suelo y un solo techo

Toda probabilidad se acota a **2%-98%**, en lugar de los límites ad hoc por canal de hoy (500-9800 en pase, 5-95% en parada, ninguno en otros). Es el equivalente al 2,5% automático de DCSS.

## Orden de aplicación, y por qué importa

**P2 y P4 primero, juntos**: son baratos, no cambian el diseño y **mejoran la calidad de todas las mediciones posteriores**. Medir P1 y P3 con menos ruido es lo que hace posible atribuir sus efectos.

**Antes de P1, la ADR 0054**: la banda de `betterTeamWinRate` está a medio punto de su techo y P1 y P3 la romperían por hacer justo lo que se pretende. No es opcional ni se puede dejar para después.

**P1 después, sola**, con su ronda completa de revalidación: RT-056, la curva de puertas y las seis puertas.

**P3 al final**, vigilando la relación de la ADR 0027, que es la que se rompe si se hace mal.

**P1 y P3 no se hacen a la vez.** Las dos tocan el equilibrio completo, y juntas harían imposible atribuir un desajuste a su causa — que es exactamente el error que ya se cometió en la fase 1 y que costó dos paquetes de trabajo desenredar.

## Consecuencias

- Los valores de los 45 perks se reescriben con la escala multiplicativa.

**Al implementarse la P1** (`fase2-diseno.md` §26) hay dos correcciones que anotar sobre lo que esta ADR
daba por hecho:

- **La escala vieja estaba, casi entera, por encima del techo de la nueva.** Midiendo el cociente de
  cuotas que producía de verdad cada efecto del catálogo, 48 de los 68 caen en el ×2 (o su inverso): un
  `pass +25` valía ×2.987 y un `interceptEvasion +5` valía ×256. La consecuencia no anticipada es que la
  capa de perks queda **más débil** que antes, y con `k ≤ 2` no hay forma de que no lo sea.
- **"Se espera que la ventaja medible de jugar bien suba" no se cumple para la P1.** El suelo sin build no
  se mueve (12,67% → 12,08%, error típico 1,34) y la separación entre perfiles se estrecha. La causa está
  medida y es estructural: los rivales ordinarios de una run no llevan perks, así que la capa de build solo
  existe en un lado del campo salvo contra los tres jefes.
- Las descripciones cambian de *"+5% de probabilidad de interceptar"* a *"un 30% más de probabilidad de interceptar"*. `estilo-descripciones.md` ya advierte de la confusión entre puntos y proporciones: ahora la convención pasa a ser **proporcional**, y hay que actualizar esa guía.
- La ADR 0035 queda retirada al completarse P1.
- Se espera que la ventaja medible de jugar bien **suba** por dos vías: menos ruido (P2) y progresión más marcada (P3).
