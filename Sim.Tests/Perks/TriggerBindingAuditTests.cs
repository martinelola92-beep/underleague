using System.Text.RegularExpressions;
using Underleague.Sim.Data;
using Underleague.Sim.Events;
using Underleague.Sim.Perks;
using Xunit.Abstractions;

namespace Underleague.Sim.Tests.Perks;

/// <summary>
/// BB-Q Alt 5 — auditoría <b>solo informativa</b>: ¿hay más condiciones que pidan un identificador que su
/// disparador no liga? Es la clase de fallo de <c>steamroller</c>, que preguntaba por <c>target</c> en un
/// evento TACKLE, donde el entrado viaja en <c>opponent</c>: la condición cargaba sin una queja y valía
/// falso para siempre (<c>ConditionCompiler.cs:779</c> devuelve 0 para un <c>who</c> sin ligar).
///
/// <para><b>No rechaza ningún perk.</b> RT-032/RT-083 dicen que un dato inválido debe ser error explícito,
/// pero convertir esto en error de carga hoy podría tumbar perks del catálogo: primero se mide. Este test
/// imprime el inventario y solo falla si aparece un caso NUEVO además de los ya inventariados.</para>
/// </summary>
public sealed class TriggerBindingAuditTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();

    private readonly ITestOutputHelper _output;
    public TriggerBindingAuditTests(ITestOutputHelper output) => _output = output;

    private enum Binding { Always, Sometimes, Never }

    /// <summary>
    /// Qué liga cada disparador, leído de las llamadas a Emit/PublishBeforeResolving/EmitCancellable de
    /// <c>MatchEngine.cs</c>. <c>owner</c> siempre está ligado (es el portador del perk) y no se audita.
    /// </summary>
    private static (Binding Actor, Binding Target, Binding Opponent) Bindings(EventType trigger) => trigger switch
    {
        // Ligan `target` — los cuatro son relaciones del MISMO equipo (receptor, asistente, saliente).
        EventType.Substitution => (Binding.Always, Binding.Always, Binding.Never),
        EventType.PassAttempted => (Binding.Always, Binding.Always, Binding.Never),
        EventType.PassCompleted => (Binding.Always, Binding.Always, Binding.Never),
        EventType.Goal => (Binding.Always, Binding.Sometimes, Binding.Never), // asistente puede ser null

        // Ligan `opponent` — todos los adversariales.
        EventType.Tackle => (Binding.Always, Binding.Never, Binding.Always),
        EventType.Foul => (Binding.Always, Binding.Never, Binding.Always),
        EventType.Injury => (Binding.Always, Binding.Never, Binding.Always),
        EventType.Save => (Binding.Always, Binding.Never, Binding.Always),
        EventType.ShotBlocked => (Binding.Always, Binding.Never, Binding.Always),
        EventType.DribbleAttempted => (Binding.Always, Binding.Never, Binding.Always),
        EventType.DribbleWon => (Binding.Always, Binding.Never, Binding.Always),
        EventType.DribbleLost => (Binding.Always, Binding.Never, Binding.Always),
        EventType.PassFailed => (Binding.Always, Binding.Never, Binding.Sometimes), // "loose"/"cancelled" no ligan

        // No ligan ninguno de los dos.
        EventType.Shot => (Binding.Always, Binding.Never, Binding.Never),
        EventType.Recovery => (Binding.Always, Binding.Never, Binding.Never),
        EventType.Card => (Binding.Always, Binding.Never, Binding.Never),
        EventType.Death => (Binding.Always, Binding.Never, Binding.Never),
        EventType.ConsumableUsed => (Binding.Always, Binding.Never, Binding.Never),

        // Actorless (EffectEngine.cs:650-656): se evalúan con actor = owner, así que `actor` vale.
        EventType.MatchStart or EventType.MatchEnd or EventType.MobStart or EventType.RefereeLeaves
            or EventType.PlayStart or EventType.PlayEnd => (Binding.Always, Binding.Never, Binding.Never),

        _ => (Binding.Always, Binding.Never, Binding.Never),
    };

    private static bool Mentions(string condition, string identifier) =>
        Regex.IsMatch(condition, $@"\b{identifier}\b");

    [Fact]
    public void InventoryOfConditionsThatAskForUnboundIdentifiers()
    {
        var rows = new List<(string Perk, EventType Trigger, string Asked, Binding Bound, string Condition)>();

        foreach (var perk in Catalog.Perks.All.OrderBy(p => p.Id, StringComparer.Ordinal))
        {
            if (perk.Condition.Length == 0)
            {
                continue;
            }

            var (actor, target, opponent) = Bindings(perk.Trigger);
            foreach (var (name, binding) in new[] { ("actor", actor), ("target", target), ("opponent", opponent) })
            {
                if (Mentions(perk.Condition, name) && binding != Binding.Always)
                {
                    rows.Add((perk.Id, perk.Trigger, name, binding, perk.Condition));
                }
            }
        }

        _output.WriteLine($"Perks con condición: {Catalog.Perks.All.Count(p => p.Condition.Length > 0)} de {Catalog.Perks.All.Count}");
        _output.WriteLine("");
        _output.WriteLine("=== condiciones que piden un identificador que su disparador NO liga siempre ===");
        if (rows.Count == 0)
        {
            _output.WriteLine("  (ninguna)");
        }

        foreach (var r in rows.OrderBy(r => r.Perk, StringComparer.Ordinal))
        {
            string severity = r.Bound == Binding.Never ? "NUNCA LIGADO (condición siempre falsa)" : "ligado solo a veces";
            _output.WriteLine($"  {r.Perk,-22} trigger={r.Trigger,-18} pide '{r.Asked}' -> {severity}");
            _output.WriteLine($"      condición: {r.Condition}");
        }

        _output.WriteLine("");
        _output.WriteLine("=== uso de identificadores por disparador (todo el catálogo) ===");
        foreach (var g in Catalog.Perks.All.Where(p => p.Condition.Length > 0).GroupBy(p => p.Trigger).OrderBy(g => g.Key.ToString(), StringComparer.Ordinal))
        {
            var (a, t, o) = Bindings(g.Key);
            int usesActor = g.Count(p => Mentions(p.Condition, "actor"));
            int usesTarget = g.Count(p => Mentions(p.Condition, "target"));
            int usesOpponent = g.Count(p => Mentions(p.Condition, "opponent"));
            _output.WriteLine(
                $"  {g.Key,-18} perks={g.Count(),3} | liga actor={a,-9} target={t,-9} opponent={o,-9} | " +
                $"lo piden: actor={usesActor} target={usesTarget} opponent={usesOpponent}");
        }

        // Report-only: no se rechaza nada. Solo se fija que no aparezca un caso NUEVO sin que nos enteremos.
        Assert.Empty(rows.Where(r => r.Bound == Binding.Never).Select(r => $"{r.Perk}:{r.Asked}"));
    }
}
