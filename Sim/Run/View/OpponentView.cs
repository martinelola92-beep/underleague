using Underleague.Sim.Run.Bosses;
using Underleague.Sim.Run.Systems.Rivals;

namespace Underleague.Sim.Run.View;

/// <summary>
/// Cómo se presenta el rival de un nodo de partido (BH-B): su nombre y, si el dato lo trae, su descripción.
/// </summary>
/// <param name="Id">Id de datos del rival: el del clan de catálogo o el del jefe.</param>
/// <param name="Name">Nombre en el idioma pedido («Turno de Ultratumba», «La Cacería»).</param>
/// <param name="Description">Línea de ojeo del clan; vacía en un jefe, que en <c>data/bosses/</c> no la tiene.</param>
/// <param name="IsBoss">True si el rival es el jefe del acto, no un clan de la liga.</param>
public sealed record OpponentCard(string Id, string Name, string Description, bool IsBoss);

/// <summary>
/// Quién se presenta como rival en un nodo, para el mapa y el ojeo. Existe porque el nodo de jefe guarda un
/// <c>OpponentId</c> <b>fantasma</b> (BE-F): sintácticamente el de un clan de la liga, pero el equipo lo construye
/// <c>BossRunSystems</c> desde <c>data/bosses/</c>. Las pantallas resolvían ese id contra el catálogo de clanes y
/// el jefe del acto se presentaba con el nombre y la descripción de un equipo de liga que nunca juega (BH-B).
/// Aquí la identidad se decide <b>por el tipo de nodo</b>, una sola vez: el jefe con su nombre, el partido de
/// catálogo con el de su clan, y nada en los nodos que no se juegan. Puro, sin E/S.
/// </summary>
public static class OpponentView
{
    /// <summary>La ficha del rival de ese nodo, o null si el nodo no se juega o no hay dato que enseñar.</summary>
    public static OpponentCard? For(MapNode node, BossCatalog bosses, RivalCatalog rivals, string language = "es")
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(bosses);
        ArgumentNullException.ThrowIfNull(rivals);

        if (node.Kind == NodeKind.Boss)
        {
            // Un jefe por acto (RF-001): se busca sin lanzar, porque un catálogo sin jefe no es un error de
            // presentación, es un nodo sin nada que enseñar.
            for (int i = 0; i < bosses.All.Count; i++)
            {
                if (bosses.All[i].Act == node.Act)
                {
                    return new OpponentCard(bosses.All[i].Id, bosses.All[i].NameIn(language), string.Empty, true);
                }
            }

            return null;
        }

        if (!NodeKinds.IsCatalogRivalMatch(node.Kind) || node.OpponentId.Length == 0)
        {
            return null;
        }

        var team = rivals.Find(node.OpponentId);
        if (team is null)
        {
            return null;
        }

        bool english = string.Equals(language, "en", StringComparison.Ordinal);
        return new OpponentCard(
            team.Id,
            english ? team.Name.En : team.Name.Es,
            english ? team.Description.En : team.Description.Es,
            false);
    }
}
