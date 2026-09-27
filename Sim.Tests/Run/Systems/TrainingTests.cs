using Underleague.Sim.Data;
using Underleague.Sim.Model;
using Underleague.Sim.Run;
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

    /// <summary>La pachanga (sesión 0) es byte a byte el mismo cambio que el entrenamiento de antes de la ADR.</summary>
    [Fact]
    public void ScrimmageEqualsThePreviousTraining()
    {
        var (state, node) = AtATrainingNode(9001UL);
        var economy = SystemsTestSupport.Systems.Economy;

        var viaTraining = TrainingSystem.Choose(state, new ChooseTrainingSession(0), economy, Catalog);
        var viaOldPath = ServiceNodeSystem.Training(state, economy, Catalog);

        Assert.Equal(viaOldPath.Roster.Count, viaTraining.Roster.Count);
        for (int i = 0; i < viaOldPath.Roster.Count; i++)
        {
            Assert.Equal(viaOldPath.Roster[i].Experience, viaTraining.Roster[i].Experience);
            Assert.Equal(viaOldPath.Roster[i].Level, viaTraining.Roster[i].Level);
            Assert.Equal(viaOldPath.Roster[i].Attributes, viaTraining.Roster[i].Attributes);
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

    /// <summary>El cambio de puesto cuesta exactamente un nivel, con su pérdida de atributos, y cambia la posición.</summary>
    [Fact]
    public void RepositionCostsALevelAndChangesPosition()
    {
        var (state, index) = AtATrainingNodeWithReposition(9100UL);
        var target = state.Roster.First(p => p.IsAvailable && p.Position != Position.Goalkeeper);
        var destination = target.Position == Position.Defender ? Position.Midfielder : Position.Defender;

        var after = TrainingSystem.Choose(
            state, new ChooseTrainingSession(index, target.Id, destination), SystemsTestSupport.Systems.Economy, Catalog);
        var updated = after.GetPlayer(target.Id);

        Assert.Equal(destination, updated.Position);
        Assert.Equal(Math.Max(1, target.Level - 1), updated.Level);
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
