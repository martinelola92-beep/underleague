# BX-8 — El portero saca de puerta sin criterio

**Síntoma (revisor, partida del 3 oct):** «el portero saca de puerta sin criterio: lanza el balón al centro, hacia los
rivales, en vez de buscar a un compañero abierto». Captura `capturas/playtest-3oct-saque-portero.png`.

## Medición (9 oct 2026)

Sonda desechable con `SimConfig.Trace` sobre 200 partidos de referencia: 443 saques de puerta, 2,2 por partido.

| Acción del portero al sacar | Cuota | La recibe su equipo | La recibe el rival |
|---|---|---|---|
| `Clear` (despeje) | **89 %** | 8 % | **92 %** |
| `ShortPass` | 11 % | 35 % | 65 % |
| `LongPass` | 0 % | — | — |

- Hay un compañero libre (a ≤ 7 casillas del portero, sin rival a menos de 2) en el 29 % de los saques.
- El balón se controla sobre todo en las filas centrales (2-4: 296 de 427).

## Causa — CONFIRMED (volcado de utilidad, RT-098)

Semillas 1 y 2, cuatro saques de puerta, la misma tabla:

| Acción | Base | Contexto | Puntuación |
|---|---|---|---|
| `Clear` | 300 | **+620** | 920 |
| `ShortPass` | 600 | +218..232 | 818 (982 con el rasgo ×1,25) |
| `LongPass` | 320 | +210..280 | 530..817 |

El +620 es `clearBase` 150 + `clearDangerBonusPerCenti` 5 × peligro 94 (`Utility.EvaluateClear`). `ctx.Danger` mide lo
cerca que está el balón de la propia portería, y en un saque de puerta el balón está en el área chica, así que **el
peligro sale casi al máximo sin amenaza ninguna**: el balón está muerto y los rivales tienen que salir del área (BI-F).
La precondición del despeje («un despeje sin peligro no es prudencia, es regalar el balón», P4) existe, pero el
instrumento que la mide confunde *posición* con *amenaza* en la reanudación.

`ShortPass` sólo gana cuando el portero tiene un rasgo que lo multiplica. Y aun así pierde el 65 %: es la segunda
mitad del síntoma, sin diagnosticar todavía.

## Hipótesis abiertas sobre el 65 % del pase corto

- (a) Los rivales esperan justo en el borde del área y cortan el pase.
- (b) El receptor elegido no es el compañero libre.
- (c) El pase tiene poco alcance.

Sin medir.

## Revisión de diseño (skill `game-design-review`, 9 oct 2026)

La regla ya existe y no hay que inventarla. La **ADR 0141 §2** escribe el saque del portero como una decisión: «corto
si hay alguien seguro, largo si no» y, si no hay opción segura, despejar. El tercer caso se dio por bueno «sin código
nuevo» porque «el peligro en su propia área es alto por definición». Esa frase es el fallo: si el peligro es siempre
alto en el área, el despeje gana **siempre** y los dos primeros casos no llegan a decidir nada.

1. **Qué experimenta el jugador:** su portero regala el balón en casi cada saque (92 %), sin que importe dónde estén
   sus compañeros.
2. **Qué decide hoy con esto:** nada. Colocar un lateral abierto no cambia el saque.
3. **Qué debería decidir:** que la colocación (y el rasgo del portero) cambie lo que pasa con el saque. Un defensa
   abierto recibe; un equipo apretado arriba obliga a jugar largo o a despejar.
4. **Regla:** ADR 0141 §2 y ADR 0143 §1 (el saque de puerta admite pase corto, largo o despeje), RF-053 (el saque de
   puerta sólo vacía el área). Se cumple una regla escrita; no se crea ninguna.
5. **Sistemas:** sólo `/Sim`, `Utility.EvaluateClear`. `/Game` dibuja lo que salga. Nada en `/data` (ver 6).
6. **Alternativas:**
   - (A) Quitar el despeje del saque de puerta: contradice el tercer caso de la ADR 0141.
   - (B) Peligro 0 para el sacador: equivale a A.
   - (C) **En el saque de puerta el despeje no cobra el bono de peligro** (balón muerto, rivales fuera del área: no
     hay amenaza que medir); sigue cobrando base y presión. Gana sólo cuando los dos pases puntúan por debajo, que es
     el «si no hay opción segura».
   - (D) Bajar `clearDangerBonusPerCenti` en `/data`: cambia también el despeje en juego abierto, que sí funciona.
     Descartada.

   **Elegida: C.**
7. **Trade-off:** el saque deja de ser un trámite neutro y pasa a depender de la colocación; el equipo que presiona
   arriba gana más balones en el saque rival. Es coste legible, no poder gratis.
