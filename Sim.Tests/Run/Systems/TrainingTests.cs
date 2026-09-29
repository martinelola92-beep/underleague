using Underleague.Sim.Data;
using Underleague.Sim.Model;
using Underleague.Sim.Run;
using Underleague.Sim.Run.View;
using Underleague.Sim.Run.Systems.Nodes;
using ProgressionRules = Underleague.Sim.Progression.Progression;

namespace Underleague.Sim.Tests.Run.Systems;

/// <summary>
/// ADR 0160: el entrenamiento se elige. Aquí se comprueba lo que sostiene esa promesa: la carta es
/// derivable y estable (W-12), la pachanga sigue siendo exactamente el entrenamiento de antes, la
/// especialización es permanente y sobrevive a una subida de nivel, y el cambio de puesto cuesta un nivel
/// y nunca toca a un portero en ninguna dirección (ADR 0080).
/// </summary>
public sealed class TrainingTests
{
    private static Catalog Catalog => SystemsTestSupport.Catalog;

    private static (RunState State, MapNode Node) AtATrainingNode(ulong seed, int skip = 0)
    {
        var state = RunEngine.Start(SystemsTestSupport.Setup(), seed, Catalog, SystemsTestSupport.Systems);
        state = SystemsTestSupport.WithFakePendingNode(state, NodeKind.Training, skip);
        return (state, state.GetNode(state.PendingNodeId));
    }

    private static MapNode Node(int id) => new(id, 1, 0, 0, NodeKind.Training, Array.Empty<int>(), string.Empty, 0);

    /// <summary>La carta se deriva del nodo y no se guarda: dos lecturas del mismo estado ven la misma (W-12).</summary>
    [Fact]
    public void TheCardIsDerivedAndStable()
    {
        var node = Node(207);
        var first = TrainingSystem.Card(node);
        var second = TrainingSystem.Card(node);

        Assert.Equal(3, first.Sessions.Count);
        Assert.Equal(TrainingSessionKind.Scrimmage, first.Sessions[0].Kind);
        for (int i = 0; i < first.Sessions.Count; i++)
        {
            Assert.Equal(first.Sessions[i].Kind, second.Sessions[i].Kind);
            Assert.Equal(first.Sessions[i].Attribute, second.Sessions[i].Attribute);
        }
    }

    /// <summary>Uno de cada tres nodos sustituye la segunda especialización por un cambio de puesto (ADR 0160).</summary>
    [Fact]
    public void EveryThirdNodeSwapsASpecializationForAReposition()
    {
        for (int id = 100; id < 130; id++)
        {
            var card = TrainingSystem.Card(Node(id));
            bool hasReposition = card.Sessions.Any(s => s.Kind == TrainingSessionKind.Reposition);
            Assert.Equal(id % 3 == 0, hasReposition);

            int specializations = card.Sessions.Count(s => s.Kind == TrainingSessionKind.Specialization);
            Assert.Equal(hasReposition ? 1 : 2, specializations);
            if (specializations == 2)
            {
                // Las dos especializaciones ofrecen atributos distintos (ADR 0160).
                var attributes = card.Sessions.Where(s => s.Kind == TrainingSessionKind.Specialization).Select(s => s.Attribute).ToArray();
                Assert.NotEqual(attributes[0], attributes[1]);
            }
        }
    }

