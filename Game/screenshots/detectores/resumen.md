# Barrido de detectores — 1000 partidos por traza, semillas 1..1000

Tres trazas: `ref` = `TestMatches.Reference` (semilla = emparejamiento); `run` = el primer partido del acto 1 de una run de `human_abattoir` con esa semilla (lo que reproduce Godot con `-- movimiento <carpeta> <semilla>`); `armado` = `ref` con `box_predator` en todos los jugadores de campo (sólo BB-I). Casos/partido ± error típico de la media.

| Detector | ref casos/partido ± e.t. | ref % partidos | run casos/partido ± e.t. | run % partidos | Peores (traza:semilla@tick, magnitud) |
|---|---|---|---|---|---|
| BA-E gol sin ángulo | 0.234 ± 0.015 | 21.0 % (210/1000) | 0.178 ± 0.013 | 16.9 % (169/1000) | ref:32@165 (1); ref:35@126 (1); ref:39@130 (1) |
| BA-J sin repliegue tras parada | 1.691 ± 0.040 | 83.6 % (836/1000) | 1.733 ± 0.040 | 85.2 % (852/1000) | ref:22@769 (5); ref:77@1555 (5); ref:84@1670 (5) |
| BB-A/L salto inexplicado | 0.003 ± 0.002 | 0.3 % (3/1000) | 0.002 ± 0.001 | 0.2 % (2/1000) | run:57@1377 (1.11); ref:74@955 (0.93); ref:343@394 (0.92) |
| BB-B robo antes de cualquier saque | 0.000 ± 0.000 | 0.0 % (0/1000) | 0.000 ± 0.000 | 0.0 % (0/1000) | — |
| BB-B robo antes del saque de centro | 0.000 ± 0.000 | 0.0 % (0/1000) | 0.000 ± 0.000 | 0.0 % (0/1000) | — |
| BB-C celebración (salto o en su campo) | 0.000 ± 0.000 | 0.0 % (0/1000) | 0.000 ± 0.000 | 0.0 % (0/1000) | — |
| BB-G2 portero perseguidor (abrazo mortal) | 0.000 ± 0.000 | 0.0 % (0/1000) | 0.000 ± 0.000 | 0.0 % (0/1000) | — |
| BB-I perk sin tiro [armado] | 0.000 ± 0.000 | 0.0 % (0/1000) | — | — | — |
| BB-K baile | 0.938 ± 0.045 | 46.2 % (462/1000) | 0.789 ± 0.043 | 40.4 % (404/1000) | run:775@1821 (54); run:775@1822 (53); ref:487@1181 (52) |
| BC-G balón suelto quieto >=15 ticks | 0.093 ± 0.010 | 8.6 % (86/1000) | 0.130 ± 0.012 | 11.4 % (114/1000) | ref:232@875 (26); ref:335@1375 (25); run:640@1177 (25) |
| BC-G balón suelto quieto >=60 ticks | 0.000 ± 0.000 | 0.0 % (0/1000) | 0.000 ± 0.000 | 0.0 % (0/1000) | — |
| BF-C delantero elige pegar sin portador a su alcance | 1.921 ± 0.061 | 74.4 % (744/1000) | 1.588 ± 0.056 | 69.6 % (696/1000) | run:794@405 (31); run:283@406 (25); ref:8@100 (22) |
| BF-C delantero pega sin balón | 0.000 ± 0.000 | 0.0 % (0/1000) | 0.000 ± 0.000 | 0.0 % (0/1000) | — |
| BH-A congelación | 0.000 ± 0.000 | 0.0 % (0/1000) | 0.000 ± 0.000 | 0.0 % (0/1000) | — |
| BN-A amontonamiento sobre el portero | 0.596 ± 0.024 | 46.0 % (460/1000) | 0.517 ± 0.022 | 41.9 % (419/1000) | ref:205@1669 (5); ref:411@405 (5); ref:466@1163 (5) |
| BO-A portador y rival atascados >3 s | 0.024 ± 0.005 | 2.4 % (24/1000) | 0.036 ± 0.006 | 3.4 % (34/1000) | run:743@1127 (173); ref:59@252 (53); ref:97@368 (53) |

## Métricas auxiliares (media por partido ± e.t.)

| Métrica | ref | run | armado |
|---|---|---|---|
| BA-E goles | 1.989 ± 0.036 | 2.001 ± 0.034 | — |
| BA-E tiros | 8.148 ± 0.111 | 8.585 ± 0.110 | — |
| BA-E tiros sin ángulo (apertura<0,5) | 0.764 ± 0.027 | 0.618 ± 0.025 | — |
| BA-J paradas retenidas | 2.299 ± 0.050 | 2.425 ± 0.052 | — |
| BA-J rezagados medios por parada retenida | 2.166 ± 0.038 | 2.101 ± 0.037 | — |
| BB-A salto de >0,6 en reanudación (apartar/colocar, diseño) | 6.478 ± 0.140 | 4.496 ± 0.119 | — |
| BB-A salto explicado (reposición, control) | 19.981 ± 0.413 | 20.228 ± 0.397 | — |
| BB-G2 ticks del portero eligiendo ChaseBall fuera del área | 2.324 ± 0.180 | 1.708 ± 0.143 | — |
| BB-I con tiro bloqueado al instante | — | — | 0.000 ± 0.000 |
| BB-I disparos del perk | — | — | 2.648 ± 0.050 |
| BB-L salida del campo tras salto | 0.000 ± 0.000 | 0.000 ± 0.000 | — |
| BF-C placajes sin balón (todos los puestos) | 2.423 ± 0.063 | 2.218 ± 0.068 | — |
| BF-C ticks de delantero eligiendo Tackle/Block sin portador | 11.864 ± 0.381 | 10.543 ± 0.361 | — |
| BH-A ticks con el dueño fuera del campo | 0.000 ± 0.000 | 0.000 ± 0.000 | — |
