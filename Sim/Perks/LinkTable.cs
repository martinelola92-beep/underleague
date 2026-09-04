using Underleague.Sim.Engine;
using Underleague.Sim.Model;

namespace Underleague.Sim.Perks;

/// <summary>
/// Zona de inicio de un jugador según la columna de su casilla-hogar, relativa a su sentido de ataque
/// (función de condición <c>startsIn</c>, docs/perks-ejes.md).
/// </summary>
internal enum StartZone
{
    OwnThird,
    Middle,
    AttackingThird,
}

/// <summary>
/// Banda de inicio de un jugador según la fila de su casilla-hogar, vista desde un jugador que mira a la
/// portería rival (función de condición <c>startsOn</c>). El visitante refleja las bandas (ADR 0021).
/// </summary>
internal enum StartFlank
{
    LeftFlank,
    Center,
    RightFlank,
}

/// <summary>
/// Geometría de la colocación en coordenadas **relativas al sentido de ataque** (RF-044, ADR 0021):
/// "adelante" es siempre hacia la portería rival e "izquierda"/"derecha" se toman desde un jugador que
/// mira hacia ella, de modo que un mismo perk describe la misma estructura para los dos equipos.
/// <para>
/// Todo es aritmética entera sobre <see cref="Cell"/> (RT-023): la colocación no cambia durante el
/// partido, así que ni las posiciones continuas ni el reloj entran aquí.
/// </para>
/// </summary>
internal static class LinkGeometry
{
    /// <summary>Avance relativo: columnas que <paramref name="other"/> está por delante de <paramref name="self"/>.</summary>
    public static int Forward(Cell self, Cell other, int team) =>
        (other.Column - self.Column) * Pitch.AttackDirection(team);

    /// <summary>
    /// Desplazamiento relativo hacia la **derecha** del jugador. El equipo 0 ataca hacia columnas
    /// crecientes, así que su derecha son filas crecientes; el equipo 1 ataca al revés y refleja también
    /// las bandas, que es lo que hace que <c>left</c> signifique lo mismo en las dos alineaciones.
    /// </summary>
    public static int Rightward(Cell self, Cell other, int team) =>
        (other.Row - self.Row) * (team == 0 ? 1 : -1);

    /// <summary>Tercio de inicio de la casilla-hogar, relativo al sentido de ataque del equipo.</summary>
    public static StartZone ZoneOfHome(Cell home, int team)
    {
        // Tercio absoluto por división entera: columnas 0-5, 6-10 y 11-15 en un campo de 16 (RT-023).
        int third = home.Column * 3 / Pitch.Columns;
        if (third == 1)
        {
            return StartZone.Middle;
        }

        bool own = team == 0 ? third == 0 : third == 2;
        return own ? StartZone.OwnThird : StartZone.AttackingThird;
    }

    /// <summary>Banda de inicio de la casilla-hogar, vista desde el jugador mirando a la portería rival.</summary>
    public static StartFlank FlankOfHome(Cell home, int team)
    {
        int center = Pitch.Rows / 2;
        if (home.Row == center)
        {
            return StartFlank.Center;
        }

        bool lowRow = home.Row < center;
        bool left = team == 0 ? lowRow : !lowRow;
        return left ? StartFlank.LeftFlank : StartFlank.RightFlank;
    }

    /// <summary>
    /// True si <paramref name="other"/> cumple la relación <paramref name="relation"/> respecto de
    /// <paramref name="self"/>. El radio de la ADR 0011 (Chebyshev &lt;= 2 entre casillas-hogar) acota
    /// todas las relaciones; las que fijan las dos coordenadas ya quedan dentro por construcción.
    /// </summary>
    public static bool Matches(Cell self, Cell other, int team, LinkRelation relation)
    {
        int forward = Forward(self, other, team);
        int right = Rightward(self, other, team);
        if (Math.Abs(forward) > 2 || Math.Abs(right) > 2)
        {
            return false;
        }

        return relation switch
        {
            LinkRelation.Beside => forward == 0 && Math.Abs(right) == 1,
            LinkRelation.Ahead => forward == 1 && Math.Abs(right) <= 1,
            LinkRelation.Behind => forward == -1 && Math.Abs(right) <= 1,
            LinkRelation.Left => right == -1,
            LinkRelation.Right => right == 1,
            LinkRelation.DiagonalAhead => forward == 1 && Math.Abs(right) == 1,
            LinkRelation.DiagonalBehind => forward == -1 && Math.Abs(right) == 1,
            _ => false,
        };
    }

