# BE-F — `NodeKinds.IsMatch` incluye `Boss`, y cada consumidor nuevo tiene que acordarse de excluirlo

Estado: **RESUELTA la lectura (23 sep 2026, opción 2)**; **abierto el dato** — el nodo de jefe sigue
guardando el `opponentId` fantasma, y quitarlo exige regenerar todos los mapas. No era un fallo: era un
patrón que estuvo a punto de morder **tres veces en un solo día** (22 sep 2026), lo que apuntaba a la
primitiva y no a quien la usa. `CLAUDE.md`: *piensa en sistemas, no en tickets.*

## El hecho

`NodeKinds.IsMatch(kind)` devuelve `true` para `LeagueMatch`, `EliteMatch` **y `Boss`**
(`Sim/Run/Map/NodeKind.cs:46-47`). Como consecuencia, el nodo de jefe **consume un hueco del cursor de
`MapGenerator`** y **guarda un `opponentId` de un rival de catálogo que nadie juega ahí**: el equipo del
jefe lo construye `BossRunSystems` desde `data/bosses/`, y `StandardRunSystems` descarta ese id.

Es decir: **todo nodo de jefe lleva un `opponentId` fantasma, sintácticamente válido y semánticamente
falso.**

## Las tres veces que ha estado a punto de morder, el mismo día

1. **`Sim/Run/Systems/Rivals/RivalHistory.cs`** (ADR 0124): excluye `Boss` a mano. Sin esa línea, la cuenta
   de «veces que he visto a este rival» mentiría.
2. **El censo de encuentros** (`_RivalCensusTests.cs`, ADR 0126): hubo que excluirlo en el encargo, por
   escrito, para que la medición no contara encuentros que no existen.
3. **`MatchResolution.ApplyRivalCredits`** (BE-B): el par (causante, víctima) se habría acreditado al clan
   equivocado. **Se descartaba, pero por accidente**: el jefe usa
   `DefaultRunSystems.OpponentFirstPlayerId` (1.000.000) y el rival de catálogo
   `RivalTeamBuilder.OpponentFirstPlayerId` (2.000.000), así que el índice salía negativo. Mover cualquiera
   de las dos constantes lo habría roto **en silencio**. Se añadió la exclusión explícita.

## Por qué importa ahora más que antes

Hasta la ADR 0124 nadie leía el `opponentId` guardado. Desde que existe memoria de rivalidad —y con la ADR
0126 (clanes canónicos) y la 0127 en camino— **ese campo pasa a ser clave primaria de la narrativa**. Un id
fantasma deja de ser un dato inerte y pasa a poder escribir una mentira en el historial del jugador.

## Opciones, sin decidir

1. **Que el nodo de jefe no guarde `opponentId`.** Parece lo correcto, pero **toca `MapGenerator`**, que
   consume el flujo `RngStreams.Map`: cambiar el cursor regenera **todos los mapas de todas las semillas** e
   invalida la referencia de balance entera. Es caro, y por eso no se ha hecho de pasada.
2. **Partir la primitiva**: `IsMatch` (los tres) y algo como `IsCatalogRivalMatch` (los dos de catálogo),
   y que cada consumidor elija explícitamente. **Barato, sin tocar el mapa ni el RNG**, y convierte una
   omisión silenciosa en una elección visible.
3. **Dejarlo y documentarlo**, aceptando que cada consumidor nuevo se acuerde. Es lo que se hace hoy, y
   lleva tres avisos en un día.

**Recomendación provisional: la 2.** Es la que elimina la clase de error en vez de parchear el caso.
No se toca sin `architecture-review`: es una primitiva compartida.

## Resuelta con la opción 2 (23 sep 2026), tras `architecture-review`

`NodeKinds.IsCatalogRivalMatch(kind)` —liga y élite, nunca el jefe— convive con `IsMatch`, que se queda
como está. Los dos consumidores que excluían `Boss` a mano pasan a preguntarlo por su nombre:
`RivalHistory` y `MatchResolution.ApplyRivalCredits`. Contrato fijado en
`Sim.Tests/Run/NodeKindsTests.cs`, con un caso que recorre el enum entero para que un `NodeKind` nuevo
obligue a decidir en vez de heredar un silencio.

**Lo que dijo la revisión de arquitectura, punto por punto:**

- **Frontera**: el cambio queda **entero dentro de `/Sim`**. `IsMatch` es público y lo consumen doce
  sitios de `/Game`, pero allí la pregunta que hacen —"¿aquí se juega?"— sigue siendo la correcta, así que
  no se toca ninguno y no hay commit que mezcle proyectos.
- **¿Elimina complejidad o la mueve?** La elimina: dos condiciones compuestas escritas de **dos formas
  distintas** pasan a un predicado con nombre. Pero el argumento de peso es otro: convierte una **omisión
  silenciosa** en una **elección visible**.
- **Determinismo**: predicado puro; los dos call sites cambian a una expresión **booleanamente
  equivalente**. Inerte por construcción, RT-024 intacto.

**Precisión que la revisión añadió y que este fichero no decía**: la opción 2 **no elimina la clase de
error, la mitiga**. Quien escriba `IsMatch` por costumbre sigue teniendo el bug; el predicado nuevo lo hace
fácil de evitar, no imposible. Eliminarla de verdad exigiría **renombrar `IsMatch`** para que el nombre
viejo no compile y obligue a leer los 28 call sites — pero doce están en `/Game`, así que rompería la regla
de no mezclar `/Sim` y `/Game` en un commit, por un beneficio que no lo paga. Se acepta la mitigación, y se
dice.

**Lo que sigue abierto, y por qué no se toca**: el nodo de jefe **sigue guardando** el `opponentId`
fantasma. Esto arregla la **lectura**, no el **dato**. La opción 1 es la que lo arreglaría de raíz, y sigue
rechazada por el mismo motivo: `MapGenerator` consume el flujo `RngStreams.Map` y mover su cursor
regeneraría todos los mapas de todas las semillas, invalidando la referencia de balance entera. Si algún
día hay que regenerar los mapas por otra razón, esta va en el mismo viaje.

## Hermanos

ADR 0124 (`RivalHistory`) · ADR 0126 (el censo) · [BE-C](./BE-C.md) (otro caso de «el mismo fichero no
tiene una noción única de qué cuenta como partido»)
