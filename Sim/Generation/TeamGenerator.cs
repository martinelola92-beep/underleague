using Underleague.Sim.Data;
using Underleague.Sim.Model;
using Underleague.Sim.Random;

namespace Underleague.Sim.Generation;

/// <summary>Generación procedural de un equipo completo (10 jugadores) a partir de raza y calidad.</summary>
public static class TeamGenerator
{
    private static readonly Position[] StarterPositions =
    {
        Position.Goalkeeper, Position.Defender, Position.Defender,
        Position.Midfielder, Position.Midfielder, Position.Midfielder, Position.Forward,
    };

    private static readonly Position[] SubstitutePositions =
    {
        Position.Defender, Position.Midfielder, Position.Forward,
    };

    /// <summary>
    /// 10 jugadores: titulares GK, DEF, DEF, MID, MID, MID, FWD (ids firstId..firstId+6) y suplentes
    /// DEF, MID, FWD (firstId+7..firstId+9). Uno de los 10 es Rare (RF-005), elegido con rng, salvo que
    /// <paramref name="uniformRarity"/> fije la rareza de toda la plantilla.
    /// Decisión fuera de la especificación: Name del equipo se fija igual a teamId (Generate no recibe
    /// un nombre de equipo separado).
    ///
    /// <para><b>Calidad y nivel son dos diales independientes</b> (paquete U). <paramref name="quality"/>
    /// es el dial de fuerza de <c>/Balance</c> (RT-052) y significa la media objetivo de atributos del
    /// equipo: <see cref="PlayerGenerator"/> lo aplica desplazando presupuesto y banda punto por punto
    /// respecto de <see cref="PlayerGenerator.QualityPivot"/>, así que un equipo de calidad 60 tiene
    /// veinte puntos más en cada atributo que uno de calidad 40. <paramref name="level"/> es la
    /// progresión dentro de la run (1..8, RF-023/Progression.MaxLevel) y vale 8 puntos de presupuesto por
    /// nivel.</para>
    ///
    /// <para>Hasta el paquete Q este método traducía la calidad a <c>nivel = Clamp(quality / 10, 1, 8)</c>
    /// e ignoraba el dial: calidad 60 contra calidad 40 eran dos niveles, 16 puntos de presupuesto sobre
    /// unos 290, y <c>betterTeamWinRate_60_vs_40</c> medía la varianza de la plantilla y no la diferencia
    /// de calidad (40,8% para el equipo "mejor" de calidad 60 contra uno de 50, medido en 2.000 partidos
    /// justo antes del arreglo). Es el defecto que el paquete U corrige.</para>
    /// </summary>
    /// <param name="styleBySlot">
    /// Instrumento de <c>/Balance</c>: impone la etiqueta de estilo del jugador de ese índice (0..9) en
    /// vez de sortearla. Lo necesitan las builds que prueban un perk con <c>tagsRequired</c> sobre una
    /// etiqueta de estilo: sin imponerla, la build es válida solo cuando el dado la da (un elfo Bulwark
    /// sale el 12% de las veces) y el lote se cae al validar. El dado se tira igual, así que el flujo de
    /// RNG no cambia; el sesgo de atributos del estilo impuesto sí se aplica.
    /// </param>
    /// <param name="extraTraitsBySlot">
    /// Instrumento de <c>/Balance</c>: rasgos que se añaden al jugador de ese índice, para las builds que
    /// prueban un perk con <c>tagsRequired</c> sobre un rasgo (por ejemplo <c>Leader</c>).
    /// </param>
    public static TeamSetup Generate(
        ref Pcg32 rng,
        Catalog catalog,
        string teamId,
        Race race,
        int quality,
        int firstPlayerId,
        int level = 1,
        Rarity? uniformRarity = null,
        IReadOnlyDictionary<int, StyleTag>? styleBySlot = null,
        IReadOnlyDictionary<int, IReadOnlyList<Trait>>? extraTraitsBySlot = null)
    {
        var raceDefinition = catalog.Race(race);
        var nameGenerator = new NameGenerator(raceDefinition);
        var usedNames = new HashSet<string>(StringComparer.Ordinal);

        int totalPlayers = StarterPositions.Length + SubstitutePositions.Length;
        int rareIndex = rng.Range(0, totalPlayers);

        var players = new List<PlayerDefinition>(totalPlayers);
        int index = 0;
        foreach (var position in StarterPositions)
        {
            players.Add(WithExtraTraits(GeneratePlayer(ref rng, catalog, raceDefinition, nameGenerator, usedNames, position, RarityOf(uniformRarity, index, rareIndex), level, firstPlayerId + index, quality, StyleOf(styleBySlot, index)), extraTraitsBySlot, index));
            index++;
        }

        foreach (var position in SubstitutePositions)
        {
            players.Add(WithExtraTraits(GeneratePlayer(ref rng, catalog, raceDefinition, nameGenerator, usedNames, position, RarityOf(uniformRarity, index, rareIndex), level, firstPlayerId + index, quality, StyleOf(styleBySlot, index)), extraTraitsBySlot, index));
            index++;
        }

        var starters = players.Take(StarterPositions.Length).ToList();
        var lineup = Lineup.Default(starters);

        return new TeamSetup(teamId, teamId, race, players, lineup);
    }

    /// <summary>Rareza del jugador <paramref name="index"/>: la uniforme si se pide, o Rare solo para el elegido por el dado (RF-005).</summary>
    private static Rarity RarityOf(Rarity? uniformRarity, int index, int rareIndex) =>
        uniformRarity ?? (index == rareIndex ? Rarity.Uncommon : Rarity.Common);

    private static StyleTag? StyleOf(IReadOnlyDictionary<int, StyleTag>? styleBySlot, int index) =>
        styleBySlot is not null && styleBySlot.TryGetValue(index, out var style) ? style : null;

    private static PlayerDefinition GeneratePlayer(
        ref Pcg32 rng,
        Catalog catalog,
        RaceDefinition race,
        NameGenerator nameGenerator,
        HashSet<string> usedNames,
        Position position,
        Rarity rarity,
        int level,
        int id,
        int quality,
        StyleTag? forcedStyle)
    {
        string name;
        do
        {
            name = nameGenerator.Next(ref rng);
        }
        while (!usedNames.Add(name));

        return PlayerGenerator.Generate(ref rng, catalog, race, position, rarity, level, id, name, quality, forcedStyle);
    }

    /// <summary>
    /// Añade los rasgos impuestos por <c>extraTraitsBySlot</c> al jugador del índice dado, sin repetir, y
    /// mantiene <c>Tags</c> coherente con <c>Traits</c> (ADR 0024: Tags = especie, estilo, posición y
    /// rasgos). Los rasgos se añaden después de generar porque no intervienen en el reparto de atributos.
    /// </summary>
    private static PlayerDefinition WithExtraTraits(PlayerDefinition player, IReadOnlyDictionary<int, IReadOnlyList<Trait>>? extraTraitsBySlot, int index)
    {
        if (extraTraitsBySlot is null || !extraTraitsBySlot.TryGetValue(index, out var extra) || extra.Count == 0)
        {
            return player;
        }

        var traits = new List<Trait>(player.Traits);
        var tags = new List<string>(player.Tags);
        foreach (var trait in extra)
        {
            if (traits.Contains(trait))
            {
                continue;
            }

            traits.Add(trait);
            tags.Add(trait.ToString());
        }

        return player with { Traits = traits, Tags = tags };
    }
}
