using Underleague.Sim.Perks;

namespace Underleague.Sim.Run.Systems.Economy;

/// <summary>
/// Arma <see cref="PerkRunEffects"/> desde las tablas de <c>data/economy</c> que dan sentido a los perks de contador
/// aislado (sólo <c>addCounter</c>): oro por unidad de contador, oro por muerte, reventa y herencia. Así la descripción
/// generada (RT-035) dice lo que el jugador gana con las cifras de los datos, sin texto a mano.
/// </summary>
public static class PerkRunEffectsBuilder
{
    public static PerkRunEffects Build(PerkCatalog perks, EconomyConfig economy)
    {
        ArgumentNullException.ThrowIfNull(perks);
        ArgumentNullException.ThrowIfNull(economy);

        var byPerk = new Dictionary<string, PerkRunEffect>(StringComparer.Ordinal);
        foreach (var perk in perks.All)
        {
            if (perk.Effects.Count == 0 || perk.Effects.Any(e => e.Type != EffectType.AddCounter))
            {
                continue;
            }

            var effect = perk.Effects[0];
            if (perk.Effects.Any(e => !string.Equals(e.Counter, effect.Counter, StringComparison.Ordinal)))
            {
                throw new InvalidOperationException($"perk {perk.Id}: varios contadores en un perk de contador aislado; PerkRunEffects describe uno");
            }

            int counterRate = economy.CounterGold.RateFor(effect.Counter);
            int deathGold = economy.DeathGold.RateFor(perk.Id);
            int resale = economy.PerkResale.PercentFor(perk.Id);
            var inheritance = economy.Inheritance.RateFor(perk.Id);

            var found = new List<PerkRunEffect>(1);
            if (counterRate > 0)
            {
                // Paga delta × tarifa del partido (GoldCalculator.CounterGoldRows): por activación, el valor del efecto.
                found.Add(new PerkRunEffect("counterGold", true, [new("gold", counterRate * effect.Value)]));
            }

            if (deathGold > 0)
            {
                // Una vez por muerte DEL PORTADOR, dentro o fuera del partido (DeathConsequences): «si muere», no «en una muerte».
                found.Add(new PerkRunEffect("deathGold", false, [new("gold", deathGold)]));
            }

            if (resale > 0)
            {
                // Es un MÍNIMO sobre el porcentaje por estado físico (MarketSystem.ResaleStatePercent): sano no cambia nada.
                found.Add(new PerkRunEffect("resale", false, [new("percent", resale)]));
            }

            if (inheritance is { } rate)
            {
                // Recibe el enlace del propio perk (DeathConsequences.LinkedTeammate): la plantilla lo nombra con {links}.
                found.Add(new PerkRunEffect("inheritance", false, [new("percent", rate.Percent), new("max", rate.MaxPerAttribute)]));
            }

            if (found.Count > 1)
            {
                throw new InvalidOperationException($"perk {perk.Id}: aparece en {found.Count} tablas de data/economy; PerkRunEffects describe una");
            }

            if (found.Count == 1)
            {
                byPerk[perk.Id] = found[0];
            }
        }

        return new PerkRunEffects(byPerk);
    }
}
