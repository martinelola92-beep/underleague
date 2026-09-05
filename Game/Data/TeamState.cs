using System;
using System.Collections.Generic;
using Underleague.Game.Autoload;
using Underleague.Sim.Data;
using Underleague.Sim.Generation;
using Underleague.Sim.Model;
using Underleague.Sim.Placement;
using Underleague.Sim.Random;
using Underleague.Sim.Run;

namespace Underleague.Game.Data;

/// <summary>
/// Lo que la pantalla de Equipo necesita saber de un equipo: el catálogo con el que se lee y la plantilla
/// con su alineación. No hay datos falsos incrustados en ninguna parte de la pantalla.
/// <para>
/// Tiene tres orígenes y ninguno de ellos es la escena: la <b>run en curso</b>
/// (<see cref="FromRun"/>), un equipo ya construido como el rival del ojeo (<see cref="Of"/>) y la
/// plantilla de pruebas con la que la pantalla se diseñó (<see cref="Load"/>).
/// </para>
/// <para>
/// Ninguna regla de juego vive aquí (RT-014): mover a un jugador lo resuelve
/// <see cref="PlacementView.WithPlayerAt"/> y guardarlo, <c>RunEngine.Apply(SetLineup)</c>.
/// </para>
/// </summary>
public sealed class TeamState
{
    /// <summary>Idioma de la interfaz. En fase 4 lo elige el jugador (RT-073); hasta entonces, español.</summary>
    public const string Language = GameData.Language;

    private TeamState(Catalog catalog, TeamSetup team, RunController? run = null)
    {
        Catalog = catalog;
        Team = team;
        Templates = catalog.Localization.Get(Language);
        _run = run;
    }

    private readonly RunController? _run;

    public Catalog Catalog { get; }

    public TeamSetup Team { get; private set; }

    public DescriptionTemplates Templates { get; }

    public IReadOnlyList<PlayerDefinition> Players => Team.Players;

    public Lineup Lineup => Team.Lineup;

    /// <summary>
    /// La plantilla de la <b>run en curso</b> (RT-030): la que se alinea de verdad. Los jugadores se
    /// convierten con <c>RunPlayer.ToDefinition</c>, que es la misma conversión con la que el motor los
    /// manda al campo —penalización de lesión leve incluida (RF-091)—, de modo que lo que la ficha
    /// enseña es lo que va a jugar.
    /// </summary>
    public static TeamState FromRun(RunController run)
    {
        ArgumentNullException.ThrowIfNull(run);
        var catalog = run.Catalog ?? throw new InvalidOperationException("la run no tiene catálogo cargado");
        return new TeamState(catalog, TeamOf(run.State!, catalog), run);
    }

    /// <summary>
    /// Un equipo cualquiera ya construido: lo que necesita el informe de ojeo para enseñar la plantilla
    /// rival con las mismas fichas que la propia (UI-010). No hay run detrás, así que no se puede mover a
    /// nadie.
    /// </summary>
    public static TeamState Of(Catalog catalog, TeamSetup team) => new(catalog, team);

    private static TeamSetup TeamOf(RunState state, Catalog catalog)
    {
        var players = new List<PlayerDefinition>(state.Roster.Count);
        for (int i = 0; i < state.Roster.Count; i++)
        {
            players.Add(state.Roster[i].ToDefinition(catalog));
        }

        return new TeamSetup(state.ClubId, state.ClubId, state.ClubRace, players, state.Lineup);
    }

    /// <summary>
    /// Carga <c>/data</c> y genera la plantilla con la semilla dada. Es el equipo de pruebas con el que
    /// la pantalla de Equipo se diseñó y con el que se regeneran sus capturas; una run de verdad entra
    /// por <see cref="FromRun"/>.
    /// </summary>
    public static TeamState Load(ulong seed)
    {
        var catalog = DataLoader.FromJson(GameData.Snapshot);

        var generation = RngStreams.Generation(seed, 0);
        var team = TeamGenerator.Generate(ref generation, catalog, "underleague_fc", Race.Orc, quality: 55, firstPlayerId: 1, level: 3);

        var rewards = RngStreams.Rewards(seed, 0);
        var withPerks = PerkAssignment.AssignInitial(ref rewards, team.Players, catalog);
        return new TeamState(catalog, team with { Players = withPerks });
    }

    /// <summary>Jugador por id, o null si no está en la plantilla.</summary>
    public PlayerDefinition? Find(int id)
    {
        for (int i = 0; i < Players.Count; i++)
        {
            if (Players[i].Id == id)
            {
                return Players[i];
            }
        }

        return null;
    }

    /// <summary>Jugador alineado en esa casilla, o null.</summary>
    public PlayerDefinition? At(Cell cell)
    {
        foreach (var slot in Lineup.Slots)
        {
            if (slot.HomeCell == cell)
            {
                return Find(slot.PlayerId);
            }
        }

        return null;
    }

    /// <summary>Casilla-hogar del jugador, o null si está en el banquillo.</summary>
    public Cell? CellOf(int playerId)
    {
        foreach (var slot in Lineup.Slots)
        {
            if (slot.PlayerId == playerId)
            {
                return slot.HomeCell;
            }
        }

        return null;
    }

    /// <summary>True si el jugador está en la alineación.</summary>
    public bool IsStarter(int playerId) => CellOf(playerId) is not null;

    /// <summary>Alineación resultante de dejar al jugador en esa casilla, <b>sin</b> aplicarla (RF-045: previsualización).</summary>
    public Lineup Preview(int playerId, Cell target) => PlacementView.WithPlayerAt(Lineup, Players, playerId, target);

    /// <summary>
    /// Aplica el movimiento. La regla es de <c>/Sim</c>; aquí solo se pide y se guarda el resultado.
    /// <para>
    /// Con una run detrás, la alineación no se guarda en esta clase: se le manda al motor como
    /// <c>SetLineup</c>, que es la única puerta por la que una decisión del jugador entra en el estado
    /// —y la que deja anotado quién sale al campo arrastrando una lesión grave (RF-093 vía 1)—. La
    /// pantalla no guarda una copia paralela de nada.
    /// </para>
    /// </summary>
    public bool Move(int playerId, Cell target)
    {
        var next = Preview(playerId, target);
        if (ReferenceEquals(next, Lineup))
        {
            return false;
        }

        if (_run is { HasRun: true })
        {
            _run.Apply(new SetLineup(next));
            Team = TeamOf(_run.State!, Catalog);
            return true;
        }

        Team = Team with { Lineup = next };
        return true;
    }
}