    /// <summary>
    /// La pachanga (sesión 0) es el entrenamiento de antes de la ADR, contado a mano (Regla J: comparar la
    /// función consigo misma no prueba nada): +40 de experiencia a cada disponible, +33 % a los canteranos
    /// (40 * 133 / 100 = 53), y nada a quien está lesionado de gravedad o muerto.
    /// </summary>
    [Fact]
    public void ScrimmageGivesFortyExperienceToEachAvailablePlayerAndAThirdMoreToYouths()
    {
        var (state, _) = AtATrainingNode(9001UL);
        var economy = SystemsTestSupport.Systems.Economy;
        Assert.Equal(40, economy.TrainingExperience);
        Assert.Equal(33, RunRules.YouthExperienceBonusPercent);

        var roster = state.Roster.ToList();
        roster[0] = roster[0] with { IsYouth = true, Experience = 7 };
        roster[1] = roster[1] with { PhysicalState = PhysicalState.SevereInjury, Experience = 11 };
        roster[2] = roster[2] with { PhysicalState = PhysicalState.MinorInjury, Experience = 13 };
        roster[3] = roster[3] with { PhysicalState = PhysicalState.Dead, Experience = 17 };
        state = state.WithRoster(roster);

        var after = TrainingSystem.Choose(state, new ChooseTrainingSession(0), economy, Catalog);

        Assert.Equal(state.Roster.Count, after.Roster.Count);
        for (int i = 0; i < state.Roster.Count; i++)
        {
            var before = state.Roster[i];
            int expected = before.Experience;
            if (before.IsAvailable)
            {
                expected += before.IsYouth ? 53 : 40;
            }

            Assert.Equal(expected, after.Roster[i].Experience);
            Assert.Equal(ProgressionRules.LevelFor(expected, Catalog.Progression), before.IsAvailable ? after.Roster[i].Level : before.Level);
        }
    }

    /// <summary>+8 permanente al atributo de la sesión, y nadie más de la plantilla lo nota (cuesta la experiencia del resto).</summary>
    [Fact]
    public void SpecializationRaisesOnlyTheChosenAttributeOfTheChosenPlayer()
    {
        var (state, node) = AtATrainingNode(9002UL);
        var card = TrainingSystem.Card(node);
        int index = Array.FindIndex(card.Sessions.ToArray(), s => s.Kind == TrainingSessionKind.Specialization);
        var attribute = card.Sessions[index].Attribute;
        var target = state.Roster.First(p => p.IsAvailable);
        int before = target.Attributes.Get(attribute);

        var after = TrainingSystem.Choose(state, new ChooseTrainingSession(index, target.Id), SystemsTestSupport.Systems.Economy, Catalog);

        var updated = after.GetPlayer(target.Id);
        Assert.Equal(Math.Min(99, before + TrainingSystem.SpecializationBonus), updated.Attributes.Get(attribute));
        Assert.Equal(target.Experience, updated.Experience);
        for (int i = 0; i < state.Roster.Count; i++)
        {
            if (state.Roster[i].Id == target.Id)
            {
                continue;
            }

            Assert.Equal(state.Roster[i].Experience, after.Roster[i].Experience);
        }
    }

    /// <summary>El +8 de la especialización se conserva íntegro tras una subida de nivel (ADR 0160: "pasa del techo del nivel").</summary>
    [Fact]
    public void SpecializationPersistsAfterALevelUp()
    {
        var (state, node) = AtATrainingNode(9003UL);
        var card = TrainingSystem.Card(node);
        int index = Array.FindIndex(card.Sessions.ToArray(), s => s.Kind == TrainingSessionKind.Specialization);
        var attribute = card.Sessions[index].Attribute;
        var target = state.Roster.First(p => p.IsAvailable && p.Level < ProgressionRules.MaxLevel);

        var specialized = TrainingSystem.Choose(state, new ChooseTrainingSession(index, target.Id), SystemsTestSupport.Systems.Economy, Catalog);
        var player = specialized.GetPlayer(target.Id);
        int specializedValue = player.Attributes.Get(attribute);

        var definition = player.ToDefinition(Catalog, applyMinorInjuryPenalty: false);
        var leveled = ProgressionRules.LevelUp(definition, player.Level + 1, Catalog.Progression);

        Assert.Equal(player.Level + 1, leveled.Level);
        Assert.Equal(
            Math.Min(99, specializedValue + Catalog.Progression.AttributesPerLevel),
            leveled.Attributes.Get(attribute));
    }

