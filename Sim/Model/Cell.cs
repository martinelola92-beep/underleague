using Underleague.Sim.Engine;

namespace Underleague.Sim.Model;

/// <summary>Casilla discreta de la cuadrícula del campo.</summary>
public readonly record struct Cell(int Column, int Row);

/// <summary>Geometría del campo: 16x5 casillas, área de 2x3, coordenadas absolutas (RF-056, 3.1).</summary>
public static class Pitch
{
    public const int Columns = 16;
    public const int Rows = 5;
    public const int AreaColumns = 2;
    public const int AreaRows = 3;

    /// <summary>True si p está dentro del área que defiende team (0: X&lt;2; 1: X&gt;14), filas 1..4.</summary>
    public static bool IsInArea(Vec2 p, int team)
    {
        bool xInArea = team == 0 ? p.X < AreaColumns : p.X > Columns - AreaColumns;
        bool yInArea = p.Y >= 1f && p.Y <= AreaRows + 1f;
        return xInArea && yInArea;
    }

    /// <summary>Centro de la portería que ataca attackingTeam: team 0 -> (16, 2.5); team 1 -> (0, 2.5).</summary>
    public static Vec2 GoalCenter(int attackingTeam) =>
        attackingTeam == 0 ? new Vec2(Columns, Rows / 2f) : new Vec2(0f, Rows / 2f);

    /// <summary>Dirección de ataque de team: +1 para el equipo 0, -1 para el equipo 1.</summary>
    public static int AttackDirection(int team) => team == 0 ? 1 : -1;

    /// <summary>Tercio del campo en el que está p, relativo a team (Own = su tercio defensivo).</summary>
    public static Zone ZoneOf(Vec2 p, int team)
    {
        float third = Columns / 3f;
        int absoluteThird = p.X < third ? 0 : (p.X < 2 * third ? 1 : 2);
        if (absoluteThird == 1)
        {
            return Zone.Middle;
        }

        bool isOwnThird = team == 0 ? absoluteThird == 0 : absoluteThird == 2;
        return isOwnThird ? Zone.Own : Zone.Opposing;
    }

    /// <summary>True si a y b son casillas distintas y contiguas (incluidas las diagonales).</summary>
    public static bool AreAdjacent(Cell a, Cell b)
    {
        if (a == b)
        {
            return false;
        }

        return Math.Abs(a.Column - b.Column) <= 1 && Math.Abs(a.Row - b.Row) <= 1;
    }

    /// <summary>Centro continuo de la casilla c.</summary>
    public static Vec2 CellCenter(Cell c) => new(c.Column + 0.5f, c.Row + 0.5f);

    /// <summary>Casilla que contiene a p, acotada a la cuadrícula.</summary>
    public static Cell CellOf(Vec2 p)
    {
        int column = Math.Clamp((int)MathF.Floor(p.X), 0, Columns - 1);
        int row = Math.Clamp((int)MathF.Floor(p.Y), 0, Rows - 1);
        return new Cell(column, row);
    }
}
