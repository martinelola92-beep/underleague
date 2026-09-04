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
/// El motor solo se construye si algún jugador en campo lleva perks: con 0 perks
/// <see cref="MatchEngine"/> no tiene <c>EffectEngine</c> y no paga absolutamente nada (§3).
/// </para>
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
            bool pairwise = effect.Target is EffectTarget.Linked or EffectTarget.LinkedWithTag;
            ResolveTargets(subscription, effect, context);
            for (int t = 0; t < _targets.Count; t++)
            {
                var player = _targets[t];
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

        return cancelled;
    }

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
