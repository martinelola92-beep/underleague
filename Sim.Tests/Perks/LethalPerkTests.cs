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
    /// El límite estructural, medido y no supuesto (encargo de aniquilación, RF-002b): <b>una build de
    /// violencia del jugador no puede ganar por incomparecencia</b>. La razón es del motor y no del
    /// balance: una lesión sufrida en el partido saca al jugador del campo
    /// (<c>MatchEngine.ResolveInjury</c> -&gt; <c>LeavePitch</c>) y el rival siempre llega sano
    /// (<c>RivalTeamBuilder</c>), así que <c>EffectEngine.IsLethalVictim</c> —que exige estar en el campo
    /// y no estar sano— no encuentra víctima nunca en el equipo rival. Si algún día cambia una de las dos
    /// cosas, este test se pone rojo y hay que remedir la aniquilación antes de seguir.
    /// </summary>
    [Fact]
    public void APlayerViolenceBuildCannotKillAnOpponent()
    {
        var lethalIds = Catalog.Perks.All.Where(p => p.Lethal).Select(p => p.Id).ToList();
        Assert.NotEmpty(lethalIds);

        int rivalDeaths = 0;
        int matches = 0;
        for (ulong seed = 1; seed <= 60; seed++)
        {
            var setup = TestMatches.Reference(Catalog, seed);
            var home = setup.Home.Players.Select(p => Carrying(p, lethalIds, Catalog)).ToList();
            setup = setup with { Home = setup.Home with { Players = home } };

            var result = Simulator.Run(setup, seed, Catalog, new SimConfig(CollectLog: false));
            matches++;
            rivalDeaths += result.Events.Count(e => e.Type == EventType.Death && e.Team == 1);
        }

        Assert.Equal(60, matches);
        Assert.Equal(0, rivalDeaths);
    }

    /// <summary>
    /// Y el reverso: el mismo perk letal sí mata a quien <b>salta al campo</b> herido, que es la única
    /// ventana que el motor deja (RF-093: un jugador sano nunca muere).
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
        var wounded = setup.Home.Players[1];
        var home = setup.Home.Players
            .Select(p => p.Id == wounded.Id ? p with { PhysicalState = PhysicalState.MinorInjury } : p)
            .ToList();
        setup = setup with
        {
            Home = setup.Home with { Players = home },
            Away = setup.Away with { Players = away },
        };

        var result = Simulator.Run(setup, 77, Catalog, new SimConfig(CollectLog: false));
        var death = Assert.Single(result.Events, e => e.Type == EventType.Death);

        Assert.Equal(wounded.Id, death.Actor);
        Assert.Equal("perk:" + perk.Id, death.Detail);

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
