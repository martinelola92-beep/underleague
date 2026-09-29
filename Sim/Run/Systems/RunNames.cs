using Underleague.Sim.Data;
using Underleague.Sim.Generation;
using Underleague.Sim.Model;
using Underleague.Sim.Random;
using Underleague.Sim.Run.Systems.Rewards;
using Underleague.Sim.Run.Systems.Rivals;

namespace Underleague.Sim.Run.Systems;

/// <summary>
/// Que un nombre completo no se repita dentro de una run (BA-G, ADR 0169). El sorteo de
/// <see cref="NameGenerator"/> no tiene memoria, y con 300 nombres posibles por raza y un centenar de jugadores
/// generados por run, repetirse era lo normal: el revisor no distinguía a dos jugadores al equipar un perk.
///
/// <para><b>Qué se vigila</b>: la plantilla (viva, muerta o vendida), lo que se ofrece al club —fichajes de pago,
/// canteranos, mercenarios, jugadores de recompensa y el canterano de un evento— y los fichajes de los clanes
/// rivales (ADR 0165), cuyos nombres el club no puede llevar (<see cref="RivalRoster.SigningPoolTickets"/>).</para>
///
/// <para><b>Cómo, sin tocar un solo dado de nadie</b>: el jugador generado conserva el nombre que ya le salió (el
/// flujo de mercado, recompensa o evento gasta exactamente lo mismo que antes) y, <b>sólo si ese nombre ya está
/// cogido</b>, se le cambia por otro sorteado del flujo propio <see cref="RngStreams.Names"/> (RT-022). Un nombre no
/// entra en ningún cálculo del partido, así que la única diferencia de una run con y sin esta regla es cómo se
/// llaman los repetidos.</para>
///
/// <para><b>Por qué hay un registro</b>: el surtido de un mercado, una recompensa o un canterano <b>se deriva, no se
/// guarda</b> (W-12) y se vuelve a derivar en cada consulta, también después de comprar. Si un fichaje comprado
/// aquí contara como «cogido» al volver a derivar, su oferta cambiaría de nombre en cuanto se pagara. Por eso cada
/// jugador admitido anota en <see cref="RunState.Counters"/> (W-11: sin subir el esquema del guardado) de <b>qué
/// elección</b> vino, y el surtido de esa misma elección no cuenta esos nombres como cogidos. Es <b>estable</b>
/// dentro de la elección y estricto fuera de ella.</para>
/// </summary>
public static class RunNames
{
    /// <summary>Prefijo del contador que anota de qué elección vino el jugador con ese nombre (<c>nameOwner:&lt;nombre&gt;</c>).</summary>
    public const string OwnerPrefix = "nameOwner:";

    /// <summary>Intentos de sortear un nombre libre del flujo propio antes de recorrer el retículo entero.</summary>
    private const int Attempts = 64;

    /// <summary>Base de la sal de los nombres de una plantilla inicial; nada más la usa.</summary>
    private const int InitialRosterSalt = 0;

    /// <summary>Base de la sal de un jugador que entra en la plantilla y ya está cogido (sumidero estricto).</summary>
    private const int AdmitSaltBase = 1_000_000;

    /// <summary>Base de la sal de un jugador del equipo de un jefe que cede su nombre al club.</summary>
    private const int BossSaltBase = 2_000_000;

    /// <summary>Sal de la primera oferta de un nodo: <c>1 + nodo·100 + posición de la oferta</c>.</summary>
    private static int SaltOf(int nodeId, int position) => 1 + (nodeId * 100) + position;

    /// <summary>
    /// De qué elección es lo que se admite ahora: 0 fuera de un nodo (la plantilla inicial), y si no
    /// <c>1 + nodo·4 + elecciones ya cobradas</c> (el jefe da dos, ADR 0043; en un mercado o un evento es siempre la
    /// primera). Se deriva del estado, así que el surtido que se consulta y el jugador que se admite coinciden.
    /// </summary>
    public static int OwnerToken(RunState state, int nodeId)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (nodeId < 0)
        {
            return 0;
        }

