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
    // La semilla se BUSCA, no se clava (ADR 0139). Estuvo clavada en 20260915, hubo que cambiarla a 14
    // cuando BB-B desplazó el consumo de RNG, y el paquete de percepción y balón aéreo la habría dejado
    // otra vez sin activaciones que examinar. Qué semilla produce pases completados es una huella del
    // flujo de aleatoriedad, no una propiedad de la vista de avisos: buscarla deja el test afirmando lo
    // que quiere afirmar —que toda activación se convierte en un aviso, en su fotograma y sobre su
    // ficha— y lo hace inmune a esto para siempre.
    private static ulong Seed => SeedValue.Value;


    /// <summary>Un perk que se cobra en cada pase completado del portador: suficiente para que salten avisos.</summary>
    private static readonly string OnPass = TestPerks.Json(
        "test_flash",
        "PASS_COMPLETED",
        """[ { "type": "modifyProbability", "target": "actor", "probability": "pass", "value": 15, "duration": "match" } ]""");

    private static readonly Lazy<ulong> SeedValue = new(() =>
        TestPerks.SeedWithActivations(TestPerks.CatalogWith(("test_flash", OnPass)), "test_flash", playerId: 1));

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
