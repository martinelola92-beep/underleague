using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Model;

namespace Underleague.Sim.Perks;

/// <summary>
/// Suscripción de un perk concreto de un jugador concreto a su disparador. Es mutable solo en el
/// contador de usos, que es lo que gobierna el límite (§2).
/// </summary>
internal sealed class PerkSubscription
{
    public PerkSubscription(PerkDefinition perk, MatchPlayer owner)
    {
        Perk = perk;
        Owner = owner;
    }

    public PerkDefinition Perk { get; }

    public MatchPlayer Owner { get; }

    /// <summary>Activaciones consumidas dentro del ámbito del límite vigente.</summary>
    public int Uses { get; set; }

    /// <summary>True si el perk ya agotó su límite en el ámbito actual.</summary>
    public bool LimitReached => Perk.Limit is { } limit && Uses >= limit.Times;
}

/// <summary>
/// Motor de efectos (RT-040..RT-043). Recibe cada evento del partido, decide qué perks se activan en el
/// orden determinista de RT-041 (rareza descendente, id de jugador ascendente, id de perk ascendente),
/// aplica sus efectos y registra la activación.
/// <para>
/// El motor solo se construye si algún jugador en campo lleva perks, un objeto equipado (RF-075..078) o
/// su equipo lleva consumibles (RF-080..085): sin nada de eso <see cref="MatchEngine"/> no tiene
/// <c>EffectEngine</c> y no paga absolutamente nada (§3). Con 0 objetos y 0 consumibles la secuencia de
/// eventos es exactamente la de antes de que existieran (comprobado con la huella de DeterminismTests).
/// </para>
///
/// <para><b>Orden de resolución con equipamiento (extensión de RT-041).</b> RT-041 fija el orden entre
/// perks simultáneos; con objetos y consumibles en el partido hacen falta dos reglas más, y las tres
/// juntas son:
/// <list type="number">
/// <item><b>Objetos equipados, antes que cualquier perk.</b> Se aplican al construir el motor, es decir
/// antes de publicar <c>MATCH_START</c>, recorriendo a los jugadores por <b>id ascendente</b> y, dentro de
/// cada objeto, primero sus <c>effects</c> y después sus <c>drawbackEffects</c>, en el orden del dato. Un
/// objeto es equipo que el jugador ya lleva puesto cuando el árbitro pita: la condición de un perk que
/// mire un atributo tiene que ver al portador <b>ya equipado</b>, no a medio vestir. Como cada jugador
/// lleva un único objeto (RF-076), el id de jugador basta para ordenar y no hace falta un tercer
/// criterio.</item>
/// <item><b>Perks</b>, con el criterio de RT-041 intacto: rareza descendente, id de jugador ascendente,
/// id de perk ordinal ascendente.</item>
/// <item><b>Consumibles, después de todo lo demás.</b> Se resuelven al principio del tick, antes de que
/// nadie decida nada, en el orden en que están equipados (equipo local antes que visitante). Llegan
/// siempre "más tarde" que los objetos, que están puestos desde el tick 0; el único empate posible es un
/// consumible manual pulsado en el tick 0, y ahí también va después.</item>
/// </list>
/// El orden importa poco numéricamente —los modificadores de atributo y de probabilidad son sumas
/// enteras y conmutativas— pero tiene que estar fijado igualmente: las condiciones NCalc de los perks
/// <b>leen</b> atributos y probabilidades, así que quién aplica antes cambia lo que el siguiente ve.</para>
/// </summary>
internal sealed class EffectEngine : IPerkLinks
{
    private static readonly int EventTypeCount = Enum.GetValues<EventType>().Length;

    private readonly MatchEngine _engine;
    private readonly MatchPlayer[] _players;
    private readonly MatchReportBuilder _report;
    private readonly Modifiers _modifiers;
    private readonly PerkSubscription[] _all;
    private readonly PerkSubscription[][] _byTrigger;
    private readonly SortedDictionary<string, int>[] _counters;
    private readonly SortedDictionary<string, int>[] _counterDeltas;
    private readonly int[] _saves;
    private readonly LinkTable? _links;
    private readonly List<MatchPlayer> _targets = new();
    private readonly ConsumableSlot[] _consumables;
    private readonly int _maxDepth;

    private int _depth;