        return 1 + (nodeId * 4) + Math.Min(RewardSystem.PicksTaken(state, nodeId), 3);
    }

    /// <summary>
    /// Valor del registro para un nombre <b>del mundo</b>: el de un jugador de datos de un clan rival. Ninguna elección del
    /// club es «propia» de él (<c>token + 1</c> nunca vale -1), así que para todas está cogido.
    /// </summary>
    private const int WorldValue = -1;

    /// <summary>
    /// Reserva los nombres de los <b>jugadores de datos</b> de los clanes rivales (<c>data/rivals/</c>, diez por raza):
    /// el club no los lleva. Son identidad —los tres rivales de una raza son el mismo clan, con los mismos nombres por
    /// puesto, y el némesis se reconoce por su nombre—, así que quien cede es el club, no el clan. Se llama una vez, al
    /// empezar la run, con los sistemas con los que se juega (un <c>IRunSystems</c> sin rivales no reserva nada).
    /// </summary>
    public static RunState ReserveWorld(RunState state, IRunSystems systems)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(systems);
        var teams = systems.Nemesis.Rivals.All;
        var counters = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var (key, value) in state.Counters)
        {
            counters[key] = value;
        }

        for (int t = 0; t < teams.Count; t++)
        {
            for (int p = 0; p < teams[t].Players.Count; p++)
            {
                counters[OwnerPrefix + teams[t].Players[p].Name] = WorldValue;
            }
        }

        return counters.Count == state.Counters.Count ? state : state with { Counters = counters };
    }

    /// <summary>
    /// Renombra, si <paramref name="rename"/>, a quien de la plantilla inicial lleve un nombre reservado —a un fichaje de
    /// los clanes rivales de su raza o a un jugador de datos de un clan— y anota todos los nombres (elección 0).
    /// <c>TeamGenerator</c> ya garantiza que no se repiten dentro del equipo, así que sólo puede haber choque con esas
    /// reservas. Con <paramref name="rename"/> false (una plantilla que trae el propio <see cref="RunSetup"/>: es lo que
    /// quien monta la run ha pedido) sólo se anotan.
    /// </summary>
    public static RunState AdmitInitialRoster(RunState state, Catalog catalog, bool rename)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(catalog);
        var book = new Book(state.Seed, catalog);
        var roster = new List<RunPlayer>(state.Roster.Count);
        for (int i = 0; i < state.Roster.Count; i++)
        {
            var player = state.Roster[i];
            if (rename && book.Taken(player.Name, player.Race, ownerToken: 0, state.Counters, roster: null, sink: false))
            {
                player = player with
                {
                    Name = book.Redraw(
                        player.Race,
                        InitialRosterSalt + i,
                        (name, race) => book.Taken(name, race, ownerToken: 0, state.Counters, roster: null, sink: false)),
                };
            }

            book.Also.Add(player.Name);
            roster.Add(player);
        }

        var next = state.WithRoster(roster);
        for (int i = 0; i < roster.Count; i++)
        {
            next = Register(next, roster[i].Name, 0);
        }

        return next;
    }

    /// <summary>
    /// El equipo de un jefe cede ante la plantilla del club (BA-G, ADR 0169): quien lleve el nombre de un jugador del club
    /// pasa a llevar otro, sorteado del flujo propio (RT-022). El jefe se genera con el flujo de generación, sin saber quién
    /// hay en la plantilla, y el mismo nombre en los dos equipos es justo lo que el marcador, el pregón y el log no aguantan.
    /// Es función pura de (estado, equipo): el ojeo y el partido, que se construyen sin que la plantilla cambie entre
    /// medias, enseñan los mismos nombres. Sólo cambia el texto: atributos, perks y alineación salen como estaban.
    /// </summary>
    public static TeamSetup YieldToClub(RunState state, TeamSetup team, int nodeId, Catalog catalog)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(team);
        ArgumentNullException.ThrowIfNull(catalog);

        var clubNames = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < state.Roster.Count; i++)
        {
            clubNames.Add(state.Roster[i].Name);
        }

        var teamNames = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < team.Players.Count; i++)
        {
            teamNames.Add(team.Players[i].Name);
        }

        var book = new Book(state.Seed, catalog);
        List<PlayerDefinition>? players = null;
        for (int i = 0; i < team.Players.Count; i++)
        {
            var player = team.Players[i];
            if (!clubNames.Contains(player.Name))
            {
                continue;
            }

            string name = book.Redraw(
                player.Race,
                BossSaltBase + (nodeId * 100) + i,
                (candidate, _) => clubNames.Contains(candidate) || teamNames.Contains(candidate));
            teamNames.Add(name);
            players ??= new List<PlayerDefinition>(team.Players);
            players[i] = player with { Name = name };
        }

        return players is null ? team : team with { Players = players };
    }

    /// <summary>
    /// Pasa por el sumidero estricto a un jugador que entra en la plantilla: si su nombre ya lo lleva alguien —de esta
    /// elección o de otra—, se le cambia. En el uso normal no hace nada, porque el surtido ya lo ha filtrado; sólo actúa
    /// si se repite la compra de una misma oferta o si la plantilla viene de fuera. Después anota de qué elección viene.
    /// </summary>
    public static RunState Admit(RunState state, RunPlayer player, Catalog catalog)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(catalog);

        int token = OwnerToken(state, state.PendingNodeId);
        var book = new Book(state.Seed, catalog);
        string name = player.Name;
        if (book.Taken(name, player.Race, token, state.Counters, state.Roster, sink: true))
        {
            name = book.Redraw(
                player.Race,
                AdmitSaltBase + state.NextPlayerId,
                (candidate, race) => book.Taken(candidate, race, token, state.Counters, state.Roster, sink: true));
        }

        var renamed = name == player.Name ? player : player with { Name = name };
        return Register(state.WithNewPlayer(renamed), name, token);
    }

    /// <summary>
    /// El surtido de una elección, filtrado: cada jugador que lleve un nombre ya cogido por otra elección, por la
    /// plantilla o por una oferta anterior de la misma lista pasa a llevar uno libre. <paramref name="players"/> va en
    /// el orden en que el llamador lo enseña; la primera oferta de la lista nunca cede ante una posterior.
    /// </summary>
    /// <param name="node">Nodo cuya elección se deriva: de él salen la sal del flujo propio y de quién es cada nombre.</param>
    /// <param name="positions">Posición de cada jugador dentro del nodo (distinta para cada uno), sal de su nuevo sorteo.</param>
    public static IReadOnlyList<RunPlayer> Distinct(
        RunState state, MapNode node, IReadOnlyList<RunPlayer> players, IReadOnlyList<int> positions, Catalog catalog)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(players);
        ArgumentNullException.ThrowIfNull(positions);
        ArgumentNullException.ThrowIfNull(catalog);

        int token = OwnerToken(state, node.Id);
        var book = new Book(state.Seed, catalog);
        var result = new List<RunPlayer>(players.Count);
        for (int i = 0; i < players.Count; i++)
        {
            var player = players[i];
            if (book.Taken(player.Name, player.Race, token, state.Counters, state.Roster, sink: false))
            {
                string name = book.Redraw(
                    player.Race,
                    SaltOf(node.Id, positions[i]),
                    (candidate, race) => book.Taken(candidate, race, token, state.Counters, state.Roster, sink: false));
                player = player with { Name = name };
            }

            book.Also.Add(player.Name);
            result.Add(player);
        }

        return result;
    }

    private static RunState Register(RunState state, string name, int token) =>
        state.WithCounter(OwnerPrefix + name, token + 1);

    /// <summary>
    /// Lo que se sabe de los nombres mientras se filtra una lista: los que ya llevan las ofertas anteriores de esa
    /// lista, los nombres reservados a los fichajes rivales por raza (calculados una vez) y el retículo de cada raza.
    /// </summary>
    private sealed class Book
    {
        private readonly ulong _seed;
        private readonly Catalog _catalog;
        private readonly Dictionary<Model.Race, HashSet<string>> _reserved = new();
        private readonly Dictionary<Model.Race, NameLattice> _lattices = new();

        public Book(ulong seed, Catalog catalog)
        {
            _seed = seed;
            _catalog = catalog;
        }

        /// <summary>Nombres que ya llevan las ofertas anteriores de la lista que se está filtrando.</summary>
        public HashSet<string> Also { get; } = new(StringComparer.Ordinal);

        /// <summary>
        /// ¿Está cogido <paramref name="name"/>? Sí si lo lleva una oferta anterior de la lista, si está reservado a los
        /// fichajes rivales de la raza, o si figura en el registro de otra elección, o en la plantilla sin registro.
        /// Un nombre registrado por la <b>propia</b> elección (<paramref name="ownerToken"/>) es el de una oferta ya
        /// comprada aquí: al derivar el surtido no cuenta como cogido (<paramref name="sink"/> false), y al admitir a un
        /// jugador sólo si sigue en la plantilla (<paramref name="sink"/> true: es comprar dos veces la misma oferta).
        /// </summary>
        public bool Taken(
            string name,
            Model.Race race,
            int ownerToken,
            IReadOnlyDictionary<string, int>? registry,
            IReadOnlyList<RunPlayer>? roster,
            bool sink)
        {
            if (Also.Contains(name) || Reserved(race).Contains(name))
            {
                return true;
            }

            if (registry is not null && registry.TryGetValue(OwnerPrefix + name, out int owner) && owner != ownerToken + 1)
            {
                return true;
            }

            bool own = registry is not null && registry.ContainsKey(OwnerPrefix + name);
            if (own && !sink)
            {
                return false;
            }

            if (roster is not null)
            {
                for (int i = 0; i < roster.Count; i++)
                {
                    if (string.Equals(roster[i].Name, name, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>Un nombre libre para <paramref name="race"/>, sorteado del flujo propio con esa sal.</summary>
        public string Redraw(Model.Race race, int salt, Func<string, Model.Race, bool> taken)
        {
            var lattice = Lattice(race);
            int count = lattice.Count;
            var rng = RngStreams.Names(_seed, salt);
            string candidate = string.Empty;
            for (int attempt = 0; attempt < Attempts; attempt++)
            {
                candidate = lattice.NameAt(rng.Range(0, count));
                if (!taken(candidate, race))
                {
                    return candidate;
                }
            }

            // Retículo casi agotado: se recorre entero desde un punto sorteado, en orden, hasta dar con uno libre.
            int start = rng.Range(0, count);
            for (int k = 0; k < count; k++)
            {
                candidate = lattice.NameAt((start + k) % count);
                if (!taken(candidate, race))
                {
                    return candidate;
                }
            }

            // Sin nombres libres en toda la raza: no hay nada mejor que repetir, y es preferible a no poder jugar.
            return candidate;
        }

        private HashSet<string> Reserved(Model.Race race)
        {
            if (!_reserved.TryGetValue(race, out var set))
            {
                set = Lattice(race).NamesOfTickets(_seed, RivalRoster.SigningPoolTickets);
                _reserved[race] = set;
            }

            return set;
        }

        private NameLattice Lattice(Model.Race race)
        {
            if (!_lattices.TryGetValue(race, out var lattice))
            {
                lattice = new NameLattice(_catalog.Race(race));
                _lattices[race] = lattice;
            }

            return lattice;
        }
    }
}
