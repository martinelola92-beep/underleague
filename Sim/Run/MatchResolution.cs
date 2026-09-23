using System.Globalization;
using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Model;
using Underleague.Sim.Perks;
using Underleague.Sim.Run.Systems.Rivals;
using ProgressionRules = Underleague.Sim.Progression.Progression;

namespace Underleague.Sim.Run;

/// <summary>
/// Lo que le pasa a la plantilla después de un partido y en qué momento exacto puede terminar la run.
/// Interno: la superficie pública del bucle es <see cref="RunEngine"/>.
///
/// <para><b>Derrota "en cualquier momento, incluido durante un partido" (RF-002b).</b>
/// <c>Simulator.Run</c> no se puede interrumpir: devuelve el partido entero. Así que la baja se detecta
/// recorriendo la <b>secuencia ordenada de eventos</b> y llevando la cuenta de disponibles tras cada
/// lesión grave o muerte propia. En cuanto la cuenta baja de 5, la run termina con el tick de ese
/// evento y <b>los eventos posteriores no se aplican a la plantilla</b>: el estado guardado es el que
/// había en ese instante, y el render puede cortar la reproducción en ese tick. Es la lectura literal
/// de "una lesión grave o una muerte en pleno partido con solo 5 en campo termina la run al
/// instante".</para>
/// </summary>
internal static class MatchResolution
{
    /// <summary>Sufijo con el que el motor marca un evento anulado por un perk (§7 de fase 1).</summary>
    private const string CancelledSuffix = ":cancelled";

    /// <summary>Detalle de una lesión grave en el evento INJURY.</summary>
    private const string SevereDetail = "severe";

    /// <summary>Prefijo del contador de inventario de consumibles (paquete X, X-9).</summary>
    private const string ConsumableOwnedCounter = RunState.ConsumableOwnedPrefix;

    /// <summary>Resultado de aplicar un partido al estado de la run.</summary>
    internal sealed record Applied(RunState State, RunMatchSummary Summary, RunOutcome Outcome);

