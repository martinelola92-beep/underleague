namespace Underleague.Sim.Run;

/// <summary>
/// Tipo de nodo del mapa de un acto (RF-011). El orden de los miembros es parte del guardado: se
/// serializa por nombre, no por valor, pero se mantiene estable de todos modos.
/// </summary>
public enum NodeKind
{
    /// <summary>Partido de liga: el nodo de partido ordinario.</summary>
    LeagueMatch,

    /// <summary>Partido de élite: más riesgo y más recompensa (RF-011).</summary>
    EliteMatch,

    /// <summary>Mercado: la única tienda del juego (RF-114). Garantía de accesibilidad en RF-011b.</summary>
    Market,

    /// <summary>Clínica: cura garantizada con coste alto en oro (RF-094).</summary>
    Clinic,

    /// <summary>
    /// Taller de implantes (RF-095). <b>Fase 3</b>: el generador de mapas de la fase 2 nunca lo produce.
    /// Está en el enum desde el principio a propósito, para que añadirlo en la fase 3 no obligue a subir
    /// la versión de esquema del guardado ni a migrar runs.
    /// </summary>
    Workshop,

    /// <summary>Entrenamiento.</summary>
    Training,

    /// <summary>Evento aleatorio.</summary>
    Event,

    /// <summary>Jefe del acto (RF-001). Perderlo termina la run (RF-002b).</summary>
    Boss,

    /// <summary>
    /// Nodo de inscripción (ADR 0046, amplía RF-011): el despacho del presidente. Paga oro y amplía la
    /// plantilla en un hueco, hasta el techo de <see cref="RunRules.MaxRosterSize"/>. Va al final del
    /// enum a propósito: se serializa por nombre, pero el orden de los que ya existían no se mueve.
    /// </summary>
    Enrollment,
}

/// <summary>Utilidades sobre <see cref="NodeKind"/> compartidas por el generador, el motor y el guardado.</summary>
public static class NodeKinds
{
    /// <summary>
    /// True si el nodo se resuelve jugando un partido: liga, élite y jefe. Es el conjunto que limita
    /// RF-003b (no más del 60% de los nodos del acto).
    /// </summary>
    public static bool IsMatch(NodeKind kind) =>
        kind is NodeKind.LeagueMatch or NodeKind.EliteMatch or NodeKind.Boss;
}
