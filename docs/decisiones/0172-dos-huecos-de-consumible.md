# ADR 0172 — Dos huecos de consumible: el hueco es la posesión, y lo comprado sale ya equipado

Fecha: 29-30 sep 2026 · Estado: **aceptada** — **decisión del revisor** (propuesta en BA-H, 29 sep, noche). Enmienda
**RF-080, RF-081, RF-082 y RF-085** de `docs/requisitos.md`. **Relacionada:** ADR 0101 (CAT-B: el consumible se compra,
se equipa y se juega), ADR 0159 (`grantConsumable`), ADR 0161 (el mercado sortea por rareza), ADR 0166 (los gritos),
BA-H.

## Contexto

BA-H: *«los consumibles no se pueden usar»*. El uso en vivo ya existe (el botón del consumible manual en el tablero de
la retransmisión, `ManualActivation`), pero **la cadena que llevaba hasta él era impracticable**: se compraba a un
zurrón de contadores sueltos (`consumable_owned:<id>`), había que ir a Equipo a meterlo en uno de **tres** huecos con
su modo, y al terminar **cada** partido la lista de equipados se vaciaba entera (`ConsumeConsumables`), así que había
que volver a equiparlo todo antes del siguiente. Un consumible que hay que volver a preparar tres pantallas después
de cada partido no se usa.

Propuesta del revisor, textual: *«dos slots, comprar solo con slot libre, y que salga ya equipado en el UI del
partido para usarlo con un clic»*. Encargo: enmendar RF-080 (de 3 a 2) y decidir cómo quedan lo que depende de él.

## Decisión

1. **RF-080: dos huecos.** La run lleva **como mucho dos** consumibles, uno por hueco, y **el hueco es la
   posesión**: no hay inventario de consumibles sueltos ni zurrón. Lo que se lleva es lo que llega al partido.
2. **Comprar o recibir exige un hueco libre** —y que no se lleve ya el mismo— **y lo deja ya equipado**, en modo
   manual. Con los dos llenos el mercado sigue enseñando los consumibles pero **bloqueados, con el motivo** (RF-012d:
   «tus 2 huecos están llenos: usa o descarta uno en Equipo antes de comprar otro» / «ya lo llevas en uno de tus
   huecos»). No se lleva el mismo consumible en los dos huecos: la activación manual se identifica por id
   (`ManualActivation.ConsumableId`) y un clic dispararía los dos a la vez.
3. **RF-081 y RF-082: el modo es por hueco y el defecto es manual.** Un consumible entra **manual** —un clic en su
   botón del tablero de la retransmisión— y el jugador puede pasarlo a **condicional** (con su disparador de RF-083)
   en Equipo, cualquiera de los dos. **Se retiran** «hasta 2 de 3 condicionales» y «al menos uno manual»: con dos
   huecos, lo primero es trivial, y lo segundo no se puede sostener cuando los consumibles persisten —el manual
   se gasta, y el condicional que queda solo dejaría la run en un estado que la regla vieja llamaría inválido—. Lo que
   RF-082 garantizaba —que exista una decisión en vivo— lo garantiza ahora el **defecto**: todo lo comprado sale
   manual, y pasarlo a condicional es una decisión que toma el jugador.
4. **RF-085: se consumen al usarse; lo no usado se queda en su hueco.** Antes «no persisten entre partidos» se
   implementaba desequipando todo y devolviendo lo no usado al zurrón. Ahora sale de su hueco **sólo el que se
   activó** (a mano o por su disparador), y el resto sigue en el suyo con su modo y su disparador para el partido
   siguiente. El jugador puede **descartar** uno en Equipo para liberar el hueco (no vuelve a ninguna parte).
5. **`SetConsumables` reconfigura, no crea.** Cada id de la lista tiene que ser uno que la run ya lleva, sin
   repetir, como mucho dos. Cierra el límite X-9 del paquete X: *«cualquier id se puede equipar sin comprobar que se
   posea»*.
6. **Eventos** (ADR 0159): `grantConsumable` necesita un hueco libre para aterrizar —sin él la opción **no es viable**,
   igual que `recruit` sin plantilla— y **sortea sólo entre los que la run no lleva** (no se sortea uno para
   rechazarlo después). La vista ya muestra la opción como no elegible.
