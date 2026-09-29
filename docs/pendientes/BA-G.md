# BA-G — Los nombres de los jugadores se repiten

**Estado:** **CERRADA para lo que enumeraba el encargo (29 sep 2026)**, con lo que queda abierto dicho abajo. Decisión: [ADR 0169](../decisiones/0169-un-nombre-no-se-repite-en-la-run.md).

## Observación

**Los nombres de los jugadores se repiten** y es difícil identificarlos al equipar un perk

## Análisis

**Causa CONFIRMED por medida** (Regla F): `NameGenerator` sortea sin memoria y una raza admite 300 (enano, elfo, no muerto),
336 (orco) o 920 (humano) nombres completos. `TeamGenerator` ya evitaba repetir **dentro de un equipo**, y nada más.
`GeneratedPlayers` (mercado, recompensa, canterano de evento) y `RivalRoster.SigningName` (fichajes de clan, ADR 0165) sortean
un nombre y siguen.

- Control (`RunNamesTests.TheRawDrawRepeatsAtTheScaleOfARun`): 100 nombres de una raza de 300, veinte semillas: **repetidos en
  todas**.
- Censo en el árbol de antes (`24fefc3`, 36 runs jugadas por la política de `/Balance`, tres razas, doce semillas cada una,
  sin vender): **5 runs (14 %) acaban con dos jugadores llamándose igual en la plantilla**, sobre plantillas de 13,4 de media.
  Es sólo el final de la run: lo que se ofrece y se descarta, o quien murió, no cuenta. Sin tocar los datos.

Hipótesis descartadas por el camino: *«el idioma cambia el sorteo»* —**REJECTED**: el generador sortea un índice, no una
cadena (RT-073), y las dos listas tienen la misma longitud—; *«los nombres de las razas se solapan»* —**REJECTED**: ninguna
pareja de razas comparte un nombre de pila ni un apellido (medido sobre `data/races/`).

## Arreglo (ADR 0169)

Se conserva el sorteo, y **sólo si el nombre ya está cogido** se cambia por otro de un flujo propio (`RngStreams.Names`, RT-022):
ni un dado del mercado, de una recompensa o de un partido se mueve. «Cogido» = la plantilla (viva, muerta o vendida), otra
oferta del mismo surtido, una elección anterior del club, o **reservado a los fichajes rivales** de su raza (40 nombres por
raza). El surtido se deriva otra vez en cada consulta (W-12), así que cada jugador admitido anota en `Counters` de qué
elección vino (`nameOwner:<nombre>`, W-11: sin subir el guardado) y la oferta de esa misma elección no lo cuenta como cogido:
comprar no cambia el nombre de nadie. Los fichajes de clan salen por **billete** de un retículo de nombres (biyección afín):
dos del mismo clan no pueden coincidir sin mirarse. Sitios: plantilla inicial, mercado, recompensa, canterano de evento y el
sumidero estricto `RunNames.Admit`. 16 tests (`RunNamesTests`).

## Medido

- **Los mismos 36 runs, después: 0 con nombre repetido** (`AWholeRunEndsWithoutARepeatedName`).
- **Nada más se mueve.** `/Balance --full-runs 150 --seed 1` (tres doctrinas, 150 runs cada una), el árbol de `main`
  (`beeb8db`) contra esta rama, con el turno de `tools/pesado.sh`: `runs.csv`, `runs-nomarket.csv` y `summary.csv`
  **idénticos byte a byte** (`cmp`); 91 s contra 69 s, sin coste medible. La revisión independiente lo comprobó aparte
  sobre el estado final de 80 runs completas (5 razas × 16 semillas): resultado, oro, atributos, perks, historial de nodos,
  memoria rival y contadores sin `nameOwner:` idénticos; sólo cambian 128 nombres. No cubierto por ninguna de las dos: runs
  con ventas (la política de `/Balance` casi no vende, CAT-I).

## Lo que queda abierto

- **Un fichaje rival cuyo billete coincide con un jugador de datos de su clan** (medido: 0,5 a 1,5 de los 40 billetes
  reservados por raza y semilla) se desplaza a un billete no reservado; ahí sí puede coincidir con el club (~1/300 por jugador).
- **Los billetes de la generación 10 en adelante** (un puesto vaciado diez veces) pueden pisarse entre sí; en 120 runs la
  generación máxima fue 1.
- **Los fichajes de un clan salen en progresión aritmética por la lista** (biyección afín sobre el retículo): sin medir si
  se nota a simple vista (revisión independiente, LIKELY).
- **Hermano encontrado, no de este pendiente** ([BT-A](./BT-A.md)): la política de `RunPolicy` intenta **vender a un fichaje sin
  experiencia** y lanza (`MarketSystem.Sell`, ADR 0108): en el censo, 9 de 60 runs con el `Setup` de los tests.
- Lo que la revisión independiente encontró y **se arregló en este pendiente**: el equipo del **jefe** repetía nombres del club
  (56 de 200 plantillas enanas contra el jefe enano) y el club podía llamarse como un **jugador de datos de un clan**
  (22 de 80 plantillas finales). Ver los puntos 6 y 7 de la ADR.

## Hermanos

[BS-A](./BS-A.md) (la esquela nombra a quien mató: con nombres repetidos era ambigua) y la nota de la
[ADR 0165](../decisiones/0165-clanes-cerrados-y-nemesis.md) sobre nombres de némesis y de fichaje.
