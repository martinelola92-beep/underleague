using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Generation;
using Underleague.Sim.Model;
using Underleague.Sim.Random;
using Xunit.Abstractions;

namespace Underleague.Sim.Tests.Analysis;

/// <summary>
/// Línea base de conducta del estilo <b>Muro</b> (decisión del revisor, 19 sep 2026), ANTES de escribir
/// ningún perk. El encargo es explícito: el objetivo de `hold_the_line` no es "Bulwark tiene otro bonus
/// numérico" sino "cuando veo un Muro en partida, entiendo que está plantado ahí por algo". Eso exige
/// saber primero qué hace hoy un Muro, para poder decir después si el perk cambió algo de verdad.
///
/// <para>No toca nada: solo lee <see cref="MatchTrace"/> (RT-098, ya verificado como de solo lectura por
/// RT-024). Sin perks, sin objetos: se compara el estilo contra los otros cuatro en los MISMOS partidos,
/// así que cualquier diferencia es del estilo, no del emparejamiento.</para>
///
/// <para>Se juega un round-robin de las cinco razas para que los cinco estilos tengan volumen: con
/// plantillas de una sola raza, el estilo dominante se come la muestra y los demás no llegan a nada.</para>
/// </summary>
[Trait("Category", "Diagnostic")]
public sealed class BulwarkBehaviourBaselineTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();
    private static readonly RefereeSetup Referee = new("Referee", RefereeTrait.Neutral, 0);
    private const int Rounds = 12;

    private readonly ITestOutputHelper _output;
    public BulwarkBehaviourBaselineTests(ITestOutputHelper output) => _output = output;

    private sealed class Tally
    {
        public long Ticks;
        public long Players;
        public double DistanceCentiCells;
        public double DistanceToOwnGoal;
        public readonly Dictionary<PlayerAction, long> Actions = new();
    }

    [Fact(Skip = "Diagnóstico bajo demanda: ~40 s. Quita el Skip para remedir la línea base de conducta por estilo.")]
    public void WhatDoesABulwarkActuallyDoOnThePitch()
    {
        var byStyle = new Dictionary<StyleTag, Tally>();

        // Estilo FORZADO sobre plantillas idénticas: sin esto la comparación está confundida con la raza
        // —un Muro es casi siempre un enano, y el enano trae su propio sesgo de atributos—. Con la misma
        // raza, la misma calidad y las mismas semillas en los cinco brazos, lo único que cambia es la
        // etiqueta, así que la diferencia ES del estilo.
        var styles = new[] { StyleTag.Neutral, StyleTag.Brute, StyleTag.Fine, StyleTag.Bulwark, StyleTag.Cold };

        int match = 0;
        foreach (var style in styles)
        {
            var forced = new Dictionary<int, StyleTag>();
            for (int slot = 0; slot < 9; slot++)
            {
                forced[slot] = style;
            }

            for (int round = 0; round < Rounds; round++)
            {
                var homeRng = RngStreams.Generation(1, round);
                var awayRng = RngStreams.Generation(1, 50_000 + round);
                // Solo el equipo de casa lleva el estilo forzado; el rival es siempre el mismo Neutral,
                // así que los cinco brazos juegan contra el MISMO oponente.
                var home = TeamGenerator.Generate(ref homeRng, Catalog, "home", Race.Human, 50, 1, 4, styleBySlot: forced);
                var neutralRival = new Dictionary<int, StyleTag>();
                for (int slot = 0; slot < 9; slot++)
                {
                    neutralRival[slot] = StyleTag.Neutral;
                }

                var away = TeamGenerator.Generate(ref awayRng, Catalog, "away", Race.Human, 50, 100001, 4, styleBySlot: neutralRival);

                var result = Simulator.Run(
                    new MatchSetup(home, away, Referee), RngStreams.MatchSeed(1, round), Catalog,
                    new SimConfig(CollectLog: false, Trace: true));

                // Solo se cuentan los jugadores del equipo de casa: los de fuera son el control Neutral.
                var styleById = new Dictionary<int, StyleTag>();
                foreach (var player in home.Players)
                {
                    styleById[player.Id] = style;
                }

                Accumulate(result.Trace, styleById, byStyle);
                match++;
            }
        }

        _output.WriteLine($"{match} partidos · estilo FORZADO sobre plantilla Human idéntica · rival siempre Neutral · sin perks ni objetos");
        _output.WriteLine("");

        var actions = byStyle.Values.SelectMany(t => t.Actions.Keys).Distinct()
            .OrderByDescending(a => byStyle.Values.Sum(t => t.Actions.GetValueOrDefault(a))).ToList();

        _output.WriteLine($"{"estilo",-9}{"jugadores",10}{"ticks",10}  " + string.Join("", actions.Select(a => $"{a,14}")));
        foreach (var style in byStyle.Keys.OrderBy(s => s.ToString(), StringComparer.Ordinal))
        {
            var t = byStyle[style];
            long decided = t.Actions.Values.Sum();
            string cells = string.Join("", actions.Select(a =>
                $"{(decided == 0 ? 0 : 100.0 * t.Actions.GetValueOrDefault(a) / decided),13:F1}%"));
            _output.WriteLine($"{style,-9}{t.Players,10}{t.Ticks,10}  {cells}");
        }

        _output.WriteLine("");
        _output.WriteLine($"{"estilo",-9}{"casillas recorridas/partido",30}{"distancia media a su portería",32}");
        foreach (var style in byStyle.Keys.OrderBy(s => s.ToString(), StringComparer.Ordinal))
        {
            var t = byStyle[style];
            double n = Math.Max(1, t.Players);
            _output.WriteLine($"{style,-9}{t.DistanceCentiCells / 100.0 / n,30:F2}{t.DistanceToOwnGoal / Math.Max(1, t.Ticks),32:F2}");
        }

        // ¿Llega el sesgo de atributos del estilo al jugador? Si no llegara, la conducta plana de arriba
        // tendría una explicación mucho más simple.
        _output.WriteLine("");
        _output.WriteLine($"{"estilo",-9}{"fuerza",9}{"velocidad",11}{"técnica",10}{"resistencia",13}{"correa",9}");
        foreach (var style in styles)
        {
            var forced = new Dictionary<int, StyleTag>();
            for (int slot = 0; slot < 9; slot++)
            {
                forced[slot] = style;
            }

            double st = 0, sp = 0, te = 0, sa = 0, le = 0;
            int n = 0;
            for (int round = 0; round < Rounds; round++)
            {
                var rng = RngStreams.Generation(1, round);
                var team = TeamGenerator.Generate(ref rng, Catalog, "t", Race.Human, 50, 1, 4, styleBySlot: forced);
                foreach (var player in team.Players)
                {
                    st += player.Attributes.Strength;
                    sp += player.Attributes.Speed;
                    te += player.Attributes.Technique;
                    sa += player.Attributes.Stamina;
                    le += player.Attributes.Leash;
                    n++;
                }
            }

            _output.WriteLine($"{style,-9}{st / n,9:F2}{sp / n,11:F2}{te / n,10:F2}{sa / n,13:F2}{le / n,9:F2}");
        }

        Assert.NotEmpty(byStyle);
    }

    private static void Accumulate(
        MatchTrace? trace, IReadOnlyDictionary<int, StyleTag> styleById, Dictionary<StyleTag, Tally> byStyle)
    {
        if (trace is null)
        {
            return;
        }

        for (int slot = 0; slot < trace.Players.Count; slot++)
        {
            if (!styleById.TryGetValue(trace.Players[slot].Id, out var style))
            {
                continue;
            }

            if (!byStyle.TryGetValue(style, out var tally))
            {
                tally = new Tally();
                byStyle[style] = tally;
            }

            tally.Players++;
            var previous = trace.PositionAt(0, slot);
            for (int frame = 0; frame < trace.FrameCount; frame++)
            {
                if (!trace.OnPitchAt(frame, slot))
                {
                    continue;
                }

                tally.Ticks++;
                var position = trace.PositionAt(frame, slot);
                tally.DistanceCentiCells += Vec2.Distance(previous, position) * 100.0;
                previous = position;

                int team = trace.Players[slot].Team;
                double ownGoalX = team == 0 ? 0 : Pitch.Columns - 1;
                tally.DistanceToOwnGoal += Math.Abs(position.X - ownGoalX);

                if (trace.ActionAt(frame, slot) is { } action)
                {
                    tally.Actions[action] = tally.Actions.GetValueOrDefault(action) + 1;
                }
            }
        }
    }
}
