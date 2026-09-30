# BU-A — La turba no estrechaba el campo ni aceleraba el juego (RF-055b)

Estado: **Implementada, medida y revisada** (30 sep 2026) por la [ADR 0175](../decisiones/0175-la-turba-estrecha-el-campo.md).
Quedan abiertas dos cosas pequeñas, en las secciones 5 y 6.

## 1. El problema original

RF-055b prometía tres cosas al entrar la turba: el árbitro se va, el campo se estrecha una fila por lado con casillas
invadidas **fijas y anunciadas**, y la velocidad global sube un 15 %. Sólo existía la primera. Knavall F7 lo llamaba «turba
real»: hoy era una etiqueta. Regla G (`tools/existe-ya.sh estrecha invadid`): no había nada por el concepto.

## 2. Alternativas descartadas (antes de implementar)

| alternativa | motivo |
|---|---|
| cambiar `Pitch.Rows` por fase | ~90 usos: rompe colocación, cámara y el reglamentario entero |
| cuadrícula estrechada de verdad (16×5) | mismo coste y no da «casillas invadidas» |
| sortear las filas cada partido | contradice RF-055b («fijas y anunciadas») y RF-012d |
| acotar sólo a los jugadores, el balón en 7 filas | el balón «sale» por debajo de un público que lo ocupa |
| subir los ticks de estados y cooldowns un 15 % | cambiaría las probabilidades por tick de las entradas sin que el jugador lo vea; se sube movimiento y golpes |

## 3. Hipótesis que la medición tocó

- **REJECTED (descartada bajo la ADR 0175 y el árbol de 6441a1e): «menos espacio + más velocidad = más daño sin árbitro»
  (RF-055d).** Las lesiones por turba no suben: −0,027 ± 0,017 (sin conclusión) emparejado por semilla; la turba dura ~80
  ticks menos (−79,7 ± 10,4, CONFIRMED). El primer lote decía −17 % (0,115 → 0,096) y con el árbol nuevo no se sostiene.
- **CONFIRMED (bug propio): acotar sólo el movimiento no basta.** El balón iba a destinos calculados contra el campo entero
  (huecos de la utilidad, pases, despejes) y salía de banda 7,57 veces por turba (0,48 sin la ADR); acotados los destinos,
  0,47. Fue la primera lectura de campaña (`runWinRate` +6) y no era la mecánica (Regla J).
- **CONFIRMED (revisión independiente): al refactorizar a `PlayBand` se perdió en `BodySeparation` el acotado del portero al
  área** (RF-057b). Restaurado en un commit propio con test (4 casos, con control positivo: falla sin la línea).
- **CONFIRMED (interruptores): el estrechamiento, no la velocidad, baja un poco a la raza elfa** en `RaceBalanceTests`
  (39,55 antes del rebase). Con el árbol de main posterior: 41,92 frente a 42,45, dentro de 1 error típico; la enmienda
  del suelo a 38 **se retiró** (el suelo sigue en 40).
- **Alternativa descartada para la puerta de razas: excluir la turba.** La puerta mide la raza en el juego real y en el
  juego real hay turba.

## 4. Lo demostrado

- CONFIRMED (test): el reglamentario es byte a byte el de main (huella sobre eventos, 44 partidos sin turba; **no cubre
  posiciones flotantes**) y, con `mob` a 0/0, la traza completa de 60 semillas también.
- CONFIRMED (test): nadie salta ni se aleja de la banda; los saques salen de dentro de la banda; +15 % exacto por unidad
  (carrera, pase, tiro, cabeceo, rechace); el suplente entra dentro de la banda; el área más su margen de salida caben en la
  banda con el máximo de filas que admite el esquema (1).

## 5. Abierta: `FullRunGateTests.TheThreeDoctrinesBuyDifferently` (LIKELY ruido de semilla)

Rojo en la rama con la semilla de la puerta (ahorradora 14,95 frente a contextual 15,94 de oro sobrante: el signo de la
desigualdad esperada se invierte) y verde en main. Con cuatro semillas (1, 10000, 20000, 30000): rama 2 rojas de 4, main 1 de
4. Con `mob` a 0/0 la rama pasa. **LIKELY** que el signo dependa de la muestra (240 runs por doctrina, diferencias de 0,5 a 1
punto sobre ~15): main también la pierde con otra semilla. No distinguible con esta muestra de 2/4 frente a 1/4. Cierre: más
semillas (p. ej. 12 por árbol) o una aserción con margen; **no se toca la puerta sin medir** (RT-057).

## 6. Abierta: decisión de diseño anotada, no tomada

La turba hace algo menos (o lo mismo) de daño, no más, contra la lectura de RF-055d («ventana natural de las builds de
violencia»). Si el revisor quiere una turba más violenta, la palanca es otra (el tipo `frenzy`), no el estrechamiento.
Tampoco se verificó visualmente un saque de banda cercano con el público delante (cinco semillas de captura sin ninguno).
