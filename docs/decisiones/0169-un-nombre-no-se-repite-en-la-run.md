# ADR 0169 — Un nombre completo no se repite dentro de la run

Fecha: 29 sep 2026 · Estado: **aceptada** (arreglo de un pendiente del revisor, [BA-G](../pendientes/BA-G.md); no cambia
ninguna regla de `docs/requisitos.md`). **Requisitos:** RF-020b, RF-114, RF-114b, RF-110, RF-015, RT-021, RT-022, RT-073.
**Relacionada:** ADR 0165 (los fichajes de los clanes rivales: enmendada), W-11 y W-12 de `docs/fase2-diseno.md`
(contadores de run y surtido derivado).

## Qué se quejaba (BA-G) y qué era

«Los nombres de los jugadores se repiten y es difícil identificarlos al equipar un perk.» **CONFIRMED** por medida
(`RunNamesTests.TheRawDrawRepeatsAtTheScaleOfARun` y un censo en el árbol anterior): una raza admite 300 (enano, elfo,
no muerto), 336 (orco) o 920 (humano) nombres completos y `NameGenerator` sortea **sin memoria**; una run genera un
centenar de jugadores (plantilla, y por cada mercado fichajes, canteranos y mercenarios, más recompensas y eventos). Con
120 jugadores sobre 300 nombres, la probabilidad de que no haya ni una pareja es del orden de e⁻²⁴. En las mismas 36
runs jugadas por la política de `/Balance` (tres razas, doce semillas), **5 acababan con dos jugadores llamándose igual
en la plantilla** (14 %), y eso mide sólo el final: lo que se ofrece y se descarta no cuenta.

## Lo que había (Regla G)

`TeamGenerator` ya garantizaba que no se repiten nombres **dentro de un equipo** (`usedNames`), pero sólo ahí. Los
otros cuatro sitios que generan jugadores —`GeneratedPlayers` (mercado, recompensa, canterano de evento) y
`RivalRoster.SigningName` (fichajes de rival)— sortean un nombre y siguen. `RivalRoster` sólo evitaba los nombres de
los diez jugadores **de datos** de su clan.

## Decisión

1. **Se conserva el sorteo y se cambia sólo el nombre de quien repite.** El jugador generado sigue llevando el nombre
   que le salió (el flujo del mercado, de la recompensa o del evento gasta **exactamente** lo mismo que antes) y, sólo si
   ese nombre ya está cogido, se le sustituye por otro del flujo nuevo **`RngStreams.Names`** (RT-022). Un nombre no
   entra en ningún cálculo del partido ni de la run, así que una run con y sin la regla es idéntica salvo en cómo se
   llaman los repetidos (medido: ver «Medición»).
2. **Qué cuenta como cogido**, para un jugador que se ofrece al club: cualquiera de la plantilla —vivo o muerto—, otro
   jugador del mismo surtido (en el orden fijo fichajes, canteranos, mercenarios), un jugador que otra elección del
   club ha admitido antes, y los nombres **reservados a los fichajes rivales** de su raza (punto 4).
3. **El surtido se deriva, no se guarda (W-12), y se vuelve a derivar tras comprar.** Si el fichaje comprado contara
   como «cogido» al volver a derivar, su oferta —y las siguientes— cambiarían de nombre en cuanto se pagara. Por eso
   cada jugador admitido anota en `RunState.Counters` (W-11, **sin subir el esquema del guardado**) la clave
   `nameOwner:<nombre>` con la **elección** de la que vino (`RunNames.OwnerToken`: 0 la plantilla inicial; si no,
   `1 + nodo·4 + elecciones ya cobradas`, porque el jefe da dos, ADR 0043). El surtido de esa elección no cuenta esos
   nombres como cogidos; los de las demás, sí. Es **estable dentro de la elección y estricto fuera de ella**.
4. **Fichajes rivales (ADR 0165, enmienda).** Siguen derivándose de (semilla, clan, puesto, generación) y de nada más,
   sin mirar la plantilla ni el reloj, pero ahora por **billete**: `NameLattice.NameOfTicket`, con el billete
   `generación·10 + puesto`, es una biyección afín `(a·t + b) mod N` del retículo de nombres de la raza, así que dos
   fichajes del mismo clan **no pueden llamarse igual** sin mirarse, y se salta el nombre de un jugador de datos del
   clan (billete + 100). Los billetes de las primeras cuatro generaciones de cada puesto (**40 nombres por raza**,
   `RivalRoster.SigningPoolTickets`) están **reservados**: el club no los lleva, ni la plantilla inicial ni ninguna
   oferta. Así un fichaje rival nunca repite a un jugador del club aunque nadie mire a nadie. Sustituye el flujo
   `RngStreams.Rewards` con sal 950.000.000 que fijó la ADR 0165.
5. **Cinco sitios, un sólo criterio** (`Sim/Run/Systems/RunNames.cs`): plantilla inicial (`RunEngine.Start`), surtido del
   mercado, opciones de recompensa, canterano de evento, y el **sumidero estricto** `RunNames.Admit` por el que entra
   todo jugador comprado o elegido (renombra sólo si se compra dos veces la misma oferta, que el mercado no retira).
   Un nombre vendido no se reutiliza.