    public EffectEngine(MatchEngine engine, MatchPlayer[] players, MatchReportBuilder report, int maxDepth)
    {
        _engine = engine;
        _players = players;
        _report = report;
        _maxDepth = maxDepth;
        _modifiers = new Modifiers(players);
        _counters = new SortedDictionary<string, int>[players.Length];
        _counterDeltas = new SortedDictionary<string, int>[players.Length];
        _saves = new int[players.Length];

        var all = new List<PerkSubscription>();
        for (int i = 0; i < players.Length; i++)
        {
            _counters[i] = new SortedDictionary<string, int>(StringComparer.Ordinal);
            _counterDeltas[i] = new SortedDictionary<string, int>(StringComparer.Ordinal);
            var definition = players[i].Definition;

            // Habilidad racial (RF-031b, ADR 0026): un perk más de data/perks/ con su campo race puesto,
            // asignado a toda la plantilla de esa raza y **sin ocupar slot**, porque no está en
            // definition.Perks y por tanto no cuenta para Progression.PerkSlots. Se suscribe aquí, junto
            // a los perks que el jugador sí lleva, para que comparta motor de efectos, límites, orden de
            // RT-041 y descripción generada (RT-035): es imposible que su texto y su efecto divirjan.
            var ability = RacialAbility(engine.Catalog, definition);
            if (ability is not null)
            {
                all.Add(new PerkSubscription(ability, players[i]));
                if (ability.AccumulatesAcrossMatches)
                {
                    SeedCounters(i, ability);
                }
            }

            for (int p = 0; p < definition.Perks.Count; p++)
            {
                var perk = engine.Catalog.Perks.Get(definition.Perks[p]);

                // ADR 0023 §4: un perk exclusivo de raza exige la etiqueta de especie para surtir efecto,
                // de modo que asignárselo a un mercenario de otra raza (RF-110/111) no funciona. Es
                // restricción de aplicación; la de aparición la aplica el generador de recompensas.
                if (perk.Race is { } required
                    && !players[i].Definition.HasTag(required.ToString()))
                {
                    continue;
                }

                all.Add(new PerkSubscription(perk, players[i]));

                // Los contadores de un perk que acumula entre partidos arrancan con el valor guardado en
                // el jugador (RF-070, §6); los de un perk que no acumula empiezan siempre a 0.
                if (perk.AccumulatesAcrossMatches)
                {
                    SeedCounters(i, perk);
                }
            }
        }

        // RT-041: rareza descendente, id de jugador ascendente, id de perk ordinal ascendente. El orden se
        // calcula una vez aquí y no se vuelve a tocar en todo el partido.
        all.Sort(static (a, b) =>
        {
            int byRarity = ((int)b.Perk.Rarity).CompareTo((int)a.Perk.Rarity);
            if (byRarity != 0)
            {
                return byRarity;
            }

            int byOwner = a.Owner.Id.CompareTo(b.Owner.Id);
            return byOwner != 0 ? byOwner : string.CompareOrdinal(a.Perk.Id, b.Perk.Id);
        });

        _all = all.ToArray();

        // Los vínculos se resuelven **una sola vez** aquí (RF-044, ADR 0021, §2.4) y solo si algún perk en
        // campo declara relaciones: sin perks de alineación, la tabla no llega a construirse.
        bool anyLinks = false;
        for (int i = 0; i < _all.Length && !anyLinks; i++)
        {
            anyLinks = _all[i].Perk.Links.Count > 0;
        }

        _links = anyLinks ? new LinkTable(players) : null;
        _byTrigger = new PerkSubscription[EventTypeCount][];
        for (int type = 0; type < EventTypeCount; type++)
        {
            _byTrigger[type] = _all.Where(s => (int)s.Perk.Trigger == type).ToArray();
        }

        // El equipamiento se pone ANTES de que empiece el partido: cuando MATCH_START se publica, los
        // objetos ya están aplicados (ver el orden de resolución en la documentación de la clase).
        ApplyEquippedItems();
        _consumables = BuildConsumables(engine.Setup);
    }

    /// <summary>Modificadores activos; el motor los consulta en cada resolución probabilística (§2).</summary>
    public Modifiers Modifiers => _modifiers;

    /// <summary>Tabla de vínculos del partido, o null si ningún perk en campo declara relaciones.</summary>
    public LinkTable? Links => _links;

    /// <summary>
    /// Perk de habilidad racial del jugador (ADR 0026), o null si su raza no declara ninguna. No se
    /// comprueba la etiqueta de especie: la habilidad se concede por la raza del jugador, que es lo que
    /// hace que un mercenario conserve la suya y no gane la del club (RF-110/111).
    /// </summary>
    public static PerkDefinition? RacialAbility(Catalog catalog, PlayerDefinition definition)
    {
        string ability = catalog.Race(definition.Race).Ability;
        if (ability.Length == 0)
        {
            return null;
        }

        return catalog.Perks.Find(ability)
            ?? throw new InvalidOperationException(
                $"la raza {definition.Race} declara la habilidad '{ability}' y no está en data/perks/");
    }

    /// <summary>Suscripciones en el orden de RT-041, para tests y diagnóstico.</summary>
    public IReadOnlyList<PerkSubscription> Subscriptions => _all;

