# El mercado aporta poco a la victoria: medición del 11 oct 2026

Problema abierto: `runWinRate_noMarket` (ganar la run sin entrar en ningún mercado) con techo 5 % (ADR 0055,
directriz del revisor: *«solo con los perks de las victorias no debería darte opción a ganar»*). La ADR 0193 lo
agravó. Esto es la medición de base y lo que dice; **no se ha cambiado nada**.

## Medición

`dotnet run --project Balance -c Release -- --full-runs 240 --seed S`, S = 1 y 2 (`main` e8d1547). 480 runs por
doctrina, juntas las dos semillas (error típico de una tasa de ~15 % con 480 runs: ~1,6 puntos).

| Política | Victoria de run | Perks en titulares | Perks comprados | Objetos | Nivel medio | Liga % acto 1 / 2 / 3 |
|---|---|---|---|---|---|---|
| Contextual | **18,8** | 6,18 | 3,20 | 4,55 | 6,14 | 81 / 62 / 54 |
| Saver | 15,6 | 6,49 | 3,47 | 3,71 | 6,10 | 81 / 57 / 45 |
| Spender | 14,6 | **8,63** | **6,50** | 4,07 | 6,13 | 81 / 56 / 44 |
| Sin mercado | **12,9** | 3,05 | 0 | 3,66 | **6,73** | 82 / 59 / 41 |

Jefes, todas las doctrinas con mercado juntas frente a sin mercado: acto 1 70 % / **74 %**, acto 2 51 / 46, acto 3
48 / 43. Oro sin gastar al acabar: 12,6 frente a 82,8. Las filas de diagnóstico del resumen (`*_market` /
`*_noMarket`) dicen lo mismo: mismos partidos, nodos y recompensas; la diferencia es de perks y objetos.

## Lo que dice

1. **LIKELY (consistente en las dos semillas, sin experimento que lo aísle del resto del gasto): el número de perks
   casi no mueve el partido.** Spender tiene 2,4 perks más en
   titulares que Contextual y gana 4,2 puntos *menos*; sin mercado, con la mitad de perks, gana la liga del acto 1
   y 2 igual que con mercado. Encaja con la ADR 0087 (ningún perk negativo, valor frente al control pequeño) y con
   la ADR 0085 (el valor de un perk se lee al horizonte y la run no lo nota).
2. **LIKELY: lo que vale del mercado es elegir contra lo que viene**, no acumular. La ventaja de Contextual se
   concentra en el acto 3 (54 frente a 41 % de liga), donde compra contra el jefe. Sin experimento propio que lo
   aísle del oro gastado en otras cosas.
3. **LIKELY: el nivel es poder gratis.** Quien no compra llega con +0,6 de nivel medio. Sin mirar el mecanismo
   (¿plantilla más corta y por tanto más minutos por jugador?).
4. Por tanto **el techo del 5 % no se alcanza moviendo precios**: la diferencia entre comprar bien y no comprar es
   de ~6 puntos de run y bajar a quien no compra a 5 exige que el mercado valga ~15. Con perks de poco poder, eso
   sólo se consigue endureciendo la run para todos (y entonces `runWinRate` sale por abajo de su 20 %).

## Opciones (decisión del revisor: tocan la directriz de la ADR 0055 o qué es un perk)

- **A. Cambiar la métrica, no el juego.** Que «el mercado es imprescindible» se mida como *ventaja de comprar bien*
  (Contextual − sin mercado ≥ N puntos) en vez de un techo absoluto. Hoy ~6.
- **B. Que el jefe pida respuesta.** Los modificadores de jefe más duros, de forma que la compra contextual (la
  única que hoy separa) decida el acto 3. Es la palanca que ya funciona; la ADR 0193 fue en sentido contrario.
- **C. Perks con más poder** (multiplicar los efectos del catálogo). Toca 111 perks y todas las puertas de
  diferenciación de builds; la más cara y la más arriesgada.
- **D. Menos poder gratis**: menos experiencia por partido. Baja a todos por igual en absoluto y no separa.

Recomendación: **A + B**. A reconoce lo medido (los perks son identidad, no potencia: eso es el juego que se ha
construido) y B hace que la identidad cuente donde la run se decide.
