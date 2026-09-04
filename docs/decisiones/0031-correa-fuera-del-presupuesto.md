# 0031. La correa sale del presupuesto de atributos

**Fecha:** 2026-09-05
**Estado:** Aceptada
**Modifica:** ADR 0025 (presupuesto) y ADR 0028 (zona de acción)
**Requisitos:** RF-022, RF-042, RF-043, RT-055

## Contexto

La ADR 0025 propuso dejar la correa **fuera** del presupuesto de atributos porque, con el radio circular de la fase 0, era el atributo de mayor valor marginal con diferencia (+6,7 puntos de tasa de victoria por cada +10 en orcos) y cualquier reparto óptimo la habría maximizado.

La ADR 0028 la devolvió al presupuesto con este razonamiento: al pasar la correa a escalar el tamaño de una **zona con forma dada por la posición**, ya no permitiría a un defensa jugar de delantero, y su valor debería caer a la escala de los demás atributos. Se dijo explícitamente que había que remedirlo antes de darlo por bueno.

**Se remidió, y la predicción falló en la dirección contraria.** Con la zona implementada (`docs/balance/fase1b-resultados.md`), +20 de correa valen **−1,6 puntos** de tasa de victoria; antes de estrechar `scaleFromLeashPercent` a 85/115 valían −5,1. No es que la correa valga poco: **es que perjudica**. Una zona más grande deshace la estructura del bloque —el jugador se aleja de su sitio, deja hueco y no llega a nada nuevo— y no compra nada a cambio.

Mientras tanto, el reparto de generación le dedica entre el **10% y el 18%** del presupuesto. Cada punto de calidad que un jugador gasta en correa es un punto tirado, y peor que tirado: empeora al jugador. Eso rompe el supuesto sobre el que se sostiene el modelo de presupuesto —que un punto valga aproximadamente lo mismo lo pongas donde lo pongas— y hace que dos jugadores con el mismo presupuesto no valgan lo mismo, que era justo lo que el modelo venía a garantizar.

## Decisión

**La correa sale del presupuesto.** Pasa a ser un **descriptor posicional**, determinado por posición, raza y etiqueta de estilo, no por reparto de puntos:

- La **forma** de la zona la sigue dando la posición (ADR 0028), sin cambios.
- El **tamaño** lo fija la combinación de posición, raza y estilo, con una escala estrecha alrededor del 100%: un enano tiene la zona algo más pequeña, un elfo algo mayor.
- El presupuesto se reparte entre los **cuatro atributos que sí compran rendimiento**: fuerza, velocidad, técnica y resistencia.
- `Leash` sigue siendo un atributo a efectos de RF-022 y RF-043 (perks, equipamiento y consumibles lo modifican), pero deja de sortearse: se deriva.

## Alternativas descartadas

- **Dejarla en el presupuesto y bajarle el peso**: no arregla el problema de fondo, que es que su valor marginal es **negativo**. Un peso pequeño sigue siendo desperdicio.
- **Darle un canal positivo medible** (llegar a balones sueltos fuera de la zona, cubrir carriles de pase): es mecánica nueva y de valor incierto, y el catálogo de lanzamiento no puede esperar a que se diseñe. Queda como candidata para la fase 3 si alguna vez se quiere que la correa vuelva a ser una decisión.
- **Aceptar que la correa sea un lastre**: convertiría a la generación en una lotería sobre cuánto presupuesto desperdicia cada jugador.

## Consecuencias

- `data/sim/tuning.json`: `generation.positionShare` pasa a cuatro atributos y `budgetByRarity` se reajusta a la baja en proporción; el criterio de la ADR 0027 (común de nivel 8 ≈ legendario de nivel 2) hay que revalidarlo tras el cambio.
- `PlayerGenerator` deriva `Leash` en vez de sortearlo, con su tabla en datos.
- Se espera que **suba la calidad efectiva de todos los jugadores** al dejar de gastar en un atributo dañino: el reajuste debe comprobar que RT-056 sigue en rango.
- El valor marginal de la correa deja de ser una métrica de balance y pasa a ser una comprobación de que la zona por posición tiene el tamaño correcto.