    /// <summary>
    /// Publica un evento del partido y devuelve <c>false</c> si algún perk lo canceló (§2, cancelEvent).
    /// La profundidad de recursión es la del contexto actual: si el evento se emite mientras se están
    /// aplicando efectos, entra un nivel más abajo (RT-042).
    /// </summary>
    public bool Publish(in MatchEvent evt) => PublishAtDepth(evt, _depth);

    /// <summary>Publica el evento a una profundidad explícita (RT-042). Separado para poder probarlo.</summary>
    public bool PublishAtDepth(in MatchEvent evt, int depth)
    {
        // Los eventos sin actor (§2) se evalúan una vez por perk con actor = owner y sin comprobar el
        // alcance: no hay ningún jugador del que el evento "sea".
        bool actorless = IsActorless(evt.Type);
        MatchPlayer? actor = actorless ? null : _engine.PlayerById(evt.Actor);
        MatchPlayer? target = actorless ? null : _engine.PlayerById(evt.Target);
        MatchPlayer? opponent = actorless ? null : _engine.PlayerById(evt.Opponent);

        // El par de la resolución que viene se fija siempre, tenga o no suscriptores este evento: el motor
        // publica PASS_ATTEMPTED, TACKLE y SHOT antes de tirar sus dados, y es ahí donde un modificador
        // por par (ADR 0021, §2.4) sabe contra quién se está jugando la acción.
        _modifiers.SetResolutionContext(evt.Type, actor, target, opponent);

        // Estadísticas del partido que el motor no lleva por jugador y stat() sí expone (RF-119). Se
        // cuentan del propio flujo de eventos, así que no hay una segunda contabilidad que desincronizar.
        if (evt.Type == EventType.Save && actor is not null)
        {
            _saves[actor.Index]++;
        }

        var subscriptions = _byTrigger[(int)evt.Type];
        if (subscriptions.Length == 0)
        {
            return true;
        }

        if (depth > _maxDepth)
        {
            _report.RecursionCuts++;
            return true;
        }

        bool cancelled = false;
        for (int i = 0; i < subscriptions.Length; i++)
        {
            var subscription = subscriptions[i];
            if (!actorless && !ScopeMatches(subscription.Perk.Scope, subscription.Owner, actor, target))
            {
                continue;
            }

            if (subscription.LimitReached)
            {
                continue;
            }

            var context = new ConditionContext(
                _engine,
                this,
                subscription.Owner,
                actorless ? subscription.Owner : actor,
                target,
                opponent,
                evt.Detail);

            bool holds;
            try
            {
                holds = subscription.Perk.CompiledCondition.Evaluate(context);
            }
            catch (Exception ex) when (ex is not InvalidOperationException)
            {
                throw new InvalidOperationException(
                    $"la condición del perk '{subscription.Perk.Id}' falló en partido: {ex.Message}", ex);
            }

            var effects = holds ? subscription.Perk.Effects : subscription.Perk.ElseEffects;
            if (effects.Count == 0)
            {
                continue;
            }

            _depth = depth + 1;
            try
            {
                if (ApplyEffects(subscription, effects, context))
                {
                    cancelled = true;
                }
            }
            finally
            {
                _depth = depth;
            }

            subscription.Uses++;
            _report.PerkActivations.Add(new PerkActivation(
                subscription.Perk.Id,
                subscription.Owner.Id,
                evt.Tick,
                evt.Type,
                holds ? evt.Detail : evt.Detail + ":else"));
        }

        return !cancelled;
    }

    /// <summary>Retira los modificadores de jugada y reinicia los límites <c>per: play</c> (§2).</summary>
    public void EndPlay()
    {
        _modifiers.ExpirePlayModifiers();
        ResetLimits(LimitScope.Play);
    }

    /// <summary>Reinicia los límites <c>per: mob</c> al empezar el gol de oro de la turba (§2).</summary>
    public void StartMob() => ResetLimits(LimitScope.Mob);

    // ---------------------------------------------------------------- equipamiento (RF-075..085)

    /// <summary>Ticks lógicos por segundo (RT-020). Lo necesita el disparador "últimos N segundos" (RF-083).</summary>
    private const int TicksPerSecond = 15;

    /// <summary>
    /// Consumible equipado por un equipo, con la marca de gastado. Un consumible se resuelve <b>una sola
    /// vez</b> por partido (RF-085, "se consumen al usarse").
    /// </summary>
    private sealed class ConsumableSlot
    {
        public ConsumableSlot(MatchConsumable consumable, int team)
        {
            Consumable = consumable;
            Team = team;
        }

        public MatchConsumable Consumable { get; }

        public int Team { get; }

        public bool Used { get; set; }
    }

    /// <summary>Objetos equipados registrados en el informe, en el orden en que se aplicaron (RT-043).</summary>
    public IReadOnlyList<ItemActivation> ItemActivations => _report.ItemActivations;

