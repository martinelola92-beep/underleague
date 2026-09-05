# 0052. La agencia frente al riesgo exige poder cambiar la formación

**Fecha:** 2026-09-05
**Estado:** **Propuesta: decisión del revisor**
**Requisitos:** RF-002d, RF-012c, RF-041, RF-093
**Corrige:** la condición 3 de la ADR 0048; revisa la ADR 0049

## Dos apuestas que no salieron

### 1. El indicador de riesgo no reduce muertes

La ADR 0048 exigía que atender al indicador de riesgo separase claramente en muertes. **No lo hace**: 1,51 frente a 1,57 ignorándolo, dentro del ruido. Y el diagnóstico es incómodo pero claro: **una alineación elegida por valor deportivo ya está cerca de la más segura**, porque el valor de un jugador incluye su aguante. El jugador no puede protegerse más porque ya lo está haciendo sin querer.

La palanca que falta está identificada: **la formación es un 2-3-1 fijo con siete casillas obligatorias**, así que expones a siete jugadores sí o sí y lo único que puedes decidir es cuáles. No hay forma de sacar del campo al frágil.

**Propuesta**: dar cuerpo a RF-002d, que ya permite jugar con **5 o 6** en inferioridad numérica. Hoy el motor lo admite y la interfaz no lo ofrece como decisión: si el jugador pudiera dejar fuera a su jugador más expuesto y jugar con seis, la reducción de riesgo sería real y tendría un precio deportivo evidente. Es la decisión que la ADR 0048 necesita y que hoy no existe.

### 2. Recortar a dos opciones hundió la ventaja de elegir bien

La ADR 0049 recortó la recompensa de liga de tres opciones a dos para devolver peso al mercado. El resultado fue **el contrario del buscado**: la ventaja de la doctrina con criterio cayó de **+5,6 a +0,2 puntos**.

La causa que sugieren los datos: **la ventaja de saber elegir no estaba en comprar, estaba en tener con qué elegir**. Con la build más pobre, el presupuesto casi nunca alcanza a lo bueno y la política con criterio degenera en la política que ahorra. Es decir: **reducir opciones no traslada la decisión al mercado, la elimina**.

**Propuesta**: devolver las **tres opciones** en liga, pero **degradar su rareza** en vez de su número. El objetivo original —que el mercado sea donde consigues lo bueno— se cumple igual, y el jugador conserva la decisión. Élite y jefe mantienen la rareza alta, que es lo que los hace deseables.

## Dos hallazgos más que conviene registrar

- **La vía 1 de RF-093 aporta cero.** Alinear a un lesionado grave sin tratar no ocurrió **ni una vez en 500 runs**: con plantilla de diez, clínica barata y suplentes sanos, nunca hace falta. Las 1,51 muertes son todas de perk rival. Esa vía es hoy teórica, y conviene saberlo antes de diseñar nada que dependa de ella.
- **La clínica pierde uso** (4,7 → 3,7 de oro por run) justo cuando la ADR 0046 predecía que subiría. El motivo es sencillo: **lo que mata ahora es el perk rival, y contra eso la clínica no cura**. Una muerte no se trata.
