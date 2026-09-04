# Ejes de activación de los perks

Clasificación **ortogonal** a la de RF-069 (`filler` / `conditional` / `ruleBreaker`, que mide potencia). Esta mide **de qué depende** que un perk se active. Ambas se cumplen a la vez: un catálogo puede tener la distribución 60/30/10 correcta y aun así ser aburrido si todos los perks dependen de lo mismo.

## Principio

**Cada eje corresponde a una decisión distinta del jugador.** Si todos los perks se activan por raza, la única decisión es a quién fichas; si todos por adyacencia, la única decisión es cómo colocas. La variedad de ejes es lo que convierte una build en varias decisiones encadenadas en lugar de una repetida.

| Eje | De qué depende | Qué decisión estimula | Mecanismo | Estado |
|---|---|---|---|---|
| **Identidad individual** | Etiqueta de estilo, rasgo y posición del portador | A quién le das el perk y a quién fichas: dentro de una misma raza, dos jugadores son distintos (RF-024b, ADR 0024) | `hasTag` sobre estilo o rasgo, `position`, `tagsRequired` | Existe (falta la variación de estilo) |
| **Acumulación** | Acciones realizadas, dentro del partido y entre partidos | A quién inviertes y si lo proteges de la lesión | `counter`, `addCounter`, `accumulatesAcrossMatches`, `stat` | Existe (falta `stat`). **15 perks acumulan entre partidos** desde el paquete Z, que es lo que RF-070 exige; todos con `limit` de una activación por partido, de modo que el contador sube como mucho +1 por partido |
| **Alineación relacional** | Quién tiene al lado, delante o detrás en la cuadrícula | Cómo dibujas la formación | Vínculos direccionales (ADR 0021) | Falta |
| **Zona de inicio** | En qué parte del campo empieza: línea (tercio) y banda (fila) | Dónde colocas a cada jugador | `startsIn`, `startsOn` | Falta |
| **Geometría en juego** | Dónde está el jugador o el balón en ese momento | Nada directo: premia el estilo de juego que produce tu build | `distanceToGoal`, `zone` | Existe |
| **Estado del partido** | Marcador, minuto, turba, criterio del árbitro | Cómo preparas el partido y qué consumibles llevas | `scoreDiff`, `tick`, `isMob`, `bias` | Existe |
| **Composición de plantilla** | Cuántos compañeros de una etiqueta hay | Qué mayoría racial o de rasgo construyes | `teammatesWithTag` | Existe |
| **Proximidad dinámica** | Quién está cerca **ahora** | Nada en el menú: se gana jugando | `nearAlly`, `nearOpponent` | Falta (ADR 0021) |

## Distribución objetivo del catálogo

Orientativa, sobre el catálogo de lanzamiento; se revisa con datos como los rangos de RT-056.

| Eje | Proporción |
|---|---|
| Identidad individual (rasgo, posición) | 15% |
| Acumulación | 20% |
| Alineación relacional | 15% |
| Zona de inicio | 10% |
| Geometría en juego | 15% |
| Estado del partido | 10% |
| Composición de plantilla | 5% |
| Proximidad dinámica | 10% |

Aparte de este reparto, un **10% del catálogo son perks exclusivos de raza** (ADR 0023), que no compiten por estas cuotas: no aparecen salvo que juegues esa raza. El otro 90% es universal.

**La especie no se usa como condición en los perks universales.** Con clubes monoraza (RF-004), condicionar por la etiqueta de especie se cumple siempre o nunca según el club: no es una decisión, es un adorno que además hace aparecer perks en runs donde no sirven. La identidad **individual** sí es una decisión, porque tanto los rasgos como la **etiqueta de estilo** varían entre jugadores de la misma raza (RF-024b, ADR 0024): en un club élfico la mayoría son `Fine`, pero el `Brute` que te salga cambia a quién le das cada perk.

## Regla de legibilidad

