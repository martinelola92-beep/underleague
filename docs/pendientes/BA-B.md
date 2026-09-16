# BA-B — BLOQUEO: en un jefe no dejó sustituir al lesionarse el segundo jugador.

**Estado:** CERRADA

## Observación

**BLOQUEO: en un jefe no dejó sustituir al lesionarse el segundo jugador.** «He tenido que cerrar el juego»

## Análisis / estado actual

**CERRADA (13 sep 2026, commit `dafc013`).** Lo daban por imposible **dos** sitios: `SubstitutionPoints.Pending`, que solo miraba `Lineup.Slots` —el once de salida—, así que al lesionarse un suplente que ya había entrado no se abría ninguna ventana; y la validación de `Simulator`, que rechazaba la sustitución aunque llegara. Se veía sobre todo en un jefe, que es donde hay más bajas por partido. Ahora sale del campo quien **estaba** en el campo, incluido quien entró por una sustitución anterior. Test de regresión: `Sim.Tests/Run/SubstitutionChainTests.cs`, que encadena puntos de sustitución reales sobre un emparejamiento desigual hasta dar con el caso

## Hermanos

_(por enlazar donde se detecten; ver `README.md` del directorio)_
