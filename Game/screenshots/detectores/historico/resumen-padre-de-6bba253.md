# Barrido de detectores — 400 partidos por traza, semillas 1..400

Tres trazas: `ref` = `TestMatches.Reference` (semilla = emparejamiento); `run` = el primer partido del acto 1 de una run de `human_abattoir` con esa semilla (lo que reproduce Godot con `-- movimiento <carpeta> <semilla>`); `armado` = `ref` con `box_predator` en todos los jugadores de campo (sólo BB-I). Casos/partido ± error típico de la media.

| Detector | ref casos/partido ± e.t. | ref % partidos | run casos/partido ± e.t. | run % partidos | Peores (traza:semilla@tick, magnitud) |
|---|---|---|---|---|---|
| BA-E gol sin ángulo | 0.298 ± 0.030 | 23.8 % (95/400) | 0.298 ± 0.030 | 23.8 % (95/400) | ref:7@1038 (1); ref:12@1230 (1); ref:33@1249 (1) |
| BA-J sin repliegue tras parada | 2.045 ± 0.076 | 83.0 % (332/400) | 2.045 ± 0.076 | 83.0 % (332/400) | ref:1@825 (5); ref:3@201 (5); ref:3@645 (5) |
| BB-A/L salto inexplicado | 0.003 ± 0.002 | 0.3 % (1/400) | 0.003 ± 0.002 | 0.3 % (1/400) | ref:110@1060 (0.81); run:110@1060 (0.81) |
| BB-B robo antes de cualquier saque | 0.000 ± 0.000 | 0.0 % (0/400) | 0.000 ± 0.000 | 0.0 % (0/400) | — |
| BB-B robo antes del saque de centro | 0.000 ± 0.000 | 0.0 % (0/400) | 0.000 ± 0.000 | 0.0 % (0/400) | — |
| BB-C celebración (salto o en su campo) | 0.000 ± 0.000 | 0.0 % (0/400) | 0.000 ± 0.000 | 0.0 % (0/400) | — |
| BB-G2 portero perseguidor (abrazo mortal) | 0.015 ± 0.006 | 1.5 % (6/400) | 0.015 ± 0.006 | 1.5 % (6/400) | ref:389@877 (483); run:389@877 (483); ref:239@259 (31) |
| BB-I perk sin tiro [armado] | 0.000 ± 0.000 | 0.0 % (0/400) | — | — | — |
| BB-K baile | 0.555 ± 0.056 | 33.0 % (132/400) | 0.555 ± 0.056 | 33.0 % (132/400) | ref:61@475 (42); run:61@475 (42); ref:61@476 (40) |
| BC-G balón suelto quieto >=15 ticks | 0.210 ± 0.023 | 19.0 % (76/400) | 0.210 ± 0.023 | 19.0 % (76/400) | ref:224@624 (656); run:224@624 (656); ref:389@877 (483) |
| BC-G balón suelto quieto >=60 ticks | 0.030 ± 0.009 | 3.0 % (12/400) | 0.030 ± 0.009 | 3.0 % (12/400) | ref:224@624 (656); run:224@624 (656); ref:389@877 (483) |
| BF-C delantero elige pegar sin portador a su alcance | 2.323 ± 0.113 | 78.5 % (314/400) | 2.323 ± 0.113 | 78.5 % (314/400) | ref:28@950 (22); ref:38@1040 (22); ref:53@616 (22) |
| BF-C delantero pega sin balón | 0.000 ± 0.000 | 0.0 % (0/400) | 0.000 ± 0.000 | 0.0 % (0/400) | — |
| BH-A congelación | 0.020 ± 0.009 | 1.5 % (6/400) | 0.020 ± 0.009 | 1.5 % (6/400) | ref:224@623 (657); run:224@623 (657); ref:389@882 (478) |
| BN-A amontonamiento sobre el portero | 0.640 ± 0.038 | 47.8 % (191/400) | 0.640 ± 0.038 | 47.8 % (191/400) | ref:19@278 (5); ref:131@674 (5); ref:305@987 (5) |
| BO-A portador y rival atascados >3 s | 0.025 ± 0.008 | 2.5 % (10/400) | 0.025 ± 0.008 | 2.5 % (10/400) | ref:14@490 (53); ref:90@899 (53); ref:117@1134 (53) |

## Métricas auxiliares (media por partido ± e.t.)

| Métrica | ref | run | armado |
|---|---|---|---|
| BA-E goles | 1.935 ± 0.056 | 1.935 ± 0.056 | — |
| BA-E tiros | 7.613 ± 0.170 | 7.613 ± 0.170 | — |
| BA-E tiros sin ángulo (apertura<0,5) | 0.915 ± 0.052 | 0.915 ± 0.052 | — |
| BA-J paradas retenidas | 2.088 ± 0.077 | 2.088 ± 0.077 | — |
| BA-J rezagados medios por parada retenida | 3.381 ± 0.086 | 3.381 ± 0.086 | — |
| BB-A salto de >0,6 en reanudación (apartar/colocar, diseño) | 6.855 ± 0.234 | 6.855 ± 0.234 | — |
| BB-A salto explicado (reposición, control) | 19.370 ± 0.657 | 19.370 ± 0.657 | — |
| BB-G2 ticks del portero eligiendo ChaseBall fuera del área | 1.480 ± 0.221 | 1.480 ± 0.221 | — |
| BB-I con tiro bloqueado al instante | — | — | 0.000 ± 0.000 |
| BB-I disparos del perk | — | — | 2.645 ± 0.081 |
| BB-L salida del campo tras salto | 0.000 ± 0.000 | 0.000 ± 0.000 | — |
| BF-C placajes sin balón (todos los puestos) | 2.555 ± 0.101 | 2.555 ± 0.101 | — |
| BF-C ticks de delantero eligiendo Tackle/Block sin portador | 14.510 ± 0.687 | 14.510 ± 0.687 | — |
| BH-A ticks con el dueño fuera del campo | 0.000 ± 0.000 | 0.000 ± 0.000 | — |
