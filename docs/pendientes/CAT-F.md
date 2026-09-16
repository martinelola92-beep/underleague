# CAT-F — `Block` (la carga sin balón, ADR 0030 §2) no se puede despertar subiéndole el peso, y está medido.

**Estado:** Abierta

## Observación

**`Block` (la carga sin balón, ADR 0030 §2) no se puede despertar subiéndole el peso, y está medido.** Decisión del revisor: «cambiar violencia por violencia», subir `blockTargetBonus` y bajar la entrada sin balón para que las lesiones no se muevan. **Intentado en tres tandas de 2.000 partidos y revertido.** Bono 400: lesiones 0,82 → **1,31** y tiros a **7,25**, los dos fuera. Bono 250 + enfriamiento 260: lesiones **0,96**, fuera. Bono 200 + enfriamiento 320: todo dentro (entradas 11,83 · lesiones 0,87 · tiros 8,15) **pero `Block` sigue eligiéndose el 0,09 %** — de 0,00 % a 0,09 % a cambio de 0,05 de lesión y 0,16 de tiro. Mal negocio

## Análisis / estado actual

**Abierta, con el diagnóstico hecho: el peso no es la palanca.** El censo de utilidad lo dice: `Block` se **descarta el 74,9 %** de las veces, antes de puntuar. Su precondición es `blockReachMaxCells` **1,2** con `blockDistancePenaltyPerCell` **300**, así que solo es evaluable cuando ya estás casi tocando al rival y, al alcance máximo, la penalización (360) se come el bono entero. Despertarlo exige **ensanchar el alcance o bajar la penalización por casilla**, y eso cambia **qué es una carga** —de «empujar a quien tienes pegado» a «arrancar contra alguien desde lejos»—, que es decisión de diseño con ADR, no una calibración. El presupuesto tampoco ayuda: `injuriesPerMatch` está en 0,82 contra un techo de 0,90

## Hermanos

_(por enlazar donde se detecten; ver `README.md` del directorio)_