8. **Estrategias:** da valor a la orden defensiva o neutra con laterales abiertos, y a presionar arriba al rival. Los
   rasgos de pase del portero empiezan a pesar en el saque.
9. **Degeneración:** si el pase corto sigue perdiéndose el 65 %, cambiar despejes por pases cortos puede dar el balón
   **más cerca** de la propia portería. Se mide: dónde se pierde, goles encajados tras saque, y el lote completo.
10. **Demostración:**
    - la sonda de esta ficha antes/después (cuota por acción, recuperación propia, fila);
    - un test permanente: en un saque de puerta el despeje puntúa sin el bono de peligro;
    - el lote de `/Balance` (skill `balance-measure`), dado que mueve posesión y tiros;
    - revisión independiente.

## Segunda causa: el «pase corto» del portero no tenía alcance — CONFIRMED

Con sólo el arreglo C, el portero pasaba en corto el 100 % de los saques y conservaba el balón el 40 %. Medido sobre
376 pases:
- el 76 % acababa en balón suelto, el 9 % interceptado y sólo el 9 % se completaba;
- el receptor estaba a **9,7 casillas** de mediana (el pase corto llega a 3);
- el receptor estaba libre en el 89 % de los pases.

Hipótesis (a) y (b) **REJECTED**; (c) **CONFIRMED**.

`Utility.EvaluatePass` filtraba el alcance con `p.IsOutfield && distance > maxCells`. Su propio resumen dice que el
portero se salta el tope **sólo en el pase largo** («el saque de puerta llega a donde llega»), pero la condición lo
eximía en los dos. Su «pase corto», la base más alta de la tabla, iba al compañero más adelantado. Arreglo:
`(p.IsOutfield || !longPass) && distance > maxCells`.

## Resultado (sonda, 200 partidos de referencia)

| | Despeje | Pase largo | Pase corto | Su equipo conserva el balón |
|---|---|---|---|---|
| Antes | 89 % | 0 % | 11 % | ~11 % |
| Sólo C | 0 % | 0 % | 100 % | 40 % |
| **C + alcance (implementado)** | 5 % | 93 % | 2 % (lo conserva el 100 %) | **~38 %** |
| C + alcance + tope de 8 c en el largo (no implementado) | 38 % | 59 % (lo conserva el 75 %) | 3 % | ~49 % |

**Decisión abierta para el revisor:** la última fila quitaría «el saque de puerta llega a donde llega», que es diseño
escrito a propósito (ADR 0030). Con el tope, el despeje queda justo para cuando no hay nadie libre a 8 casillas (hay
compañero libre en el 3 % de esos despejes), que es la letra de la ADR 0141, y el equipo conserva más. Sin el tope, el
pase largo del portero va al más adelantado, a ~9,7 casillas, y el 71 % termina en balón suelto disputado. No lo
implemento sin su decisión.

## Balance (`/Balance --runs 3000`, semillas 1 y 2; cada término con el otro apagado)

| Celda | Goles | Tiros | Entradas | Tiros de ángulo cerrado |
|---|---|---|---|---|
| base | 2,43 / 2,18 | 9,10 / 8,59 | 9,6 / 11,8 | 9,8 % / 11,1 % |
| sólo C (despeje) | 2,09 / 1,95 | 8,07 / 7,94 | 11,0 / 12,7 | 12,9 % / 13,2 % |
| sólo alcance | 2,49 / 2,35 | 9,29 / 8,95 | 9,6 / 11,3 | 9,5 % / — |
| C + alcance | 2,11 / 1,99 | 8,14 / 7,97 | 10,8 / 12,9 | 12,4 % / 12,8 % |

- Ninguna métrica con banda sale de ella.
- El movimiento es del término C, el mismo con las dos semillas: el portero ya no regala el balón al lado de su área,
  así que hay menos contraataques centrales. Unos −0,3 goles y −1 tiro por partido, que quedan en banda (7-15) pero
  más cerca del suelo.
- Los tiros de ángulo cerrado y desde la línea de fondo suben 2-3 puntos de cuota. En absoluto es poco (0,89 → 1,01
  por partido): es composición, porque desaparecen los centrales del contraataque.
- La victoria del equipo mejor se mueve en direcciones contrarias según la semilla: ruido.

## Puertas (`Category=Gate`, completas, base y cambio en el mismo árbol)

Las mismas 6 rojas en la base (HEAD) y con el cambio; ninguna roja nueva:

