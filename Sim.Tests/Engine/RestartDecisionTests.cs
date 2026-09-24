using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Model;

namespace Underleague.Sim.Tests.Engine;

/// <summary>
/// ADR 0143 — <b>el balón parado es una jugada, no «el balón en los pies y a jugar»</b>.
///
/// <para>El motor ya tenía las seis reanudaciones, pero en cinco de ellas el sacador recibía el balón y
/// decidía con la tabla de siempre: de un saque de banda se podía rematar y de un córner se podía chutar a
/// puerta. Y en el penalti seguían dentro del área los doce jugadores.</para>
///
/// <para>Estos tests fijan las reglas nuevas sobre partidos reales, no sobre escenarios montados: son
/// afirmaciones del tipo «esto <b>nunca</b> pasa», que es justo lo que un lote grande puede demostrar y un
/// escenario a mano no.</para>
/// </summary>
public sealed class RestartDecisionTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();

    private const int Matches = 200;

    /// <summary>
    /// <b>De un saque de banda y de un córner no se dispara a puerta.</b> Es la regla del fútbol y, antes
    /// de esto, el motor la rompía siempre que la utilidad veía portería: el sacador era un portador
    /// cualquiera con el balón en un sitio raro.
    /// </summary>
    [Fact]
    public void DeUnSaqueDeBandaODeUnCornerNoSeDisparaAPuerta()
    {
        int throwIns = 0;
        int corners = 0;

        for (ulong seed = 1; seed <= Matches; seed++)
        {
            var result = Simulator.Run(
                TestMatches.Reference(Catalog, seed), seed, Catalog,
                SimConfig.Default with { CollectLog = true });

            for (int i = 0; i < result.Events.Count; i++)
            {
                var e = result.Events[i];
                if (e.Type != EventType.Recovery || e.Detail is not ("throwIn" or "corner"))
                {
                    continue;
                }

                if (e.Detail == "throwIn")
                {
                    throwIns++;
                }
                else
                {
                    corners++;
                }

                // El sacador decide en el MISMO tick en que recibe el balón, así que un disparo suyo
                // saldría aquí mismo.
                for (int j = i + 1; j < result.Events.Count && result.Events[j].Tick == e.Tick; j++)
                {
                    var next = result.Events[j];
                    bool shotByTaker = next.Type is EventType.Shot or EventType.Goal && next.Actor == e.Actor;
                    Assert.False(shotByTaker, $"semilla {seed}: se disparó directamente de un '{e.Detail}'");
                }
            }
        }

        Assert.True(throwIns > 0, "ningún saque de banda en la muestra: el test no cubre nada");
        Assert.True(corners + throwIns > 0, "ninguna reanudación de banda ni de córner en la muestra");
    }

    /// <summary>
    /// <b>En el penalti, dentro del área sólo el lanzador y el portero.</b> Es RF-054 y es lo que hace que
    /// un penalti se parezca a un penalti en vez de a un tiro con doce personas alrededor.
    /// </summary>
    [Fact]
    public void EnElPenaltiSoloLanzadorYPorteroDentroDelArea()
    {
        int framesChecked = 0;

        for (ulong seed = 1; seed <= 400 && framesChecked == 0; seed++)
        {
            var trace = Simulator.Run(
                TestMatches.Reference(Catalog, seed), seed, Catalog,
                SimConfig.Default with { Trace = true }).Trace!;

            // El penalti se reconoce por la FASE del partido, que es lo que el motor marca de verdad: no
            // hay evento propio, y buscarlo por el tiro sería buscar el final en vez de la situación.
            for (int frame = 0; frame < trace.FrameCount; frame++)
            {
                if (trace.PhaseAt(frame) != MatchPhase.Penalty)
                {
                    continue;
                }

                framesChecked++;

                // El área que se vacía es la que se defiende, y el que la defiende es el portero que está
                // dentro. Se cuenta por área, que es lo que la regla dice.
                for (int team = 0; team < 2; team++)
                {
                    int inside = 0;
                    for (int p = 0; p < trace.Players.Count; p++)
                    {
                        if (trace.OnPitchAt(frame, p) && Pitch.IsInArea(trace.PositionAt(frame, p), team))
                        {
                            inside++;
                        }
                    }

                    Assert.True(
                        inside <= 2,
                        $"semilla {seed}, fotograma {frame}: {inside} jugadores dentro del área del equipo {team} durante un penalti");
                }
            }
        }

        Assert.True(framesChecked > 0, "ningún penalti en cuatrocientos partidos: el test no cubre nada");
    }

    private static int IndexOf(MatchTrace trace, int playerId)
    {
        for (int i = 0; i < trace.Players.Count; i++)
        {
            if (trace.Players[i].Id == playerId)
            {
                return i;
            }
        }

        return 0;
    }
}
