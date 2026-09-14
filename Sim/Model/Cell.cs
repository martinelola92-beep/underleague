using Underleague.Sim.Engine;

namespace Underleague.Sim.Model;

/// <summary>Casilla discreta de la cuadrícula del campo.</summary>
public readonly record struct Cell(int Column, int Row);

/// <summary>Geometría del campo: 16x7 casillas, área de 2x4, coordenadas absolutas (RF-056, 3.1, ADR 0103, campo de siete filas).</summary>
public static class Pitch
{
    public const int Columns = 16;
    // 7 (13 sep 2026, decisión del revisor, sucesora de la ADR 0103): con seis filas no hay fila central
    // -el centro geométrico cae entre la 2 y la 3- y el portero, el delantero único y el pivote único
    // quedaban media casilla descentrados. Con siete el número es impar y vuelve a existir una única fila
    // central (Rows / 2 = 3). La cámara en tres cuartos también encaja mejor: 16 / (7 · sen 60°) = 2,64
    // contra un marco de pantalla de 2,67, más ajustado que el 3,08 que daba con seis filas y 60°.
    public const int Rows = 7;
    /// <summary>
    /// Columnas de colocación de una alineación, contadas desde la portería propia (RF-040..045): una
    /// casilla-hogar vive en 0..7 y el motor refleja la columna para el equipo 1. Los tercios de inicio
    /// (<c>startsIn</c>) se miden sobre este tramo, no sobre las 16 columnas del campo: con 16 el tercio
    /// atacante caía en las columnas 11-15, donde ningún jugador puede colocarse, y todo perk que lo
    /// exigiera era letra muerta (corregido en el paquete U).
    /// </summary>
    public const int PlacementColumns = 8;

    public const int AreaColumns = 2;
    // 4 (se mantiene igual que con seis filas): con siete filas, 4 de 7 (57 %) es lo más cercano a
    // conservar el 60 % histórico (3 de 5 con el campo original) sin pasarse: 5 de 7 daría el 71 %,
    // demasiado, y bajar a 3 encogería el dominio del portero y agravaría D-21.
    public const int AreaRows = 4;

    /// <summary>True si p está dentro del área que defiende team (0: X&lt;2; 1: X&gt;14), filas 1..5.</summary>
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
