using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Model;
using Underleague.Sim.Perks;
using Underleague.Sim.Run.Systems.Rivals;

namespace Underleague.Sim.Tests.Perks;

/// <summary>
/// Los perks letales del catálogo (RF-093 vía 2, RF-013, ADR 0046): que existen, que solo matan a quien
/// ya no está sano, que el ojeo los destaca, que ningún rival del acto 1 lleva ninguno, y el límite
/// estructural que hace que ganar por aniquilación no sea hoy una vía.
/// </summary>
public sealed class LethalPerkTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();
    private static readonly RivalCatalog Rivals = RivalLoader.FromJson(TestData.LoadAllFiles());

    /// <summary>ADR 0046: entre tres y cinco perks letales, y ninguno puede ser inalcanzable para el jugador.</summary>
    [Fact]
    public void TheCatalogHasBetweenThreeAndFiveLethalPerks()
    {
        var lethal = Catalog.Perks.All.Where(p => p.Lethal).ToList();

        Assert.InRange(lethal.Count, 3, 5);
        foreach (var perk in lethal)
        {
            // Universales: el jugador de cualquier raza puede llegar a llevarlos (ADR 0023).
            Assert.Null(perk.Race);

            // RF-069: matar es romper una regla, no rellenar.
            Assert.Equal(PerkKind.RuleBreaker, perk.Kind);

            // RF-013: si se anuncia como letal, tiene que poder alcanzar a un rival (lo exige el cargador).
            Assert.NotEmpty(perk.Effects);
        }

        // Repartidos por canal (ADR 0035): no todos muerden sobre la misma probabilidad.
        var channels = lethal
            .SelectMany(p => p.Effects)
            .Where(e => e.Type == EffectType.ModifyProbability)
            .Select(e => e.Probability)
            .Distinct()
            .ToList();
        Assert.True(channels.Count >= 3, $"los letales usan {channels.Count} canales distintos y deberían usar al menos 3");
    }

    /// <summary>ADR 0046: el acto 1 es el taller. Ningún rival suyo mata.</summary>
    [Fact]
    public void NoAct1RivalCarriesALethalPerk()
    {
        foreach (string id in Rivals.OfAct(1))
        {
            var team = RivalTeamBuilder.Build(Rivals.Find(id)!, Catalog);
            Assert.Empty(Scouting.LethalPerks(team, Catalog));
        }
    }

    /// <summary>
    /// ADR 0046: escasos y tardíos, pero de verdad presentes en los actos 2 y 3, y el informe de ojeo
    /// (RF-013) los destaca sin que haya que jugar el partido.
    /// </summary>
    [Fact]
    public void LateRivalsCarryLethalPerksAndScoutingShowsThem()
    {
        foreach (int act in new[] { 2, 3 })
        {
            int lethalTeams = 0;
            foreach (string id in Rivals.OfAct(act))
            {
                var team = RivalTeamBuilder.Build(Rivals.Find(id)!, Catalog);
                var threats = Scouting.LethalPerks(team, Catalog);
                if (threats.Count > 0)
                {
                    lethalTeams++;
                    foreach (var threat in threats)
                    {
                        Assert.True(Catalog.Perks.Get(threat.PerkId).Lethal);
                        Assert.NotEmpty(threat.PlayerName);
                    }
                }
            }

            Assert.InRange(lethalTeams, 1, Rivals.OfAct(act).Count - 1);
        }
    }

    /// <summary>
    /// <b>La asimetría que la ADR 0047 iba a rodear ya no existe</b> (ADR 0048): un perk letal propio SÍ
    /// mata rivales. Antes no podía: <c>IsLethalVictim</c> exigía que la víctima no estuviera sana, una
    /// lesión sufrida en el partido saca al lesionado del campo y los rivales se generan siempre sanos
    /// (<c>RivalTeamBuilder</c>), así que no había víctima posible y la build de violencia se quedaba sin
    /// culminación. Medido entonces: 0 muertes rivales en 60 partidos.
    ///
    /// <para>Y el riesgo que la ADR 0046 manda vigilar sigue vigilado: matar rivales baja su plantilla y
    /// por debajo de cinco pierden por incomparecencia (RF-002b). Ganar así tiene que ser posible pero
    /// lento y caro, <b>nunca la vía eficiente</b>, así que el test acota las muertes por arriba: si una
    /// build de violencia extrema empieza a segar equipos enteros, hay que encarecer la letalidad.</para>
    /// </summary>
    [Fact]
    public void APlayerViolenceBuildNowKillsOpponentsButCannotMowThemDown()
    {
        var lethalIds = Catalog.Perks.All.Where(p => p.Lethal).Select(p => p.Id).ToList();
        Assert.NotEmpty(lethalIds);

        int rivalDeaths = 0;
        int matches = 0;
        int worst = 0;
        for (ulong seed = 1; seed <= 60; seed++)
        {
            // El extremo ALCANZABLE de una build de violencia: UN legendario con sus cinco slots llenos
            // de letales (RF-023). Repartir los cuatro letales entre los siete titulares no lo es —haría
            // falta que el pool regalara veintiocho copias— y con esa build sí se llega a segar equipos,
            // que es una medición interesante pero no una configuración que el juego pueda producir.
            var setup = TestMatches.Reference(Catalog, seed);
            var butcher = setup.Home.Players[6];
            var home = setup.Home.Players
                .Select(p => p.Id == butcher.Id ? Carrying(p, lethalIds, Catalog) : p)
                .ToList();
            setup = setup with { Home = setup.Home with { Players = home } };

            var result = Simulator.Run(setup, seed, Catalog, new SimConfig(CollectLog: false));
            matches++;
            int deaths = result.Events.Count(e => e.Type == EventType.Death && e.Team == 1);
            rivalDeaths += deaths;
            worst = Math.Max(worst, deaths);
        }

        Assert.Equal(60, matches);
        Assert.True(rivalDeaths > 0, "la build de violencia extrema no mató a nadie: la ADR 0048 no se ha aplicado");

        // El techo: el mínimo de RF-002b son cinco disponibles de diez, así que segar un equipo entero en
        // un partido pide seis muertos. Con los cuatro letales repartidos entre los SIETE titulares —una
        // build imposible de montar en una run real— no se llega, y por tanto la aniquilación no es la
        // vía eficiente de ganar (riesgo que la ADR 0046 manda vigilar).
        Assert.True(
            worst < 6,
            $"el peor partido dejó {worst} rivales muertos: con seis, un equipo de diez pierde por incomparecencia (RF-002b)");
    }

    /// <summary>
    /// El mismo perk letal mata a quien <b>salta al campo</b> herido: sigue siendo el caso más probable
    /// —el estado multiplica la tirada— aunque desde la ADR 0048 ya no sea el único posible. Y estaba
    /// anunciado antes de jugar (RF-013).
    /// </summary>
    [Fact]
    public void TheSameLethalPerkKillsSomeoneWhoTookTheFieldAlreadyHurt()
    {
        var perk = Catalog.Perks.Get("skullsplitter");
        Assert.True(perk.Lethal);

        var setup = TestMatches.Reference(Catalog, 77);
        var carrier = setup.Away.Players[0];
        var away = setup.Away.Players
            .Select(p => p.Id == carrier.Id ? WithTags(p) with { Rarity = Rarity.Legendary, Perks = new[] { perk.Id } } : p)
            .ToList();
        // El tocado tiene que estar al ALCANCE del portador: la letalidad cae con la distancia de
        // emparejamiento y a cuatro casillas ya vale cero, así que se elige al titular local más cercano
        // al portador (ADR 0048). Que la elección exista es justamente la palanca de colocación.
        var carrierCell = setup.Away.Lineup.Slots.First(s => s.PlayerId == carrier.Id).HomeCell;
        int nearestId = setup.Home.Lineup.Slots
            .OrderBy(s => Lethality.Matchup(s.HomeCell, carrierCell))
            .ThenBy(s => s.PlayerId)
            .First()
            .PlayerId;
        var wounded = setup.Home.Players.First(p => p.Id == nearestId);
        var home = setup.Home.Players
            .Select(p => p.Id == wounded.Id ? p with { PhysicalState = PhysicalState.MinorInjury } : p)
            .ToList();
        setup = setup with
        {
            Home = setup.Home with { Players = home },
            Away = setup.Away with { Players = away },
        };

        // Ya no es una certeza sino una tirada (ADR 0048), así que se mide sobre una tanda: el tocado
        // muere y muere MÁS que cualquier compañero sano, que es lo que queda de la regla vieja de
        // RF-093 —el estado dejó de ser una puerta y pasó a ser un multiplicador— y lo que hace que
        // sentarlo sea una jugada y no una superstición.
        var deathsByPlayer = new Dictionary<int, int>();
        for (ulong seed = 60; seed < 120; seed++)
        {
            var result = Simulator.Run(setup, seed, Catalog, new SimConfig(CollectLog: false));
            foreach (var e in result.Events.Where(e => e.Type == EventType.Death && e.Detail == "perk:" + perk.Id))
            {
                deathsByPlayer[e.Actor] = deathsByPlayer.TryGetValue(e.Actor, out int n) ? n + 1 : 1;
            }
        }

        Assert.True(
            deathsByPlayer.TryGetValue(wounded.Id, out int woundedDeaths) && woundedDeaths > 0,
            "el jugador que saltó al campo tocado no murió ni una vez en sesenta partidos");
        foreach (var (id, count) in deathsByPlayer)
        {
            Assert.True(
                id == wounded.Id || count < woundedDeaths,
                $"el jugador sano {id} murió {count} veces y el tocado {woundedDeaths}: el estado tiene que pesar");
        }

        // Y estaba anunciado antes de jugar (RF-013).
        Assert.Contains(Scouting.LethalPerks(setup.Away, Catalog), t => t.PerkId == perk.Id);
    }

    /// <summary>Las dos etiquetas que exigen los letales del catálogo, para que el portador pueda llevarlos.</summary>
    private static PlayerDefinition WithTags(PlayerDefinition player)
    {
        var tags = new List<string>(player.Tags);
        foreach (string tag in new[] { "Dirty", "Aggressive" })
        {
            if (!tags.Contains(tag))
            {
                tags.Add(tag);
            }
        }

        return player with { Tags = tags };
    }

    private static bool Eligible(PlayerDefinition player, PerkDefinition perk)
    {
        for (int i = 0; i < perk.TagsRequired.Count; i++)
        {
            if (!player.HasTag(perk.TagsRequired[i]))
            {
                return false;
            }
        }

        return perk.PositionOnly is null || perk.PositionOnly == player.Position;
    }

    /// <summary>El jugador con todos los letales que puede llevar: la build de violencia llevada al extremo.</summary>
    private static PlayerDefinition Carrying(PlayerDefinition player, IReadOnlyList<string> lethalIds, Catalog catalog)
    {
        var tags = new List<string>(WithTags(player).Tags);
        foreach (string tag in new[] { "Dirty", "Aggressive" })
        {
            if (!tags.Contains(tag))
            {
                tags.Add(tag);
            }
        }

        // Rareza legendaria: cinco slots (RF-023), los que hacen falta para llevar todos los letales a la
        // vez. Es el extremo de la build de violencia, no una plantilla realista.
        var withTags = player with { Tags = tags, Rarity = Rarity.Legendary };
        var perks = new List<string>();
        for (int i = 0; i < lethalIds.Count; i++)
        {
            if (Eligible(withTags, catalog.Perks.Get(lethalIds[i])))
            {
                perks.Add(lethalIds[i]);
            }
        }

        perks.Sort(StringComparer.Ordinal);
        return withTags with { Perks = perks };
    }
}