    /// <summary>
    /// El cambio de puesto cuesta exactamente un nivel, con su pérdida de atributos, deja la experiencia en
    /// el mínimo del nivel nuevo (o el jugador «sube» solo la próxima vez, Regla I), cambia la posición y
    /// mantiene las etiquetas coherentes con ella. Se parte de un nivel 4: con un jugador de nivel 1 (los de
    /// una run recién empezada) la comparación de niveles sería 1 == 1 y no probaría nada.
    /// </summary>
    [Fact]
    public void RepositionCostsALevelAndChangesPosition()
    {
        var (state, index) = AtATrainingNodeWithReposition(9100UL);
        var tuning = Catalog.Progression;
        var target = state.Roster.First(p => p.IsAvailable && p.Position != Position.Goalkeeper) with
        {
            Level = 4,
            Experience = ProgressionRules.MinExperienceForLevel(4, tuning) + 25,
            Attributes = new Attributes(60, 61, 62, 63, 40),
            Perks = Array.Empty<string>(),
        };
        state = state.WithPlayer(target);
        var destination = target.Position == Position.Defender ? Position.Midfielder : Position.Defender;
        Assert.Contains(target.Position.ToString(), target.Tags);

        var after = TrainingSystem.Choose(
            state, new ChooseTrainingSession(index, target.Id, destination), SystemsTestSupport.Systems.Economy, Catalog);
        var updated = after.GetPlayer(target.Id);

        int perLevel = tuning.AttributesPerLevel;
        Assert.Equal(destination, updated.Position);
        Assert.Equal(3, updated.Level);
        Assert.Equal(ProgressionRules.MinExperienceForLevel(3, tuning), updated.Experience);
        Assert.Equal(3, ProgressionRules.LevelFor(updated.Experience, tuning));
        Assert.Equal(new Attributes(60 - perLevel, 61 - perLevel, 62 - perLevel, 63 - perLevel, 40), updated.Attributes);

        // Etiquetas coherentes con la posición nueva (ADR 0024): la vieja sale, la nueva entra, y el resto se queda.
        Assert.Contains(destination.ToString(), updated.Tags);
        Assert.DoesNotContain(target.Position.ToString(), updated.Tags);
        Assert.Equal(target.Tags.Count, updated.Tags.Count);
        Assert.Equal(
            target.Tags.Where(t => t != target.Position.ToString()).OrderBy(t => t, StringComparer.Ordinal),
            updated.Tags.Where(t => t != destination.ToString()).OrderBy(t => t, StringComparer.Ordinal));

        // Nadie más se toca.
        foreach (var other in state.Roster.Where(p => p.Id != target.Id))
        {
            Assert.Equal(other, after.GetPlayer(other.Id));
        }
    }

    /// <summary>Tras un cambio de puesto la run sigue jugando: el partido siguiente se resuelve sin excepción.</summary>
    [Fact]
    public void AMatchCanBePlayedAfterARepositionOfAStarter()
    {
        var (state, index) = AtATrainingNodeWithReposition(9101UL);
        var starters = state.Lineup.Slots.Select(s => s.PlayerId).ToHashSet();
        var target = state.Roster.First(p => p.IsAvailable && p.Position != Position.Goalkeeper && starters.Contains(p.Id));
        state = state.WithPlayer(target with { Level = 3, Experience = ProgressionRules.MinExperienceForLevel(3, Catalog.Progression) });

        // Un destino que ningún perk suyo bloquee.
        var destination = TrainingSystem.FieldPositions.First(
            p => p != target.Position && TrainingSystem.BlockingPerk(state.GetPlayer(target.Id), p, Catalog) is null);
        var moved = TrainingSystem.Choose(
            state, new ChooseTrainingSession(index, target.Id, destination), SystemsTestSupport.Systems.Economy, Catalog);
        Assert.Equal(destination, moved.GetPlayer(target.Id).Position);

        var left = RunEngine.Apply(moved, new LeaveNode(), Catalog, SystemsTestSupport.Systems);
        var played = SystemsTestSupport.PlayNextMatch(left);

        Assert.True(played.NodeHistory.Count > left.NodeHistory.Count, "el partido se jugó y quedó en el historial");
        Assert.Equal(destination, played.GetPlayer(target.Id).Position);
    }

