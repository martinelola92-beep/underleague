# 0038. El precio y la frecuencia como palancas de balance

**Fecha:** 2026-09-05
**Estado:** Aceptada (directriz del revisor)
**Requisitos:** RF-071, RF-114, RF-114e, RT-055, RT-057
**Complementa:** ADR 0036 (el objeto sube atributos) y ADR 0037 (la economía es la dificultad)

## Contexto

Hasta ahora, cuando un objeto o un perk destacaba sobre los de su rareza, la única respuesta era **bajarle los números**. Eso tiene un coste que se paga en diseño: un objeto interesante nerfeado se vuelve aburrido, y el catálogo tiende a la uniformidad, que es lo contrario de lo que un roguelite necesita.

El revisor propone la alternativa: **si algo es mejor que sus pares sin estar roto, se encarece en vez de debilitarse.**

## Decisión

**El precio es una palanca de balance de primer orden, no una etiqueta derivada de la rareza.** Dos objetos comunes que suben lo mismo en atributos distintos **no valen lo mismo y no deben costar lo mismo**.

### Por qué ahora es calculable

Con la ADR 0036, un objeto es un paquete de atributos, y ya existe la tabla de valor marginal (`docs/balance/fase1b-resultados.md`): fuerza +11,1 · técnica +7,5 · velocidad +6,6 · resistencia +3,0 puntos de tasa de victoria por cada +20 repartidos en la plantilla. Así que **el valor de un objeto se calcula, no se mide**:

```
valor(objeto) = Σ (bonus_atributo × valorMarginal_atributo)
precio(objeto) = precioBase(rareza) × valor(objeto) / valorMedio(rareza)
```

Consecuencia directa y deseable: un objeto de +10 de fuerza cuesta casi el cuádruple que uno de +10 de resistencia, **aunque los dos sean comunes**. Sin esa corrección, el de resistencia sería basura y ocuparía sitio en el surtido sin ser nunca una opción; con ella, los dos son elegibles según lo que te quede en la bolsa.

### La palanca equivalente donde no hay precio

Los perks se obtienen sobre todo **gratis**, eligiendo uno de tres tras ganar (RF-071). Ahí el precio no interviene, así que la palanca es la **frecuencia**: el peso de cada perk en el pool de recompensas es inversamente proporcional a su valor medido. Un perk excelente sigue siendo excelente; simplemente sale menos.

Quedan así dos palancas paralelas alimentadas por el mismo número:

| Vía de obtención | Palanca |
|---|---|
| Mercado (objetos, perks, jugadores, consumibles) | **precio** |
| Recompensa tras victoria (RF-071) | **frecuencia en el pool** |

### El límite

**El precio balancea diferencias, no rotura.** Si un objeto solo se equilibra con un precio que nadie puede pagar, no está caro: está roto, y hay que cambiarlo. La regla operativa: si el precio justo supera el oro medio disponible en un acto, el problema es el objeto.

Y no exime de RT-055: una build no puede superar el 70% de tasa de victoria porque sus piezas sean caras. Encarecer retrasa el acceso; no arregla un desequilibrio de potencia.

## Alternativas descartadas

- **Precio derivado solo de la rareza** (lo que había): obliga a que todo lo de una rareza valga lo mismo, y como no lo vale, la mitad del surtido es relleno.
- **Nerfear siempre**: uniformiza el catálogo y destruye lo que hace memorable a un objeto.
- **Precio a mano por objeto**: funciona con 12 objetos y deja de funcionar con más de 50, que es el tamaño previsto del catálogo de lanzamiento.

## Consecuencias

- `data/economy/` deja de fijar el precio por rareza y pasa a fijar el **precio base por rareza más el multiplicador por valor**. El precio concreto de cada objeto se deriva al cargar y se puede inspeccionar con `/Balance --describe`.
- Hace falta una tabla de valor por perk para alimentar la frecuencia. Los objetos se calculan; los perks hay que **medirlos**, y esa medición pasa a ser parte del lote de balance.
- Un común caro puede costar más que un raro barato. Es deliberado: la rareza dice cuántos atributos toca (ADR 0036), no cuánto vale.
- La tabla de valor marginal por atributo se convierte en una pieza de infraestructura, no en un resultado de una medición puntual: hay que remedirla cuando cambie el motor.
