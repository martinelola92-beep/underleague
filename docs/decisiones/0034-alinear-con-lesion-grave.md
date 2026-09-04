# 0034. Alinear a un jugador con lesión grave

**Fecha:** 2026-09-05
**Estado:** **Propuesta: decisión del revisor.** Resuelve una contradicción entre RF-092 y RF-093
**Requisitos:** RF-090..093, RF-002d, RF-012d, RF-097

## La contradicción

- **RF-092**: *"**Lesión grave**: el jugador **no puede alinearse** hasta recibir tratamiento en un nodo de clínica o taller."*
- **RF-093**, caso 1: la muerte solo puede producirse cuando *"el jugador **se alineó arrastrando una lesión grave sin tratar**"*.

Si RF-092 lo impide, el caso 1 de RF-093 es inalcanzable y la única vía de muerte real sería el perk letal. Hasta ahora el motor implementaba RF-092 al pie de la letra, así que *"un jugador sano nunca muere"* se cumplía **por vacuidad**: no había ninguna forma de morir.

## Lectura aplicada

Se ha implementado la lectura que hace **coherentes ambos requisitos**: alinear a un lesionado grave **es posible**, es una decisión del jugador, y su precio es el riesgo de muerte. Es el mismo patrón que RF-002d, que permite jugar en inferioridad con 5 sabiendo que una sola baja termina la run: el juego no te protege de ti mismo, te informa y te deja decidir.

Se cumple RF-012d porque el aviso es explícito y estructurado (`LineupWarnings`, con el riesgo por jugador), y la marca de riesgo es **por partido**: un lesionado grave solo se expone si está en la alineación decidida para ese partido, nunca por relleno automático ni por herencia de una alineación anterior.

## Lo que queda por decidir: ¿cuál es el coste inmediato?

Hoy alinear a un lesionado grave **no penaliza sus atributos**: RF-091 define el −15% solo para la lesión leve. El único coste es el riesgo de muerte.

**Recomendación: que penalice, del orden del −30%** (el doble de la leve). Motivo de diseño: con 5 o 6 jugadores disponibles, muchas veces no hay alternativa a alinearlo, y entonces el riesgo de muerte no es una decisión sino un impuesto. Con una penalización fuerte, la pregunta se vuelve real y se juega cada vez: *¿salgo con cinco sanos o con seis y uno cojo?* Y encaja con la identidad del juego: un jugador roto que salta al campo debería **verse** roto, no solo estar en peligro.

La alternativa —dejarlo sin penalización— es defendible si se quiere que la tensión venga solo del riesgo de perderlo para siempre, pero entonces conviene subir la probabilidad de muerte para que el riesgo pese.

## Consecuencias

- Si se acepta: RF-091 se amplía a la lesión grave con su porcentaje, `requisitos.md` sube a v0.9.2, y RF-092 se reescribe como *"no debería alinearse; hacerlo expone al jugador a morir"* en vez de una prohibición.
- Si se rechaza la lectura entera y RF-092 vuelve a ser una prohibición dura, hay que retirar el caso 1 de RF-093 y la única muerte posible será el perk letal, lo que deja el desgaste de plantilla —el recurso central declarado del juego— dependiendo de un solo mecanismo.
