using Underleague.Sim.Data;
using Underleague.Sim.Model;
using Underleague.Sim.Perks;
using Underleague.Sim.Random;
using Underleague.Sim.Run.Systems.Economy;
using Underleague.Sim.Run.Systems.Items;
using ProgressionRules = Underleague.Sim.Progression.Progression;

namespace Underleague.Sim.Run.Systems.Medical;

/// <summary>
/// Tabla de tres resultados del herrero (ADR 0164): porcentajes enteros que suman 100 (RT-023).
/// </summary>
public readonly record struct BlacksmithOdds(int CurePercent, int ImprovePercent, int WorsenPercent);

/// <summary>
/// Clínica (RF-094). Hasta la <b>ADR 0099</b> tenía un solo servicio —un jugador, precio fijo, resultado
/// garantizado— y por eso no había nada que decidir salvo a quién. Ahora ofrece tres, y la decisión es cuál:
/// <list type="number">
/// <item><b>Por pieza</b>, garantizado: <see cref="Treat"/> con el precio de la gravedad
/// (<c>clinicCost</c> / <c>clinicMinorCost</c>). Es el de siempre.</item>
/// <item><b>La plantilla entera</b>, garantizado: <see cref="TreatSquad"/> a <c>clinicSquadCost</c>, tarifa
/// plana que no mira cuántos heridos hay. Cara con uno, barata con cuatro.</item>
/// <item><b>El matasanos</b>: <see cref="Treat"/> con <c>risky</c>. Cuesta <c>clinicRiskyPercent</c>% del
/// precio normal y no garantiza nada: puede no curar (<c>clinicRiskyFailPercent</c>) y además puede
/// <b>empeorar</b> un escalón (<c>clinicRiskyWorsePercent</c>) —sano ← leve ← grave ← <b>muerto</b>—.</item>
/// </list>
///
/// <para>El matasanos cumple las cinco condiciones de la ADR 0048 para que un jugador pueda morir: el
/// porcentaje <b>se ve antes de elegir</b> (RF-012d), se puede evitar sin más que pagar el precio normal, el
/// equipo del muerto vuelve al inventario por el mismo camino que cualquier muerte, y es raro. Lo que añade
/// es que por primera vez la muerte puede venir de una <b>decisión de menú</b>, que es exactamente el juego
/// que el revisor pidió: carnicería administrada.</para>
/// </summary>
public static class MedicalSystem
{
    /// <summary>Trata a un jugador. Con <c>decision.Risky</c> es el matasanos: más barato y sin garantía.</summary>
    public static RunState Treat(RunState state, TreatPlayer decision, EconomyConfig economy) =>
        Treat(state, decision, economy, catalog: null);

    /// <summary>
    /// Igual, pero mirando los perks del jugador: con <paramref name="catalog"/> una inmunidad
    /// <see cref="ImmunityKind.MinorInjuryClinicCost"/> hace que su lesión <b>leve</b> se cure sin
    /// factura. La grave nunca se exime: el perk dice que se cura solo lo que a los demás les cuesta
    /// dinero, no que sea invulnerable.
    /// </summary>
    public static RunState Treat(RunState state, TreatPlayer decision, EconomyConfig economy, Catalog? catalog, ItemCatalog? items = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(decision);
        ArgumentNullException.ThrowIfNull(economy);
        var node = NodeGuards.RequireOpen(state, NodeKind.Clinic, "tratar a un jugador");
        var player = state.GetPlayer(decision.PlayerId);
        RequireNotCrippled(player, decision);
        bool minor = player.PhysicalState == PhysicalState.MinorInjury && player.MinorInjuries > 0;
        if (player.PhysicalState != PhysicalState.SevereInjury && !minor)
        {
            throw new ArgumentException(
                $"el jugador {player.Id} está {player.PhysicalState}: la clínica trata lesiones graves (RF-092, RF-094) y leves (AZ-G, ADR 0090), no a un sano",
                nameof(decision));
        }

        // El exento no pasa por caja ni por el matasanos: su leve simplemente no llega a la clínica.
        if (minor && catalog is not null && IsExemptFromMinorInjuryBill(player, catalog))
        {
            return state.WithPlayer(player with { PhysicalState = PhysicalState.Healthy, MinorInjuries = 0 });
        }

        // AZ-G (ADR 0090): la leve se cura a su propio precio, menor que el de la grave.
        int full = minor ? economy.ClinicMinorCost : economy.ClinicCost;
        int cost = decision.Risky ? RiskyCost(full, economy) : full;
        if (state.Gold < cost)
        {
            throw new ArgumentException(
                $"tratar a {player.Id} cuesta {cost} de oro y la run solo tiene {state.Gold}",
                nameof(decision));
        }

        state = state.AddGold(-cost);
        if (!decision.Risky)
        {
            return state.WithPlayer(player with { PhysicalState = PhysicalState.Healthy, MinorInjuries = 0 });
        }

        // El oro ya está pagado: el matasanos cobra por intentarlo. La tirada es función de (semilla, nodo,
        // tratamientos arriesgados ya hechos en ese nodo), así que repetir la run la reproduce (RT-021/024).
        string rolls = RunState.ClinicRollsPrefix + node.Id;
        int done = state.Counter(rolls);
        var rng = RngStreams.Clinic(state.Seed, node.Id);
        for (int i = 0; i < done; i++)
        {
            rng.Range(0, 100);
        }

        int roll = rng.Range(0, 100);
        state = state.WithCounter(rolls, done + 1);
        if (roll < economy.ClinicRiskyWorsePercent)
        {
            // Grave -> muerto: no es un `PhysicalState = Dead` a secas. La muerte del matasanos tiene las
            // mismas consecuencias de run que la de partido (objeto al almacén, reliquia, Herencia, oro
            // de muerte: DeathConsequences), que es lo que la ADR 0048 (condición 4) exige de cualquier
            // muerte y que aquí se saltaba.
            return player.PhysicalState == PhysicalState.SevereInjury
                ? DeathConsequences.Kill(state, player.Id, PlayerDeathCause.Quack, catalog, economy, items)
                : state.WithPlayer(Worse(player));
        }

        return roll < economy.ClinicRiskyWorsePercent + economy.ClinicRiskyFailPercent
            ? state
            : state.WithPlayer(player with { PhysicalState = PhysicalState.Healthy, MinorInjuries = 0 });
    }