**Un perk combina como máximo dos ejes.** Tres condiciones encadenadas producen un perk que nadie puede aprender, que la descripción generada (RT-035) convierte en una frase ilegible y que hace imposible atribuir una derrota a una causa (riesgo "el jugador no sabe por qué perdió"). Si un efecto necesita tres condiciones para estar equilibrado, el problema es el efecto, no la condición.

## Funciones nuevas que exige esta taxonomía

Se añaden al conjunto de RT-034 y a `fase1-diseno.md` §2 cuando se implementen:

| Función | Devuelve |
|---|---|
| `startsIn(who, 'OwnThird' \| 'Middle' \| 'AttackingThird')` | bool, según la columna de la casilla-hogar |
| `startsOn(who, 'LeftFlank' \| 'Center' \| 'RightFlank')` | bool, según la fila de la casilla-hogar |
| `linked(who, 'beside' \| 'ahead' \| 'behind' \| 'left' \| 'right' \| ...)` | bool, existe vínculo en esa relación (ADR 0021) |
| `nearAlly(who, 'Tag', cells)` / `nearOpponent(who, 'Tag', cells)` | bool, proximidad real en el momento del evento |
| `stat(who, 'goals' \| 'passesCompleted' \| 'tacklesWon' \| 'shots' \| ...)` | int, estadística del partido en curso, sin declarar contadores propios |

`stat` merece justificación: hoy un perk de acumulación necesita declarar su propio `addCounter` sobre un disparador y leerlo después, lo que gasta dos efectos y obliga a que el contador exista antes de poder usarlo. Exponer las estadísticas que el motor ya lleva para el informe post-partido (RF-119) abarata mucho toda la familia de acumulación y elimina una fuente de errores.

## Escala de valores por canal (ADR 0035)

Un punto porcentual no vale lo mismo en todos los canales: las bases van de 40 (`injure`) a 7.700 (`pass`), así que la misma cifra escrita en dos perks produce efectos que se diferencian en dos órdenes de magnitud. Cada canal declara su **escalón** en `data/sim/tuning.json` → `probabilityChannels.<canal>.step`, en puntos porcentuales, y un valor legal de `modifyProbability` es ese escalón por **1, 2, 3, 5 o 10 pasos**. La tabla completa está en `fase1b-diseno.md` §1.4; en corto: `intercept`, `injure`, `injury`, `foul`, `card` e `interceptEvasion` valen 1; `tackle`, `tackleEvasion` y `severeInjury` valen 3; `pass`, `dribble`, `save` y `shotOnTarget` valen 5. La comprobación la hace `Sim.Perks.PerkLoader` al cargar.

Consecuencia práctica al escribir un perk: en los canales de base diminuta se escribe **1, 2 o 3**, nunca 10, salvo que se quiera de verdad un interruptor; en los de base grande, 5 a 25 es el rango normal y 50 es el techo.

## Objetivo `linked`

Los efectos de la familia de alineación actúan **sobre el vínculo**, no sobre el portador: `target: "linked"` (todos los vinculados) o `target: "linkedWithTag:<Tag>"`. Es el tipo de modificador por par que la ADR 0021 exige añadir al motor de efectos.

**El modificador por par solo existe en el canal `pass`.** Un vínculo une a dos **compañeros**, y la única resolución del motor que enfrenta a dos compañeros es el pase de uno al otro: en `intercept`, `tackle`, `dribble`, `shotOnTarget` o `save` la contraparte de la tirada es siempre un rival, así que un bono "del par" nunca se aplicaba y el perk era letra muerta (medido: quitar `covering_shadow` o `pivot_duo` de una build no cambiaba ni un partido de 4.800). Fuera del pase, `target: "linked"` se lee como lo que dice —**el bono es del compañero vinculado**, un modificador normal sobre él— y el vínculo sigue siendo la condición que lo hace existir. Detalle en `fase2-diseno.md` §16, costura 4.