| Puerta | Base | Con BX-8 |
|---|---|---|
| `coherentBuildsBeatNone_orc_violence` (≥ 58) | 52,16 | 55,68 |
| `badBuildsLoseToNone_elf_brawler` (≤ 45) | 45,26 | 46,04 |
| `betterTeamWinRate_human_60_vs_human_40` (≤ 90) | 90,36 | 90,96 |
| `bossGate_grimhold_guns_correct` (65-80 ±2,5) | **61,84 roja** | en banda |
| `bossGate_eternal_crown_good` (40-55 ±2,5) | en banda | **35,37 roja** |
| `bossGate_eternal_crown_excellent` (50-70 ±2,5) | 41,48 | 39,96 |

Dentro de la puerta de jefes, una submétrica entra en banda y otra sale. **Sin evidencia de que sea efecto y no
ruido**: una sola semilla. Queda anotada para el siguiente lote de jefes.

## Revisión independiente (10 oct) y lo medido después

Hallazgos y estado:

1. **Puerta de jefes.** `eternal_crown_good` entra en rojo y `grimhold_guns_correct` sale, con una sola semilla. El
   revisor tiene razón: «ruido» no estaba medido. Se mide con la semilla 2 (abajo).
2. **El síntoma no está resuelto del todo.** Es cierto: el rival se queda ~62 % de los saques, porque el pase largo
   del portero va al más adelantado (`rank = advance`, sin coste por distancia) a ~9,7 casillas.
   - Mejora mucho sobre el 92 %, pero no es «buscar a un compañero abierto».
   - Lo que lo cierra es la decisión abierta de arriba (tope del pase largo del portero), que es del revisor.
3. **Hermano: el portero con el balón en juego abierto** (tras blocar). Medido con una sonda desechable sobre 200
   partidos, base → cambio:

   | | Despeje | Pase corto | Conserva el pase corto | Conserva en total |
   |---|---|---|---|---|
   | Base | 82 % | 18 % | 21 % | 24 % |
   | Con BX-8 | 77 % | 22 % | 55 % | 34 % |

   El riesgo de que el arreglo del alcance aumentara los despejes queda **REJECTED**: el juego abierto mejora.
   - El bono de peligro tras blocar no se ha tocado (el despeje sigue en 77 %). Es la misma confusión de posición con
     amenaza, pero tras una parada sí puede haber rivales cerca (rechace). **Sin decidir**, anotado como hermano.
4. **Los pesos del portero se calibraron sobre el fallo** (`ShortPass` 600, `LongPass` 320, desde `1994bae`). Cierto.
   Se anota; recalibrarlos es balance aparte, con su lote.
5. **BN-A / ADR 0152** (la comba de 3,5 sólo va en el despeje del portero). Con menos despejes cambia la forma del
   «despeje devuelto». Sin medir.
6. **Atribución.** La regla «sólo el largo no tiene tope» está en `docs/fase1b-diseno.md:320`, citada por la ADR 0030,
   no en el texto de esa ADR.

### Puerta de jefes con la semilla 2 (la constante `Seed` de `BossGateTests` cambiada temporalmente)

| | `grimhold_guns_correct` | `eternal_crown_correct` | `eternal_crown_good` | `eternal_crown_excellent` |
|---|---|---|---|---|
| Base s1 / s2 | 61,84 / 61,70 (rojas) | en banda / en banda | en banda / en banda | 41,48 / 41,27 (rojas) |
| BX-8 s1 / s2 | verde / verde | en banda / **12,17** | **35,37 / 33,69** | 39,96 / 36,54 |

**CONFIRMED** (dos semillas, misma dirección): el arreglo hace más difícil el jefe final `eternal_crown` en los tres
niveles, en 2-7 puntos, y más fácil `grimhold_guns`, que entra en banda. «Ruido» queda **REJECTED**.

## Estado (10 oct 2026): en la rama `bx8-saque-de-puerta`, NO en `main`

El arreglo es correcto según lo escrito (ADR 0141 §2, `fase1b-diseno.md:320`) y mejora lo que mide:
- el saque de puerta conserva el ~11 % → 38 %;
- el juego abierto del portero, 24 % → 34 %.

Pero empuja una puerta a rojo y deja abierta una decisión que la movería otra vez. Para el revisor:

1. **¿El pase largo del portero tiene tope (8 casillas, como el de todos)?**
   - Con tope: el saque se conserva el ~49 %, y el despeje queda para cuando no hay nadie libre.
   - Sin tope (lo publicado en la rama): «llega a donde llega», 38 %.
2. Con esa respuesta, **recalibrar `eternal_crown`** con su ADR (RT-057), o aceptar la curva nueva. Medir antes con el
   resto de puertas.

Pendientes del paquete, cuando se retome:
- los tests que faltan según la revisión (el despeje cobra el bono de peligro fuera del saque de puerta; quién
  conserva el balón tras el saque);
- los hermanos (bono de peligro tras blocar; falta a favor dentro de la propia área);
- BN-A / ADR 0152 sin medir;
- los pesos del portero calibrados sobre el fallo.
