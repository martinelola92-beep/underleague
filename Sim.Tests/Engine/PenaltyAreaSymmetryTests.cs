using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Model;
using Underleague.Sim.Random;

namespace Underleague.Sim.Tests.Engine;

/// <summary>
/// La ADR 0121 (docs/decisiones/0121-el-area-del-portero-esta-centrada.md) centró la banda del área del
/// portero, pero <see cref="AreaSymmetryTests"/> y <see cref="GoalkeeperHomeCellAreaTests"/> solo prueban
/// la primitiva (<c>Pitch.IsInArea</c>) y la validación de la casilla-hogar. Ninguno de los dos ejercita
/// las dos reglas del <b>motor</b> que la ADR dice que arregla: qué falta se pita penalti
/// (<c>MatchEngine.ResolveFoul</c>, "inOwnArea") y cuándo el portero persigue un balón suelto
/// (<c>Utility.EvaluateChaseBall</c>). Esta es la revisión independiente pidiendo esos dos test (revisión
/// de la ADR 0121, hallazgo BD-A).
///
/// <para><b>Por qué el primero es estadístico y no exacto.</b> <c>ResolveFoul</c> y <c>ResolveTackle</c>
/// son privados: el único punto de entrada público de <see cref="MatchEngine"/> es <c>Run()</c>, que juega
/// el partido completo con su propia aleatoriedad. No hay forma de forzar una falta en una fila exacta sin
/// tocar <c>/Sim</c> (añadir un gancho de test) ni sin recurrir a reflexión sobre miembros privados, que no
/// tiene precedente en este proyecto. Se opta por la alternativa que el encargo ofrece: un lote con semilla
/// fija, correlacionando cada falta de penalti con la fila en la que ocurrió a través de
/// <c>MatchTrace.PhaseAt</c> (RT-098 lo hace por jugador y tick; esto es el mismo truco por fase y tick).
/// </para>
///
/// <para><b>Por qué solo se compara la fila 1 contra la 5.</b> El hallazgo BD-A
/// (docs/pendientes/BD-A.md), de la propia medición de la ADR 0121, deja **abierta** una asimetría entre
/// las filas 2 y 4 (3 a 1) que la ADR 0121 no causa ni arregla: exigir aquí que 2 y 4 salgan balanceadas
/// haría fallar el test por un problema ajeno y ya documentado. Las filas 1 y 5 son exactamente las que la
/// ADR 0121 mueve (el borde de la banda, antes pegado arriba); son la comparación con poder discriminativo
/// para <b>este</b> cambio.</para>
/// </summary>
public sealed class PenaltyAreaSymmetryTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();

    /// <summary>Semilla base del lote y de la medición de referencia de la ADR 0121 (docs/balance.md).</summary>
    private const ulong BaseSeed = 777UL;

    /// <summary>
    /// Partidos del lote: 3.000 tardan ~9 s (medido) y dan una muestra de 126 penaltis (fila 1: 12, fila
    /// 2: 67, fila 4: 21, fila 5: 26 — filas 0, 3 y 6 vacías) con la que discriminar sin acercarse al
    /// presupuesto de 60 s del fichero. La ADR 0121 midió ~36-39 en 300 partidos con emparejamientos
    /// variados; el mismo emparejamiento fijo con semillas de partido distintas da una tasa de penaltis
    /// por partido similar (≈0,042 aquí contra ≈0,12-0,13 allí, del mismo orden).
    /// </summary>
    // 4500 desde la ADR 0139: con 3000 la muestra de filas extremas cayó a 19 —por debajo del mínimo de
    // 20 que el propio test exige para pronunciarse— porque el paquete de balón aéreo y despeje cambió
    // dónde acaba el balón y los penaltis en las filas 1 y 5 se hicieron más raros. Ampliar la muestra es
    // lo único que se puede hacer sin tocar el juego: el test no falla por un reparto asimétrico, falla
    // por no poder discriminar. Que la tasa haya bajado queda anotado para la fase de balance (BD-A).
    //
    // 4500 -> 9000 (24 sep 2026, BC-A y BI-F): otra vez lo mismo, y por lo mismo. Los arreglos de la
    // reanudación y del saque de puerta vuelven a mover dónde acaba el balón y la muestra de filas
    // extremas cae a 13. Medido, 4500 partidos, penaltis por fila:
    //
    //   antes    [0, 12, 99, 15, 13, 20, 0] = 159
    //   después  [0,  7, 76,  6, 47,  6, 0] = 142
    //
    // Conviene leer las dos cifras que importan antes de ampliar por inercia: el reparto que este test
    // afirma -filas 1 contra 5- pasa de 12/20 a 7/6, o sea MÁS simétrico que antes, y de regalo las filas
    // 2 y 4, que son el hallazgo abierto de BD-A, pasan de 99/13 a 76/47. Lo único que empeora es la
    // POTENCIA: 13 no llega al suelo de 20. Se dobla la muestra (13 x 2 = 26, con margen) y se anota el
    // movimiento de BD-A, que sigue siendo cosa de la fase de balance y no de aquí.
    private const int Matches = 9000;

    /// <summary>Mínimo de penaltis en las filas 1+5 para que la comparación tenga algo que discriminar.</summary>
    private const int MinimumExtremeRowSample = 20;

    private static readonly Lazy<IReadOnlyList<int>> PenaltyFoulRows = new(CollectPenaltyFoulRows);

    /// <summary>
    /// Ninguna falta que el motor convierte en penalti puede caer fuera de la banda real del área
    /// (<see cref="Pitch.AreaTop"/>..<see cref="Pitch.AreaBottom"/>): con la banda vieja (pegada arriba,
    /// filas 0 libre arriba y 2 abajo) una falta en fila 5 completa (Y en [5, 5,5)) habría sido "fuera del
    /// área" y no habría generado penalti, así que este assert por sí solo ya discrimina la banda vieja de
    /// la nueva para esa franja.
    /// </summary>
    [Fact]
    public void NoPenaltyFoulFallsOutsideTheAreaBand()
    {
        var rows = PenaltyFoulRows.Value;
        Assert.NotEmpty(rows);

        // Cell.Row es la fila entera (floor de Y): con Pitch.AreaTop=1,5 y Pitch.AreaBottom=5,5 (AreaRows=4,
        // Rows=7) ninguna fila que llegue a penalti puede ser 0 o 6, en banda vieja o nueva: es la
        // comprobación más barata y no depende de qué banda esté vigente.
        foreach (int row in rows)
        {
            Assert.InRange(row, (int)MathF.Floor(Pitch.AreaTop), (int)MathF.Ceiling(Pitch.AreaBottom) - 1);
        }
    }

    /// <summary>
    /// El reparto de penaltis entre la fila 1 y la fila 5 -las dos que la ADR 0121 desplaza- debe ser
    /// simétrico dentro de ruido. Antes de la ADR (banda [1, 5], una fila libre arriba y dos abajo) la
    /// medición real fue 10 contra 1 en 300 partidos (docs/decisiones/0121-el-area-del-portero-esta-centrada.md,
    /// "Lo medido"): una
    /// proporción de 10 a 1 no pasaría el margen de esta prueba, que es la comprobación de que el test
    /// discrimina la banda vieja de la nueva sin tener que volver a tocar /Sim.
    /// </summary>
    [Fact]
    public void PenaltyFoulsAtTheExtremeRowsAreBalanced()
    {
        var rows = PenaltyFoulRows.Value;
        int row1 = rows.Count(r => r == 1);
        int row5 = rows.Count(r => r == 5);

        Assert.True(
            row1 + row5 >= MinimumExtremeRowSample,
            $"muestra insuficiente para discriminar: fila1={row1}, fila5={row5} en {Matches} partidos (semilla {BaseSeed})");

        int larger = Math.Max(row1, row5);
        int smaller = Math.Min(row1, row5);

        // Margen generoso para una muestra de decenas de penaltis (no es una puerta de balance): la mayor
        // no puede triplicar a la menor más un colchón fijo de ruido. 10 contra 1 (banda vieja medida) no lo
        // cumple: 10 > 1*3+3=6.
        Assert.True(
            larger <= (smaller * 3) + 3,
            $"reparto asimétrico entre filas 1 y 5: fila1={row1}, fila5={row5} en {Matches} partidos (semilla {BaseSeed})");
    }

    /// <summary>
    /// Recoge, en Matches partidos con el mismo emparejamiento de referencia y una semilla de partido
    /// distinta por índice (RngStreams.MatchSeed, RT-022), la fila de cada falta que el motor convirtió en
    /// penalti. Se identifica el instante exacto con la traza (RT-098 lo hace por jugador y tick con el
    /// volcado de utilidad; aquí es "en qué tick pasa <c>_phase</c> a Penalty por primera vez tras estar en
    /// cualquier otra fase", que ocurre en el MISMO tick en el que <c>ResolveFoul</c> resolvió la falta:
    /// <c>MatchEngine.Run</c> llama a <c>Capture</c> justo después de cada <c>Step</c>) y se toma la fila del
    /// evento FOUL sin cancelar de ese mismo tick.
    /// </summary>
    private static List<int> CollectPenaltyFoulRows()
    {
        var setup = TestMatches.Reference(Catalog, BaseSeed);
        var config = SimConfig.Default with { CollectLog = false, Trace = true };

        var perMatch = new List<int>[Matches];
        Parallel.For(0, Matches, i =>
        {
            ulong matchSeed = RngStreams.MatchSeed(BaseSeed, i);
            // Catálogo por hilo (ThreadCatalogs): las condiciones de perk compiladas no son reentrantes.
            var result = Simulator.Run(setup, matchSeed, ThreadCatalogs.Current, config);
            perMatch[i] = ExtractPenaltyFoulRows(result);
        });

        // Reducción en orden de índice, no por finalización de hilo (RT-041, RT-057): el resultado no
        // depende de qué hilo terminó antes, aunque aquí solo importe el conteo, no el orden.
        var rows = new List<int>();
        for (int i = 0; i < Matches; i++)
        {
            rows.AddRange(perMatch[i]);
        }

        return rows;
    }

    private static List<int> ExtractPenaltyFoulRows(MatchResult result)
    {
        var rows = new List<int>();
        var trace = result.Trace;
        if (trace is null)
        {
            return rows;
        }

        var previousPhase = MatchPhase.Kickoff;
        bool firstFrame = true;
        for (int frame = 0; frame < trace.FrameCount; frame++)
        {
            var phase = trace.PhaseAt(frame);
            bool enteredPenalty = phase == MatchPhase.Penalty && (firstFrame || previousPhase != MatchPhase.Penalty);
            if (enteredPenalty)
            {
                int tick = trace.TickAt(frame);
                var foul = FindUncancelledFoulAt(result, tick);
                if (foul is not null)
                {
                    rows.Add(foul.Cell.Row);
                }
            }

            previousPhase = phase;
            firstFrame = false;
        }

        return rows;
    }

    /// <summary>
    /// El evento FOUL sin el sufijo ":cancelled" (EmitCancellable) del tick dado, o null si no hay
    /// ninguno: <c>ResolveFoul</c> siempre emite exactamente uno antes de decidir el penalti, en el mismo
    /// tick que abre la fase Penalty (no hay avance de tick entre los dos).
    /// </summary>
    private static MatchEvent? FindUncancelledFoulAt(MatchResult result, int tick)
    {
        foreach (var e in result.Events)
        {
            if (e.Tick == tick && e.Type == EventType.Foul && e.Detail == "foul")
            {
                return e;
            }
        }

        return null;
    }

    // ---------------------------------------------------------------- portero: perseguir balón suelto

    /// <summary>Centro geométrico del campo (3,5 con Rows=7): eje de simetría de la banda del área.</summary>
    private const float Center = Pitch.Rows / 2f;

    public static IEnumerable<object[]> MirroredBoundaryYPairs()
    {
        // Justo en el borde de la banda (1,5 y 5,5): el portero debe poder perseguir en los dos. Con la
        // banda vieja ([1, 5]) 5,5 quedaba fuera del área y 1 no perseguía ahí.
        yield return new object[] { 1.5f, 5.5f, true };
        // Justo fuera, a una décima de cada borde: en ninguno de los dos debe perseguir. Con la banda
        // vieja, 1,4 SÍ estaba dentro (perseguía) y 5,6 no: es la pareja más discriminativa.
        yield return new object[] { 1.4f, 5.6f, false };
        // Cómodamente dentro de la banda en los dos lados.
        yield return new object[] { 2f, 5f, true };
        // Cómodamente fuera en los dos lados.
        yield return new object[] { 1f, 6f, false };
    }

    /// <summary>
    /// El portero solo persigue un balón suelto dentro de la banda del área (<c>Utility.EvaluateChaseBall</c>,
    /// <c>Pitch.IsInArea</c>), y debe hacerlo igual arriba que abajo: el mismo par espejado debe dar la
    /// misma decisión (perseguir o no) en los dos lados. La fila se lee del <c>UtilityRow</c> de
    /// <c>ChaseBall</c> devuelto por <c>Utility.Choose</c> (RT-098), no de una reimplementación de
    /// <c>IsInArea</c> en el test.
    /// </summary>
    [Theory]
    [MemberData(nameof(MirroredBoundaryYPairs))]
    public void GoalkeeperChasesLooseBallSymmetricallyAtTheNewBandEdges(float y, float mirroredY, bool shouldChase)
    {
        Assert.Equal(mirroredY, (2f * Center) - y, precision: 5);

        var upper = ChaseBallRowFor(y);
        var lower = ChaseBallRowFor(mirroredY);

        Assert.Equal(shouldChase, !upper.Rejected);
        Assert.Equal(shouldChase, !lower.Rejected);
    }

    /// <summary>
    /// Fila de utilidad de <c>ChaseBall</c> para un portero solitario (equipo 0, área en X&lt;2) con un
    /// balón suelto y parado en la fila <paramref name="ballY"/>. Mismo arnés ligero que
    /// <see cref="TackleBiasTests"/> y <see cref="OffBallTests"/>: jugador y contexto a mano, sin motor
    /// completo, porque lo que se prueba es la evaluación de una acción, no el partido.
    /// </summary>
    private static UtilityRow ChaseBallRowFor(float ballY)
    {
        var keeper = Player(1, Position.Goalkeeper, new Cell(0, 3));
        keeper.Index = 0;

        var ball = new Ball
        {
            InterceptAttempted = new bool[1],
            Position = new Vec2(0.5f, ballY),
        };

        var context = new UtilityContext(
            new[] { keeper }, ball, ChaseBallWeights(), Catalog.Tuning.ActionZone, Catalog.Tuning.Pass.InterceptRadiusCells);
        context.TacticalStates[0] = TacticalState.OutOfPossession;
        context.TacticalStates[1] = TacticalState.OutOfPossession;
        // El portero es su propio perseguidor designado (AW-S): sin esto, el descarte por "no soy el más
        // cercano" tapa el que se quiere medir (el del área).
        context.NearestToBall[0] = keeper;

        var rows = new List<UtilityRow>();
        Utility.Choose(context, keeper, rows);
        return Row(rows, PlayerAction.ChaseBall);
    }

    private static UtilityRow Row(List<UtilityRow> rows, PlayerAction action)
    {
        foreach (var row in rows)
        {
            if (row.Action == action)
            {
                return row;
            }
        }

        throw new InvalidOperationException($"la tabla de utilidad no contiene la acción {action}");
    }

    private static MatchPlayer Player(int id, Position position, Cell home, int team = 0)
    {
        var definition = new PlayerDefinition(
            id, "p" + id, Race.Human, position, Rarity.Common, 1,
            new Attributes(50, 50, 50, 50, 50),
            Array.Empty<Trait>(),
            new List<string> { "Neutral", position.ToString() },
            PhysicalState.Healthy);
        return new MatchPlayer(definition, team, home, Catalog);
    }

    /// <summary>
    /// Pesos sintéticos: solo ChaseBall tiene peso base (con Retreat como única alternativa legal
    /// relevante), mismo estilo que <see cref="TackleBiasTests.TackleWeights"/>. Lo que se lee de aquí no
    /// es qué elige el portero, sino si ChaseBall queda descartado (Rejected) en su propia fila.
    /// </summary>
    private static AiWeights ChaseBallWeights()
    {
        int positions = Enum.GetValues<Position>().Length;
        int actions = Enum.GetValues<PlayerAction>().Length;
        var baseTable = new int[positions, actions];
        baseTable[(int)Position.Goalkeeper, (int)PlayerAction.ChaseBall] = 100;
        baseTable[(int)Position.Goalkeeper, (int)PlayerAction.Retreat] = 10;

        var tacticalTable = new int[Enum.GetValues<TacticalState>().Length, actions];
        for (int s = 0; s < tacticalTable.GetLength(0); s++)
        {
            for (int a = 0; a < actions; a++)
            {
                tacticalTable[s, a] = 100;
            }
        }

        var context = new AiContext(
            0, 100, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            TackleDistanceMaxCells: 0f,
            TackleOutOfReachPenalty: 0,
            TackleBallCarrierBonus: 0,
            RetreatDistanceBonusPerCell: 0,
            RetreatAtHomePenalty: 0);

        var shifts = new BlockShift[Enum.GetValues<TacticalState>().Length];
        return new AiWeights(baseTable, tacticalTable, TestData.NeutralMentality(), TestData.OffBallTackleAdjust(), context, shifts);
    }
}
