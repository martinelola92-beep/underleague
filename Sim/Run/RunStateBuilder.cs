using Underleague.Sim.Data;
using Underleague.Sim.Model;

namespace Underleague.Sim.Run;

/// <summary>
/// Modo de depuración (RT-062): construye un <see cref="RunState"/> arbitrario -acto 2 con una
/// plantilla concreta, por ejemplo- <b>sin jugar los nodos previos</b>.
///
/// <para>Parte siempre de un <see cref="RunEngine.Start"/> real, de modo que el estado resultante es
/// consistente: tiene los tres mapas de esa semilla, sus árbitros y su instantánea de <c>/data</c>. Lo
/// que hace el constructor es <i>saltar</i> a la situación pedida. La otra vía de RT-062, cargar un
/// estado predefinido escrito a mano, es <see cref="Save.RunSave.Load"/> (el guardado admite un estado
/// sin instantánea de datos, precisamente para esto).</para>
/// </summary>
public sealed class RunStateBuilder
{
    private RunState _state;

    private RunStateBuilder(RunState state) => _state = state;

    /// <summary>Empieza una run normal y devuelve el constructor para saltar desde ahí.</summary>
    public static RunStateBuilder From(RunSetup setup, ulong seed, Catalog catalog, IRunSystems? systems = null) =>
        new(RunEngine.Start(setup, seed, catalog, systems));

    /// <summary>Continúa desde un estado ya existente (por ejemplo, uno cargado del guardado).</summary>
    public static RunStateBuilder From(RunState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return new RunStateBuilder(state);
    }

    /// <summary>Sitúa la run en la entrada del acto indicado, 1..3, sin jugar los actos anteriores.</summary>
    public RunStateBuilder AtAct(int act)
    {
        _state = _state.WithAct(act);
        return this;
    }

    /// <summary>Sitúa la run en un nodo concreto, que debe pertenecer al acto actual.</summary>
    public RunStateBuilder AtNode(int nodeId)
    {
        var node = _state.CurrentMap.Find(nodeId)
            ?? throw new ArgumentOutOfRangeException(nameof(nodeId), nodeId, $"el nodo no pertenece al acto {_state.Act}");
        _state = _state.WithCurrentNode(node.Id).WithPhase(RunPhase.OnMap).WithPendingNode(-1);
        return this;
    }

    /// <summary>
    /// Sitúa la run justo antes del jefe del acto actual: en un nodo cualquiera de la capa anterior, de
    /// modo que <see cref="RunEngine.AvailableNodes"/> devuelva el jefe. Es el salto que más se usa para
    /// probar la victoria y la derrota de RF-002b.
    /// </summary>
    public RunStateBuilder BeforeBoss()
    {
        var map = _state.CurrentMap;
        var boss = map.Get(map.BossNodeId);
        for (int i = 0; i < map.Nodes.Count; i++)
        {
            var node = map.Nodes[i];
            if (node.Layer != boss.Layer - 1)
            {
                continue;
            }

            for (int e = 0; e < node.Next.Count; e++)
            {
                if (node.Next[e] == boss.Id)
                {
                    return AtNode(node.Id);
                }
            }
        }

        throw new InvalidOperationException($"el mapa del acto {_state.Act} no tiene ningún nodo que lleve al jefe");
    }

    /// <summary>Fija el oro.</summary>
    public RunStateBuilder WithGold(int gold)
    {
        _state = _state.WithGold(gold);
        return this;
    }

    /// <summary>Sustituye la plantilla entera y recalcula la alineación por defecto.</summary>
    public RunStateBuilder WithRoster(IEnumerable<RunPlayer> roster)
    {
        _state = _state.WithRoster(roster);
        _state = _state.WithLineup(RunLineup.Default(_state));
        return this;
    }

    /// <summary>Sustituye a un jugador de la plantilla por otro con el mismo id.</summary>
    public RunStateBuilder WithPlayer(RunPlayer player)
    {
        _state = _state.WithPlayer(player);
        return this;
    }

    /// <summary>
    /// Fija el estado físico de un jugador. Con esto se llega en una línea a la situación de RF-002b:
    /// una plantilla con exactamente cinco disponibles.
    /// </summary>
    public RunStateBuilder WithPlayerState(int playerId, PhysicalState physicalState)
    {
        var player = _state.GetPlayer(playerId);
        _state = _state.WithPlayer(player with
        {
            PhysicalState = physicalState,
            MinorInjuries = physicalState == PhysicalState.MinorInjury ? Math.Max(player.MinorInjuries, 1) : 0,
        });
        return this;
    }

    /// <summary>Deja exactamente <paramref name="count"/> jugadores disponibles, apartando a los de mayor id con lesión grave.</summary>
    public RunStateBuilder WithAvailablePlayers(int count)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "el número de disponibles no puede ser negativo");
        }

        var roster = new List<RunPlayer>(_state.Roster);
        for (int i = roster.Count - 1; i >= 0; i--)
        {
            int available = 0;
            for (int j = 0; j < roster.Count; j++)
            {
                if (roster[j].IsAvailable)
                {
                    available++;
                }
            }

            if (available <= count)
            {
                break;
            }

            if (roster[i].IsAvailable)
            {
                roster[i] = roster[i] with { PhysicalState = PhysicalState.SevereInjury, MinorInjuries = 0 };
            }
        }

        _state = _state.WithRoster(roster);
        return this;
    }

    /// <summary>Fija la alineación.</summary>
    public RunStateBuilder WithLineup(Lineup lineup)
    {
        _state = _state.WithLineup(lineup);
        return this;
    }

    /// <summary>Fija un contador de run (los que usan los paquetes X e Y).</summary>
    public RunStateBuilder WithCounter(string name, int value)
    {
        _state = _state.WithCounter(name, value);
        return this;
    }

    /// <summary>Devuelve el estado construido.</summary>
    public RunState Build() => _state;
}
