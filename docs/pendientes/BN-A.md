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

- **REJECTED bajo el motor sin la posesión táctica** (27 sep 2026): que sea porque, sin dueño del balón,
  el equipo que saca «no tiene la posesión» (`HoldingTeam = -1`). Darle sólo esa posesión deja el borde
  igual (2,52 frente a 2,57). Pero el motor tiene **dos** ideas de posesión y la táctica (`TacticalStates`:
  pesos y bloque) seguía en «sin balón»; la hipótesis queda abierta para la versión que cambia las dos.
- **LIKELY, sin experimento propio**: es la **posición de formación** de los defensas —en un campo de 16
  columnas su casilla está a 2-3 columnas de la portería— más el delantero rival que los cubre.

### «La reanudación es del que la saca»: aplicada ([ADR 0155](../decisiones/0155-la-reanudacion-es-del-equipo-que-la-saca.md)) sabiendo su coste

El revisor la aprobó («2 sí»). La versión completa —las dos posesiones leen de `RestartHolder`, penalti y
saque de centro fuera— hace lo que promete: el equipo que saca se prepara para recibir en banda 91,6 %,
puerta 96,6 %, falta 97,2 % y córner 100 % (sin la regla, 0-6 %). Pero medida en **cuatro semillas**
(4.000 partidos cada una, contra `816619e`) **baja la carnicería**:

| semilla | goles | lesiones por partido | entradas |
|---|---|---|---|
| 1 | +0,12 ± 0,03 | 0,80 → 0,69 (−14 %) | 9,0 → 7,9 |
| 2 | −0,06 ± 0,02 | 0,41 → 0,40 (−4 %) | 9,8 → 9,3 |
| 3 | +0,15 ± 0,02 | 0,55 → 0,45 (−18 %) | 10,6 → 9,0 |
| 4 | +0,10 ± 0,02 | 0,90 → 0,76 (−16 %) | 10,6 → 8,6 |

- **REJECTED** como causa de la bajada: que la transición (12 ticks de empuje del que pierde el balón) se
  gastara durante el balón parado; congelarla deja las lesiones casi igual (−11 / −6 / −15 / −17 %).
- La causa sigue sin aislar. Se paró hasta que el revisor decidiera con este dato, y decidió: **«aplica»**.
  La carnicería se compensa en la fase de balance.
- La primera versión (sólo `HoldingTeam`) apenas movía el saque de puerta y el de banda; la segunda incluía
  sin decirlo el saque de centro, que explicaba más de la mitad de la subida de goles de una semilla.
  Las dos las cazó la revisión independiente.
- De paso salieron dos defectos anteriores, arreglados aparte: el penalti pitado a mitad de tick no vaciaba
  el área ese tick, y un rival en una esquina del campo dentro de la barrera no puede salir sin cruzar por
  delante del balón (límite geométrico documentado; el test lo exime sólo a él).

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

## Barrido de detectores (3 oct 2026)

**CONFIRMED, residual.** Detector validado contra la build anterior al arreglo (3,02 episodios/partido, 89,8 % de partidos) y
sintéticos. Build actual: **0,60 ± 0,02 por partido** (`ref`, 46 %) y 0,52 (`run`, 42 %): la ADR 0152 lo bajó unas cinco veces
pero no lo cerró. **Ninguno es un saque de puerta**: los 1.113 episodios ocurren en juego abierto, con el portero reteniendo 20
ticks y 5 compañeros a menos de 2 casillas. Peores: `ref` semillas 205 (tick 1669), 411 (tick 405), 466 (tick 1163) y
`run` semilla 61 tick 1509. Posible conexión con la pausa del portero de la ADR 0178 (BA-J): LIKELY, sin aislar. Informe: [barrido-detectores-2026-10-03](../analisis/barrido-detectores-2026-10-03.md).

## Sube tras la ADR 0184: es el grupo del instante de la parada, no un amontonamiento (3 oct 2026, tarde)

Barrido de la tarde: **0,596 → 0,765 ± 0,028** por partido (`ref`), 0,517 → 0,645 (`run`). **No se arregla en `/Sim`**;
queda como decisión del revisor.

- **CONFIRMED, la sostenida (ADR 0184)** — sonda por variante, 1.000 partidos por traza (`WorstCaseProbeTests.StuckAndCrowdByVariant`),
  que reproduce el barrido al milésimo: `main` 0,765 / 0,645; sostenida a 0 0,571 / 0,478; sólo BV-B apagada 0,699 /
  0,571; las dos apagadas 0,603 / 0,511 (la mañana: 0,596 / 0,517). La ADR 0188 no pesa (la variante «ninguna» la lleva).
- **Qué cuenta el detector — CONFIRMED con la misma sonda**: en el **95 %** de los casos (745 de 780) el grupo ya está en el
  fotograma en que el portero coge el balón, casi siempre tras una parada retenida (`SAVE held`, 556), y son defensas y
  medios que estaban **cubriendo dentro de su área** (`CoverSpace` el tick anterior: 893 + 275 dentro del área, 715 fuera).
  Se disuelve andando (RF-053): con ≥ 2 a menos de 2 casillas quedan 573 a los 3 ticks, 220 a los 6, 84 a los 10 y 18 a los
  15. Sin sostenida el perfil de disolución es el mismo (10 ticks: 11 % frente a 9 %); lo que cambia es **cuántas paradas
  pillan defensas cubriendo dentro** (745 frente a 532): la línea que la ADR 0184 sostiene.
- **REJECTED como arreglo, medido**: que el hueco de `FindSpace` respete el área cerrada (llevar la candidata al borde,
  como hace el movimiento). Quita los huecos dentro del área (206 → 0) pero el borde queda junto al portero y la
  persistencia sube (a los 10 ticks 84 → 103, a los 15 18 → 37); el total no se mueve (0,780 → 0,757 ± 0,027). Revertido.
- **Instrumento nuevo** (`SymptomDetectors.GoalkeeperCrowdPersisting`, fila propia en el barrido, validado en sintético):
  el grupo que **sigue** a los 10 ticks (umbral provisional: el tiempo en que se ha disuelto el 85-90 % de los grupos).
  `main` 0,084 / 0,068, la mañana (variante «ninguna») 0,042 / 0,035: también se dobla, por la misma razón (más paradas
  con defensas dentro), y a los 10 ticks los que quedan siguen saliendo (destino «fuera» 109 de 167; 47 con el hueco
  dentro del área).

**Decisión que queda para el revisor** (cambia una regla, no se hace sin él): lo que se mide no es que los compañeros
vayan hacia el portero, sino que estaban allí defendiendo y salen andando. Volver a 0,60 pide (a) que la línea no
sostenga la cobertura dentro de su propia área —tocar la ADR 0184—, (b) salir del área cerrada más deprisa que andando
—tocar RF-053 en la ADR 0152—, o (c) aceptar que el detector de base cuenta defensa legítima y vigilar la fila
persistente.

**Por defecto, (c)** (coordinador, 3 oct 2026, tras la revisión independiente): la ficha sigue **abierta**; se vigila la
fila «BN-A amontonamiento que sigue a los 10 ticks» del barrido (hoy 0,084 / 0,068) y no se toca la regla mientras el
revisor no elija (a) o (b).
