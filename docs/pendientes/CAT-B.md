# CAT-B — Un consumible se puede comprar pero no se puede equipar: nadie emite `SetConsumables`.

**Estado:** Cerrada (ADR 0101) — ver "Análisis / estado actual". Dejó abierto CAT-C

## Observación

**Un consumible se puede comprar pero no se puede equipar: nadie emite `SetConsumables`.** La decisión existe y está implementada entera en `/Sim` (`RunSetup.cs:81`, `RunEngine.cs:635` `ApplyConsumables`, con el límite de 3 de RF-080 y el slot manual de RF-082), y el mercado los **vende** tanto en la medición como en el juego (`MarketSystem.BuyConsumable`, `MarketScreen.cs:97`, cuarta columna). Lo que no existe es quien la emita: buscar `SetConsumables` en `Game/` y en `Sim/Analysis/` no devuelve **ni una llamada**. Dos frentes: (a) en la **build jugable** el jugador gasta oro en un consumible que entra en el inventario y no puede llevar a ningún partido —no hay pantalla de equipado; `TeamScreen` no menciona consumibles—, así que la cuarta categoría del mercado (RF-114) cobra y no devuelve nada; (b) en la **medición**, `RunPolicy` tampoco los equipa, así que los cuatro consumibles de RF-084 están calibrados y jamás se juegan, y cualquier cambio en `/data/consumables` es invisible para las 43 puertas (así se coló CAT-A)

## Análisis / estado actual

**CERRADA (13 sep 2026, decisión del revisor; ADR 0101).** Las dos mitades: `/Game` equipa desde una sección nueva de la pantalla de Equipo (`Game/Ui/ConsumablesPanel.cs`, captura `equipo-consumibles.png`), y `RunPolicy` compra —la última del ciclo de mercado, por detrás de perks, objetos y fichajes— y equipa antes de cada partido. Hizo falta además arreglar el **precio**: era plano y valía 45, más que un perk raro, porque el `_doc` de la economía daba por hecho que «no lo compra nadie»; pasa a escalera por rareza 10-16-26-45 (ADR 0101). Medido (1.200 runs × doctrina): `brokeMarketRunShare` 11,58, `purchasesPerMarket` 1,32, `mastersReached` 40,8, `runWinRate` 22,83 (baja 2,5 desde 25,33: el oro que se queda quieto ahora compra algo que se gasta). Y de paso, la descripción generada dejaba de decir la verdad: ponía «el portador» en un efecto que alcanza al equipo entero (RT-035, corregido en `DescribeEffects`). Instrumento: `Sim.Tests/Analysis/ConsumableLoopTests.cs`. **Deja abierto CAT-C**

## Hermanos

_(por enlazar donde se detecten; ver `README.md` del directorio)_