    /// <summary>Distancia al cuadrado entre casillas-hogar, en casillas; entera, para desempatar sin flotantes.</summary>
    public static int SquaredDistance(Cell a, Cell b)
    {
        int dc = a.Column - b.Column;
        int dr = a.Row - b.Row;
        return (dc * dc) + (dr * dr);
    }
}

/// <summary>
/// Vínculos direccionales del partido (RF-044, ADR 0021, §2.4). Se resuelven **una sola vez** al
/// construir el motor de efectos, a partir de las casillas-hogar de la alineación, y no se vuelven a
/// tocar: durante el partido consultarlos cuesta un acceso a array.
/// <para>
/// Un candidato por relación: el más cercano por distancia entre casillas-hogar y, a igual distancia, el
/// de id ascendente. Si no hay candidato **no hay vínculo**, y el perk aplica sus <c>elseEffects</c>:
/// eso es lo que convierte la formación en una decisión con coste.
/// </para>
/// </summary>
internal sealed class LinkTable
{
    private static readonly int RelationCount = Enum.GetValues<LinkRelation>().Length;

    private readonly MatchPlayer[] _players;
    private readonly int[] _links;

    /// <summary>Resuelve la tabla completa para los jugadores dados (ya ordenados por id ascendente).</summary>
    public LinkTable(MatchPlayer[] players)
    {
        _players = players;
        _links = new int[players.Length * RelationCount];
        for (int i = 0; i < _links.Length; i++)
        {
            _links[i] = -1;
        }

        for (int i = 0; i < players.Length; i++)
        {
            var self = players[i];
            for (int r = 0; r < RelationCount; r++)
            {
                _links[(i * RelationCount) + r] = Resolve(players, i, self, (LinkRelation)r);
            }
        }
    }

    /// <summary>Índice del vinculado de player en esa relación; -1 si no hay candidato.</summary>
    public int LinkedIndex(MatchPlayer player, LinkRelation relation) =>
        _links[(player.Index * RelationCount) + (int)relation];

    /// <summary>Vinculado de player en esa relación, o null si no hay ninguno.</summary>
    public MatchPlayer? Linked(MatchPlayer player, LinkRelation relation)
    {
        int index = LinkedIndex(player, relation);
        return index < 0 ? null : _players[index];
    }

    /// <summary>True si player tiene vínculo en esa relación (función de condición <c>linked</c>).</summary>
    public bool HasLink(MatchPlayer player, LinkRelation relation) => LinkedIndex(player, relation) >= 0;

    /// <summary>True si other es el vinculado de player en alguna de las relaciones indicadas.</summary>
    public bool IsLinked(MatchPlayer player, MatchPlayer other, IReadOnlyList<LinkRelation> relations)
    {
        for (int i = 0; i < relations.Count; i++)
        {
            if (LinkedIndex(player, relations[i]) == other.Index)
            {
                return true;
            }
        }

        return false;
    }

    private static int Resolve(MatchPlayer[] players, int selfIndex, MatchPlayer self, LinkRelation relation)
    {
        int best = -1;
        int bestDistance = int.MaxValue;
        for (int j = 0; j < players.Length; j++)
        {
            var other = players[j];
            if (j == selfIndex || other.Team != self.Team)
            {
                continue;
            }

            if (!LinkGeometry.Matches(self.HomeCell, other.HomeCell, self.Team, relation))
            {
                continue;
            }

            int distance = LinkGeometry.SquaredDistance(self.HomeCell, other.HomeCell);

            // El array llega ordenado por id ascendente (RT-041), así que recorrerlo en orden y quedarse
            // con el estrictamente más cercano ya desempata por id ascendente sin comparación adicional.
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = j;
            }
        }

        return best;
    }
}
