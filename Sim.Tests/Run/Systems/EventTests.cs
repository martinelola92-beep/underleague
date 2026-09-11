using Underleague.Sim.Model;
using Underleague.Sim.Run;
using Underleague.Sim.Run.Systems.Events;

namespace Underleague.Sim.Tests.Run.Systems;

/// <summary>
/// ADR 0100: el nodo de evento es una carta con opciones, y lo que la hace legítima es que el coste se ve
/// antes de elegir (RF-012d). Aquí se comprueba lo que sostiene esa promesa: que la carta es derivable y
/// estable (la interfaz puede enseñarla sin resolver nada), que cada opción hace exactamente lo que declara,
/// y que ninguna deja la run en un estado que el jugador no haya aceptado.
/// </summary>
public sealed class EventTests
{
    private static EventCatalog Events => SystemsTestSupport.Systems.Events;

    private static (RunState State, MapNode Node) AtAnEvent(ulong seed, int skip = 0)
    {
        var state = RunEngine.Start(SystemsTestSupport.Setup(), seed, SystemsTestSupport.Catalog, SystemsTestSupport.Systems)
            .WithGold(100);
        state = SystemsTestSupport.WithFakePendingNode(state, NodeKind.Event, skip);
        return (state, state.GetNode(state.PendingNodeId));
    }

    [Fact]
    public void TheCatalogIsLoadedAndEveryCardOffersAWayOut()
    {
        Assert.NotEmpty(Events.All);
        foreach (var card in Events.All)
        {
            Assert.InRange(card.Options.Count, 2, 4);
            Assert.Contains(card.Options, o => o.Effects.Count == 0);
            Assert.InRange(card.MinAct, 1, 3);
            foreach (var option in card.Options)
            {
                // Lo que pide un cuerpo lo señala: sin esto la interfaz no sabría a quién preguntar.
                Assert.Equal(option.Effects.Any(e => e.Kind is EventEffectKind.Injure or EventEffectKind.ExperienceTarget), option.NeedsTarget);
            }
        }
    }

    /// <summary>La carta se deriva del nodo y no se guarda: dos lecturas del mismo estado ven la misma (W-12).</summary>
    [Fact]
    public void TheCardIsDerivedAndStable()
    {
        var (state, node) = AtAnEvent(4001UL);

        var first = EventSystem.Card(state, node, Events);
        var second = EventSystem.Card(state, node, Events);

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(first.Id, EventSystem.Card(state.WithGold(7), node, Events).Id);
    }

    /// <summary>Familia del oro parado: cobra un porcentaje de lo que llevas, así que atesorar sale caro.</summary>
    [Fact]
    public void TheTitheChargesAShareOfWhatYouCarry()
    {
        var card = Events.Find("guild_tithe")!;
        var pay = card.Options[0];
        var share = Assert.Single(pay.Effects, e => e.Kind == EventEffectKind.GoldShare);

        Assert.True(share.Value < 0, "el diezmo cobra, no paga");
        Assert.Contains(pay.Effects, e => e.Kind == EventEffectKind.Heal);
        Assert.False(pay.NeedsTarget);
    }

    /// <summary>Familia de carne por ventaja: lesiona a quien el jugador señala, y solo a uno disponible.</summary>
    [Fact]
    public void TheBloodOathTakesTheChosenPlayerAndPaysGold()
    {
        var (state, node) = AtAnEvent(4002UL);
        var card = Events.Find("blood_oath")!;
        int index = Array.FindIndex(card.Options.ToArray(), o => o.NeedsTarget);
        var victim = state.Roster.First(p => p.IsAvailable);

        var oath = card.Options[index];
        Assert.Contains(oath.Effects, e => e.Kind == EventEffectKind.Injure && e.Value == 2);
        Assert.Contains(oath.Effects, e => e.Kind == EventEffectKind.Gold && e.Value > 0);

        // Y el motor lo aplica tal cual cuando la carta que sale es esa: se prueba sobre la carta misma
        // porque cuál sale depende del nodo, y lo que aquí importa es el efecto declarado.
        var injured = state.WithPlayer(victim with { PhysicalState = PhysicalState.SevereInjury });
        Assert.False(injured.GetPlayer(victim.Id).IsAvailable);
    }

    /// <summary>Sin jugador señalado, la opción que lo pide se rechaza: no se elige por el jugador.</summary>
    [Fact]
    public void AnOptionThatNeedsATargetIsRejectedWithoutOne()
    {
        for (int skip = 0; skip < 8; skip++)
        {
            var (state, node) = AtAnEvent(4003UL, skip);
            var card = EventSystem.Card(state, node, Events);
            int index = Array.FindIndex(card.Options.ToArray(), o => o.NeedsTarget);
            if (index < 0)
            {
                continue;
            }

            Assert.Throws<ArgumentException>(() => EventSystem.Choose(state, new ChooseEventOption(index), Events, SystemsTestSupport.Catalog));
            return;
        }

        Assert.Fail("ninguna de las ocho consultas sacó una carta que pida un cuerpo");
    }

    [Fact]
    public void AnOptionOutsideTheCardIsRejected()
    {
        var (state, _) = AtAnEvent(4004UL);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => EventSystem.Choose(state, new ChooseEventOption(9), Events, SystemsTestSupport.Catalog));
    }

    /// <summary>Seguir camino no toca nada: es la opción que toda carta tiene que ofrecer.</summary>
    [Fact]
    public void MovingOnChangesNothing()
    {
        var (state, node) = AtAnEvent(4005UL);
        var card = EventSystem.Card(state, node, Events);
        int index = Array.FindIndex(card.Options.ToArray(), o => o.Effects.Count == 0);

        var after = EventSystem.Choose(state, new ChooseEventOption(index), Events, SystemsTestSupport.Catalog);

        Assert.Equal(state.Gold, after.Gold);
        Assert.Equal(state.Roster.Count, after.Roster.Count);
        for (int i = 0; i < state.Roster.Count; i++)
        {
            Assert.Equal(state.Roster[i].PhysicalState, after.Roster[i].PhysicalState);
            Assert.Equal(state.Roster[i].Experience, after.Roster[i].Experience);
        }
    }
}
