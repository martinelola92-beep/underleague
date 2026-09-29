using System.Text.Json;
using Underleague.Sim.Data;

namespace Underleague.Sim.Run.Systems.Bets;

/// <summary>
/// Condición que evalúa una apuesta del vestuario (ADR 0157). Un valor por fila de <c>data/bets/bets.json</c>;
/// las definiciones exactas de cada una viven en <see cref="BetConditions"/>.
/// </summary>
public enum BetKind
{
    BloodBeforeGoals,
    HuntTheStar,
    EyeForEye,
    Comeback,
    IntoTheMob,
    CleanHands,
    Thrashing,
    ThreeNames,
    YouthDecides,
    ShortAndClean,
    RefereeBlind,
}

/// <summary>
/// Ficha de datos de una apuesta (ADR 0157, RF-114h, RF-114i): nombre y texto de la condición localizados,
/// apuesta fija por acto y cobro por dificultad.
/// </summary>
/// <param name="StakeByAct">Oro apostado en los actos 1, 2 y 3. Provisional, sin medir (Regla H).</param>
/// <param name="PayoutPercentByDifficulty">
/// Cobro BRUTO (incluye la apuesta) como porcentaje de la apuesta, índices 0..4 = dificultad 1..5 (RF-012).
/// Sale de <c>85 / p</c> del censo (<c>Balance --bet-census</c>, ADR 0157, sección «Censo»).
/// </param>
public sealed record BetDefinition(
    string Id,
    BetKind Kind,
    LocalizedName Name,
    LocalizedName Condition,
    IReadOnlyList<int> StakeByAct,
    IReadOnlyList<int> PayoutPercentByDifficulty)
{
    /// <summary>Oro que se apuesta en ese acto (1..3; fuera de rango se ajusta al extremo más cercano).</summary>
    public int StakeFor(int act) => StakeByAct[Math.Clamp(act, 1, StakeByAct.Count) - 1];

    /// <summary>Porcentaje bruto que cobra la apuesta contra una dificultad 1..5 (fuera de rango se ajusta).</summary>
    public int PayoutPercentFor(int difficulty) =>
        PayoutPercentByDifficulty[Math.Clamp(difficulty, 1, PayoutPercentByDifficulty.Count) - 1];
}

/// <summary>
/// Catálogo de apuestas de <c>data/bets/bets.json</c> (RT-041: ordenado por id, no un Dictionary iterado),
/// fuera del <see cref="Catalog"/> compartido porque solo lo consume el bucle de run
/// (<c>StandardRunSystems</c>), como <c>RefereeCatalog</c> o <c>EventCatalog</c>. Toda <see cref="BetKind"/>
/// tiene exactamente una ficha: lo exige el cargador.
/// </summary>
public sealed class BetCatalog
{
    /// <summary>Catálogo vacío: ningún nodo ofrece apuesta. Para tests y llamadores antiguos sin <c>data/bets/</c>.</summary>
    public static BetCatalog Empty { get; } = new(Array.Empty<BetDefinition>());

    private readonly IReadOnlyList<BetDefinition> _bets;

    public BetCatalog(IReadOnlyList<BetDefinition> bets)
    {
        ArgumentNullException.ThrowIfNull(bets);
        _bets = bets.OrderBy(b => b.Id, StringComparer.Ordinal).ToList();
    }

    /// <summary>Todas las apuestas, por id ascendente.</summary>
    public IReadOnlyList<BetDefinition> All => _bets;

    /// <summary>Ficha con ese id, o null.</summary>
    public BetDefinition? Find(string id)
    {
        for (int i = 0; i < _bets.Count; i++)
        {
            if (string.Equals(_bets[i].Id, id, StringComparison.Ordinal))
            {
                return _bets[i];
            }
        }

        return null;
    }

    /// <summary>Ficha de esa condición, o null.</summary>
    public BetDefinition? Find(BetKind kind)
    {
        for (int i = 0; i < _bets.Count; i++)
        {
            if (_bets[i].Kind == kind)
            {
                return _bets[i];
            }
        }

        return null;
    }
}

/// <summary>Carga <c>data/bets/bets.json</c> (RT-012: sin E/S, recibe el contenido ya leído).</summary>
public static class BetLoader
{
    private const string Path = "bets/bets.json";

    /// <summary>Catálogo de apuestas de la instantánea de ficheros indicada.</summary>
    public static BetCatalog FromJson(IReadOnlyDictionary<string, string> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        if (!files.TryGetValue(Path, out var content))
        {
            throw new DataException(Path, "$", "fichero requerido ausente");
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(content);
        }
        catch (JsonException ex)
        {
            throw new DataException(Path, "$", $"JSON inválido: {ex.Message}");
        }

        using (document)
        {
            var root = Json.Root(Path, document);
            var bets = new List<BetDefinition>();
            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            var seenKinds = new HashSet<BetKind>();
            foreach (var node in root.Prop("bets").EnumerateArray())
            {
                var bet = Parse(node);
                if (!seenIds.Add(bet.Id))
                {
                    throw new DataException(Path, node.Path + ".id", $"id de apuesta repetido: '{bet.Id}'");
                }

                if (!seenKinds.Add(bet.Kind))
                {
                    throw new DataException(Path, node.Path + ".kind", $"la condición {bet.Kind} ya tiene ficha: una apuesta por condición");
                }

                bets.Add(bet);
            }

            foreach (var kind in Enum.GetValues<BetKind>())
            {
                if (!seenKinds.Contains(kind))
                {
                    throw new DataException(Path, "$.bets", $"falta la ficha de la condición {kind}");
                }
            }

            return new BetCatalog(bets);
        }
    }

    private static BetDefinition Parse(Json node)
    {
        var kindNode = node.Prop("kind");
        string kindText = kindNode.AsString();
        if (!Enum.TryParse<BetKind>(kindText, ignoreCase: false, out var kind) || !Enum.IsDefined(kind))
        {
            throw new DataException(kindNode.File, kindNode.Path, $"condición de apuesta desconocida: '{kindText}'");
        }

        return new BetDefinition(
            node.Str("id"),
            kind,
            LocalizedNameJson.Read(node.Prop("name")),
            LocalizedNameJson.Read(node.Prop("condition")),
            ReadInts(node.Prop("stakeByAct"), 3, 1, int.MaxValue),
            ReadInts(node.Prop("payoutPercentByDifficulty"), 5, 100, 2000));
    }

    private static IReadOnlyList<int> ReadInts(Json array, int count, int min, int max)
    {
        var values = new List<int>(count);
        foreach (var item in array.EnumerateArray())
        {
            int value = item.AsInt();
            if (value < min || value > max)
            {
                throw new DataException(item.File, item.Path, $"valor {value} fuera de rango [{min}, {max}]");
            }

            values.Add(value);
        }

        if (values.Count != count)
        {
            throw new DataException(array.File, array.Path, $"se esperaban {count} valores y hay {values.Count}");
        }

        return values;
    }
}
