# 0010. Rango de empates al final del reglamentario (RT-056)

**Fecha:** 2026-09-03
**Estado:** Propuesta (requiere decisión del revisor: cambia un rango de RT-056, RT-057)
**Requisitos:** RT-056, RT-057, RF-055b, RF-055c

## Contexto

RT-056 pide a la vez: mayoría de resultados entre 1-0 y 3-2, menos del 5% de partidos con más de 5 goles totales, y menos del 15% de empates. La métrica de empates se interpreta como "empate al final del reglamentario" (I-7), es decir, frecuencia con la que se entra en el gol de oro de la turba.

Con marcadores aproximadamente independientes entre equipos (Poisson de media λ por equipo), la probabilidad de empate es `e^(-2λ)·I₀(2λ)`: 30% con λ≈1,3 (2,6 goles/partido), 20% con λ≈3, y solo baja del 15% a partir de unos 8 goles por partido, lo que rompe las otras dos condiciones. El ajuste de fase 0 (paquete E) deja el resto de la fila en rango y los empates en el 29%.

## Decisión propuesta

Subir el rango a **"menos del 30% de empates al final del reglamentario"** y considerarlo el techo de frecuencia de la turba: uno de cada tres o cuatro partidos llega al gol de oro. Alternativa (no excluyente, fase 1 o posterior): una mecánica que correlacione marcadores (el equipo que va por detrás arriesga más: más tiros y más entradas), que reduciría los empates sin inflar los goles y además es interesante como diseño.

## Alternativas descartadas

- Mantener <15% y subir los goles: rompe "mayoría entre 1-0 y 3-2" y "<5% con más de 5 goles".
- Retirar la métrica: la frecuencia de la turba es un dato de diseño importante (RF-055d da a ambos bandos motivos para buscar o evitar el empate).

## Consecuencias

Si se acepta: actualizar la tabla de `balance.md`, `Sim/Analysis/MatchMetrics.cs` (rango de `drawShareAtRegulation` y pasar de INFO a IN/OUT), la puerta estadística, y subir `requisitos.md` a v0.9.1 con la nota. Si se rechaza: hay que diseñar la mecánica de correlación antes de cerrar la fase 0.
