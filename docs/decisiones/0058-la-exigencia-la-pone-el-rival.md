# 0058. La exigencia de build la pone el rival, no la escala del perk

**Fecha:** 2026-09-06
**Estado:** Aceptada e **implementada** (`fase2-diseno.md` §27); **su propio criterio de falsificación se
cumple**. Los tres puntos están aplicados y las seis puertas siguen como estaban o mejor, pero el hueco
entre build buena y mediocre **no se abre**: 6,81 → 6,28 puntos en el acto 2 con error típico 1,31, frente
a los 9,8 que la ADR exige. La causa está medida y es geométrica, no de calibración (§27.6): la capa de
build del rival es un solo número y baja a las dos builds a la vez, hacia la parte plana de la sigmoide,
donde la misma diferencia de fuerza vale menos puntos; y el techo por rareza no separa a las dos builds
porque llevan casi la misma mezcla de rarezas (42,8/45,5/11,4/0,4 frente a 56,2/35,5/8,2/0,1), lo que da
solo un 6,9% de diferencia de multiplicador medio y **una décima de punto** de tasa de victoria. Lo que sí
se mueve por primera vez es el **suelo sin build**: 12,08% → 10,08%, −2,00 con error típico 1,28. Y el
guardarraíl de las derrotas del acto 1 se rompe (29,3% → 30,9%) por una vía que no es la capa del rival
sino el techo por rareza sobre los catorce perks del jefe (AK-A)
**Corrige:** el techo `k ≤ 2` que el encargo de la P1 fijó a ojo; **releva** a la ADR 0057 en la elección de palanca
**Requisitos:** RT-055, RF-032, RF-002c
**Relacionada con:** ADR 0033 (curva de puertas), ADR 0050 (P1), ADR 0054 (banda 70-88), ADR 0056 (separación entre perfiles)

## Lo que la P1 enseñó

La P1 —perks que multiplican cuotas— **es correcta y se queda**. Arregla un defecto real: medido efecto a efecto, `pass +25` valía ×2.987 y clavaba el canal en su techo del 98% con un solo perk. Con cuotas, la misma cifra vale lo mismo en cualquier canal y el décimo perk de una línea sigue valiendo algo.

Pero **no era la palanca del suelo**, y la medición lo dice sin ambigüedad: 12,67% → 12,08% sobre 1.200 runs por lado, con error típico 1,34. No se distingue de cero.

Y trajo un coste que apunta al problema de verdad: **la capa de perks quedó más débil que antes**. Con `k ≤ 2` no hay forma de que no lo sea, porque casi todo el catálogo valía más que ×2 en la escala vieja. El resultado va en contra de la directriz del revisor —la build mediocre sube de 47,3 a 50,0 en el acto 2, la mala completa la run un 14,34% en vez de un 12,58%, y el hueco entre build buena y mediocre **se estrecha** de 9,8 a 6,8 puntos.

## El diagnóstico, enunciado bien

Los rivales ordinarios **sí** llevan perks: salen de `data/rivals/`, no de `PerkAssignment`. El problema es cuántos.

| | perks por plantilla de 10 |
|---|---|
| Rival ordinario, acto 1 | 1-2 |
| Rival ordinario, acto 2 | 3 |
| Rival ordinario, acto 3 | 2-4 |
| Jefe | 14 |
| **Jugador al final de la run** | **10-15** |

La capa de build del rival ordinario es **plana y mínima**: no crece con el acto y no llega ni a un tercio de la del jugador en el acto 3. En los ~20 partidos ordinarios de una run **nadie te exige build**; el nivel y los atributos bastan. El único sitio donde la build se examina son los tres jefes.

Eso explica los tres objetivos fallidos de la ADR 0056 a la vez, y explica también por qué la P1 no los movió: **cambiar lo que vale un perk no afecta al equipo que no tiene perks.** Solo cambia la altura del examen para los dos, y en un examen que el que no lleva build ya aprobaba.

## Decisión

Dos cambios que van juntos porque se compensan entre sí, y una corrección de convención.

**1. El techo de la escala depende de la rareza.** El `k ≤ 2` del encargo de la P1 lo fijé antes de saber que el catálogo real valía de ×2 a ×2.987; era un número a ojo y la medición lo falsificó. La rareza pasa a comprar cuota de verdad: un común mueve poco, un legendario mueve mucho. Esto devuelve potencia a la capa de build **por el sitio donde vive la decisión** —lo que se paga en el mercado y lo que sueltan los jefes— en vez de subirla en plano.

**2. La capa de build del rival ordinario crece con el acto.** Es lo que convierte los veinte partidos ordinarios en una exigencia progresiva de build en lugar de un trámite de atributos. Perder un partido ordinario no termina la run (RF-002c): cuesta el oro y la recompensa, así que el equipo sin build llega a los jefes más pobre, que es exactamente el mecanismo que la ADR 0057 buscaba en la economía y no encontró.

**El acto 1 no sube, o sube poco.** Sigue siendo el taller donde se aprende y se arma la build; ya se resintió al recalibrar su jefe en la P1 (derrotas del 22,2% al 29,3%, AJ-E). La pendiente va en los actos 2 y 3, que es literalmente lo que la ADR 0057 dejó previsto.

**3. La descripción habla de cuota, no de proporción de probabilidad.** La convención que aplicó la P1 —"un 30% más de probabilidad de pase" para `k = 1,3`— **miente en los canales de base alta**: en `pass` (base 77%) el aumento real es del 5,6%. No existe frase corta en proporción de probabilidad que sea exacta para una multiplicación de cuotas, así que se escribe la multiplicación: **"multiplica por 1,3 sus opciones de pasar"**. Es exacta en todos los canales y enseña el modelo mental correcto —que dos perks se multiplican, no se suman—, que es justo lo que la P1 hace.

## Por qué los dos primeros van en el mismo paquete

Van en direcciones opuestas sobre la build buena: el punto 1 la fortalece y el punto 2 la presiona. Separarlos obliga a dos revalidaciones completas de la curva y las puertas para medir dos estados intermedios que no son el diseño que queremos. Y hay un riesgo real de sobrecorregir en el primero si se mide antes de aplicar el segundo.

## Qué falsificaría esta decisión

- **La tasa de victoria de la run sube por encima del 30%.** Sería haber hecho el juego más fácil en vez de más exigente con la build.
- **`betterTeamWinRate` pasa de 88** (ADR 0054): la habilidad domina y el azar deja de dar partidos.
- **El suelo sin build no baja del 10%** aun con la capa del rival crecida. Entonces no es el rival: es que el nivel y los atributos pesan demasiado frente a la build en el propio motor, y la conversación pasa a ser la P3 que la ADR 0057 suspendió, con los ojos abiertos sobre lo que cuesta.
- **El hueco entre build buena y mediocre no se abre** por encima de los 9,8 puntos que tenía antes de la P1. El objetivo de la ADR 0056 es separación entre perfiles; si la separación no crece, el paquete no ha servido para lo que se hizo.