    /// <summary>Nadie en 99 de ese atributo es candidato a la especialización: ni en la regla, ni en la elección, ni en la vista.</summary>
    [Fact]
    public void NobodyAtNinetyNineIsACandidateForSpecialization()
    {
        var (state, node) = AtATrainingNode(9004UL);
        var card = TrainingSystem.Card(node);
        int index = Array.FindIndex(card.Sessions.ToArray(), s => s.Kind == TrainingSessionKind.Specialization);
        var attribute = card.Sessions[index].Attribute;
        var economy = SystemsTestSupport.Systems.Economy;
        var capped = state.Roster.First(p => p.IsAvailable);
        var almost = state.Roster.First(p => p.IsAvailable && p.Id != capped.Id);
        state = state
            .WithPlayer(capped with { Attributes = capped.Attributes.With(attribute, 99) })
            .WithPlayer(almost with { Attributes = almost.Attributes.With(attribute, 97) });

        Assert.False(TrainingSystem.CanSpecialize(state.GetPlayer(capped.Id), attribute));
        Assert.True(TrainingSystem.CanSpecialize(state.GetPlayer(almost.Id), attribute));
        Assert.Throws<ArgumentException>(() => TrainingSystem.Choose(state, new ChooseTrainingSession(index, capped.Id), economy, Catalog));

        var row = TrainingView.Build(state, Catalog, economy, "es")!.Sessions[index];
        Assert.DoesNotContain(row.Targets, t => t.PlayerId == capped.Id);
        Assert.Contains(row.Targets, t => t.PlayerId == almost.Id);

        // Con +8 a 97 se topa en 99, no se pasa.
        var after = TrainingSystem.Choose(state, new ChooseTrainingSession(index, almost.Id), economy, Catalog);
        Assert.Equal(99, after.GetPlayer(almost.Id).Attributes.Get(attribute));

        // Y si todos los disponibles están en 99, la sesión deja de estar disponible.
        var everyone = state.WithRoster(state.Roster.Select(p => p with { Attributes = p.Attributes.With(attribute, 99) }));
        Assert.False(TrainingView.Build(everyone, Catalog, economy, "es")!.Sessions[index].Available);
    }

    /// <summary>
    /// Regla I: perder un nivel por el cambio de puesto y ganar después menos experiencia de la que falta para
    /// el umbral no devuelve el nivel; con lo justo, sí. La pachanga da 40 y de 3 a 4 faltan 200, así que se
    /// comprueba con cinco pachangas (200 - 40 = 160 < 200) y con la sexta cruza.
    /// </summary>
    [Fact]
    public void ALostLevelIsNotRecoveredByGainingLessExperienceThanTheThresholdNeeds()
    {
        var (state, index) = AtATrainingNodeWithReposition(9102UL);
        var tuning = Catalog.Progression;
        var economy = SystemsTestSupport.Systems.Economy;
        var target = state.Roster.First(p => p.IsAvailable && p.Position != Position.Goalkeeper) with
        {
            Level = 4,
            Experience = ProgressionRules.MinExperienceForLevel(4, tuning) + 30,
            Perks = Array.Empty<string>(),
        };
        state = state.WithPlayer(target);
        var destination = target.Position == Position.Defender ? Position.Midfielder : Position.Defender;
        var lost = TrainingSystem.Choose(state, new ChooseTrainingSession(index, target.Id, destination), economy, Catalog)
            .GetPlayer(target.Id);
        Assert.Equal(3, lost.Level);

        int threshold = ProgressionRules.MinExperienceForLevel(4, tuning);
        int missing = threshold - lost.Experience;
        int scrimmage = economy.TrainingExperience;
        Assert.True(missing > scrimmage, "instrumento: una pachanga sola no cubre lo que falta");

        // Se aplica el entrenamiento de pachanga directamente (un nodo sólo se elige una vez).
        var current = lost;
        var running = state.WithPlayer(lost);
        while (current.Experience + scrimmage < threshold)
        {
            running = ServiceNodeSystem.Training(running, economy, Catalog);
            current = running.GetPlayer(target.Id);
            Assert.Equal(3, current.Level);
            Assert.Equal(3, ProgressionRules.LevelFor(current.Experience, tuning));
        }

        running = ServiceNodeSystem.Training(running, economy, Catalog);
        Assert.Equal(4, running.GetPlayer(target.Id).Level);
    }

