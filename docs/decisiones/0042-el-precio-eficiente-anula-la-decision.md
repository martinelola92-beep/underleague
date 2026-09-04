# 0042. Un mercado perfectamente tasado no tiene decisiones

**Fecha:** 2026-09-05
**Estado:** **Propuesta: decisión del revisor**
**Tensión entre:** ADR 0037 (la escasez es la dificultad) y ADR 0038 (el precio se deriva del valor)
**Requisitos:** RF-114, RF-114k, RT-055

## El problema, medido

La ADR 0037 exige que la doctrina **contextual** —comprar según lo que falta para la puerta siguiente— gane a las dos puras por al menos 8 puntos, porque si no, la tienda no tiene decisión.

Medido con todo implementado: contextual **17,8** · ahorradora **17,8** · gastadora 12,2. Gana a la gastadora por 5,6 y **empata exactamente con la ahorradora**.

El diagnóstico inicial fue que faltaba implementar la ADR 0036. Se implementó, y el empate persiste. La causa es más incómoda: **las dos decisiones se anulan entre sí.**

La ADR 0038 fija el precio proporcional al valor. Si todo está perfectamente tasado, **no hay gangas**, y saber elegir no puede aportar nada: comprar el objeto que más te sirve cuesta exactamente lo que vale, así que la ventaja de saber cuál te sirve se la come el precio. Es la hipótesis del mercado eficiente aplicada a una tienda de roguelite: en un mercado perfecto, el criterio no rinde.

Comprobado en las dos direcciones: con los objetos a mitad de precio la contextual sube a +5,0; subiendo su magnitud —lo que sube el precio en la misma proporción— la ahorradora vuelve a ganar.

## Y hay una segunda causa, de la ADR 0036

Al convertir los objetos en **atributos puros** ganamos que su valor sea calculable, pero perdimos algo que no vimos: **un +10 de fuerza vale casi lo mismo en cualquier equipo**. Los objetos dejaron de tener afinidad con una build concreta, y sin afinidad no existe "esto me viene bien *a mí*", que es de donde nace la decisión de compra.

Dicho de otro modo: el precio se deriva del valor **medio**, y la ganga solo existe si el valor **para tu build** se aparta de esa media. Con atributos puros esa dispersión es pequeña.

## Tres salidas, no excluyentes

1. **Que el valor de un atributo dependa del portador.** La tabla de valor marginal es hoy global; lo natural es que un +10 de fuerza valga más en un defensa que en un extremo, y más en una build de contacto que en una de pase. Si el precio sigue derivándose del valor **medio** y el valor real depende de a quién se lo pongas, la ganga reaparece sin tocar la ADR 0038. **Es la salida que mejor conserva ambas decisiones**, y exige medir el valor marginal por posición y por estilo, no solo global.
2. **Dispersión deliberada de precios.** Que el precio no sea exactamente proporcional al valor sino que oscile en una banda, de modo que a veces haya gangas y a veces atracos. Es lo que hace cualquier tienda de roguelite. Barato de implementar, y convierte visitar el mercado en información útil.
3. **Dar a la doctrina contextual la transferencia de objetos**, que la ADR 0036 declara como la razón de ser del formato y la política automática hoy ni considera. No es balance, es que la política no está jugando con todas las reglas.

## Recomendación

Aplicar **las tres**, en ese orden: la 3 primero porque es un defecto de la medición y no del juego; la 2 porque es barata y hace el mercado más interesante por sí sola; y la 1 porque es la que responde a la pregunta de fondo —qué hace que un objeto sea *para mí*— y de paso mejora la tabla de la que cuelgan todos los precios.

Si aun así la contextual no se separa, entonces la conclusión honesta es que **con objetos de atributos puros la tienda no puede ser una decisión profunda**, y habría que revisar la ADR 0036 para devolverles algo de especificidad, aceptando el coste de balanceo que eso trae.