6. **Los jugadores de datos de los clanes también se reservan** *(añadido tras la revisión independiente, que midió 22 de
   80 plantillas finales con un jugador llamado como uno de datos, más que el defecto que se arreglaba)*. Son identidad
   —los tres rivales de una raza son el mismo clan con los mismos nombres por puesto, y el némesis se reconoce por su
   nombre—, así que **cede el club**: `RunNames.ReserveWorld` anota al empezar la run los 50 nombres de `data/rivals/` con
   un valor que ninguna elección del club tiene por suyo (`-1`), sin pasar el catálogo de rivales a los nueve sitios que
   consultan el surtido: el registro ya viaja en el estado. La plantilla inicial cede ante ellos (`AdmitInitialRoster`).
7. **El equipo de un jefe cede ante el club** *(revisión independiente: 56 de 200 plantillas enanas coincidían con el jefe
   enano, 17 de 72 runs de la misma raza)*. El jefe se genera con el flujo de generación y sin saber quién hay en la
   plantilla, y su nombre no es identidad (`data/bosses/` sólo trae el del equipo). `RunNames.YieldToClub`, llamado por
   `BossRunSystems.OpponentFor`, cambia el nombre de quien coincide con alguien de la plantilla, con el flujo propio; id,
   atributos, perks y alineación salen como los de sus datos. Es función pura de (estado, equipo): el ojeo y el partido,
   que se construyen sin que la plantilla cambie entre medias, enseñan los mismos nombres.

## Lo que no cubre, dicho

- **Un guardado anterior** cambia los nombres de los fichajes rivales que aún no eran némesis (ahora salen por billete) y
  `RivalKiller.Name` los recalcula, así que una esquela de una muerte anterior podría nombrar a otro. No sube el
  esquema del guardado (W-11); el proyecto rechaza los guardados de versión antigua en cada subida y esta no la sube.
  Aceptado a esta altura del desarrollo (sin lanzar).
- **Los fichajes rivales se numeran por raza, no por clan** (la biyección de billetes sale de la semilla y la raza): hoy hay
  un clan por raza, y si hubiera dos de la misma, sus fichajes compartirían nombres de un clan a otro (no dentro de uno).
- **Un fichaje rival cuyo nombre coincide con un jugador de datos de su clan** se desplaza al billete siguiente, que ya
  no está reservado: ahí el club podría, con probabilidad ≈ 1/300 por jugador, llevar el mismo nombre. **Medido** con
  los datos de hoy (300 semillas): de los 40 billetes reservados de cada raza, coinciden con un jugador de datos de su
  clan 0,49 (humano, 920 nombres), 1,19 (orco), 1,23 (enano), 1,35 (elfo) y 1,48 (no muerto) por semilla, un 1-4 %.
- **Los billetes de la generación 4 en adelante** (un mismo puesto vaciado cinco veces) no están reservados; siguen
  siendo únicos dentro del clan.
- **Una plantilla que trae el propio `RunSetup`** (tests) se anota, no se renombra.
- **La política de `/Balance`** intenta vender a un fichaje sin experiencia y lanza (medido en el censo, sin relación con los
  nombres): [BT-A](../pendientes/BT-A.md).

## Las diez preguntas (`game-design-review`, pase mínimo: no es una mecánica, es identidad legible)

1. **Qué vive el jugador**: distingue a sus jugadores por el nombre al elegir a quién dar un perk. 2. **Qué debería ser**:
un nombre por jugador. 3. **Regla que representa**: ninguna de juego; RF-020b (nombres por raza, sin duplicar). 4. **Sistema
responsable**: `/Sim` (es estado de la run), `/Game` no decide nada. 5. **Hermano**: [BS-A](../pendientes/BS-A.md) (la
esquela nombra a quien mató, y con nombres repetidos era ambigua) y la nota de la ADR 0165 sobre nombres de némesis y de
fichaje. 7. **Abstracción existente**: `usedNames` de `TeamGenerator`, extendido a la run; contadores W-11. 8. **Causa,
no síntoma**: el sorteo sin memoria. 9. **Segundo orden**: un nombre menos posible por jugador en la raza (el retículo de
300 pierde 40 reservados y los 10 de datos de su raza) y un nombre que ya no se libera al vender. 10. **Cómo se prueba**:
`RunNamesTests` (20), un test en `EventEffectTests` y la comparación de `/Balance`.

## Arquitectura (`architecture-review`)

- **Frontera**: entero dentro de `/Sim`; `/Game` no cambia. **Primitiva nueva**: `RngStreams.Names` (flujo propio, como
  `Clinic` y `Referees`) y `NameLattice`. **Determinismo**: la biyección y el retículo son enteros (`long`), el registro es
  un `SortedDictionary` que ya existía, los `HashSet` sólo se usan para pertenencia y nunca se recorren para decidir
  (RT-041). **Guardado**: sin versión nueva (W-11); un guardado anterior no tiene registro y `Taken` cae a la plantilla.
- **Coste**: cada oferta compara con la plantilla y con 40 nombres reservados; el `Book` construye el conjunto de una
  raza una vez por derivación.

## Medición

`/Balance --full-runs 200 --seed 1` en el árbol anterior y en éste, con el turno de `tools/pesado.sh`: ver el resultado
en [BA-G](../pendientes/BA-G.md). Un nombre no entra en ningún cálculo; lo que se comprueba es que **nada más se mueve**.
