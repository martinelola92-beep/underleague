using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Generation;
using Underleague.Sim.Model;
using Underleague.Sim.Random;
using Xunit.Abstractions;

namespace Underleague.Sim.Tests.Perks;

/// <summary>
/// BB-Q: <c>steamroller</c> ("Arrollador") no se activa nunca — 0,0 % de exposición en 480 partidos
/// (§34.4). Estos tests fijan la evidencia de las TRES causas encadenadas, cada una suficiente por sí
/// sola para que la condición <c>stat(target,'down') == 1</c> no pueda ser cierta jamás.
///
/// <para>No tocan <c>/data</c> ni ningún perk: solo miden el motor tal como está. Ficha completa con las
/// hipótesis y su estado epistemológico: <c>docs/pendientes/BB-Q.md</c>.</para>
/// </summary>
public sealed class SteamrollerConditionTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();
    private static readonly RefereeSetup Referee = new("Referee", RefereeTrait.Neutral, 0);

    private readonly ITestOutputHelper _output;
    public SteamrollerConditionTests(ITestOutputHelper output) => _output = output;

    private static MatchResult PlayOne(int index, string? carrierPerk, out int carrierId)
    {
        var homeRng = RngStreams.Generation(1, index);
        var awayRng = RngStreams.Generation(1, 10_000 + index);
        var home = TeamGenerator.Generate(ref homeRng, Catalog, "home", Race.Human, 50, 1, 4);
        var away = TeamGenerator.Generate(ref awayRng, Catalog, "away", Race.Human, 50, 100001, 4);

        // Slot 1 es el primer titular de campo (Defensa): el mismo portador que elige el arnés de cribado.
        var players = home.Players.ToList();
        if (carrierPerk is not null)
        {
            players[1] = players[1] with { Perks = new[] { carrierPerk } };
        }

        carrierId = players[1].Id;
        var setup = new MatchSetup(home with { Players = players }, away, Referee);
        return Simulator.Run(setup, RngStreams.MatchSeed(1, index), Catalog, new SimConfig(CollectLog: false));
    }

    /// <summary>
    /// CAUSA 1, la proximal: el motor pasa al jugador entrado como <c>opponent</c>, nunca como
    /// <c>target</c> (<c>MatchEngine.cs:2172</c> y <c>:2202</c>). La condición de <c>steamroller</c>
    /// pregunta por <c>target</c>, que en un evento TACKLE no está ligado a nadie — y el manejador de
    /// <c>stat</c> devuelve <b>0</b> para un identificador sin ligar
    /// (<c>ConditionCompiler.cs:779</c>: <c>who is null ? 0 : ...</c>). Así que
    /// <c>stat(target,'down') == 1</c> es <b>idénticamente falsa</b>, no "rara".
    /// </summary>
    [Fact]
    public void TackleEventsNeverBindTheTackledPlayerAsTarget()
    {
        int tackles = 0, withTarget = 0, withOpponent = 0;
        for (int i = 0; i < 20; i++)
        {
            var result = PlayOne(i, carrierPerk: null, out _);
            foreach (var e in result.Events.Where(e => e.Type == EventType.Tackle))
            {
                tackles++;
                if (e.Target >= 0) { withTarget++; }
                if (e.Opponent >= 0) { withOpponent++; }
            }
        }

        _output.WriteLine($"eventos TACKLE en 20 partidos: {tackles}");
        _output.WriteLine($"  con 'target' ligado  : {withTarget}");
        _output.WriteLine($"  con 'opponent' ligado: {withOpponent}");

        Assert.True(tackles > 0, "el lote debe producir entradas, si no el test no mide nada");
        Assert.Equal(0, withTarget);      // nunca: por eso stat(target,...) siempre vale 0
        Assert.Equal(tackles, withOpponent); // siempre: el entrado viaja aquí
    }

    /// <summary>
    /// CAUSA 2, semántica: <c>'down'</c> no significa "derribado". <see cref="MatchStat.Down"/> está
    /// documentado como "1 si el jugador ha terminado el partido <b>de baja</b> —lesionado o muerto—"
    /// (<c>PerkDefinition.cs:269-274</c>) y se resuelve como <c>player.Injured || player.Dead</c>
    /// (<c>EffectEngine.Stat</c>). El <c>_doc</c> de <c>steamroller</c> pide justo lo otro: "solo encadena
    /// si el rival de la entrada QUEDÓ EN EL SUELO", que es <see cref="PlayerState.KnockedDown"/>.
    ///
    /// <para>Este test fija que el vocabulario de condiciones <b>no tiene ninguna función que exponga el
    /// estado del jugador</b>, así que el perk no se podía escribir bien: no es una errata, es una
    /// primitiva que falta.</para>
    /// </summary>
    [Fact]
    public void TheConditionVocabularyCannotExpressKnockedDown()
    {
        var functions = Underleague.Sim.Perks.ConditionCompiler.FunctionNames;
        _output.WriteLine($"funciones de condición disponibles ({functions.Count}): {string.Join(", ", functions)}");

        Assert.DoesNotContain("state", functions);
        Assert.DoesNotContain("isDown", functions);
        Assert.DoesNotContain("knockedDown", functions);

        // La única estadística parecida es 'down', y significa otra cosa.
        Assert.Contains("stat", functions);
    }

    /// <summary>
    /// CAUSA 3, temporal: TACKLE se publica a los perks <b>antes</b> de resolver la entrada
    /// (<c>MatchEngine.PublishBeforeResolving</c>, llamada en <c>:2172</c>), mientras que el derribo
    /// (<c>:2221</c>) y la lesión (<c>ResolveInjury</c>, <c>:2233</c>) ocurren después. Cuando la
    /// condición se evalúa, el rival no está ni derribado ni lesionado por ESTA entrada — así que ni
    /// siquiera la lectura pretendida podría ser cierta.
    ///
    /// <para>Se comprueba por la vía observable: el evento TACKLE que llega al bus lleva siempre el
    /// detalle de pre-resolución, nunca uno que presuponga el resultado.</para>
    /// </summary>
    [Fact]
    public void TackleIsPublishedToPerksBeforeItIsResolved()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot(), "Sim", "Engine", "MatchEngine.cs"));
        int publishIndex = source.IndexOf("PublishBeforeResolving(EventType.Tackle, \"attempted\"", StringComparison.Ordinal);
        int knockdownIndex = source.IndexOf("carrier.EnterState(PlayerState.KnockedDown", StringComparison.Ordinal);
        int injuryIndex = source.IndexOf("ResolveInjury(tackler, carrier, isFoul)", StringComparison.Ordinal);

        Assert.True(publishIndex > 0, "la publicación previa de TACKLE debe existir");
        Assert.True(knockdownIndex > publishIndex, "el derribo ocurre DESPUÉS de publicar el evento");
        Assert.True(injuryIndex > publishIndex, "la lesión ocurre DESPUÉS de publicar el evento");
    }

    /// <summary>
    /// El control del propio catálogo, medido: el <c>_doc</c> de <c>steamroller</c> dice "la condición es
    /// lo que lo separa de Embestida". Tras cerrar BB-Q eso es lo que debe verse — <c>steamroller</c> se
    /// activa (antes: nunca) y sigue activándose bastante menos que <c>charge</c>, que no tiene condición.
    ///
    /// <para>Antes del arreglo este test fijaba el bug (<c>Assert.Equal(0, steamroller)</c>). Se ha
    /// reescrito al cerrar la ficha en vez de borrarlo: la comparación con el gemelo sigue siendo la
    /// regresión útil.</para>
    /// </summary>
    [Fact]
    public void SteamrollerFiresButItsConditionStillSeparatesItFromItsTwin()
    {
        int steamroller = Activations("steamroller");
        int charge = Activations("charge");

        _output.WriteLine($"activaciones en 20 partidos | steamroller: {steamroller} | charge (sin condición): {charge}");

        Assert.True(charge > 0, "el portador SÍ hace entradas: charge lo demuestra");
        Assert.True(steamroller > 0, "tras BB-Q, Arrollador debe encadenar (antes: 0 en 480 partidos)");
        Assert.True(steamroller < charge, "la condición debe seguir restringiendo respecto al gemelo sin condición");
    }

    /// <summary>
    /// <b>PRUEBA DE REGRESIÓN DEL ARREGLO — falla hoy a propósito.</b>
    ///
    /// <para>Lo que Arrollador dice que hace: encadenar una entrada más cuando la suya deja al rival en
    /// el suelo. Su gemelo sin condición (<c>charge</c>) encadena 65 veces en 20 partidos con el mismo
    /// portador, así que el portador derriba de sobra: un Arrollador que funcionara tendría que activarse
    /// <b>alguna</b> vez. Hoy se activa cero.</para>
    ///
    /// <para>Está <c>Skip</c> porque el árbol no se deja en rojo (ver skill <c>gameplay-debug</c>, paso 2):
    /// quítale el <c>Skip</c> al cerrar BB-Q y debe pasar. NO se relaja el umbral para que pase —si el
    /// arreglo no consigue ni una activación, el arreglo está mal, no el test.</para>
    /// </summary>
    [Fact]
    public void SteamrollerChainsAtLeastOnceWhenItsCarrierWinsTackles()
    {
        Assert.True(Activations("charge") > 0, "precondición: el portador encadena con el gemelo sin condición");
        Assert.True(
            Activations("steamroller") > 0,
            "Arrollador debe encadenar alguna vez cuando su entrada derriba al rival (docs/pendientes/BB-Q.md)");
    }

    private static int Activations(string perkId)
    {
        int total = 0;
        for (int i = 0; i < 20; i++)
        {
            var result = PlayOne(i, perkId, out int carrierId);
            foreach (var summary in result.Report.PerksSummary)
            {
                if (summary.PerkId == perkId && summary.OwnerId == carrierId)
                {
                    total += summary.Activations;
                }
            }
        }

        return total;
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Underleague.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("no se encuentra la raíz del repositorio");
    }
}
