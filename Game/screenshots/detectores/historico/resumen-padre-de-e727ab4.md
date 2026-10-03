# Barrido de detectores — 400 partidos por traza, semillas 1..400

Tres trazas: `ref` = `TestMatches.Reference` (semilla = emparejamiento); `run` = el primer partido del acto 1 de una run de `human_abattoir` con esa semilla (lo que reproduce Godot con `-- movimiento <carpeta> <semilla>`); `armado` = `ref` con `box_predator` en todos los jugadores de campo (sólo BB-I). Casos/partido ± error típico de la media.

| Detector | ref casos/partido ± e.t. | ref % partidos | run casos/partido ± e.t. | run % partidos | Peores (traza:semilla@tick, magnitud) |
|---|---|---|---|---|---|
| BA-E gol sin ángulo | 0.243 ± 0.025 | 21.0 % (84/400) | 0.243 ± 0.025 | 21.0 % (84/400) | ref:22@126 (1); ref:25@218 (1); ref:35@186 (1) |
| BA-J sin repliegue tras parada | 2.020 ± 0.069 | 87.5 % (350/400) | 2.020 ± 0.069 | 87.5 % (350/400) | ref:3@150 (5); ref:7@543 (5); ref:7@643 (5) |
| BB-A/L salto inexplicado | 0.008 ± 0.004 | 0.8 % (3/400) | 0.008 ± 0.004 | 0.8 % (3/400) | ref:47@201 (1.05); run:47@201 (1.05); ref:207@1105 (0.72) |
| BB-B robo antes de cualquier saque | 0.000 ± 0.000 | 0.0 % (0/400) | 0.000 ± 0.000 | 0.0 % (0/400) | — |
| BB-B robo antes del saque de centro | 0.000 ± 0.000 | 0.0 % (0/400) | 0.000 ± 0.000 | 0.0 % (0/400) | — |
| BB-C celebración (salto o en su campo) | 0.000 ± 0.000 | 0.0 % (0/400) | 0.000 ± 0.000 | 0.0 % (0/400) | — |
| BB-G2 portero perseguidor (abrazo mortal) | 0.000 ± 0.000 | 0.0 % (0/400) | 0.000 ± 0.000 | 0.0 % (0/400) | — |
| BB-I perk sin tiro [armado] | 0.000 ± 0.000 | 0.0 % (0/400) | — | — | — |
| BB-K baile | 18.573 ± 0.629 | 99.5 % (398/400) | 18.573 ± 0.629 | 99.5 % (398/400) | ref:255@738 (113); run:255@738 (113); ref:387@639 (108) |
| BC-G balón suelto quieto >=15 ticks | 0.070 ± 0.013 | 7.0 % (28/400) | 0.070 ± 0.013 | 7.0 % (28/400) | ref:256@1041 (100); run:256@1041 (100); ref:121@747 (85) |
| BC-G balón suelto quieto >=60 ticks | 0.008 ± 0.004 | 0.8 % (3/400) | 0.008 ± 0.004 | 0.8 % (3/400) | ref:256@1041 (100); run:256@1041 (100); ref:121@747 (85) |
| BF-C delantero elige pegar sin portador a su alcance | 1.743 ± 0.106 | 65.0 % (260/400) | 1.743 ± 0.106 | 65.0 % (260/400) | ref:62@286 (22); ref:72@896 (22); ref:93@474 (22) |
| BF-C delantero pega sin balón | 0.000 ± 0.000 | 0.0 % (0/400) | 0.000 ± 0.000 | 0.0 % (0/400) | — |
| BH-A congelación | 0.003 ± 0.002 | 0.3 % (1/400) | 0.003 ± 0.002 | 0.3 % (1/400) | ref:40@618 (628); run:40@618 (628) |
| BN-A amontonamiento sobre el portero | 3.020 ± 0.110 | 89.8 % (359/400) | 3.020 ± 0.110 | 89.8 % (359/400) | ref:12@139 (5); ref:31@1964 (5); ref:49@270 (5) |
| BO-A portador y rival atascados >3 s | 0.105 ± 0.018 | 9.0 % (36/400) | 0.105 ± 0.018 | 9.0 % (36/400) | ref:40@614 (632); run:40@614 (632); ref:92@284 (217) |

## Métricas auxiliares (media por partido ± e.t.)

| Métrica | ref | run | armado |
|---|---|---|---|
| BA-E goles | 1.740 ± 0.060 | 1.740 ± 0.060 | — |
| BA-E tiros | 7.223 ± 0.152 | 7.223 ± 0.152 | — |
| BA-E tiros sin ángulo (apertura<0,5) | 0.810 ± 0.046 | 0.810 ± 0.046 | — |
| BA-J paradas retenidas | 2.028 ± 0.069 | 2.028 ± 0.069 | — |
| BA-J rezagados medios por parada retenida | 3.637 ± 0.077 | 3.637 ± 0.077 | — |
| BB-A salto de >0,6 en reanudación (apartar/colocar, diseño) | 7.668 ± 0.265 | 7.668 ± 0.265 | — |
| BB-A salto explicado (reposición, control) | 17.665 ± 0.678 | 17.665 ± 0.678 | — |
| BB-G2 ticks del portero eligiendo ChaseBall fuera del área | 0.818 ± 0.136 | 0.818 ± 0.136 | — |
| BB-I con tiro bloqueado al instante | — | — | 0.000 ± 0.000 |
| BB-I disparos del perk | — | — | 2.793 ± 0.090 |
| BB-L salida del campo tras salto | 0.000 ± 0.000 | 0.000 ± 0.000 | — |
| BF-C placajes sin balón (todos los puestos) | 2.518 ± 0.102 | 2.518 ± 0.102 | — |
| BF-C ticks de delantero eligiendo Tackle/Block sin portador | 9.785 ± 0.601 | 9.785 ± 0.601 | — |
| BH-A ticks con el dueño fuera del campo | 0.000 ± 0.000 | 0.000 ± 0.000 | — |
