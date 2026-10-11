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
            int counterRate = economy.CounterGold.RateFor(effect.Counter);
            int deathGold = economy.DeathGold.RateFor(perk.Id);
            int resale = economy.PerkResale.PercentFor(perk.Id);
            var inheritance = economy.Inheritance.RateFor(perk.Id);

            if (counterRate > 0)
            {
                byPerk[perk.Id] = new PerkRunEffect("counterGold", true, [new("gold", counterRate * effect.Value)]);
            }
            else if (deathGold > 0)
            {
                byPerk[perk.Id] = new PerkRunEffect("deathGold", true, [new("gold", deathGold)]);
            }
            else if (resale > 0)
            {
                byPerk[perk.Id] = new PerkRunEffect("resale", false, [new("percent", resale)]);
            }
            else if (inheritance is { } rate)
            {
                byPerk[perk.Id] = new PerkRunEffect("inheritance", true, [new("percent", rate.Percent), new("max", rate.MaxPerAttribute)]);
            }
        }

        return new PerkRunEffects(byPerk);
    }
}