7. **Guardados anteriores** (los que llevan un inventario suelto y hasta tres equipados): se **pliegan a los dos
   huecos al cargar**, con una migración explícita (`RunState.FoldLegacyConsumables`, llamada desde
   `RunSave.Load`): primero lo que iba equipado (hasta dos, con su modo), luego las copias sueltas por id ascendente
   (manuales, **sin repetir**: de un id repetido gana la primera copia); los contadores viejos se borran y **lo que
   no cabe no desaparece en silencio**: `RunSave.Load(json, out lost)` devuelve un id por copia que se queda fuera y
   el mapa lo dice **una vez** al continuar la partida («Esta partida guardada llevaba más consumibles de los que
   caben…: X, Y»). Se eligió avisar y no reembolsar: un consumible no tiene precio de venta en `/Sim` (el precio del
   mercado es de compra, ADR 0101) y inventarlo sería una regla de economía nueva. No sube la versión del
   esquema porque **la forma del guardado no cambia** (sigue siendo `consumables[]` y `counters{}`), sólo lo que
   significa; queda escrito aquí en vez de hacerse a escondidas (`modelo-datos.md`, «Versionado»). El único coste es
   para una run vieja con tres equipados o stock suelto, y el juego aún no está publicado.
8. **Cada consumible declara su disparador sugerido en `/data`** (`suggestedTrigger`, obligatorio en el esquema, uno de
   los seis de RF-083 sin umbral; provisional, sin medir). Es el que ponen Equipo al pasarlo de manual a condicional
   y la política: no uno por familia, que daba «¡Aguantad!» al ir perdiendo. Aguantar (`hold_the_line`) y el plan
   maestro esperan al empate; ir a por él, arriba y lo sucio, al ir por detrás; lo médico, a la lesión (el milagro
   del sanador, a la turba); lo sobrenatural, al tramo final; provocar a la grada, al empate.
9. **Descartar pide confirmación** (segunda pulsación en el mismo botón; cualquier otra acción la cancela): tira el
   consumible para siempre.
10. **La política automática** (`RunPolicy`) compra con un hueco libre (ya no hay `ConsumableStockTarget`) y **configura
   como condicional todo lo que lleva**, con su `suggestedTrigger`: en `/Balance` nadie pulsa un manual (`ManualTick` −1). Enmienda el punto 2 de
   la ADR 0101 y «lo que el manual no mide»: antes el primer consumible iba manual y se perdía en la medición; con dos
   huecos eso sería perder la mitad, así que la doctrina cambia con la regla y **el cambio se mide aparte** (abajo).

## Las diez preguntas (`game-design-review`)

1. **Qué experimenta el jugador:** compra un consumible y ya lo ve en su tablero del partido, con su botón; sabe que
   lleva dos y que no puede comprar un tercero sin usar o tirar uno. 2-3. **Qué decide:** qué comprar sabiendo que sólo
   caben dos (coste de oportunidad real en un hueco, no en oro), **cuándo** pulsarlo o con qué disparador dejarlo
   solo, y si tira uno para hacer sitio. Hoy decidía casi nada: no llegaba a usarlo. 4. **Regla:** RF-080..082 y
   RF-085, enmendados aquí; RF-083 y RF-084 no cambian. 5. **Sistemas:** `/Sim` (`RunState`, `RunEngine.ApplyConsumables`,
   `MarketSystem`, `MarketView`, `EventSystem`, `MatchResolution`, `RunSave`, `RunPolicy`); `/Game` (Equipo, mercado,
   tablero); ningún dato. La regla vive sólo en `/Sim` y la pantalla no la repite: lee `RunRules.ConsumableSlots` y
   `MarketRow.Block`. 6. **Alternativas:** (a) mantener tres huecos y el zurrón y sólo hacer que comprar equipe: no
   cumple la decisión del revisor y deja el problema de tener que equipar tras cada partido; (b) permitir duplicados
   con `ManualActivation` por hueco: cambia el contrato de la activación y el guardado por un caso que el catálogo de 20
   apenas provoca; (c) que comprar con los dos llenos sustituya a uno: contra «comprar sólo con slot libre».
   7. **Trade-off:** menos consumibles a mano y más decisión al comprarlos (se gana legibilidad y uso, se pierde
   acaparar). 8. **Estrategias:** un consumible caro y bueno ocupa un hueco de 45 de oro que no puede ocupar otro:
   ya no se «tiene de todo». Con los gritos (ADR 0166) y los dos huecos, el jugador elige entre un grito y un médico.
   9. **Degeneración:** (i) llevar un consumible guardado sin usarlo indefinidamente bloquea el hueco: es una
   decisión del jugador, con descarte disponible; (ii) los eventos que dan consumibles quedan no viables con los huecos
   llenos: es la mitad de su opción, no un bloqueo de la run.
   10. **Cómo se demuestra:** tests en `Sim.Tests` (`ConsumableSlotsTests`, y los de eventos, equipo, guardado y bucle
   de consumibles actualizados), lote de `/Balance` antes/después, capturas de Equipo con dos huecos, del mercado con
   hueco libre y con los dos llenos, y del partido con los botones.

