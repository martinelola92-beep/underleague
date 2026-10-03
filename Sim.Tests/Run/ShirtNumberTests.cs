using System.Text.Json;
using Underleague.Sim.Engine;
using Underleague.Sim.Model;
using Underleague.Sim.Run;
using Underleague.Sim.Run.Save;
using Underleague.Sim.Tests.Run.Systems;

namespace Underleague.Sim.Tests.Run;

/// <summary>
/// BX-4: el dorsal de cada jugador es fijo toda la run. Antes lo repartía la traza de cada partido por puesto, así que
/// el mismo jugador cambiaba de número según quién jugara y en qué puesto.
/// </summary>
public sealed class ShirtNumberTests
{
    private static Underleague.Sim.Data.Catalog Catalog => SystemsTestSupport.Catalog;

    private static RunState Started() => RunEngine.Start(SystemsTestSupport.Setup(), 20260905UL, Catalog, SystemsTestSupport.Systems);

    [Fact]
    public void EveryPlayerStartsWithADistinctPositiveNumber()
    {
        var state = Started();
        Assert.All(state.Roster, p => Assert.True(p.ShirtNumber > 0));
        Assert.Equal(state.Roster.Count, state.Roster.Select(p => p.ShirtNumber).Distinct().Count());
    }

    [Fact]
    public void ANewSigningTakesTheFirstFreeNumber_AndTheStayersKeepTheirs()
    {
        var state = Started();
        var template = state.Roster[0] with { Id = -1, ShirtNumber = 0 };

        // El que fiche sin número recibe el primero libre (el del vendido).
        var withOne = state.WithoutPlayer(state.Roster[3].Id);
        int freed = state.Roster[3].ShirtNumber;
        var signed = withOne.WithNewPlayer(template);
        var newcomer = signed.Roster.Single(p => p.Id == signed.NextPlayerId - 1);
        Assert.Equal(freed, newcomer.ShirtNumber);

        // Si el que trae ya está ocupado, el primero libre: nadie pisa a nadie.
        var clash = withOne.WithNewPlayer(template with { ShirtNumber = state.Roster[0].ShirtNumber });
        var clashing = clash.Roster.Single(p => p.Id == clash.NextPlayerId - 1);
        Assert.NotEqual(state.Roster[0].ShirtNumber, clashing.ShirtNumber);
        Assert.Equal(clash.Roster.Count, clash.Roster.Select(p => p.ShirtNumber).Distinct().Count());

        // Los que estaban conservan el suyo.
        foreach (var kept in withOne.Roster)
        {
            Assert.Equal(kept.ShirtNumber, signed.Roster.Single(p => p.Id == kept.Id).ShirtNumber);
        }
    }

    [Fact]
    public void TheNumberSurvivesPositionChangesAndSaveLoad()
    {
        var state = Started();
        var player = state.Roster[2];
        var moved = state.WithPlayer(player with { Position = Position.Forward, PhysicalState = PhysicalState.MinorInjury });
        Assert.Equal(player.ShirtNumber, moved.Roster.Single(p => p.Id == player.Id).ShirtNumber);

        var loaded = RunSave.Load(RunSave.Save(moved));
        foreach (var expected in moved.Roster)
        {
            Assert.Equal(expected.ShirtNumber, loaded.Roster.Single(p => p.Id == expected.Id).ShirtNumber);
        }
    }

    [Fact]
    public void ASaveFromBeforeTheNumbersLoadsAndNumbersByIdAscending()
    {
        var state = Started();
        using var document = JsonDocument.Parse(RunSave.Save(state));

        // Se reescribe el guardado como lo dejaba la versión 9: sin "shirtNumber" en ningún jugador.
        var loaded = RunSave.Load(WithoutShirtNumbers(document.RootElement));

        Assert.Equal(RunState.CurrentSchemaVersion, loaded.SchemaVersion);
        var ordered = loaded.Roster.OrderBy(p => p.Id).ToList();
        for (int i = 0; i < ordered.Count; i++)
        {
            Assert.Equal(i + 1, ordered[i].ShirtNumber);
        }
    }

    [Fact]
    public void TheMatchTraceUsesTheRunNumbersAndStillDealsOutTheRivalsOwn()
    {
        var catalog = TestData.LoadCatalog();
        var setup = TestMatches.Reference(catalog, 7UL);
        var home = setup.Home.Players.Select((p, i) => p with { ShirtNumber = 90 - (i * 7) }).ToList();
        var result = Simulator.Run(
            setup with { Home = setup.Home with { Players = home } }, 7UL, catalog, SimConfig.Default with { Trace = true });
        var trace = result.Trace!;

        var traced = trace.Players.Where(t => t.Team == 0).ToList();
        Assert.True(traced.Count >= 7);
        foreach (var t in traced)
        {
            Assert.Equal(home.Single(p => p.Id == t.Id).ShirtNumber, t.Number);
        }

        // El rival no trae dorsales: reparto de siempre (el portero es el 1).
        Assert.Equal(1, trace.Players.Single(t => t.Team == 1 && t.Role == Position.Goalkeeper).Number);
    }

    private static string WithoutShirtNumbers(JsonElement root)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            Copy(writer, root);
        }

        return System.Text.Encoding.UTF8.GetString(stream.ToArray()).Replace("\"schemaVersion\":10", "\"schemaVersion\":9", StringComparison.Ordinal);

        static void Copy(Utf8JsonWriter w, JsonElement e)
        {
            switch (e.ValueKind)
            {
                case JsonValueKind.Object:
                    w.WriteStartObject();
                    foreach (var property in e.EnumerateObject())
                    {
                        if (property.Name == "shirtNumber")
                        {
                            continue;
                        }

                        w.WritePropertyName(property.Name);
                        Copy(w, property.Value);
                    }

                    w.WriteEndObject();
                    break;
                case JsonValueKind.Array:
                    w.WriteStartArray();
                    foreach (var item in e.EnumerateArray())
                    {
                        Copy(w, item);
                    }

                    w.WriteEndArray();
                    break;
                default:
                    e.WriteTo(w);
                    break;
            }
        }
    }
}
