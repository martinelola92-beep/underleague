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

}

/// <summary>Utilidades sobre <see cref="NodeKind"/> compartidas por el generador, el motor y el guardado.</summary>
public static class NodeKinds
{
    /// <summary>
    /// True si el nodo se resuelve jugando un partido: liga, élite y jefe. Es el conjunto que limita
    /// RF-003b (no más del 60% de los nodos del acto).
    ///
    /// <para><b>Ojo al jefe (BE-F).</b> Esto responde "¿aquí se juega?", que es lo que quieren el motor de
    /// run, el mapa y la interfaz. <b>No</b> responde "¿enfrente hay un clan del catálogo?": el equipo del
    /// jefe lo construye <c>BossRunSystems</c> desde <c>data/bosses/</c>, y el <c>OpponentId</c> que su
    /// nodo guarda es un <b>id fantasma</b> —sintácticamente válido, semánticamente falso— que nadie
    /// juega. Para todo lo que lea ese id (memoria de rivalidad, censos, crédito de rival) la pregunta
    /// correcta es <see cref="IsCatalogRivalMatch"/>.</para>
    /// </summary>
    public static bool IsMatch(NodeKind kind) =>
        kind is NodeKind.LeagueMatch or NodeKind.EliteMatch or NodeKind.Boss;

    /// <summary>
    /// True si el nodo se juega <b>contra un clan del catálogo de rivales</b>, es decir si su
    /// <c>OpponentId</c> significa algo: liga y élite, nunca el jefe.
    ///
    /// <para><b>Por qué existe (BE-F).</b> Antes, cada consumidor que leía <c>OpponentId</c> tenía que
    /// acordarse de escribir <c>IsMatch(k) &amp;&amp; k != Boss</c> por su cuenta, y en un solo día
    /// (22 sep 2026) eso estuvo a punto de morder tres veces: la cuenta de "veces que he visto a este
    /// clan" habría mentido, un censo habría contado encuentros inexistentes, y el crédito de rival se
    /// habría acreditado al clan equivocado —ese último se descartaba **por accidente**, porque los
    /// rangos de id del jefe y del rival de catálogo no se solapan, así que mover cualquiera de las dos
    /// constantes lo habría roto en silencio—.</para>
    ///
    /// <para>Desde la ADR 0124 hay memoria de rivalidad y el <c>OpponentId</c> guardado dejó de ser un
    /// dato inerte: es la clave con la que se escribe la narrativa del jugador. Un id fantasma leído por
    /// error ya no es un detalle interno, es una mentira en su historial.</para>
    ///
    /// <para><b>Lo que esto NO arregla</b>: el nodo de jefe <b>sigue guardando</b> el id fantasma. Esto
    /// arregla la lectura, no el dato. Quitarlo de raíz exige que <c>MapGenerator</c> deje de asignarle
    /// rival, y ese cursor consume el flujo <c>RngStreams.Map</c>: cambiarlo regeneraría todos los mapas
    /// de todas las semillas e invalidaría la referencia de balance entera. Ver
    /// <c>docs/pendientes/BE-F.md</c>.</para>
    /// </summary>
    public static bool IsCatalogRivalMatch(NodeKind kind) =>
        kind is NodeKind.LeagueMatch or NodeKind.EliteMatch;
}