    /// <summary>
    /// Aplica el objeto de cada jugador (RF-075..078), por id ascendente y antes de que empiece el
    /// partido. No ocupa slot de perk (RF-076): no se suscribe a ningún disparador, no tiene condición y
    /// no aparece en <see cref="Subscriptions"/>; es un pasivo que dura todo el partido.
    ///
    /// <para>Un objeto <b>restringido</b> cuyo portador no lleva la etiqueta no aporta nada —tampoco su
    /// contrapartida— y así queda anotado en el informe, que es lo que permite distinguir "no funcionó"
    /// de "no lo llevaba".</para>
    /// </summary>
    private void ApplyEquippedItems()
    {
        for (int i = 0; i < _players.Length; i++)
        {
            var owner = _players[i];
            if (owner.Definition.Item is not { } item)
            {
                continue;
            }

            if (!item.AppliesTo(owner.Definition))
            {
                _report.ItemActivations.Add(
                    new ItemActivation(item.Id, owner.Id, owner.Team, 0, "restricted:" + item.RequiredTag));
                continue;
            }

            int applied = ApplyPassiveEffects(owner, item.Effects);
            applied += ApplyPassiveEffects(owner, item.DrawbackEffects);
            _report.ItemActivations.Add(new ItemActivation(
                item.Id,
                owner.Id,
                owner.Team,
                applied,
                item.DrawbackEffects.Count > 0 ? "cursed" : "equipped"));
        }
    }

    /// <summary>Aplica una lista de efectos pasivos al portador y devuelve cuántos surtieron efecto.</summary>
    private int ApplyPassiveEffects(MatchPlayer owner, IReadOnlyList<EffectDefinition> effects)
    {
        int applied = 0;
        for (int i = 0; i < effects.Count; i++)
        {
            if (ApplyPassiveEffect(owner, effects[i]))
            {
                applied++;
            }
        }

        return applied;
    }

    /// <summary>
    /// Un efecto pasivo: sin disparador, sin condición, sin contador y con duración de partido. Es el
    /// subconjunto que un objeto o un consumible pueden declarar (el cargador de <c>data/items</c> y
    /// <c>data/consumables</c> ya recorta a <c>modifyAttribute</c> y <c>modifyProbability</c>; los otros
    /// tres se admiten aquí porque son igual de pasivos y no cuesta nada). Los que necesitan contexto de
    /// evento —cancelar, contar, derribar, mover el criterio— se ignoran a propósito: un objeto no
    /// reacciona a nada.
    /// </summary>
    private bool ApplyPassiveEffect(MatchPlayer player, EffectDefinition effect)
    {
        switch (effect.Type)
        {
            case EffectType.ModifyAttribute:
                _modifiers.AddAttribute(player.Index, effect.Attribute, effect.Value, expiresAtPlayEnd: false);
                return true;
            case EffectType.ModifyProbability:
                _modifiers.AddProbability(player.Index, effect.Probability, effect.Value, expiresAtPlayEnd: false);
                return true;
            case EffectType.ModifyLeash:
                _modifiers.AddLeash(player.Index, effect.Value, expiresAtPlayEnd: false);
                return true;
            case EffectType.ModifyKnockdownTicks:
                _modifiers.AddKnockdownTicks(player.Index, effect.Value);
                return true;
            case EffectType.Immunity:
                _modifiers.AddImmunity(player.Index, effect.Immunity);
                return true;
            default:
                return false;
        }
    }

    /// <summary>Consumibles equipados de los dos equipos, local primero (RF-080..083).</summary>
    private static ConsumableSlot[] BuildConsumables(MatchSetup setup)
    {
        var home = setup.Home.Consumables;
        var away = setup.Away.Consumables;
        if (home.Count == 0 && away.Count == 0)
        {
            return Array.Empty<ConsumableSlot>();
        }

        var slots = new List<ConsumableSlot>(home.Count + away.Count);
        for (int i = 0; i < home.Count; i++)
        {
            slots.Add(new ConsumableSlot(home[i], 0));
        }

        for (int i = 0; i < away.Count; i++)
        {
            slots.Add(new ConsumableSlot(away[i], 1));
        }

        return slots.ToArray();
    }

    /// <summary>
    /// Resuelve los consumibles cuyo disparador se cumple (RF-081..083). Lo llama el motor al principio
    /// de cada tick, antes de que nadie decida nada, así que el disparador ve el estado consolidado del
    /// tick anterior y el efecto vale ya para este tick.
    ///
    /// <para>El efecto de un consumible <b>no tiene portador</b>: lo usa el entrenador y alcanza a todos
    /// los jugadores de su equipo que estén en el campo en ese instante, hasta el final del partido. El
    /// campo <c>target</c> del efecto se ignora por eso.</para>
    /// </summary>
    public void ResolveConsumables()
    {
        for (int i = 0; i < _consumables.Length; i++)
        {
            var slot = _consumables[i];
            if (slot.Used || !TriggerHolds(slot))
            {
                continue;
            }

            slot.Used = true;
            for (int p = 0; p < _players.Length; p++)
            {
                if (_players[p].Team == slot.Team && _players[p].OnPitch)
                {
                    ApplyPassiveEffects(_players[p], slot.Consumable.Effects);
                }
            }

            _report.ConsumableActivations.Add(new ConsumableActivation(
                slot.Consumable.Id, slot.Team, _engine.Tick, TriggerName(slot.Consumable.Trigger)));
            _engine.EmitConsumableUsed(slot.Consumable, slot.Team);
        }
    }

