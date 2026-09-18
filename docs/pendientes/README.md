# Índice de problemas de gameplay

Un fichero por problema (mismo patrón que `docs/decisiones/`), no una tabla plana. Antes de
proponer una hipótesis sobre un síntoma, consulta aquí si ya existe un fichero para él o para
uno de sus hermanos — memoria que no se consulta es memoria inexistente
(`docs/analisis/auditoria-organizacion-v2.md` §3).

Estado epistemológico dentro de cada fichero (Regla F, ver V2/V3 de la auditoría de
organización): una hipótesis puede estar **REJECTED** por un experimento (no vuelve a
probarse sin evidencia nueva), **LIKELY** (consistente con lo medido pero sin experimento
propio que la aísle), o **CONFIRMED** (reproducida). Una hipótesis rejected bajo un sistema
puede reabrirse si ese sistema cambia — se anota "descartada bajo la ADR X", nunca "falsa"
a secas.

| Id | Título | Estado |
|---|---|---|
| [BA-A](./BA-A.md) | BLOQUEO: un nodo de evento sin opciones y sin salida. | Cerrada |
| [BA-B](./BA-B.md) | BLOQUEO: en un jefe no dejó sustituir al lesionarse el segundo jugador | Cerrada |
| [BA-C](./BA-C.md) | Con seis filas no hay fila central y la alineación queda descentrada. | Cerrada |
| [BA-D](./BA-D.md) | Los jugadores se teletransportan al reanudar una falta. | Cerrada |
| [BA-E](./BA-E.md) | Goles sin ángulo. | Ver fichero |
| [BA-F](./BA-F.md) | El 3D está mal. | Ver fichero |
| [BA-G](./BA-G.md) | Los nombres de los jugadores se repiten | Ver fichero |
| [BA-H](./BA-H.md) | Los consumibles no se pueden usar. | Ver fichero |
| [BA-I](./BA-I.md) | La sustitución por lesión no debería ser obligatoria | Ver fichero |
| [BA-J](./BA-J.md) | Tras una parada, el equipo defensor debería replegarse | Ver fichero |
| [BA-K](./BA-K.md) | Cortinilla o transición | Resuelta |
| [BA-L](./BA-L.md) | La escena de capturas (`Scenes/Capturas.tscn`) no produce nada en esta | Cerrada |
| [BA-M](./BA-M.md) | Origen de BA-N: la puerta de equipar pasó de roja a verde por cero cen | Cerrada |
| [BA-L2](./BA-L2.md) | `CaptureRunner` pierde el árbol de escena entre `informe` y `recompensa` | Resuelta; `recompensa.png` en blanco queda abierto |
| [BA-N](./BA-N.md) | Equipar ya no vale el escalón que la ADR 0033 exige | Cerrada (ADR 0116; deuda documentada) |
| [BB-A](./BB-A.md) | Los jugadores se teletransportan al reanudar | Resuelta |
| [BB-B](./BB-B.md) | En el saque de centro los defensores van a robar el balón antes de que | Resuelta (ADR 0115), tras dos intentos rechazados |
| [BB-C](./BB-C.md) | «Celebra» se activa cuando ya han vuelto a su campo | Resuelta |
| [BB-D](./BB-D.md) | Propuesta del revisor: parar unos segundos en los eventos que detienen | Ver fichero |
| [BB-E](./BB-E.md) | Salen dos canteranos en el mercado; debería salir uno como mucho | Cerrada |
| [BB-F](./BB-F.md) | Un jugador se lesiona y no se ve en qué posición jugaba | Ver fichero |
| [BB-G](./BB-G.md) | El balón se queda parado en el campo | Resuelta (ADR 0117) |
| [BB-G2](./BB-G2.md) | Riesgo latente encontrado de camino, sin evidencia de que se dispare | Ver fichero |
| [BB-H](./BB-H.md) | «Todavía no hay eventos, habrá que diseñarlos» | Ver fichero |
| [BB-I](./BB-I.md) | «Depredador de área» pareció activarse en un momento que no era un tir | Ver fichero |
| [BB-J](./BB-J.md) | «Mentalidad de manada» no sirve jugando con enanos | Previsibilidad resuelta (tooltip); diseño abierto |
| [BB-K](./BB-K.md) | Dos jugadores del mismo equipo que quieren la misma casilla «bailan y  | Ver fichero |
| [BB-L](./BB-L.md) | El lesionado «sale volando» del campo | Resuelta |
| [BB-M](./BB-M.md) | «Sed de médula» lesionó a un jugador lejos de la acción | Ver fichero |
| [BB-N](./BB-N.md) | El saque de córner no ocurre nunca (0/60 partidos, dos árboles) | Abierta |
| [BB-O](./BB-O.md) | Un jugador fuera del campo puede conservar el balón y congelar el partido | Abierta |
| [BB-P](./BB-P.md) | Las puertas de un solo partido/semilla se leen como causa cuando son ruido | Abierta |
| [BB-Q](./BB-Q.md) | «Arrollador» no se activa nunca (0/480 partidos): tres causas encadenadas | Resuelta (Alt 0: RECOVERY + detail()) |
| [BB-R](./BB-R.md) | Dos perks dicen ser MAESTROS y no exigen ni cierran nada (ADR 0051 al 0 %) | Abierta |
| [BB-S](./BB-S.md) | `build-neutral-reference.py` dice que escribe las referencias y solo imprime | Abierta |
| [BB-T](./BB-T.md) | La puerta de impacto de equipamiento cae a 0,5 puntos al cuadrar el catálogo | Abierta |
| [CAT-A](./CAT-A.md) | `field_bandage` usaba el canal `injure`, que protegía al rival. | Ver fichero |
| [CAT-B](./CAT-B.md) | Un consumible se puede comprar pero no se puede equipar: nadie emite ` | Ver fichero |
| [CAT-C](./CAT-C.md) | La medición no puede ejercitar el consumible manual, así que subestima | Ver fichero |
| [CAT-D](./CAT-D.md) | ¿Una fila más de campo (16×5 → 16×6), con las DOS filas centrales como | Ver fichero |
| [CAT-E](./CAT-E.md) | Con seis filas, todas las builds ganan más al equipo sin perks, y dos  | Ver fichero |
| [CAT-F](./CAT-F.md) | `Block` (la carga sin balón, ADR 0030 §2) no se puede despertar subién | Ver fichero |
| [CAT-G](./CAT-G.md) | El equipo de la referencia neutra es una palanca que mueve tres métric | Ver fichero |
| [CAT-H](./CAT-H.md) | ¿Compraría alguien un objeto maldito? | Ver fichero |
| [CAT-I](./CAT-I.md) | La política automática casi nunca vende, así que las reglas de venta n | Ver fichero |
| [CAT-J](./CAT-J.md) | Dos puertas de `BuildGateTests` en rojo en HEAD (elf_brawler, passChain), preexistente | Abierta |
