# CAT-H — ¿Compraría alguien un objeto maldito?

**Estado:** Abierta

## Observación

**¿Compraría alguien un objeto maldito?** Desde la ADR 0107 los cuatro valen negativo **incluso bien colocados**: entre −18 y −53. El −18 de `berserker_totem` es casi un empate y tiene sentido en un central puro; el −53 de `martyrs_relic` probablemente no lo tenga en nadie

## Análisis / estado actual

**Abierta.** La causa es que la tabla de valor de la ADR 0038 es **global**: no sabe distinguir la técnica de un central de la de un medio, así que no puede expresar «negativo mal colocado, positivo bien colocado», que es lo que RF-077 promete («efecto potente y contrapartida permanente», es decir un **canje**, no una trampa). Salidas: (a) una tabla de valor **por posición**, que es un instrumento nuevo y caro; (b) calibrar los cuatro cerca de cero por arriba en vez de claramente negativos, aceptando que el cargador solo garantice «no es una ganga»; o (c) aceptar que el maldito es una trampa y decirlo en RF-077. Sin prisa: el juego funciona y las puertas están en verde

## Hermanos

_(por enlazar donde se detecten; ver `README.md` del directorio)_
