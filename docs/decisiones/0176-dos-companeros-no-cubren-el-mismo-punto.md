# 0176 — Dos compañeros no cubren el mismo punto (BB-K)

Fecha: 30 sep 2026 · Estado: **aceptada** · Requisitos: RF-042, RT-041, RT-096, RT-098 · Ficha: [BB-K](../pendientes/BB-K.md)

## Problema

Dos defensas con zonas solapadas calculaban en `EvaluateCover` el mismo punto (la entrada de la recta balón → portería
propia en su zona) y se quedaban uno encima del otro, empujados por la separación de cuerpos: el «baile». `CoverSpace`
era la única acción de colocación que no miraba a los compañeros (`OfferSupport` y `FindSpace` sí).

## Causa (CONFIRMED, 30 sep)

Volcado de utilidad (semilla 1, tick 1198, ids 1 y 2): los dos eligen `CoverSpace` (738 y 826, con `MarkOpponent`
en 665 y 381) con destinos a 0,04-0,14 casillas. Sonda de baile (racha de ≥ 4 inversiones de rumbo, la de la ficha)
sobre 150 partidos de referencia: **17,3 episodios por partido, 96,6 % en `CoverSpace`, racha máxima de 100 fotogramas**.

## Decisión

`context.coverSpacingCells` (dato, 0 = apagado, bit a bit el motor de antes) = **0,8**. El jugador de id mayor que
cubre (`CoverSpace`, estado `Positioning`) se **desliza por la perpendicular** a la recta de cobertura hasta quedar a
esa distancia del destino del compañero de id menor (reparto por id ascendente, como `Marking`, RT-041/RT-097).
Continuo en el punto bruto (sin umbral que oscile) y medido sobre el bruto. Un primer intento, apartarse
radialmente, chocó con el borde delantero de la zona (tres centrocampistas clavados) y dejó 0,9 episodios de los que
la mitad eran de cobertura; el deslizamiento lateral lo resuelve.

Procedencia del 1,0 (Regla H): barrido en 60 partidos, episodios de cobertura 879 (0), 111 (0,6), 23 (0,8), 25 (1,0),
28 (1,5), 17 (2,0). Por debajo de 0,76 (contacto de dos orcos, `bodyRadius` 38 + 38) no sirve; de 0,8 en adelante
satura. Se toma 1,0 por dejar margen sobre el contacto y ser la menor cifra redonda.

## Efecto medido

Sonda, 150 partidos: 17,3 → **0,9** episodios por partido (cobertura 16,1 → 0,48; racha máxima 100 → 6 en cobertura).
Lo que queda es `FindSpace` (dos compañeros al mismo hueco): hermano anotado, no tocado. Lote de referencia
(`--runs 10000`, semilla 1): ver la ficha.

## Diez preguntas, en corto

1 el jugador ve dos defensas temblando (6-17 veces por partido); 2 cada uno en su sitio; 3 cobertura de zona
(`simulacion.md`); 4 `/Sim` + un dato; 5 hermanos BC-G (histéresis) y `FindSpace`; 6 `OfferSupport`/`FindSpace`
ya lo tenían; 7 precedente `Marking.AssignTeam`; 8 ataca el solapamiento, no el temblor; 9 riesgo: el segundo
defensa se abre y deja pasillo, se mide en el lote; 10 sonda + lote + puertas.

## Enmienda (30 sep 2026): 1,0 → 0,8 por la puerta de `betterTeamWinRate`

Con 1,0 las puertas completas daban `betterTeamWinRate_human_60_vs_human_40` = 95,78 (banda RT-056 70-90, 1.000 partidos,
semilla 1). **Causa CONFIRMED por aislamiento** (`StatisticalTests`, 1.000 partidos): apagar sólo la separación
(0) devuelve la métrica a banda; apagar sólo la marca del delantero (ADR 0179) o sólo la pausa (ADR 0178) no; con las
tres apagadas y sólo el deber de BC-G, también en banda. Barrido de la separación contra la puerta: 0 y 0,6 dentro, 0,8
dentro, 0,85 fuera, 0,9 y 0,95 dentro, 1,0 fuera: **no es monótono**, es una sola plantilla por pareja (lote
`--seed 2`: la referencia de `main` ya da 98,4 en esa fila). Se toma **0,8** —la menor cifra que satura el baile
(sonda: 23 episodios de cobertura en 60 partidos contra 25 con 1,0) y apenas por encima del contacto de dos orcos (0,76)—.
Las puertas `StatisticalTests`, `FullRunGateTests` y `RarityAndBossTests` pasan (29/29). El margen de esa fila sigue
siendo el de una plantilla: se anota en BB-P.