    /// <summary>
    /// ¿Se cumple el disparador (RF-083)? Todo entero y sin azar: el mismo estado da siempre la misma
    /// respuesta.
    /// </summary>
    private bool TriggerHolds(ConsumableSlot slot)
    {
        var consumable = slot.Consumable;
        int team = slot.Team;
        return consumable.Trigger switch
        {
            // El manual es una entrada del jugador guardada en el estado inicial (docs/arquitectura.md):
            // -1 significa "no lo pulsó", y en /Balance no hay quien lo pulse.
            ConsumableTrigger.Manual => consumable.ManualTick >= 0 && _engine.Tick >= consumable.ManualTick,
            ConsumableTrigger.ScoreBehind => _engine.ScoreDiff(team) < 0,

            // "Marcador empatado" es un disparador, no un estado inicial: un 0-0 recién sacado del centro
            // no es que el marcador se haya igualado, así que hace falta al menos un gol en el partido.
            ConsumableTrigger.ScoreTied => _engine.ScoreDiff(team) == 0 && _engine.GoalsOf(team) > 0,
            ConsumableTrigger.LastSeconds => _engine.TicksLeftInRegulation
                <= (consumable.Threshold > 0 ? consumable.Threshold : 20) * TicksPerSecond,
            ConsumableTrigger.MobStart => _engine.IsMob,
            ConsumableTrigger.OwnInjury => TeamHasInjured(team),
            ConsumableTrigger.OwnRedCard => TeamHasSentOff(team),
            ConsumableTrigger.GoalsConceded => _engine.GoalsOf(1 - team) >= Math.Max(1, consumable.Threshold),
            _ => _engine.BiasFor(team) < consumable.Threshold,
        };
    }

    /// <summary>Nombre estable del disparador para el informe y el log (RF-119, RT-043).</summary>
    private static string TriggerName(ConsumableTrigger trigger) => trigger switch
    {
        ConsumableTrigger.Manual => "manual",
        ConsumableTrigger.ScoreBehind => "scoreBehind",
        ConsumableTrigger.ScoreTied => "scoreTied",
        ConsumableTrigger.LastSeconds => "lastSeconds",
        ConsumableTrigger.MobStart => "mobStart",
        ConsumableTrigger.OwnInjury => "ownInjury",
        ConsumableTrigger.OwnRedCard => "ownRedCard",
        ConsumableTrigger.GoalsConceded => "goalsConceded",
        _ => "refereeBiasBelow",
    };

    private bool TeamHasInjured(int team)
    {
        for (int i = 0; i < _players.Length; i++)
        {
            if (_players[i].Team == team && _players[i].Injured)
            {
                return true;
            }
        }

        return false;
    }

