using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Generation;
using Underleague.Sim.Model;
using Underleague.Sim.Random;
using Xunit.Abstractions;

namespace Underleague.Sim.Tests.Balance;

/// <summary>
/// El experimento de instrumentación de §21.3 (encargo del 19 sep 2026): conecta <c>SimConfig.DumpUtility</c>
/// (RT-098, ya existente en el motor — no se crea instrumentación nueva) al caso de <c>cannon</c> para
/// distinguir, dentro de la ventana de distancia (8,11] casillas donde el bono debería importar, entre
/// las cuatro hipótesis A-D del encargo. Estrictamente diagnóstico: no toca ScreeningRunner, ni ningún
/// umbral, ni /data.
/// </summary>
public sealed class CannonUtilityDumpTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();
    private const int Quality = 50;
    private const int Level = 4;
    private const int BaseRangeCells = 8;
    private const int EffectiveRangeCells = 11;
    private static readonly RefereeSetup Referee = new("Referee", RefereeTrait.Neutral, 0);

    private readonly ITestOutputHelper _output;
    public CannonUtilityDumpTests(ITestOutputHelper output) => _output = output;

    private sealed record WindowSample(
        int Roster, int Tick, int CarrierId,
        UtilityRow? ArmedShoot, PlayerAction ArmedChosen,
        UtilityRow? ControlShoot, PlayerAction ControlChosen);

    private static int FindByPosition(TeamSetup team, Position position)
    {
        for (int i = 0; i < team.Players.Count; i++)
        {
            if (team.Players[i].Position == position)
            {
                return i;
            }
        }

        return -1;
    }

    [Fact]
    public void CompareShootUtilityInsideTheEffectiveWindowArmedVsControl()
    {
        // CannonDribblingStateDiagnosticTests ya demostró que el portador REAL de cannon (Defensa, vía
        // FindEligibleCarrierSlot) nunca entra en Dribbling en 20 partidos — Shoot nunca es ni siquiera
        // una acción legal para él, así que no hay nada que volcar. Se usa un Delantero aquí a propósito,
        // como el único portador que SÍ llega a Dribbling, para poder completar el experimento de
        // comparación de utilidad que pedía el encargo — no es el portador que usa ScreeningRunner hoy.
        var perk = Catalog.Perks.All.Single(p => p.Id == "cannon");
        var race = perk.Race ?? Race.Human;
        var samples = new List<WindowSample>();

        for (int roster = 0; roster < 400 && samples.Count < 15; roster++)
        {
            var homeRng = RngStreams.Generation(1, roster);
            var awayRng = RngStreams.Generation(1, 10_000 + roster);
            var home = TeamGenerator.Generate(ref homeRng, Catalog, "home", race, Quality, 1, Level);
            var away = TeamGenerator.Generate(ref awayRng, Catalog, "away", race, Quality, 100001, Level);

            int carrierSlot = FindByPosition(home, Position.Forward);
            if (carrierSlot < 0)
            {
                continue;
            }

            var armedPlayers = home.Players.ToList();
            armedPlayers[carrierSlot] = armedPlayers[carrierSlot] with { Perks = new[] { perk.Id } };
            var armedHome = home with { Players = armedPlayers };
            int carrierId = armedPlayers[carrierSlot].Id;
            ulong matchSeed = RngStreams.MatchSeed(1, roster * 2);

            // Paso 1: encontrar, con traza, los ticks candidatos (balón en el portador, distancia en ventana).
            var traceConfig = new SimConfig(CollectLog: false, Trace: true);
            var traceResult = Simulator.Run(new MatchSetup(armedHome, away, Referee), matchSeed, Catalog, traceConfig);
            var trace = traceResult.Trace;
            if (trace is null)
            {
                continue;
            }

            int slot = -1;
            for (int i = 0; i < trace.Players.Count; i++)
            {
                if (trace.Players[i].Id == carrierId)
                {
                    slot = i;
                    break;
                }
            }

            if (slot < 0)
            {
                continue;
            }

            int team = trace.Players[slot].Team;
            var candidateTicks = new List<int>();
            for (int frame = 0; frame < trace.FrameCount; frame++)
            {
                if (!trace.OnPitchAt(frame, slot) || trace.BallOwnerAt(frame) != slot)
                {
                    continue;
                }

                // Shoot solo es legal en Dribbling (StateMachine.LegalActions) — sin este filtro, el tick
                // "candidato" puede caer en Positioning/Chasing (con el balón nominalmente asignado pero
                // sin control real todavía), donde Choose ni siquiera evalúa Shoot como opción.
                if (trace.StateAt(frame, slot) != PlayerState.Dribbling)
                {
                    continue;
                }

                float distance = Vec2.Distance(trace.PositionAt(frame, slot), Pitch.GoalCenter(team));
                if (distance > BaseRangeCells && distance <= EffectiveRangeCells)
                {
                    candidateTicks.Add(trace.TickAt(frame));
                }
            }

            if (candidateTicks.Count == 0)
            {
                continue;
            }

            // Un candidato por roster (el primero), para diversificar entre plantillas en vez de agotar
            // los 15 en un solo partido con mucha posesión en ventana.
            int candidateTick = candidateTicks[0];

            var dumpConfig = new SimConfig(CollectLog: false, DumpUtility: (carrierId, candidateTick));
            var armedDumpResult = Simulator.Run(new MatchSetup(armedHome, away, Referee), matchSeed, Catalog, dumpConfig);
            var controlDumpResult = Simulator.Run(new MatchSetup(home, away, Referee), matchSeed, Catalog, dumpConfig);

            var armedDump = armedDumpResult.Report.UtilityDump;
            var controlDump = controlDumpResult.Report.UtilityDump;
            if (armedDump is null || controlDump is null)
            {
                continue;
            }

            var armedShoot = armedDump.Rows.FirstOrDefault(r => r.Action == PlayerAction.Shoot);
            var controlShoot = controlDump.Rows.FirstOrDefault(r => r.Action == PlayerAction.Shoot);

            samples.Add(new WindowSample(
                roster, armedDump.Tick, carrierId,
                armedShoot, armedDump.Chosen, controlShoot, controlDump.Chosen));
        }

        Assert.NotEmpty(samples);

        _output.WriteLine($"{samples.Count} muestras dentro de la ventana de efecto (8,11] casillas, con balón en el portador.");
        _output.WriteLine("roster | tick | armado: Base/Tactical/Trait/Context/Score/Rejected -> Elegida | control: mismos campos -> Elegida");

        int contextDiffers = 0, shootChosenArmed = 0, shootChosenControl = 0, controlEqualsArmedBase = 0;
        foreach (var s in samples)
        {
            string armedRow = s.ArmedShoot is { } a
                ? $"{a.Base}/{a.TacticalMultiplier}/{a.TraitMultiplier}/{a.Context}/{a.Score}/{a.Rejected}"
                : "(sin fila Shoot — rechazada antes de puntuar o fuera de las legales)";
            string controlRow = s.ControlShoot is { } c
                ? $"{c.Base}/{c.TacticalMultiplier}/{c.TraitMultiplier}/{c.Context}/{c.Score}/{c.Rejected}"
                : "(sin fila Shoot)";
            _output.WriteLine($"{s.Roster} | {s.Tick} | {armedRow} -> {s.ArmedChosen} | {controlRow} -> {s.ControlChosen}");

            if (s.ArmedShoot is { } aa && s.ControlShoot is { } cc)
            {
                if (aa.Context != cc.Context)
                {
                    contextDiffers++;
                }

                if (aa.Base == cc.Base && aa.TacticalMultiplier == cc.TacticalMultiplier && aa.TraitMultiplier == cc.TraitMultiplier)
                {
                    controlEqualsArmedBase++;
                }
            }

            if (s.ArmedChosen == PlayerAction.Shoot)
            {
                shootChosenArmed++;
            }

            if (s.ControlChosen == PlayerAction.Shoot)
            {
                shootChosenControl++;
            }
        }

        _output.WriteLine("");
        _output.WriteLine($"muestras donde Context (armado) != Context (control) para Shoot: {contextDiffers}/{samples.Count}");
        _output.WriteLine($"muestras donde Base/Tactical/Trait NO relacionados con cannon coinciden armado=control (confirma que no hay divergencia previa de estado): {controlEqualsArmedBase}/{samples.Count}");
        _output.WriteLine($"Shoot fue la acción elegida: armado={shootChosenArmed}/{samples.Count}, control={shootChosenControl}/{samples.Count}");
    }
}
