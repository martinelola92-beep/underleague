# Barrido de detectores — 1000 partidos por traza, semillas 1..1000

Tres trazas: `ref` = `TestMatches.Reference` (semilla = emparejamiento); `run` = el primer partido del acto 1 de una run de `human_abattoir` con esa semilla (lo que reproduce Godot con `-- movimiento <carpeta> <semilla>`); `armado` = `ref` con `box_predator` en todos los jugadores de campo (sólo BB-I). Casos/partido ± error típico de la media.

| Detector | ref casos/partido ± e.t. | ref % partidos | run casos/partido ± e.t. | run % partidos | Peores (traza:semilla@tick, magnitud) |
|---|---|---|---|---|---|
| BA-E gol sin ángulo | 0.176 ± 0.013 | 16.3 % (163/1000) | 0.143 ± 0.012 | 13.3 % (133/1000) | ref:241@631 (1); ref:410@574 (1); ref:500@783 (1) |
| BA-J sin repliegue tras parada | 1.839 ± 0.040 | 85.5 % (855/1000) | 1.681 ± 0.038 | 83.5 % (835/1000) | ref:14@517 (5); ref:17@109 (5); ref:21@127 (5) |
| BB-A/L salto inexplicado | 0.002 ± 0.001 | 0.2 % (2/1000) | 0.003 ± 0.002 | 0.3 % (3/1000) | run:340@869 (2.49); run:168@1345 (0.78); run:820@325 (0.69) |
| BB-B robo antes de cualquier saque | 0.000 ± 0.000 | 0.0 % (0/1000) | 0.000 ± 0.000 | 0.0 % (0/1000) | — |
| BB-B robo antes del saque de centro | 0.000 ± 0.000 | 0.0 % (0/1000) | 0.000 ± 0.000 | 0.0 % (0/1000) | — |
| BB-C celebración (salto o en su campo) | 0.000 ± 0.000 | 0.0 % (0/1000) | 0.000 ± 0.000 | 0.0 % (0/1000) | — |
| BB-G2 portero perseguidor (abrazo mortal) | 0.000 ± 0.000 | 0.0 % (0/1000) | 0.000 ± 0.000 | 0.0 % (0/1000) | — |
| BB-I perk sin tiro [armado] | 0.000 ± 0.000 | 0.0 % (0/1000) | — | — | — |
| BB-K baile | 0.864 ± 0.034 | 52.9 % (529/1000) | 0.815 ± 0.035 | 49.4 % (494/1000) | run:827@1934 (21); ref:343@2035 (18); run:379@869 (15) |
| BC-G balón suelto quieto >=15 ticks | 0.043 ± 0.007 | 3.9 % (39/1000) | 0.059 ± 0.008 | 5.5 % (55/1000) | ref:482@1195 (23); run:166@625 (23); run:282@314 (23) |
| BC-G balón suelto quieto >=60 ticks | 0.000 ± 0.000 | 0.0 % (0/1000) | 0.000 ± 0.000 | 0.0 % (0/1000) | — |
| BF-C delantero elige pegar sin portador a su alcance | 2.339 ± 0.072 | 79.6 % (796/1000) | 1.950 ± 0.057 | 78.6 % (786/1000) | run:386@60 (31); ref:77@846 (22); ref:91@128 (22) |
| BF-C delantero pega sin balón | 0.000 ± 0.000 | 0.0 % (0/1000) | 0.001 ± 0.001 | 0.1 % (1/1000) | run:447@107 (1) |
| BH-A congelación | 0.000 ± 0.000 | 0.0 % (0/1000) | 0.001 ± 0.001 | 0.1 % (1/1000) | run:130@1025 (280) |
| BN-A amontonamiento sobre el portero | 0.765 ± 0.028 | 53.2 % (532/1000) | 0.645 ± 0.025 | 48.5 % (485/1000) | ref:44@140 (5); ref:58@180 (5); ref:117@669 (5) |
| BO-A portador y rival atascados >3 s | 0.047 ± 0.007 | 4.6 % (46/1000) | 0.048 ± 0.007 | 4.7 % (47/1000) | run:810@1175 (62); ref:29@1081 (60); run:162@89 (56) |

## Métricas auxiliares (media por partido ± e.t.)

| Métrica | ref | run | armado |
|---|---|---|---|
| BA-E goles | 1.920 ± 0.034 | 1.844 ± 0.033 | — |
| BA-E tiros | 8.082 ± 0.104 | 7.844 ± 0.102 | — |
| BA-E tiros sin ángulo (apertura<0,5) | 0.643 ± 0.026 | 0.475 ± 0.023 | — |
| BA-J paradas retenidas | 2.355 ± 0.049 | 2.223 ± 0.048 | — |
| BA-J rezagados medios por parada retenida | 2.225 ± 0.036 | 2.127 ± 0.036 | — |
| BB-A salto de >0,6 en reanudación (apartar/colocar, diseño) | 7.343 ± 0.158 | 5.168 ± 0.136 | — |
| BB-A salto explicado (reposición, control) | 19.204 ± 0.390 | 18.660 ± 0.390 | — |
| BB-G2 ticks del portero eligiendo ChaseBall fuera del área | 1.981 ± 0.158 | 1.370 ± 0.125 | — |
| BB-I con tiro bloqueado al instante | — | — | 0.000 ± 0.000 |
| BB-I disparos del perk | — | — | 2.806 ± 0.051 |
| BB-L salida del campo tras salto | 0.000 ± 0.000 | 0.000 ± 0.000 | — |
| BF-C placajes sin balón (todos los puestos) | 2.807 ± 0.067 | 2.387 ± 0.070 | — |
| BF-C ticks de delantero eligiendo Tackle/Block sin portador | 14.839 ± 0.444 | 13.848 ± 0.398 | — |
| BH-A ticks con el dueño fuera del campo | 0.000 ± 0.000 | 0.000 ± 0.000 | — |