    private bool TeamHasSentOff(int team)
    {
        for (int i = 0; i < _players.Length; i++)
        {
            if (_players[i].Team == team && _players[i].State == PlayerState.SentOff)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Contador del jugador (RF-070); 0 si nunca se ha escrito.</summary>
    public int Counter(MatchPlayer player, string name) =>
        _counters[player.Index].TryGetValue(name, out int value) ? value : 0;

    /// <summary>
    /// Contadores que los perks con <c>accumulatesAcrossMatches: true</c> han sumado en este partido,
    /// ordenados por id de jugador y, dentro de cada jugador, por nombre de contador ordinal (§6).
    /// </summary>
    public IReadOnlyList<PlayerCounterDelta> CounterDeltas()
    {
        var result = new List<PlayerCounterDelta>();
        for (int i = 0; i < _players.Length; i++)
        {
            foreach (var (name, delta) in _counterDeltas[i])
            {
                result.Add(new PlayerCounterDelta(_players[i].Id, name, delta));
            }
        }

        result.Sort(static (a, b) =>
        {
            int byPlayer = a.PlayerId.CompareTo(b.PlayerId);
            return byPlayer != 0 ? byPlayer : string.CompareOrdinal(a.Counter, b.Counter);
        });
        return result;
    }

    /// <summary>Resumen de activaciones por (perk, jugador), ordenado por id de perk y luego de jugador.</summary>
    public IReadOnlyList<PerkActivationSummary> Summary()
    {
        var counts = new int[_all.Length];
        var activations = _report.PerkActivations;
        for (int i = 0; i < activations.Count; i++)
        {
            for (int s = 0; s < _all.Length; s++)
            {
                if (_all[s].Owner.Id == activations[i].OwnerId
                    && string.Equals(_all[s].Perk.Id, activations[i].PerkId, StringComparison.Ordinal))
                {
                    counts[s]++;
                    break;
                }
            }
        }

        var summary = new List<PerkActivationSummary>(_all.Length);
        for (int s = 0; s < _all.Length; s++)
        {
            summary.Add(new PerkActivationSummary(_all[s].Perk.Id, _all[s].Owner.Id, counts[s]));
        }

        summary.Sort(static (a, b) =>
        {
            int byPerk = string.CompareOrdinal(a.PerkId, b.PerkId);
            return byPerk != 0 ? byPerk : a.OwnerId.CompareTo(b.OwnerId);
        });
        return summary;
    }

    /// <summary>
    /// Eventos sin actor (§2): se evalúan una vez por perk con <c>actor = owner</c> y sin comprobar el
    /// alcance. La lista es por tipo de evento y no por "¿trae Actor?", como manda la especificación:
    /// PLAY_START sí lleva actor en el motor, pero como disparador es un evento de partido, no de jugador.
    /// </summary>
    internal static bool IsActorless(EventType type) => type
        is EventType.MatchStart
        or EventType.MatchEnd
        or EventType.MobStart
        or EventType.RefereeLeaves
        or EventType.PlayStart
        or EventType.PlayEnd;

    private static bool ScopeMatches(PerkScope scope, MatchPlayer owner, MatchPlayer? actor, MatchPlayer? target) =>
        scope switch
        {
            PerkScope.Actor => actor is not null && actor.Id == owner.Id,
            PerkScope.Target => target is not null && target.Id == owner.Id,
            PerkScope.Team => actor is not null && actor.Team == owner.Team,
            PerkScope.OpposingTeam => actor is not null && actor.Team != owner.Team,
            _ => true,
        };

    private void SeedCounters(int playerIndex, PerkDefinition perk)
    {
        var stored = _players[playerIndex].Definition.Counters;
        for (int e = 0; e < perk.Effects.Count; e++)
        {
            SeedCounter(playerIndex, stored, perk.Effects[e]);
        }

        for (int e = 0; e < perk.ElseEffects.Count; e++)
        {
            SeedCounter(playerIndex, stored, perk.ElseEffects[e]);
        }
    }

    private void SeedCounter(int playerIndex, IReadOnlyDictionary<string, int> stored, EffectDefinition effect)
    {
        if (effect.Type != EffectType.AddCounter || effect.Counter.Length == 0)
        {
            return;
        }

        if (stored.TryGetValue(effect.Counter, out int value))
        {
            _counters[playerIndex][effect.Counter] = value;
        }
    }

    private void ResetLimits(LimitScope scope)
    {
        for (int i = 0; i < _all.Length; i++)
        {
            if (_all[i].Perk.Limit is { } limit && limit.Per == scope)
            {
                _all[i].Uses = 0;
            }
        }
    }

    /// <summary>Aplica los efectos en orden y devuelve true si alguno canceló el evento.</summary>
    private bool ApplyEffects(PerkSubscription subscription, IReadOnlyList<EffectDefinition> effects, in ConditionContext context)
    {
        bool cancelled = false;

        // RF-093 vía 2: un perk marcado como letal mata a los rivales sobre los que aplica sus efectos,
        // y solo a ellos. La lista se aloja únicamente si el perk es letal (ninguno del catálogo lo es
        // todavía) y es local, no un buffer compartido, porque matar publica DEATH y eso puede reentrar
        // aquí con otro perk.
        List<MatchPlayer>? victims = subscription.Perk.Lethal ? new List<MatchPlayer>() : null;
        for (int i = 0; i < effects.Count; i++)
        {
            var effect = effects[i];
            if (effect.Type == EffectType.CancelEvent)
            {
                cancelled = true;
                continue;
            }

            if (effect.Type == EffectType.ModifyBias)
            {
                // El signo del dato es "a favor del equipo del portador"; el criterio del árbitro del
                // motor es positivo a favor del local (RF-060), así que se invierte para el visitante.
                _engine.ApplyBiasDelta(subscription.Owner.Team == 0 ? effect.Value : -effect.Value);
                continue;
            }

            if (effect.Type == EffectType.AddCounter)
            {
                AddCounter(subscription, effect.Counter, effect.Value);
                continue;
            }

            if (effect.Type == EffectType.ModifyExperience)
            {
                // Actúa **fuera** del partido (ADR 0026, Humanos): lo lee Sim.Progression.Progression al
                // repartir la experiencia. Dentro del partido solo deja su activación en el informe.
                continue;
            }

            int value = EffectValue(subscription, effect);
            bool expiresAtPlayEnd = effect.Duration == EffectDuration.Play;

            // Un modificador **por par** (ADR 0021) solo existe si hay una resolución que enfrente al
            // portador con su vinculado, y el vinculado es siempre un COMPAÑERO: la única resolución que
            // enfrenta a dos compañeros es el pase de uno al otro. En los demás canales -intercept,
            // tackle, dribble, remate, parada- la contraparte de la tirada es un rival, así que el par
            // (portador, compañero) no se forma nunca y el bono no se aplicaba jamás: medido, quitar
            // covering_shadow o pivot_duo de una build no cambiaba ni un partido (§16, costura 4).
            // Fuera del pase, "al vinculado" se lee como lo que dice: el bono es del compañero vinculado.
            bool pairwise = effect.Target is EffectTarget.Linked or EffectTarget.LinkedWithTag
                && effect.Probability == ProbabilityKind.Pass;
            ResolveTargets(subscription, effect, context);
            for (int t = 0; t < _targets.Count; t++)
            {
                var player = _targets[t];
                if (victims is not null && IsLethalVictim(subscription.Owner, player) && !victims.Contains(player))
                {
                    victims.Add(player);
                }

                switch (effect.Type)
                {
                    case EffectType.ModifyAttribute:
                        _modifiers.AddAttribute(player.Index, effect.Attribute, value, expiresAtPlayEnd);
                        break;
                    case EffectType.ModifyLeash:
                        _modifiers.AddLeash(player.Index, value, expiresAtPlayEnd);
                        break;
                    case EffectType.ModifyProbability when pairwise:
                        // Objetivo vinculado: el bono no es del portador ni del vinculado por separado,
                        // sino **del par** (ADR 0021). Vale en la resolución que enfrenta a los dos -el
                        // pase hacia ese compañero concreto- y en ninguna otra.
                        _modifiers.AddPairProbability(
                            subscription.Owner.Index, player.Index, effect.Probability, value, expiresAtPlayEnd);
                        break;
                    case EffectType.ModifyProbability:
                        _modifiers.AddProbability(player.Index, effect.Probability, value, expiresAtPlayEnd);
                        break;
                    case EffectType.ModifyKnockdownTicks:
                        _modifiers.AddKnockdownTicks(player.Index, value);
                        break;
                    case EffectType.Immunity:
                        _modifiers.AddImmunity(player.Index, effect.Immunity);
                        break;
                    case EffectType.SetState:
                        _engine.KnockDown(player, effect.Ticks);
                        break;
                    default:
                        break;
                }
            }
        }

        if (victims is not null)
        {
            // Orden fijo: id de jugador ascendente, igual que el resto del motor (RT-041).
            victims.Sort(static (a, b) => a.Id.CompareTo(b.Id));
            for (int v = 0; v < victims.Count; v++)
            {
                _engine.Kill(victims[v], "perk:" + subscription.Perk.Id);
            }
        }

        return cancelled;
    }

    /// <summary>
    /// Quién puede morir por un perk letal (RF-093). Tres condiciones, y las tres son necesarias: es
    /// <b>rival</b> del portador del perk (de ahí "perk rival letal"), está <b>en el campo</b>, y
    /// <b>no está sano</b> —o llegó al partido arrastrando una lesión (RF-090) o ya se ha lesionado en
    /// este—. La tercera es la que hace que "un jugador sano nunca muere" sea una garantía del sistema y
    /// no una casualidad: sin ella, un perk letal mataría al primero al que alcanzara.
    /// </summary>
    private static bool IsLethalVictim(MatchPlayer owner, MatchPlayer candidate) =>
        candidate.Team != owner.Team
        && candidate.OnPitch
        && (candidate.Injured || candidate.Definition.PhysicalState != PhysicalState.Healthy);

    private int EffectValue(PerkSubscription subscription, EffectDefinition effect)
    {
        if (!effect.UsesCounter)
        {
            return effect.Value;
        }

        int divisor = effect.CounterDivisor > 0 ? effect.CounterDivisor : 1;
        int value = effect.ValuePerCounter * Counter(subscription.Owner, effect.Counter) / divisor;
        if (effect.MaxValue != 0)
        {
            int bound = Math.Abs(effect.MaxValue);
            value = Math.Clamp(value, -bound, bound);
        }

        return value;
    }

    private void AddCounter(PerkSubscription subscription, string name, int value)
    {
        if (name.Length == 0 || value == 0)
        {
            return;
        }

        int index = subscription.Owner.Index;
        _counters[index][name] = Counter(subscription.Owner, name) + value;
        if (subscription.Perk.AccumulatesAcrossMatches)
        {
            _counterDeltas[index][name] = (_counterDeltas[index].TryGetValue(name, out int delta) ? delta : 0) + value;
        }
    }

    /// <summary>
    /// Rellena el buffer de objetivos del efecto (§2). Los objetivos colectivos se recorren por id
    /// ascendente porque <see cref="_players"/> ya está ordenado así (RT-041).
    /// </summary>
    private void ResolveTargets(PerkSubscription subscription, EffectDefinition effect, in ConditionContext context)
    {
        _targets.Clear();
        var owner = context.Owner;
        if (effect.Target is EffectTarget.Linked or EffectTarget.LinkedWithTag)
        {
            ResolveLinkedTargets(subscription, effect, owner);
            return;
        }

        switch (effect.Target)
        {
            case EffectTarget.Actor:
                AddSingle(context.Actor);
                return;
            case EffectTarget.Target:
                AddSingle(context.Target);
                return;
            case EffectTarget.Opponent:
                AddSingle(context.Opponent);
                return;
            case EffectTarget.Owner:
                AddSingle(owner);
                return;
            default:
                break;
        }

        for (int i = 0; i < _players.Length; i++)
        {
            var player = _players[i];
            if (!player.OnPitch)
            {
                continue;
            }

            bool sameTeam = player.Team == owner.Team;
            bool matches = effect.Target switch
            {
                EffectTarget.Team => sameTeam,
                EffectTarget.OpposingTeam => !sameTeam,
                EffectTarget.Adjacent => sameTeam && player.Id != owner.Id && Pitch.AreAdjacent(owner.HomeCell, player.HomeCell),
                EffectTarget.WithTag => sameTeam && player.Definition.HasTag(effect.TargetTag),
                EffectTarget.AdjacentWithTag => sameTeam
                    && player.Id != owner.Id
                    && Pitch.AreAdjacent(owner.HomeCell, player.HomeCell)
                    && player.Definition.HasTag(effect.TargetTag),
                _ => false,
            };

            if (matches)
            {
                _targets.Add(player);
            }
        }
    }

    /// <summary>
    /// Objetivos vinculados (ADR 0021): un candidato por relación declarada, resuelto antes del partido.
    /// El recorrido sigue el orden de <c>links</c> tal y como está en el dato, que es fijo, y descarta
    /// repetidos (dos relaciones pueden dar el mismo compañero) y a quien ya no está en el campo.
    /// </summary>
    private void ResolveLinkedTargets(PerkSubscription subscription, EffectDefinition effect, MatchPlayer owner)
    {
        if (_links is null)
        {
            return;
        }

        var relations = subscription.Perk.Links;
        for (int i = 0; i < relations.Count; i++)
        {
            var linked = _links.Linked(owner, relations[i]);
            if (linked is null || !linked.OnPitch || _targets.Contains(linked))
            {
                continue;
            }

            if (effect.Target == EffectTarget.LinkedWithTag && !linked.Definition.HasTag(effect.TargetTag))
            {
                continue;
            }

            _targets.Add(linked);
        }
    }

    // ---------------------------------------------------------------- IPerkLinks (§1.5)

    /// <inheritdoc/>
    public bool HasLink(MatchPlayer player, LinkRelation relation) =>
        _links is not null && _links.HasLink(player, relation);

    /// <inheritdoc/>
    public bool NearAlly(MatchPlayer player, string tag, int cells) => Near(player, tag, cells, sameTeam: true);

    /// <inheritdoc/>
    public bool NearOpponent(MatchPlayer player, string tag, int cells) => Near(player, tag, cells, sameTeam: false);

    /// <inheritdoc/>
    public int Stat(MatchPlayer player, MatchStat stat) => stat switch
    {
        MatchStat.Goals => player.Goals,
        MatchStat.PassesCompleted => player.PassesCompleted,
        MatchStat.TacklesWon => player.TacklesWon,
        MatchStat.Shots => player.Shots,
        _ => _saves[player.Index],
    };

    /// <summary>
    /// Proximidad **real** en el momento del evento (ADR 0021, familia dinámica), con radio en casillas y
    /// comparación al cuadrado para no calcular raíces. Es la distancia entre posiciones, no entre
    /// casillas-hogar: es lo que el jugador ve en el campo.
    /// </summary>
    private bool Near(MatchPlayer player, string tag, int cells, bool sameTeam)
    {
        if (!player.OnPitch)
        {
            return false;
        }

        float radius = cells;
        float limit = radius * radius;
        for (int i = 0; i < _players.Length; i++)
        {
            var other = _players[i];
            if (other.Id == player.Id || !other.OnPitch || (other.Team == player.Team) != sameTeam)
            {
                continue;
            }

            if (!other.Definition.HasTag(tag))
            {
                continue;
            }

            var delta = other.Position - player.Position;
            if ((delta.X * delta.X) + (delta.Y * delta.Y) <= limit)
            {
                return true;
            }
        }

        return false;
    }

    private void AddSingle(MatchPlayer? player)
    {
        if (player is not null)
        {
            _targets.Add(player);
        }
    }
}