    /// <summary>Elegir dos veces lanza (antes tres pachangas seguidas daban 3 x 40), y la vista queda resuelta con nada pulsable.</summary>
    [Fact]
    public void ChoosingTwiceThrowsAndTheViewShowsItResolvedWithNothingAvailable()
    {
        var (state, node) = AtATrainingNode(9005UL);
        var economy = SystemsTestSupport.Systems.Economy;
        var card = TrainingSystem.Card(node);
        int specialization = Array.FindIndex(card.Sessions.ToArray(), s => s.Kind == TrainingSessionKind.Specialization);
        var target = state.Roster.First(p => p.IsAvailable);

        var before = TrainingView.Build(state, Catalog, economy, "es")!;
        Assert.False(before.Resolved);
        Assert.Contains(before.Sessions, s => s.Available);

        var once = TrainingSystem.Choose(state, new ChooseTrainingSession(0), economy, Catalog);

        Assert.Throws<InvalidOperationException>(() => TrainingSystem.Choose(once, new ChooseTrainingSession(0), economy, Catalog));
        Assert.Throws<InvalidOperationException>(
            () => TrainingSystem.Choose(once, new ChooseTrainingSession(specialization, target.Id), economy, Catalog));

        var view = TrainingView.Build(once, Catalog, economy, "es")!;
        Assert.True(view.Resolved);
        Assert.All(view.Sessions, s => Assert.False(s.Available));
        Assert.Equal(state.Roster[0].Experience + economy.TrainingExperience, once.Roster[0].Experience);
    }

    /// <summary>El portero nunca cambia de puesto, y nadie pasa a portero (ADR 0080, ADR 0160).</summary>
    [Fact]
    public void RepositionNeverTouchesGoalkeepersInEitherDirection()
    {
        var (state, repositionIndex) = AtATrainingNodeWithReposition(9200UL);
        var goalkeeper = state.Roster.First(p => p.Position == Position.Goalkeeper && p.IsAvailable);
        var fieldPlayer = state.Roster.First(p => p.IsAvailable && p.Position != Position.Goalkeeper);

        Assert.Throws<ArgumentException>(() => TrainingSystem.Choose(
            state, new ChooseTrainingSession(repositionIndex, goalkeeper.Id, Position.Defender), SystemsTestSupport.Systems.Economy, Catalog));

        Assert.Throws<ArgumentException>(() => TrainingSystem.Choose(
            state, new ChooseTrainingSession(repositionIndex, fieldPlayer.Id, Position.Goalkeeper), SystemsTestSupport.Systems.Economy, Catalog));
    }

    /// <summary>Busca, entre los nodos de entrenamiento reales de la run, uno cuya carta traiga cambio de puesto.</summary>
    private static (RunState State, int SessionIndex) AtATrainingNodeWithReposition(ulong seed)
    {
        for (int skip = 0; skip < 40; skip++)
        {
            var state = RunEngine.Start(SystemsTestSupport.Setup(), seed, Catalog, SystemsTestSupport.Systems);
            RunState pending;
            try
            {
                pending = SystemsTestSupport.WithFakePendingNode(state, NodeKind.Training, skip);
            }
            catch (InvalidOperationException)
            {
                break;
            }

            var node = pending.GetNode(pending.PendingNodeId);
            var card = TrainingSystem.Card(node);
            int index = Array.FindIndex(card.Sessions.ToArray(), s => s.Kind == TrainingSessionKind.Reposition);
            if (index >= 0)
            {
                return (pending, index);
            }
        }

        throw new InvalidOperationException("no se ha encontrado ningún nodo de entrenamiento con cambio de puesto en esta run");
    }
}