## Implementación

- `/Sim`: `RunRules.ConsumableSlots = 2` (sustituye a `MaxEquippedConsumables = 3`); `RunState.HasFreeConsumableSlot`,
  `CarriesConsumable`, `CanTakeConsumable`, `WithTakenConsumable`, `FoldLegacyConsumables`; se retiran
  `ConsumablesOwned`, `OwnedConsumables` y el prefijo `consumable_owned:` como estado vivo (queda como
  `LegacyConsumableOwnedPrefix`, sólo para migrar). `MarketSystem.BuyConsumable` exige hueco y deja el consumible
  equipado; `RewardBlock.NoConsumableSlot` y `AlreadyCarried` en `MarketRow.Block`; `EventSystem` (viabilidad y sorteo);
  `MatchResolution.ConsumeConsumables` (sale sólo el usado); `RunEngine.ApplyConsumables`; `RunSave.Load`;
  `RunPolicy` (`ConfigureConsumables`, sin `ConsumableStockTarget`).
- `/Game`: Equipo (`ConsumablesPanel`, `ConsumablesView`): dos huecos con su modo, «Hacer manual», «Otro disparador» y
  «Descartar»; mercado (`MarketBagSlot`): los huecos con cuántos quedan y el motivo del bloqueo; el tablero del
  partido ya pintaba un botón por manual (dos caben).
- `/Balance`: `consumablesBoughtPerRun` y `consumablesUsedPerRun` (informativas, sin banda: Regla H) en `summary.csv`, y
  las columnas `consumablesBought`/`consumablesUsed` en `runs.csv`.

## Medición (`/Balance --full-runs 600 --seed 1`, antes y después, mismo árbol de datos)

| métrica | antes | después | Δ |
|---|---|---|---|
| `consumablesUsedPerRun` | 1,12 | **2,15** | +1,03 |
| `consumablesBoughtPerRun` | 2,31 | 2,67 | +0,36 |
| `runWinRate` (contextual) | 15,50 | 16,00 | +0,50 |
| `brokeMarketRunShare` | 7,83 | 8,50 | +0,67 |
| `affordableShareAtMarket` | 69,79 | 69,11 | −0,68 |
| `leftoverGoldShare` | 8,64 | 8,60 | −0,04 |
| `purchasesPerMarket` | 1,28 | 1,30 | +0,02 |

Ninguna métrica cambia de estado (dentro/fuera de banda): `runWinRate` y `brokeMarketRunShare` ya estaban **fuera**
antes (por debajo de su banda) y siguen igual de fuera. **Lectura, con la vara de la Regla F:**

- **CONFIRMED (por construcción):** los consumibles usados por run casi se duplican. **LIKELY que la causa sea la
  doctrina de la política**, no la regla: antes de cada partido el primer consumible iba manual y no disparaba nunca
  en `/Balance`, así que de dos comprados sólo uno servía; ahora sirven los dos (1,12 → 2,15 es ×1,9). No se aisló con un
  control (política vieja con la regla nueva).
- **Sin efecto demostrado** en `runWinRate` ni en `brokeMarketRunShare`: una sola semilla y 600 runs por doctrina dan un
  error típico de ~1,5 y ~1,1 puntos, y los dos cambios son de menos de la mitad. Se anota el **signo** (más consumibles
  usados, un poco más de victoria y de runs sin oro en el mercado) y no se afirma más.
- Se compran más porque **se usan más y liberan el hueco**; el tope ya no es un contador propio sino los dos huecos.
- No se movió el oro sobrante ni las compras por mercado: el consumible sigue siendo lo último que la política compra
  (ADR 0101).

## Queda abierto

- Los **hermanos de BA-H** siguen: `master_plan`, `smoke_flare` y `professional_foul` (multiplicadores con nombre de
  conducta) y **si una reliquia de un compañero es vendible** (ADR 0161) no se tocan aquí.
- **Los condicionales no se ven en el tablero hasta que saltan** (el aviso «usa un consumible» ya existe): un jugador
  que pasa uno a condicional no ve en la retransmisión que está «armado». Candidato a una etiqueta pequeña junto a los
  botones; sin medir que haga falta.
- ~~La opción de evento sin hueco aparece deshabilitada sin decir por qué~~: ahora añade «no tienes hueco libre»
  (`EventOptionRow.NoConsumableSlot`). Las demás opciones inviables (sin plantilla, sin a quién señalar) siguen mudas.
- **Medido tras la revisión independiente** con el disparador sugerido: ver la segunda tabla de la medición.
- La doctrina de la política sigue sin pulsar nunca un manual (CAT-C de la ADR 0101): la cifra es de un jugador que
  sólo configura disparadores.
