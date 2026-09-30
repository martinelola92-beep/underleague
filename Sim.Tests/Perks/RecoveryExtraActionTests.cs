using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Generation;
using Underleague.Sim.Model;
using Underleague.Sim.Perks;
using Underleague.Sim.Random;
using Xunit.Abstractions;

namespace Underleague.Sim.Tests.Perks;

/// <summary>
/// BB-Q Alt 0: <c>extraAction</c> admite <see cref="EventType.Recovery"/> y lo traduce a
/// <c>RepeatTackle</c>. Es la forma de reaccionar al RESULTADO de una entrada sin romper el modelo
/// pre-resolución: <c>Emit(Recovery, "tackle", tackler)</c> (<c>MatchEngine.cs:2223</c>) se publica justo
/// después del derribo de <c>:2221</c>, así que una entrada ganada ES un rival en el suelo.
///
/// <para>No hay <see cref="EventType"/> nuevo, ni primitiva de condición nueva, ni cambio en el
/// comportamiento de TACKLE.</para>
/// </summary>
public sealed class RecoveryExtraActionTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();
    private static readonly RefereeSetup Referee = new("Referee", RefereeTrait.Neutral, 0);
    private const int Matches = 20;

    /// <summary>
    /// Partidos del lote que comprueba que la rama es **alcanzable**. Son 200 y no los 20 del resto del
    /// fichero, y no es un parche para poner el test en verde (24 sep 2026).
    ///
    /// <para>Con 20 era un test que fallaba **por mala suerte** —lo que `CLAUDE.md` define como test mal
    /// escrito—: `steamroller` encadena ~22 veces por cada 200 partidos, o sea ~2,2 en 20, y la
    /// probabilidad de ver **cero** por azar rondaba el **11 %**. Al cambiar el flujo del balón (BC-A,
    /// BI-F) salió ese cero. Medido a 200 partidos antes y después del cambio: **21 → 23**. La tasa no se
    /// movió; sólo cambió qué partidos salen.</para>
    ///
    /// <para>El resto del fichero se queda en 20 a propósito: sus bandas (`ExistingPerksAreUnchanged`)
    /// están calibradas por cada 20 partidos y multiplicarlas por diez sería cambiar lo que miden.</para>
    /// </summary>
    private const int ReachabilityMatches = 200;

    private readonly ITestOutputHelper _output;
    public RecoveryExtraActionTests(ITestOutputHelper output) => _output = output;

    private static MatchResult Play(int index, string? perkId, out int carrierId, int slot = 1)
    {
        var homeRng = RngStreams.Generation(1, index);
        var awayRng = RngStreams.Generation(1, 10_000 + index);
        var home = TeamGenerator.Generate(ref homeRng, Catalog, "home", Race.Human, 50, 1, 4);
        var away = TeamGenerator.Generate(ref awayRng, Catalog, "away", Race.Human, 50, 100001, 4);

        var players = home.Players.ToList();
        if (perkId is not null)
        {
            players[slot] = players[slot] with { Perks = new[] { perkId } };
        }

        carrierId = players[slot].Id;
        var setup = new MatchSetup(home with { Players = players }, away, Referee);
        return Simulator.Run(setup, RngStreams.MatchSeed(1, index), Catalog, new SimConfig(CollectLog: false));
    }

    private static int Activations(MatchResult result, string perkId, int ownerId) =>
        result.Report.PerksSummary
            .Where(s => s.PerkId == perkId && s.OwnerId == ownerId)
            .Sum(s => s.Activations);

    /// <summary>El cargador admite la combinación, y el efecto llega a ejecutarse: el perk se activa.</summary>
    [Fact]
    public void RecoveryWithDetailTackleCanDriveExtraAction()
    {
        var perk = Catalog.Perks.All.Single(p => p.Id == "steamroller");
        Assert.Equal(EventType.Recovery, perk.Trigger);
        Assert.Equal("detail() == 'tackle'", perk.Condition);
        Assert.Equal(EffectType.ExtraAction, perk.Effects.Single().Type);

        int total = 0;
        for (int i = 0; i < ReachabilityMatches; i++)
        {
            total += Activations(Play(i, "steamroller", out int carrierId), "steamroller", carrierId);
        }

        _output.WriteLine($"activaciones de steamroller en {ReachabilityMatches} partidos: {total}");
        Assert.True(total > 0, "RECOVERY(detail='tackle') debe poder activar extraAction");
    }

    /// <summary>
    /// Una RECOVERY que no sea de entrada NO lo dispara. Se comprueba contra el flujo real: las
    /// activaciones nunca superan el número de eventos <c>RECOVERY "tackle"</c> cuyo actor es el portador,
    /// aunque en los mismos partidos haya muchas más recuperaciones de otros tipos.
    /// </summary>
    [Fact]
    public void RecoveriesThatAreNotTacklesDoNotFireIt()
    {
        int activations = 0, tackleRecoveries = 0, otherRecoveries = 0;
        for (int i = 0; i < Matches; i++)
        {
            var result = Play(i, "steamroller", out int carrierId);
            activations += Activations(result, "steamroller", carrierId);
            foreach (var e in result.Events.Where(e => e.Type == EventType.Recovery))
            {
                if (e.Actor != carrierId)
                {
                    continue;
                }

                if (e.Detail == "tackle") { tackleRecoveries++; } else { otherRecoveries++; }
            }
        }

        _output.WriteLine($"RECOVERY del portador: 'tackle' {tackleRecoveries} | de otro tipo {otherRecoveries}");
        _output.WriteLine($"activaciones de steamroller: {activations}");

        Assert.True(otherRecoveries > 0, "debe haber recuperaciones de otro tipo, si no el test no discrimina");
        Assert.True(
            activations <= tackleRecoveries,
            $"steamroller se activó {activations} veces con solo {tackleRecoveries} recuperaciones por entrada");
    }

    /// <summary>
    /// <b>Sin perks, la capacidad RECOVERY→<c>RepeatTackle</c> no hace absolutamente nada</b>, que es la
    /// afirmación de BB-Q Alt 0. Y ésta es la forma de comprobarla que <b>no</b> depende del flujo de
    /// aleatoriedad: una entrada repetida es, por construcción, un <b>segundo</b> <c>TACKLE</c> del mismo
    /// jugador en el <b>mismo tick</b> —<c>MatchEngine.RepeatTackle</c> se ejecuta dentro de la resolución
    /// del primero—. Sin perks eso no puede ocurrir nunca; y si el motor dejara de evaluar TACKLE tampoco
    /// habría entradas, así que se comprueban las dos mitades.
    ///
    /// <para><b>Por qué ya no fija el número exacto de eventos.</b> Fijaba una <b>huella</b> del flujo de
    /// RNG —17 / 13 / 26 / 8 / 16 en su última versión— y hubo que regenerarla <b>tres veces el mismo
    /// día</b> (ADR 0135, pasos 1, 2 y 2b), siempre por el mismo motivo: cualquier cambio de puntería o de
    /// geometría del disparo desplaza el consumo de aleatoriedad y con él todos los partidos, sin que el
    /// comportamiento medido cambie. El propio test dejó escrito que a la cuarta tocaba replantearlo en
    /// serio en vez de regenerarlo. Ésta es la cuarta (ADR 0136, la acción <c>Cross</c>), y esto es el
    /// replanteo: se afirma la <b>regla</b>, que es estable, en vez de la huella, que no lo es.</para>
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void WithNoPerksNobodyEverTacklesTwiceInTheSameTick(int index)
    {
        var result = Play(index, perkId: null, out _);

        var tackles = result.Events.Where(e => e.Type == EventType.Tackle).ToList();
        Assert.True(tackles.Count > 0, $"semilla {index}: sin entradas, este test no comprueba nada");

        var seen = new HashSet<(int Tick, int Actor)>();
        foreach (var tackle in tackles)
        {
            Assert.True(
                seen.Add((tackle.Tick, tackle.Actor)),
                $"semilla {index}: el jugador {tackle.Actor} entró dos veces en el tick {tackle.Tick} sin llevar ningún perk");
        }

        _output.WriteLine($"semilla {index}: {tackles.Count} eventos TACKLE, ninguno repetido en el mismo tick");
    }


    /// <summary>La recursión sigue acotada por <c>_maxDepth</c> (RT-042): el partido termina y el corte es finito.</summary>
    [Fact]
    public void RecursionIsStillBoundedByMaxDepth()
    {
        for (int i = 0; i < Matches; i++)
        {
            var result = Play(i, "steamroller", out _);
            Assert.True(result.Report.Ticks > 0, "el partido debe terminar");
            Assert.True(result.Report.RecursionCuts >= 0);
            Assert.True(result.Report.Tackles < 10_000, "una cadena sin cortar dispararía las entradas");
        }
    }

    /// <summary>
    /// BC-B: un perk cuyo efecto vuelve a publicar su propio disparador (extraAction) no pasa de su límite.
    /// Con el uso consumido después de aplicar los efectos, la llamada anidada no lo veía y el perk se
    /// encadenaba hasta la profundidad máxima: <c>double_shot</c> daba siempre 5 activaciones con límite 1.
    /// </summary>
    /// <para>La fila de <c>steamroller</c> es solo de guarda: con estas plantillas no llegaba a encadenarse
    /// ni con el fallo. Las otras dos fallan con el código anterior (5 activaciones y 6 acciones en un tick).</para>
    [Theory]
    [InlineData("double_shot", 6, EventType.Shot, true)]
    [InlineData("charge", 1, EventType.Tackle, true)]
    [InlineData("steamroller", 1, EventType.Tackle, false)]
    public void APerkThatRetriggersItselfStillRespectsItsLimit(string perkId, int slot, EventType repeated, bool mustActivate)
    {
        var limit = Catalog.Perks.All.Single(p => p.Id == perkId).Limit;
        Assert.NotNull(limit);
        int active = 0;
        for (int i = 0; i < Matches; i++)
        {
            var result = Play(i, perkId, out int carrierId, slot);
            int n = Activations(result, perkId, carrierId);
            Assert.InRange(n, 0, limit!.Times);
            active += n > 0 ? 1 : 0;

            // Lo observable: la acción repetida del portador ocurre como mucho dos veces en un mismo tick
            // (la suya y UNA extra), no hasta la profundidad de recursión.
            int most = result.Events.Where(e => e.Type == repeated && e.Actor == carrierId)
                .GroupBy(e => e.Tick).Select(g => g.Count()).DefaultIfEmpty(0).Max();
            Assert.InRange(most, 0, 2);
        }

        _output.WriteLine($"{perkId}: se activa en {active} de {Matches} partidos, nunca más de {limit!.Times} vez por partido");
        if (mustActivate)
        {
            Assert.True(active > 0, $"{perkId} no se activa en ningún partido: la prueba no demostraría nada");
        }
    }

    /// <summary>
    /// Los cuatro perks que ya usaban RECOVERY <b>siguen comportándose igual</b>: los tres que se activan
    /// se siguen activando, y <c>road_warrior</c> —cuya condición no la cumple una entrada— <b>sigue sin
    /// activarse nunca</b>. Esa última fila es la que de verdad vigila BB-Q Alt 0: si la capacidad nueva
    /// hubiera ensanchado el disparador, <c>road_warrior</c> empezaría a saltar.
    ///
    /// <para><b>Por qué es una banda y ya no un número exacto.</b> Fijaba el total exacto de activaciones
    /// en veinte partidos y hubo que regenerarlo <b>cinco veces</b> —ADR 0121, 0129, 0132/0133, y tres
    /// veces el mismo 23 sep 2026 con los pasos 1, 2 y 2b de la ADR 0135—, siempre por lo mismo: el total
    /// es una <b>huella del flujo de aleatoriedad</b>, y cualquier cambio del motor que mueva dónde se
    /// consume RNG la desplaza sin que estos perks hayan cambiado. El propio test dejó escrito que a la
    /// cuarta tocaba replantearlo en serio. Ésta es la cuarta (ADR 0136, la acción <c>Cross</c>).</para>
    ///
    /// <para><b>El techo es ~2× el valor observado, y eso es deliberado.</b> La primera versión de esta
    /// banda la puso en 40-60 y la revisión independiente encontró el agujero: un encadenamiento <b>×2</b>
    /// (11→22, 19→38, 17→34) cabía dentro, y encadenarse es exactamente el fallo que BC-B encontró —
    /// <c>double_shot</c> pasando de 1 a 5 activaciones por partido—. Con el techo en 25 / 40 / 35 un ×2
    /// se sale y un desplazamiento de semillas no.</para>
    /// </summary>
    [Theory]
    [InlineData("charge", 1, 1, 25)]
    [InlineData("lane_reader", 1, 1, 40)]
    // ADR 0136: road_warrior estuvo clavado en 0 a través de CINCO desplazamientos de semilla, y con el
    // centro pasa a 1 en veinte partidos. Medido que lo causa el centro y no la deriva: con el centro
    // apagado por dato (crossTargetGoalDistanceCells = 0) los cuatro perks reproducen EXACTAMENTE sus
    // valores antiguos (charge 11, lane_reader 19, sweeper_keeper 17, road_warrior 0). Y la vía es real y
    // prevista: un centro que nadie remata deja el balón suelto en el área, alguien lo recupera, y eso es
    // un RECOVERY —el disparador de este perk—. No es el disparador ensanchándose, es que ahora hay
    // recuperaciones donde antes no las había. La banda se abre lo justo para eso.
    [InlineData("road_warrior", 1, 0, 5)]
    // ADR 0139: sweeper_keeper pasa de 17 a 55 activaciones en veinte partidos, y la causa está MEDIDA, no
    // supuesta. El portero es la excepción del juego aéreo —tiene manos, así que un balón alto a su
    // alcance lo ATRAPA en vez de cabecearlo—, y atrapar es un RECOVERY, que es el disparador de este
    // perk. Medido apagando la rama aérea por dato (ball.controlHeightCells = 99): las recuperaciones del
    // portero en veinte partidos pasan de 123 a 37, así que el triple viene de ahí y de ningún sitio más.
    // No es el disparador ensanchándose ni un encadenamiento: son recuperaciones que antes no existían,
    // igual que le pasó a road_warrior con el centro.
    //
    // El techo se mantiene en ~2x lo observado, que es la propiedad que este test defiende: un
    // encadenamiento x2 se sigue saliendo y un desplazamiento de semillas no.
    [InlineData("sweeper_keeper", 0, 1, 110)]

    public void ExistingPerksAreUnchanged(string perkId, int slot, int min, int max)
    {
        int total = 0;
        for (int i = 0; i < Matches; i++)
        {
            total += Activations(Play(i, perkId, out int carrierId, slot), perkId, carrierId);
        }

        _output.WriteLine($"{perkId}: {total} activaciones en {Matches} partidos (banda {min}-{max})");
        Assert.True(
            total >= min && total <= max,
            $"{perkId}: {total} activaciones en {Matches} partidos, fuera de la banda {min}-{max}");
    }
}
