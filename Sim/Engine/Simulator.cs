using Underleague.Sim.Data;
using Underleague.Sim.Events;
using Underleague.Sim.Model;

namespace Underleague.Sim.Engine;

/// <summary>
/// Configuración de una simulación de partido.
/// </summary>
/// <param name="DumpUtility">
/// Si no es null, vuelca la tabla de utilidad (RT-098) de PlayerId. El jugador solo decide cada
/// tuning.decisionIntervalTicks ticks (desplazado por su id), así que el volcado no espera al tick exacto:
/// captura la PRIMERA decisión de ese jugador en un tick >= Tick, una sola vez por partido.
/// </param>
public sealed record SimConfig(bool CollectLog = true, (int PlayerId, int Tick)? DumpUtility = null, int? RegulationTicksOverride = null)
{
    /// <summary>Configuración por defecto: con log, sin volcado de utilidad, duración reglamentaria estándar.</summary>
    public static SimConfig Default { get; } = new();
}

/// <summary>Resultado de simular un partido completo: secuencia ordenada de eventos e informe final.</summary>
public sealed record MatchResult(IReadOnlyList<MatchEvent> Events, MatchReport Report);

/// <summary>Punto de entrada público del simulador de partidos (RT-013).</summary>
public static class Simulator
{
    /// <summary>Titulares mínimos y máximos por equipo (RF-059, §2.2).</summary>
    private const int MinStarters = 5;
    private const int MaxStarters = 7;

    /// <summary>Última columna relativa válida para una casilla-hogar (§2.2).</summary>
    private const int MaxHomeColumn = 7;

    /// <summary>
    /// Puro: sin efectos secundarios, sin E/S, sin reloj. Valida el setup (lanza ArgumentException con
    /// mensaje claro) y ejecuta el partido completo devolviendo eventos e informe (§3).
    /// </summary>
    public static MatchResult Run(MatchSetup setup, ulong seed, Catalog catalog, SimConfig config)
    {
        ArgumentNullException.ThrowIfNull(setup);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(config);
        Validate(setup, catalog);

        var engine = new MatchEngine(setup, seed, catalog, config);
        return engine.Run();
    }

    private static void Validate(MatchSetup setup, Catalog catalog)
    {
        if (setup.Home is null || setup.Away is null)
        {
            throw new ArgumentException("el MatchSetup debe tener equipo local y visitante", nameof(setup));
        }

        if (setup.Referee is null)
        {
            throw new ArgumentException("el MatchSetup debe tener árbitro", nameof(setup));
        }

        ValidateTeam(setup.Home, "Home", catalog);
        ValidateTeam(setup.Away, "Away", catalog);

        for (int i = 0; i < setup.Home.Players.Count; i++)
        {
            int id = setup.Home.Players[i].Id;
            for (int j = 0; j < setup.Away.Players.Count; j++)
            {
                if (setup.Away.Players[j].Id == id)
                {
                    throw new ArgumentException(
                        $"los identificadores de jugador deben ser únicos en el partido: {id} aparece en ambos equipos",
                        nameof(setup));
                }
            }
        }
    }

    private static void ValidateTeam(TeamSetup team, string side, Catalog catalog)
    {
        if (team.Players is null || team.Players.Count == 0)
        {
            throw new ArgumentException($"el equipo {side} ('{team.Id}') no tiene jugadores", nameof(team));
        }

        for (int i = 0; i < team.Players.Count; i++)
        {
            for (int j = i + 1; j < team.Players.Count; j++)
            {
                if (team.Players[i].Id == team.Players[j].Id)
                {
                    throw new ArgumentException(
                        $"el equipo {side} ('{team.Id}') repite el identificador de jugador {team.Players[i].Id}",
                        nameof(team));
                }
            }

            var traits = team.Players[i].Traits;
            for (int t = 0; t < traits.Count; t++)
            {
                _ = catalog.Trait(traits[t]);
            }
        }

        if (team.Lineup is null || team.Lineup.Slots is null)
        {
            throw new ArgumentException($"el equipo {side} ('{team.Id}') no tiene alineación", nameof(team));
        }

        var slots = team.Lineup.Slots;
        if (slots.Count < MinStarters || slots.Count > MaxStarters)
        {
            throw new ArgumentException(
                $"el equipo {side} ('{team.Id}') alinea {slots.Count} titulares; deben ser entre {MinStarters} y {MaxStarters}",
                nameof(team));
        }

        int goalkeepers = 0;
        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            PlayerDefinition? definition = null;
            for (int j = 0; j < team.Players.Count; j++)
            {
                if (team.Players[j].Id == slot.PlayerId)
                {
                    definition = team.Players[j];
                    break;
                }
            }

            if (definition is null)
            {
                throw new ArgumentException(
                    $"el equipo {side} ('{team.Id}') alinea al jugador {slot.PlayerId}, que no está en su plantilla",
                    nameof(team));
            }

            if (definition.Position == Position.Goalkeeper)
            {
                goalkeepers++;
            }

            if (slot.HomeCell.Column < 0 || slot.HomeCell.Column > MaxHomeColumn
                || slot.HomeCell.Row < 0 || slot.HomeCell.Row >= Pitch.Rows)
            {
                throw new ArgumentException(
                    $"el equipo {side} ('{team.Id}') coloca al jugador {slot.PlayerId} en la casilla "
                        + $"({slot.HomeCell.Column},{slot.HomeCell.Row}); debe estar en 0..{MaxHomeColumn} x 0..{Pitch.Rows - 1}",
                    nameof(team));
            }

            // El portero se refleja tal cual para el equipo 1 (MatchEngine.AddTeam solo invierte la
            // columna), así que su casilla-hogar en coordenadas relativas debe caer ya dentro de su propia
            // área: columna dentro de las AreaColumns desde su portería y fila en 1..AreaRows. Sin esta
            // comprobación, un portero alineado fuera del área nunca dispara GoalkeeperLeftArea porque
            // "salir" no tiene sentido si nunca estuvo dentro (revisión independiente, fase 0).
            if (definition.Position == Position.Goalkeeper
                && (slot.HomeCell.Column >= Pitch.AreaColumns || slot.HomeCell.Row < 1 || slot.HomeCell.Row > Pitch.AreaRows))
            {
                throw new ArgumentException(
                    $"el equipo {side} ('{team.Id}') alinea al portero {slot.PlayerId} en la casilla "
                        + $"({slot.HomeCell.Column},{slot.HomeCell.Row}); debe estar dentro de su área "
                        + $"(columna < {Pitch.AreaColumns}, fila entre 1 y {Pitch.AreaRows})",
                    nameof(team));
            }

            for (int j = i + 1; j < slots.Count; j++)
            {
                if (slots[j].HomeCell == slot.HomeCell)
                {
                    throw new ArgumentException(
                        $"el equipo {side} ('{team.Id}') repite la casilla-hogar ({slot.HomeCell.Column},{slot.HomeCell.Row})",
                        nameof(team));
                }

                if (slots[j].PlayerId == slot.PlayerId)
                {
                    throw new ArgumentException(
                        $"el equipo {side} ('{team.Id}') alinea dos veces al jugador {slot.PlayerId}",
                        nameof(team));
                }
            }
        }

        if (goalkeepers != 1)
        {
            throw new ArgumentException(
                $"el equipo {side} ('{team.Id}') alinea {goalkeepers} porteros; debe alinear exactamente 1",
                nameof(team));
        }
    }
}
