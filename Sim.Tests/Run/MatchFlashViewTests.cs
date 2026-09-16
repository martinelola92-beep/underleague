using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Run.View;
using Underleague.Sim.Tests.Perks;

namespace Underleague.Sim.Tests.Run;

/// <summary>
/// El aviso de perk activado (C9): que el motor emita <c>PERK_TRIGGERED</c> cuando un perk se cobra, que
/// la vista lo ponga en su fotograma y sobre su ficha, y —lo importante— que <b>añadirlo no cambie el
/// partido</b> (RT-024): es un evento de presentación, no una jugada.
/// </summary>
public sealed class MatchFlashViewTests
{
    // 14, no 20260915: con esa semilla el portador de test_flash completaba 0/3 pases (BB-B, un fix ajeno
    // de ClampToArea/IsOutfield desplazó el consumo de RNG del partido lo suficiente para cruzar a cero —
    // el mismo ruido de cualquier cambio de /Sim que ya se documentó en RunPolicyItemSlotTests). Con 14
    // completa 4 de 11 intentos, con margen de verdad en vez de al borde.
    private const ulong Seed = 14UL;

    /// <summary>Un perk que se cobra en cada pase completado del portador: suficiente para que salten avisos.</summary>
    private static readonly string OnPass = TestPerks.Json(
        "test_flash",
        "PASS_COMPLETED",
        """[ { "type": "modifyProbability", "target": "actor", "probability": "pass", "value": 15, "duration": "match" } ]""");

    [Fact]
    public void EveryActivationBecomesAFlashOnItsFrameAndOnItsPlayer()
    {
        var catalog = TestPerks.CatalogWith(("test_flash", OnPass));
        var setup = TestPerks.Match(catalog, Seed, (1, new[] { "test_flash" }));
        var result = Simulator.Run(setup, Seed, catalog, SimConfig.Default with { Trace = true });
        var trace = result.Trace!;

        var triggered = result.Events.Where(e => e.Type == EventType.PerkTriggered).ToList();
        Assert.NotEmpty(triggered);

        var flashes = MatchFlashView.Build(result.Events, trace, catalog);
        Assert.Equal(triggered.Count, flashes.Count);

        foreach (var flash in flashes)
        {
            // El fotograma es el del tick, y la ficha es la del portador: sin esas dos cosas el aviso se
            // pintaría sobre la cabeza equivocada o en el minuto equivocado.
            Assert.InRange(flash.Frame, 0, trace.FrameCount - 1);
            Assert.Equal(flash.PlayerId, trace.Players[flash.Player].Id);
            Assert.Equal(trace.Players[flash.Player].Team, flash.Team);
            Assert.Equal("test_flash", flash.PerkId);
            Assert.False(string.IsNullOrEmpty(flash.Name));
        }

        // Orden determinista (RT-041): fotograma ascendente y, dentro del fotograma, ficha ascendente.
        for (int i = 1; i < flashes.Count; i++)
        {
            Assert.True(
                flashes[i - 1].Frame < flashes[i].Frame
                    || (flashes[i - 1].Frame == flashes[i].Frame && flashes[i - 1].Player < flashes[i].Player),
                "los avisos no están ordenados por fotograma e id");
        }
    }

    /// <summary>
    /// El aviso es presentación: quitarlo de la secuencia tiene que dejar exactamente el partido que había
    /// antes de C9. Se compara contra el mismo partido sin ningún perk asignado no —eso mediría otra
    /// cosa— sino contra sí mismo: ningún PERK_TRIGGERED cae después de MATCH_END ni delante de MATCH_START.
    /// </summary>
    [Fact]
    public void TheNoticeNeverBreaksTheEnvelopeOfTheMatch()
    {
        var catalog = TestPerks.CatalogWith(("test_flash", OnPass));
        var setup = TestPerks.Match(catalog, Seed, (1, new[] { "test_flash" }));
        var events = Simulator.Run(setup, Seed, catalog, SimConfig.Default).Events;

        Assert.Equal(EventType.MatchStart, events[0].Type);
        Assert.Equal(EventType.MatchEnd, events[^1].Type);
    }
}