    /// <summary>
    /// Cura a <b>toda</b> la plantilla por una tarifa plana (ADR 0099). Lanza si no hay a quién curar: pagar
    /// por nada no es una jugada, es un error de la interfaz.
    /// </summary>
    public static RunState TreatSquad(RunState state, EconomyConfig economy)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(economy);
        NodeGuards.RequireOpen(state, NodeKind.Clinic, "curar a la plantilla");
        int cost = economy.ClinicSquadCost;
        if (state.Gold < cost)
        {
            throw new ArgumentException(
                $"curar a la plantilla cuesta {cost} de oro y la run solo tiene {state.Gold}",
                nameof(state));
        }

        var roster = new List<RunPlayer>(state.Roster);
        int treated = 0;
        for (int i = 0; i < roster.Count; i++)
        {
            if (!NeedsTreatment(roster[i]))
            {
                continue;
            }

            roster[i] = roster[i] with { PhysicalState = PhysicalState.Healthy, MinorInjuries = 0 };
            treated++;
        }

        if (treated == 0)
        {
            throw new ArgumentException("no hay ningún jugador lesionado que curar", nameof(state));
        }

        return state.AddGold(-cost).WithRoster(roster);
    }

    /// <summary>Precio base del herrero (ADR 0164): un porcentaje del precio del médico, nunca menos de uno.</summary>
    public static int BlacksmithBasePrice(EconomyConfig economy)
    {
        ArgumentNullException.ThrowIfNull(economy);
        return Math.Max(1, economy.ClinicCost * economy.Blacksmith.PricePercent / 100);
    }

    /// <summary>
    /// La tabla de tres resultados del herrero para <paramref name="extraGold"/> de oro invertido (ADR 0164,
    /// RF-095, RF-095b). <b>Es la única fuente</b>: la vista (<c>BlacksmithView</c>) la enseña y
    /// <see cref="Forge"/> la tira, así que lo que se ve es lo que se juega. Suma siempre 100.
    /// </summary>
    public static BlacksmithOdds BlacksmithOddsFor(EconomyConfig economy, int extraGold)
    {
        ArgumentNullException.ThrowIfNull(economy);
        var config = economy.Blacksmith;
        if (extraGold < 0 || extraGold > config.MaxExtraGold)
        {
            throw new ArgumentOutOfRangeException(
                nameof(extraGold),
                extraGold,
                $"el oro extra del herrero va de 0 a {config.MaxExtraGold} (rendimiento decreciente con tope, RF-095b)");
        }

        int shift = 0;
        for (int i = 0; i < extraGold; i++)
        {
            shift += config.ShiftByExtraGold[i];
        }

        int toCure = shift * config.CureSharePercent / 100;
        return new BlacksmithOdds(
            config.BaseCurePercent + toCure,
            config.BaseImprovePercent + (shift - toCure),
            config.BaseWorsenPercent - shift);
    }

    /// <summary>Ranuras que el jugador ya tiene ocupadas por una prótesis.</summary>
    public static IReadOnlyList<string> OccupiedSlots(RunPlayer player)
    {
        ArgumentNullException.ThrowIfNull(player);
        var slots = new List<string>(player.Prostheses.Count);
        for (int i = 0; i < player.Prostheses.Count; i++)
        {
            slots.Add(player.Prostheses[i].Slot);
        }

        return slots;
    }

    /// <summary>True si al jugador le queda alguna ranura libre en la que el catálogo pueda instalar algo.</summary>
    public static bool HasFreeProsthesisSlot(RunPlayer player, ProsthesisCatalog prostheses)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(prostheses);
        if (player.Prostheses.Count >= RunRules.MaxProstheses)
        {
            return false;
        }

        var occupied = OccupiedSlots(player);
        for (int i = 0; i < prostheses.All.Count; i++)
        {
            if (!occupied.Contains(prostheses.All[i].Slot))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// El herrero (ADR 0164, RF-095, RF-095b, RF-095c). Paga el precio base más el oro extra y tira una vez la
    /// tabla de <see cref="BlacksmithOddsFor"/> con un flujo propio derivado de (semilla, nodo, jugador)
    /// (<see cref="OfferStream"/>, desplazamiento 9000 + id de jugador; nunca el de partido, RT-022). El jugador
    /// sale <b>sano</b> en los tres casos; la curación lo deja como estaba, y la mejora y el empeoramiento le
    /// instalan una prótesis en una ranura libre —con su efecto aplicado a los atributos y la etiqueta
    /// <c>Scrap</c>—. A la tercera prótesis pierde la etiqueta de especie y gana <c>Automaton</c>. Nunca mata.
    /// </summary>
    public static RunState Forge(RunState state, ForgePlayer decision, EconomyConfig economy, ProsthesisCatalog prostheses)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(decision);
        ArgumentNullException.ThrowIfNull(economy);
        ArgumentNullException.ThrowIfNull(prostheses);
        var node = NodeGuards.RequireOpen(state, NodeKind.Clinic, "acudir al herrero");
        var player = state.GetPlayer(decision.PlayerId);
        RequireNotCrippled(player, decision);
        if (player.PhysicalState != PhysicalState.SevereInjury)
        {
            throw new ArgumentException(
                $"el jugador {player.Id} está {player.PhysicalState}: el herrero trata lesiones graves (ADR 0164, RF-095)",
                nameof(decision));
        }

        if (!HasFreeProsthesisSlot(player, prostheses))
        {
            throw new ArgumentException(
                $"el jugador {player.Id} no tiene ninguna ranura libre para una prótesis (una ranura ocupada no se repite, ADR 0164)",
                nameof(decision));
        }

        if (player.Id >= BlacksmithStreamSpan)
        {
            // OfferStream numera nodeId * 10_000 + desplazamiento: con 9000 + id, un id >= 1000 caería en el
            // flujo del nodo siguiente y dejaría de ser independiente (RT-022).
            throw new InvalidOperationException(
                $"el jugador {player.Id} tiene un id demasiado alto para el flujo del herrero (< {BlacksmithStreamSpan}, ADR 0164)");
        }

        var odds = BlacksmithOddsFor(economy, decision.ExtraGold);
        int cost = BlacksmithBasePrice(economy) + decision.ExtraGold;
        if (state.Gold < cost)
        {
            throw new ArgumentException(
                $"el herrero cuesta {cost} de oro para {player.Id} y la run solo tiene {state.Gold}",
                nameof(decision));
        }

        state = state.AddGold(-cost);
        var rng = OfferStream.For(state.Seed, node.Id, BlacksmithStreamBase + player.Id);
        int roll = rng.Range(0, 100);
        var healthy = player with { PhysicalState = PhysicalState.Healthy, MinorInjuries = 0 };
        if (roll < odds.CurePercent)
        {
            return state.WithPlayer(healthy);
        }

        var kind = roll < odds.CurePercent + odds.ImprovePercent ? ProsthesisKind.Improve : ProsthesisKind.Worsen;
        var candidates = prostheses.Candidates(kind, OccupiedSlots(player));
        var chosen = candidates[rng.Range(0, candidates.Count)];
        return state.WithPlayer(Install(healthy, chosen));
    }

    /// <summary>Desplazamiento de <see cref="OfferStream"/> del herrero; se le suma el id del jugador (tabla en <see cref="OfferStream"/>).</summary>
    public const int BlacksmithStreamBase = 9000;

    /// <summary>Ids de jugador que caben en el flujo del herrero: <c>OfferStream</c> suma <c>nodeId * 10_000</c>, así que <c>9000 + id</c> debe quedarse por debajo de 10_000.</summary>
    public const int BlacksmithStreamSpan = 10_000 - BlacksmithStreamBase;

    /// <summary>Prótesis que hacen falta para que el jugador gane la etiqueta <c>Automaton</c> (RF-095c).</summary>
    public const int ProsthesesForAutomaton = RunRules.MaxProstheses;

    /// <summary>Etiqueta de un jugador con alguna prótesis (ADR 0164).</summary>
    public const string ScrapTag = "Scrap";

    /// <summary>Etiqueta que se suma a la de especie con tres prótesis (RF-095c enmendada).</summary>
    public const string AutomatonTag = "Automaton";

    /// <summary>
    /// Instala <paramref name="prosthesis"/>: efecto sobre los atributos (permanente, 1..99), ranura registrada
    /// en <see cref="RunPlayer.Prostheses"/>, etiqueta <see cref="ScrapTag"/> y, a la tercera, además la etiqueta
    /// <see cref="AutomatonTag"/> (la especie se conserva).
    /// </summary>
    public static RunPlayer Install(RunPlayer player, ProsthesisDefinition prosthesis)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(prosthesis);
        var installed = new List<RunProsthesis>(player.Prostheses) { new(prosthesis.Slot, prosthesis.Id) };
        var tags = new List<string>(player.Tags);
        if (!tags.Contains(ScrapTag))
        {
            tags.Add(ScrapTag);
        }

        if (installed.Count >= ProsthesesForAutomaton && !tags.Contains(AutomatonTag))
        {
            // RF-095c enmendada (ADR 0164): la tercera prótesis GANA `Automaton` y CONSERVA la etiqueta de
            // especie. Perderla apagaría perks raciales y objetos restringidos en silencio, y hacía reventar
            // `Simulator.ValidatePerks` (un perk con `tagsRequired` de su especie). La pérdida de especie se
            // aplaza hasta que exista la familia de perks de autómata que la compense.
            tags.Add(AutomatonTag);
        }

        return player with
        {
            Attributes = prosthesis.ApplyTo(player.Attributes),
            Prostheses = installed,
            Tags = tags,
        };
    }

    /// <summary>
    /// Cura las lesiones <b>leves</b> de toda la plantilla (ADR 0170): el jugador en estado leve vuelve sano y con el
    /// contador acumulado a cero. La lesión grave, el sano y el muerto no se tocan. Es lo que hace el jefe al superarlo
    /// (<c>healsMinorInjuries</c>) y el efecto <c>heal</c> de las cartas de evento.
    /// </summary>
    public static RunState HealMinorInjuries(RunState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var next = state;
        for (int i = 0; i < state.Roster.Count; i++)
        {
            var player = state.Roster[i];
            if (player.PhysicalState == PhysicalState.MinorInjury)
            {
                next = next.WithPlayer(player with { PhysicalState = PhysicalState.Healthy, MinorInjuries = 0 });
            }
        }

        return next;
    }

    /// <summary>Lesionado que la clínica puede tratar: grave, o leve con lesiones acumuladas (RF-091).</summary>
    public static bool NeedsTreatment(RunPlayer player)
    {
        ArgumentNullException.ThrowIfNull(player);
        return (player.PhysicalState == PhysicalState.SevereInjury && !player.IsCrippled)
            || (player.PhysicalState == PhysicalState.MinorInjury && player.MinorInjuries > 0);
    }

    /// <summary>Precio del matasanos: un porcentaje del normal, nunca menos de uno (cobrar cero no es una decisión).</summary>
    public static int RiskyCost(int fullCost, EconomyConfig economy)
    {
        ArgumentNullException.ThrowIfNull(economy);
        return Math.Max(1, fullCost * economy.ClinicRiskyPercent / 100);
    }

    /// <summary>
    /// Si los perks del jugador le eximen de la factura por una lesión leve. Se lee sobre la definición
    /// <b>sin</b> la penalización de la leve aplicada, igual que <c>RunState</c> cuando consulta
    /// <see cref="ImmunityKind.MinorInjuryPenalty"/>: lo que se pregunta es qué perks lleva, no cómo de
    /// tocado está.
    /// </summary>
    private static bool IsExemptFromMinorInjuryBill(RunPlayer player, Catalog catalog)
    {
        var definition = player.ToDefinition(catalog, applyMinorInjuryPenalty: false);
        return ProgressionRules.HasImmunity(definition, catalog, ImmunityKind.MinorInjuryClinicCost);
    }

    private static void RequireNotCrippled(RunPlayer player, object decision)
    {
        if (player.IsCrippled)
        {
            throw new ArgumentException(
                $"el jugador {player.Id} está lisiado ({RunRules.MaxProstheses} prótesis y lesión grave): no tiene cura (ADR 0187)",
                nameof(decision));
        }
    }

    /// <summary>Un escalón hacia abajo: leve → grave, grave → muerto (ADR 0048, ADR 0099).</summary>
    private static RunPlayer Worse(RunPlayer player) => player.PhysicalState switch
    {
        PhysicalState.MinorInjury => player with { PhysicalState = PhysicalState.SevereInjury, MinorInjuries = 0 },
        _ => player with { PhysicalState = PhysicalState.Dead },
    };
}
