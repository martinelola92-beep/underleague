# BE-F — `NodeKinds.IsMatch` incluye `Boss`, y cada consumidor nuevo tiene que acordarse de excluirlo

Estado: **abierta, de diseño de primitiva**. No es un fallo: es un patrón que ya ha estado a punto de
morder **tres veces en un solo día** (22 sep 2026), lo que sugiere que el problema es la primitiva y no
quien la usa. `CLAUDE.md`: *piensa en sistemas, no en tickets.*

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

## Hermanos

ADR 0124 (`RivalHistory`) · ADR 0126 (el censo) · [BE-C](./BE-C.md) (otro caso de «el mismo fichero no
tiene una noción única de qué cuenta como partido»)