    /// <summary>
    /// Aplica el resultado de un partido: bajas, experiencia, contadores y partidos en el banquillo, y
    /// decide si la run termina.
    /// </summary>
    internal static Applied Apply(
        RunState state,
        MapNode node,
        MatchLineup lineup,
        MatchResult result,
        Catalog catalog)
    {
        var players = new List<RunPlayer>(state.Roster);
        var playedIds = new List<int>(lineup.Starters.Count);
        for (int i = 0; i < lineup.Starters.Count; i++)
        {
            playedIds.Add(lineup.Starters[i].Id);
        }

        // ADR 0094: el suplente que entró por una sustitución forzada jugó (RF-025: 100 % de experiencia), y
        // deja de contar como banquillo en ese partido.
        var benchIds = new List<int>(lineup.Bench.Count);
        for (int i = 0; i < lineup.Bench.Count; i++)
        {
            int id = lineup.Bench[i].Id;
            if (PlayedTicks(result.Report, id) > 0)
            {
                playedIds.Add(id);
            }
            else
            {
                benchIds.Add(id);
            }
        }

        playedIds.Sort();
        benchIds.Sort();

        // 1. La penalización de las lesiones leves ya se ha gastado en este partido (RF-091: "durante el
        //    siguiente partido"), así que los titulares salen de él con el contador a cero antes de
        //    sumar las lesiones nuevas. Los suplentes conservan la suya: no la han gastado.
        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].MinorInjuries > 0
                && players[i].PhysicalState == PhysicalState.MinorInjury
                && Contains(playedIds, players[i].Id))
            {
                players[i] = players[i] with { MinorInjuries = 0, PhysicalState = PhysicalState.Healthy };
            }
        }

        // 2. Bajas, en orden de evento, vigilando el mínimo de plantilla tick a tick.
        int defeatTick = -1;
        int injuries = 0;
        int deaths = 0;
        var recovered = new List<string>();
        var deathDetails = new List<PlayerDeathDetail>();
        var events = result.Events;
        for (int i = 0; i < events.Count && defeatTick < 0; i++)
        {
            var matchEvent = events[i];
            if (matchEvent.Team != 0 || matchEvent.Detail.EndsWith(CancelledSuffix, StringComparison.Ordinal))
            {
                continue;
            }

            int index = IndexOf(players, matchEvent.Actor);
            if (index < 0)
            {
                continue;
            }

            switch (matchEvent.Type)
            {
                case EventType.Injury:
                    injuries++;
                    players[index] = matchEvent.Detail.StartsWith(SevereDetail, StringComparison.Ordinal)
                        ? players[index] with { PhysicalState = PhysicalState.SevereInjury }
                        : players[index] with
                        {
                            PhysicalState = PhysicalState.MinorInjury,
                            MinorInjuries = players[index].MinorInjuries + 1,
                        };
                    break;

                case EventType.Death:
                    deaths++;

                    // Paquete BB: primitiva "run-level: oro y atributos al salir de la plantilla"
                    // (docs/analisis/perks-catalogo-unificado.md §3.2). Se registra ANTES de marcar al
                    // jugador como muerto (aunque sus perks no cambian al morir, así que el orden aquí no
                    // importa) para que Seguro de vida y Herencia puedan leer, después del partido, con
                    // qué perks murió y a quién habría heredado. El vinculado se resuelve con la MISMA
                    // geometría estática que usa el motor en partido (no toca Sim/Perks ni Sim/Engine),
                    // pero SIN comprobar aquí si ya ha muerto: esa comprobación la hace
                    // Economy.InheritanceSystem contra el estado final, porque es allí donde también se
                    // resuelve el caso "el vinculado muere DESPUÉS, en este mismo partido". El matador
                    // (enmienda R3 de ADR 0124) sale directo del Opponent del evento DEATH, que MatchEngine
                    // ya rellena con quien mata (rival o propio); -1 si no hubo matador.
                    deathDetails.Add(new PlayerDeathDetail(
                        players[index].Id,
                        players[index].Perks,
                        ResolveLinkedTeammate(lineup, players[index], catalog),
                        matchEvent.Opponent));

                    // ADR 0048, condición 4 ("se puede rehacer"): el objeto del muerto VUELVE AL
                    // INVENTARIO, no se entierra con él. Es la mitad recuperable de una muerte y la que
                    // permite que morir sea caro sin ser irreparable; sin ella, retirar la garantía de
                    // RF-093 dejaría al jugador con una pérdida doble y sin salida (RF-075..078).
                    if (players[index].Item is { } inherited)
                    {
                        recovered.Add(inherited);
                        players[index] = players[index] with { Item = null };
                    }

                    players[index] = players[index] with { PhysicalState = PhysicalState.Dead };
                    break;

                default:
                    continue;
            }

            if (AvailableCount(players) < RunRules.MinimumAvailablePlayers)
            {
                defeatTick = matchEvent.Tick;
            }
        }

        // 3. Experiencia (RF-025) y nivel (RF-027), con los multiplicadores de perk fuera de partido.
        ApplyProgression(players, playedIds, benchIds, result, catalog);

        // 3b. Historial de carrera (RF-122, ADR 0124): solo la plantilla PROPIA (Team 0). Los jugadores
        //     rivales también producen PlayerMatchStats y se descartan, como en el resto de este método
        //     (línea de arriba, "matchEvent.Team != 0"). Se recorre la plantilla por id ascendente
        //     (RT-041); PlayerMatchStats.TicksOnPitch decide si el partido cuenta para RunCareer.Matches.
        var ownStats = result.Report.Players;
        for (int i = 0; i < players.Count; i++)
        {
            for (int s = 0; s < ownStats.Count; s++)
            {
                if (ownStats[s].Team == 0 && ownStats[s].PlayerId == players[i].Id)
                {
                    players[i] = players[i].WithCareerFrom(ownStats[s]);
                    break;
                }
            }
        }

        // 4. Partidos seguidos en el banquillo: los mercenarios abandonan tras 3 (RF-111). Quien los
        //    hace marcharse es el paquete X; el contador es del estado y se lleva aquí.
        for (int i = 0; i < players.Count; i++)
        {
            if (Contains(playedIds, players[i].Id))
            {
                players[i] = players[i] with { MatchesBenched = 0 };
            }
            else if (players[i].IsAvailable)
            {
                players[i] = players[i] with { MatchesBenched = players[i].MatchesBenched + 1 };
            }
        }

        bool won = result.Report.Winner == 0;
        var summary = new RunMatchSummary(
            node.Id,
            node.Kind,
            won,
            result.Report.Goals[0],
            result.Report.Goals[1],
            result.Report.Ticks,
            result.Report.WentToGoldenGoal,
            playedIds,
            benchIds,
            injuries,
            deaths,
            result.Report)
        {
            CounterDeltas = result.CounterDeltas,
            DeathDetails = deathDetails,
        };

        var next = state
            .WithRoster(players)
            .WithNodeCompleted(node.Id, node.Kind, won ? NodeResult.Won : NodeResult.Lost);

        // 4b. Memoria de "quién knaveó a quién" contra un rival concreto (BE-B, enmienda de la ADR 0124,
        //     tabla "Dónde vive cada memoria"). Pasada APARTE de la del paso 2: esa mira solo Team 0 y para
        //     en defeatTick (BE-C, semántica que no se toca aquí); esta mira los eventos de los dos
        //     bandos y no se detiene, porque no cambia el estado de la plantilla ni el resultado del
        //     partido -es contabilidad pura sobre RunState.Counters (RT-054)-.
        next = ApplyRivalCredits(next, node, players, result.Events);

        // El almacén se rellena en orden de id de objeto (RT-041), no en orden de muerte.
        recovered.Sort(StringComparer.Ordinal);
        for (int i = 0; i < recovered.Count; i++)
        {
            next = next.WithStockedItem(recovered[i], 1);
        }

        if (recovered.Count > 0)
        {
            next = next.WithCounter(
                RunState.ItemsRecoveredCounter, next.Counter(RunState.ItemsRecoveredCounter) + recovered.Count);
        }

        // 5. Consumibles gastados y alineación depurada. Los dos son consecuencia directa del partido y
        //    los dos tienen que ocurrir aquí, antes de que el jugador vuelva al mapa.
        next = ConsumeConsumables(next, result.Report);
        next = PruneLineup(ClearSevereInjuryRisks(next));

        var outcome = Decide(node, won, defeatTick, result.Report.Ticks);
        return new Applied(next, summary, outcome);
    }

    /// <summary>
    /// Las dos únicas vías de derrota y la única de victoria (RF-002, RF-002b). La baja durante el
    /// partido manda sobre el resultado: si la run terminó en el tick 300, lo que pasara en el 900 ya
    /// no cuenta.
    /// </summary>
    private static RunOutcome Decide(MapNode node, bool won, int defeatTick, int ticks)
    {
        if (defeatTick >= 0)
        {
            return new RunOutcome(RunOutcomeKind.Defeat, DefeatCause.NotEnoughPlayers, node.Id, defeatTick);
        }

        if (node.Kind != NodeKind.Boss)
        {
            // RF-002c: perder un partido ordinario no termina la run.
            return RunOutcome.InProgress;
        }

        if (!won)
        {
            return new RunOutcome(RunOutcomeKind.Defeat, DefeatCause.BossMatchLost, node.Id, ticks);
        }

        return node.Act >= RunRules.Acts
            ? new RunOutcome(RunOutcomeKind.Victory, DefeatCause.None, node.Id, ticks)
            : RunOutcome.InProgress;
    }

    private static void ApplyProgression(
        List<RunPlayer> players,
        IReadOnlyList<int> playedIds,
        IReadOnlyList<int> benchIds,
        MatchResult result,
        Catalog catalog)
    {
        var played = new List<PlayerDefinition>(playedIds.Count);
        var bench = new List<PlayerDefinition>(benchIds.Count);
        for (int i = 0; i < players.Count; i++)
        {
            if (Contains(playedIds, players[i].Id))
            {
                played.Add(players[i].ToDefinition(catalog, applyMinorInjuryPenalty: false));
            }
            else if (Contains(benchIds, players[i].Id))
            {
                bench.Add(players[i].ToDefinition(catalog, applyMinorInjuryPenalty: false));
            }
        }

        var awards = ProgressionRules.AwardExperience(played, bench, catalog, catalog.Progression);
        for (int a = 0; a < awards.Count; a++)
        {
            int index = IndexOf(players, awards[a].PlayerId);
            if (index < 0)
            {
                continue;
            }

            var player = players[index];

            // +33% de experiencia para los canteranos (RF-114c). El resto de multiplicadores (habilidad
            // racial de los humanos, perks con modifyExperience) ya los ha aplicado Progression.
            int experience = awards[a].Experience;
            if (player.IsYouth)
            {
                experience = experience * (100 + RunRules.YouthExperienceBonusPercent) / 100;
            }

            var definition = player.ToDefinition(catalog, applyMinorInjuryPenalty: false);
            definition = ProgressionRules.ApplyCounterDeltas(definition, result.CounterDeltas);

            int total = player.Experience + experience;
            int level = ProgressionRules.LevelFor(total, catalog.Progression);
            definition = ProgressionRules.LevelUp(definition, level, catalog.Progression);

            players[index] = player with
            {
                Experience = total,
                Level = definition.Level,
                Attributes = definition.Attributes,
                Counters = definition.Counters,
            };
        }
    }

    /// <summary>
    /// Gasta los consumibles del partido (RF-085). Los usados descuentan su unidad del inventario
    /// (<c>consumable_owned:&lt;id&gt;</c>, el contador que rellena el mercado del paquete X, X-9) y la
    /// lista de equipados se vacía entera: "no persisten entre partidos" es literal, y RF-080 dice que se
    /// equipan <b>antes de cada partido</b>. Lo que no se usó sigue en el inventario y se puede volver a
    /// equipar.
    /// </summary>
    private static RunState ConsumeConsumables(RunState state, MatchReport report)
    {
        if (state.Consumables.Count == 0)
        {
            return state;
        }

        var next = state;
        var used = report.ConsumableActivations;
        for (int i = 0; i < used.Count; i++)
        {
            if (used[i].Team != 0)
            {
                continue;
            }

            string counter = ConsumableOwnedCounter + used[i].ConsumableId;
            next = next.WithCounter(counter, Math.Max(0, next.Counter(counter) - 1));
        }

        return next.WithConsumables(Array.Empty<EquippedConsumable>());
    }

    /// <summary>
    /// Borra las marcas de "alineado a sabiendas con lesión grave" (RF-093 vía 1). El riesgo se asume
    /// para un partido concreto; el siguiente exige volver a tomarlo.
    /// </summary>
    private static RunState ClearSevereInjuryRisks(RunState state)
    {
        var next = state;
        foreach (var (name, value) in state.Counters)
        {
            if (value != 0 && name.StartsWith(RunLineup.RiskCounterPrefix, StringComparison.Ordinal))
            {
                next = next.WithCounter(name, 0);
            }
        }

        return next;
    }

    /// <summary>
    /// Quita de la alineación guardada a quien ya no puede jugar (RF-092, RF-093). Sin esto, un titular
    /// que sale del partido con lesión grave seguiría en la alineación y volvería a saltar al campo en el
    /// partido siguiente <b>sin que nadie lo decidiera</b>, que es exactamente lo que RF-093 no puede
    /// permitir: alinear a un lesionado grave es una decisión explícita, y hay que volver a tomarla.
    /// </summary>
    private static RunState PruneLineup(RunState state)
    {
        var slots = state.Lineup.Slots;
        var kept = new List<LineupSlot>(slots.Count);
        for (int i = 0; i < slots.Count; i++)
        {
            var player = state.FindPlayer(slots[i].PlayerId);
            if (player is not null && player.IsAvailable)
            {
                kept.Add(slots[i]);
            }
        }

        return kept.Count == slots.Count ? state : state.WithLineup(new Lineup(kept));
    }

    private static int AvailableCount(IReadOnlyList<RunPlayer> players)
    {
        int count = 0;
        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].IsAvailable)
            {
                count++;
            }
        }

        return count;
    }

    private static int IndexOf(IReadOnlyList<RunPlayer> players, int id)
    {
        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].Id == id)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// Compañero vinculado de <paramref name="player"/> en la alineación INICIAL de este partido (paquete
    /// BB, §3.2): el primero de sus perks, en el orden ascendente en que ya vienen en
    /// <see cref="RunPlayer.Perks"/> (RT-041), que declare relaciones de vínculo
    /// (<see cref="PerkDefinition.Links"/>), resuelto con la MISMA geometría que usa el motor durante el
    /// partido (<see cref="LinkGeometry"/>): mismas casillas-hogar, mismo desempate por distancia y por
    /// id ascendente. Solo ese primer perk decide -si tiene vínculos declarados pero ninguna relación
    /// resuelve candidato, no se prueba con el siguiente perk-: es una decisión del paquete BB para que
    /// "el vinculado" sea una respuesta única y determinista, no una lista.
    ///
    /// <para>Solo mira <see cref="MatchLineup.Lineup"/> (la colocación con la que se empezó el partido):
    /// un suplente que entra por una sustitución forzada (ADR 0094) no tiene casilla-hogar propia aquí, así
    /// que no puede ser origen ni destino de un traspaso si muere o hereda tras entrar. Es una limitación
    /// conocida, documentada en el informe del paquete BB.</para>
    ///
    /// <para>Devuelve -1 sin comprobar si el candidato sigue vivo: esa comprobación la hace
    /// <c>Economy.InheritanceSystem</c> contra el estado final de la plantilla, no aquí.</para>
    /// </summary>
    private static int ResolveLinkedTeammate(MatchLineup lineup, RunPlayer player, Catalog catalog)
    {
        var slots = lineup.Lineup.Slots;
        int selfIndex = -1;
        var homes = new Cell[slots.Count];
        var teams = new int[slots.Count];
        for (int i = 0; i < slots.Count; i++)
        {
            homes[i] = slots[i].HomeCell;
            teams[i] = 0;
            if (slots[i].PlayerId == player.Id)
            {
                selfIndex = i;
            }
        }

        if (selfIndex < 0)
        {
            return -1;
        }

        var perks = player.Perks;
        for (int p = 0; p < perks.Count; p++)
        {
            var perk = catalog.Perks.Find(perks[p]);
            if (perk is null || perk.Links.Count == 0)
            {
                continue;
            }

            for (int r = 0; r < perk.Links.Count; r++)
            {
                int candidate = LinkGeometry.ResolveLink(homes, teams, selfIndex, perk.Links[r]);
                if (candidate >= 0)
                {
                    return slots[candidate].PlayerId;
                }
            }

            return -1;
        }

        return -1;
    }

    /// <summary>
    /// Construye y aplica los deltas de <see cref="RunState.RivalCreditPrefix"/> (BE-B): recorre
    /// <paramref name="events"/> en su orden ya determinista (RT-041) buscando lesiones y muertes con
    /// causante conocido en las que un lado es la plantilla propia y el otro un rival de
    /// <see cref="RivalTeamBuilder"/>. Aritmética entera, sin RNG (RT-021, RT-023): un evento solo suma
    /// una unidad al contador de su par exacto.
    /// </summary>
    private static RunState ApplyRivalCredits(
        RunState state, MapNode node, IReadOnlyList<RunPlayer> players, IReadOnlyList<MatchEvent> events)
    {
        if (node.OpponentId.Length == 0)
        {
            // Nodo procedural sin catálogo de rivales (RF-015): no hay identidad de clan estable con la
            // que relacionar nada en partidos posteriores. Mismo guardia que Systems.Rivals.RivalHistory.
            return state;
        }

        if (!NodeKinds.IsCatalogRivalMatch(node.Kind))
        {
            // El nodo de jefe guarda un OpponentId FANTASMA y sus hechos se acreditarían al clan
            // equivocado (BE-F). La pregunta es "¿enfrente hay un clan del catálogo?", no "¿aquí se
            // juega?", y desde BE-F la primitiva la responde por su nombre en vez de dejarla a que cada
            // consumidor se acuerde de excluir Boss a mano.
            return state;
        }

        var deltas = new Dictionary<string, int>();
        for (int i = 0; i < events.Count; i++)
        {
            var matchEvent = events[i];
            if (matchEvent.Detail.EndsWith(CancelledSuffix, StringComparison.Ordinal))
            {
                // Un perk que anula una lesión o una muerte no acredita nada (mismo criterio que la
                // atribución de RunCareer, ADR 0124).
                continue;
            }

            string kind = matchEvent.Type switch
            {
                EventType.Injury => "Injury",
                EventType.Death => "Death",
                _ => string.Empty,
            };
            if (kind.Length == 0 || matchEvent.Opponent < 0)
            {
                // Sin causante (Kill/EmitCancellable sin matador) no hay par que registrar.
                continue;
            }

            bool victimIsOwn = IndexOf(players, matchEvent.Actor) >= 0;
            bool causerIsOwn = IndexOf(players, matchEvent.Opponent) >= 0;
            if (victimIsOwn == causerIsOwn)
            {
                // Los dos propios, los dos rivales, o un id que no aparece en ninguno de los dos bandos
                // (por ejemplo un jefe fuera del rango de RivalTeamBuilder): no es un par propio x rival.
                continue;
            }

            int rivalId = victimIsOwn ? matchEvent.Opponent : matchEvent.Actor;
            int rivalIndex = rivalId - RivalTeamBuilder.OpponentFirstPlayerId;
            if (rivalIndex < 0)
            {
                // No viene de RivalTeamBuilder (rango de id fuera del rival de datos): sin índice estable
                // dentro del fichero JSON del clan, no hay con qué relacionarlo en partidos posteriores.
                continue;
            }

            int ownId = victimIsOwn ? matchEvent.Actor : matchEvent.Opponent;
            string direction = victimIsOwn ? "suffered" : "caused";
            string key = RunState.RivalCreditPrefix + node.OpponentId + ":"
                + rivalIndex.ToString(CultureInfo.InvariantCulture) + ":"
                + ownId.ToString(CultureInfo.InvariantCulture) + ":"
                + direction + kind;

            deltas[key] = deltas.TryGetValue(key, out int current) ? current + 1 : 1;
        }

        if (deltas.Count == 0)
        {
            return state;
        }

        var keys = new List<string>(deltas.Keys);
        keys.Sort(StringComparer.Ordinal);
        var next = state;
        for (int i = 0; i < keys.Count; i++)
        {
            next = next.WithCounter(keys[i], next.Counter(keys[i]) + deltas[keys[i]]);
        }

        return next;
    }

    /// <summary>
    /// Ticks que jugó un jugador <b>de la plantilla propia</b> (equipo 0) en este partido. Decide quién
    /// cuenta como "jugó" para la experiencia y para el contador de banquillo.
    ///
    /// <para>El <c>Team == 0</c> es de BE-C. Buscar sólo por id era inocuo <b>por accidente</b>: los
    /// rivales arrancan sus ids en 1.000.000 o 2.000.000 y la plantilla propia crece de decenas en
    /// decenas, así que los rangos no se solapan. Pero esa separación no la fuerza ningún assert, y el día
    /// que se estrechara —un <c>OpponentFirstPlayerId</c> más bajo, o ids propios que dejaran de
    /// reiniciarse por run— esto habría dado los ticks del rival equivocado <b>en silencio</b>, sin
    /// excepción y sin test que lo viera. Es el mismo filtro que el paso 3b ya hace catorce líneas más
    /// arriba.</para>
    /// </summary>
    private static int PlayedTicks(MatchReport report, int playerId)
    {
        for (int i = 0; i < report.Players.Count; i++)
        {
            if (report.Players[i].PlayerId == playerId && report.Players[i].Team == 0)
            {
                return report.Players[i].TicksOnPitch;
            }
        }

        return 0;
    }

    private static bool Contains(IReadOnlyList<int> ids, int id)
    {
        for (int i = 0; i < ids.Count; i++)
        {
            if (ids[i] == id)
            {
                return true;
            }
        }

        return false;
    }
}
