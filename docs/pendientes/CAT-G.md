# CAT-G — El equipo de la referencia neutra es una palanca que mueve tres métricas en direcciones opuestas.

**Estado:** Abierta

## Observación

**El equipo de la referencia neutra es una palanca que mueve tres métricas en direcciones opuestas.** Desde la ADR 0107 la referencia lleva objetos, y la elección de cuáles decide a la vez `buildsWinDifferently_injuries` (si llevan **resistencia**, el denominador aguanta más la lesión y el ratio baja), `coherentBuildsBeatNone_*` (si llevan poco, las coherentes no llegan al 58) y `randomBuildLosesToNone_*`. Medido: equipo por rol → ratio de lesiones **1,29** contra 1,4; los siete con `duelists_gloves` → coherentes de orco a **55,0-55,6** contra 58 y aleatoria a **42,50**, bajo su suelo

## Análisis / estado actual

**CERRADA (ADR 0107).** Resuelto mecanizando lo único que seguía eligiéndose a mano: se **buscan** los siete objetos normales cuyo perfil de atributos sea lo más **plano** posible. Resultado: +20 exactos en los cinco atributos, **desviación cero**, así que la referencia no favorece ni castiga ningún canal por construcción. Las ocho métricas de fase 1 entran **sin tocar ninguna banda**, y con ellas las 43 puertas

## Hermanos

_(por enlazar donde se detecten; ver `README.md` del directorio)_
