# BN-A — Los jugadores se amontonan sobre el portero cuando tiene el balón

Estado: **Implementada** ([ADR 0152](../decisiones/0152-el-area-del-portero-se-cierra-y-el-portero-saca-con-comba.md), 27 sep 2026): el área del
portero se cierra cuando tiene el balón y el portero despeja con comba. **Queda abierto** el grupo en el
borde del área en el saque de puerta (abajo).

## Observación

«A veces el portero atrapa un balón y los jugadores de su equipo se amontonan sobre él. Cuando el portero
atrapa un balón todos los jugadores deben alejarse de él. En el fútbol real hay un área y cuando el portero
tiene el balón deben salir de ella.» (Revisor, 27 sep 2026.)

## Medición antes de tocar nada (60 partidos `TestMatches.Reference`)

| cómo llega el balón al portero | n | ticks con él | compañeros a <1 casilla | a <2 (≥2 en…) |
|---|---|---|---|---|
| saque de puerta (`RECOVERY/goalKick`) | 109 | 5 | **2,41** | 2,67 (89 %) |
| parada atrapada (`SAVE/held`) | 114 | 5 | 0,12 | 0,96 (32 %) |
| balón suelto (`RECOVERY/loose`) | 139 | 5 | 0,06 | 0,77 (27 %) |

**CONFIRMED**: el amontonamiento está en el **saque de puerta**, no en la parada. Durante su cuenta atrás
los defensas y centrocampistas del equipo que saca están en `CoverSpace` (87 de 88 defensas) y su
destino —entre el balón y la portería— cae dentro del área, junto al portero. Tras una parada el portero
suelta el balón a los 5 ticks y apenas da tiempo a nada.

## Arreglo (regla del revisor)

`MatchEngine.ClosedKeeperArea`: el área de un equipo está **cerrada** cuando su portero tiene el balón
dentro de ella o su equipo tiene un saque de puerta pendiente. `Move` lleva al borde más cercano el
destino de cualquier jugador de campo, de los dos equipos, que caiga dentro, y no deja que el paso de uno
que va por fuera corte una esquina. Se sale **andando** (RF-053).

Medido: 0 jugadores dentro del área al sacar de puerta y **0 compañeros a <1 casilla del portero** (antes
2,41). Nadie entra con el área cerrada; la separación de cuerpos mete a alguien como mucho 0,057 casillas
(75 veces en 83.513 comprobaciones). Balance, 4.000 partidos × 2 semillas contra `c2ee03c`: ningún rango
se mueve; goles, tiros, cambios de posesión, cadenas de pase y reparto por tercios iguales dentro del
ruido; entradas −0,22 / −0,54. Test permanente `KeeperAreaTests`. Captura `area-saque-puerta.png`.

## Lo que queda abierto: el grupo en el borde del área

La captura del saque de puerta enseña a la defensa **en el borde del área**, en fila, con el delantero
rival encima: 2,5 compañeros y 1,0 rivales por saque a menos de 0,6 casillas del borde.

- **REJECTED** (bajo el motor de 27 sep 2026): que sea porque, sin dueño del balón, el equipo que saca «no
  tiene la posesión» (`HoldingTeam = -1`) y no puede buscar hueco. Darle la posesión durante el saque de
  puerta deja el borde igual (2,52 frente a 2,57) y no mueve ninguna métrica.
- **LIKELY, sin experimento propio**: es la **posición de formación** de los defensas —en un campo de 16
  columnas su casilla está a 2-3 columnas de la portería— más el delantero rival que los cubre. No es
  gente persiguiendo al portero; si molesta, es una decisión de colocación en el saque de puerta.
- **Medido y NO aplicado**: dar la posesión al equipo que saca en **todas** las reanudaciones cambia el
  carácter de córners, bandas y faltas en las dos semillas: +0,3 centros, +0,5 pases en profundidad, +2
  puntos de tiros desde la línea de fondo, algo menos de tiros. Es la regla correcta en concepto (una
  reanudación es del que la saca), pero cambia el juego y lo decide el revisor.

## El efecto de segundo orden, y lo que decidió el revisor

La revisión independiente midió que cerrar el área multiplicaba un bucle ya existente: el portero despeja,
un rival **solo** lo cabecea de vuelta sin ningún evento y el portero lo atrapa otra vez (361 → 548 en 150
partidos). **CONFIRMED** con un evento de diagnóstico temporal: casi todas las vueltas son «despeje → rival
solo cabecea (a veces dos o tres) → el portero lo coge».

- **REJECTED** como arreglo: que el que está solo controle el balón. Bucle 548 → 3, pero goles +34 % y
  `possessionChanges` fuera de banda: el despeje caía en un rival libre.
- **Decisión del revisor**: el problema es cómo saca el portero. Despeje del portero con pico 3,5 (ADR
  0152). Contado de forma estructural (siguiente dueño del balón): 551 → **4** vueltas en 150 partidos;
  goles iguales, más entradas y lesiones, más juego en el centro, nada fuera de banda. Se probó además
  +3 casillas de distancia y se quitó: no hacía falta y bajaba los goles un 17 %.
- **Corrección de instrumento** (revisión independiente, regla J): el primer contador miraba 40 ticks
  desde el despeje y el despeje nuevo tarda 36-44 sólo en volar; su «548 → 0» no probaba nada.

## Hermanos

[BI-F](./BI-F.md) (el saque de puerta saca del área a los rivales, de golpe), ADR 0143 (el penalti), la
barrera de BB-B.
