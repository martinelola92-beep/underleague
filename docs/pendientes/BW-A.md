# BW-A — Los tests compartían un Catalog entre hilos

Estado: **CERRADA** (3 oct 2026).

## Observación

En el bucle de tests tras el arreglo de BO-A, `RunPolicySellTests.AWholePolicyRunNeverTriesToSellASigningWhoHasNotPlayed`
lanzó una vez «la sustitución del jugador 1000003 por el 1000009 en el tick 1022 no es legal»: la reproducción del partido
con la sustitución no coincidía con la primera pasada. No se reprodujo ni sola (60 runs) ni en el bucle siguiente.

## Causa

**LIKELY, sin experimento que la aísle** (no se reprodujo): `SystemsTestSupport.Catalog` era un `static Catalog` usado por
39 clases de test que xUnit ejecuta en paralelo. Las condiciones NCalc compiladas guardan su contexto en la instancia, así
que dos hilos que evalúan el mismo perk se lo pisan: `ThreadCatalogs` ya lo había medido (tasas de activación distintas
entre ejecuciones) y CLAUDE.md prohíbe compartir un Catalog entre hilos. Una evaluación pisada cambia el partido entre la
primera pasada y la reproducción, que es exactamente el síntoma.

## Arreglo

`SystemsTestSupport.Catalog` devuelve `ThreadCatalogs.Current` (un catálogo por hilo, Regla G: el patrón ya existía).
Las cuatro clases que lo copiaban en un `static readonly` propio (`RunEquipmentTests`, `NemesisTests`,
`PostMatchViewNicknameTests`, `GazetteViewTests`) lo leen ahora con `=>`. `Systems` sigue compartido: no lleva condiciones
compiladas.

Tiempo del bucle `Category!=Gate&Category!=Diagnostic`: **3 m 40 s** (antes, en la misma sesión, 3 m 49 s – 4 m 24 s).
1.925 tests en verde.
