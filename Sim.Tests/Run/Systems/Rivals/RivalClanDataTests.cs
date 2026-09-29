using System.Security.Cryptography;
using System.Text;
using Underleague.Sim.Model;
using Underleague.Sim.Run.Systems.Rivals;
using Underleague.Sim.Tests.Run.Systems;

namespace Underleague.Sim.Tests.Run.Systems.Rivals;

/// <summary>
/// ADR 0165, RF-015 enmendada: los quince rivales son cinco clanes que cruzan los tres actos. Cambia la
/// <b>identidad</b> (clan, nombre, nombres por puesto), no ninguna cifra: ese es el segundo test.
/// </summary>
public sealed class RivalClanDataTests
{
    private static readonly RivalCatalog Rivals = SystemsTestSupport.Systems.Rivals;

    /// <summary>
    /// Huella (SHA-256) de las cifras de cada rival tal y como estaban ANTES de la ADR 0165: raza, acto,
    /// dificultad y, por puesto, posición, rareza, nivel, atributos, rasgos, perks y etiqueta de estilo. No
    /// entran ni los nombres ni la descripción. Se calcularon sobre los ficheros de <c>data/rivals/</c> del
    /// commit anterior; si cambia una cifra de un rival, este test falla y obliga a decirlo en una ADR de balance.
    /// </summary>
    private static readonly Dictionary<string, string> NumbersBeforeAdr0165 = new(StringComparer.Ordinal)
    {
        ["act1_dwarf_bastion"] = "776a7d51b89533b57301c81d30ac95b371ee70a0d146562cba49f6107be22659",
        ["act1_elf_swiftwing"] = "7bee73c2503a312b068da36d2e9a653ca2444b2b40d6f4224cde16ca758d3b75",
        ["act1_human_academy"] = "7bbeae08e3c0395cd023ba301d7a6a765507c6680b7f3e85d6f2e4b4100ac3c4",
        ["act1_orc_ironclad"] = "d89a986d6a4c77149b74368bf53547b04e27c4263a62dcfb3029212b03e18a0f",
        ["act1_undead_graveshift"] = "7de1f1bb9f9f96482f9b86005d6389b8831049881b26fc449d113b77dd35622c",
        ["act2_dwarf_shieldwall"] = "152ec5bf18850296385dd7867c0e924d8cf12f69f6728c821bd8e12e7fe01423",
        ["act2_elf_virtuosi"] = "b3e0e56bf204eeb1a1aab5d7d61e3cb8d7161ac3a24b25d9c768688fe1325832",
        ["act2_human_tacticians"] = "0918bcc4ba4a64b6e488f36c48da79fd51f8e814c6e373f7d479782fa66cff28",
        ["act2_orc_warband"] = "6cdb3becc20584dc01698cad961ae3e56abcac4b7b694bf928a21181f689f37d",
        ["act2_undead_deadwalkers"] = "abacd19d2cb5fe813142fe339b6bc9447d7248b46977a415fe87083971524e95",
        ["act3_dwarf_ironkings"] = "ac856efaa009cfbe47065af73a6dffe19f09f178319824ed5c3410f47423e0da",
        ["act3_elf_stormblades"] = "2d4be476f3e88fb994c426da1ba1306d401878345d87da0186449a5ba46ad3d2",
        ["act3_human_allstars"] = "6f62125e27d229ff63ebef8a08f728460f3ed30a74d8f55ab529dec61d110076",
        ["act3_orc_warlords"] = "8204f6fe0e01cd6f3693535db1027621b5eb26b118bc00c6c78442901526704e",
        ["act3_undead_legion"] = "e8e445be76da05876c3a89b6bb56e129bd5c177bb4da901df47d9ef63f436666",
    };

    [Fact]
    public void FiveClansCrossAllThreeActsWithOneRivalPerActEach()
    {
        Assert.Equal(15, Rivals.All.Count);
        Assert.Equal(5, Rivals.ClanIds.Count);
        foreach (string clan in Rivals.ClanIds)
        {
            for (int act = 1; act <= 3; act++)
            {
                Assert.NotNull(Rivals.OfClan(clan, act));
            }
        }
    }

    [Fact]
    public void AClanKeepsItsRaceItsNameAndTheNameOfEverySlotInTheThreeActs()
    {
        foreach (string clan in Rivals.ClanIds)
        {
            var first = Rivals.OfClan(clan, 1)!;
            for (int act = 2; act <= 3; act++)
            {
                var later = Rivals.OfClan(clan, act)!;
                Assert.Equal(first.Race, later.Race);
                Assert.Equal(first.Name, later.Name);
                for (int slot = 0; slot < first.Players.Count; slot++)
                {
                    Assert.Equal(first.Players[slot].Name, later.Players[slot].Name);
                    Assert.Equal(first.Players[slot].Position, later.Players[slot].Position);
                }
            }
        }
    }

    [Fact]
    public void TheDescriptionOfActsTwoAndThreeIsTheirOwnAndBilingual()
    {
        foreach (string clan in Rivals.ClanIds)
        {
            var act1 = Rivals.OfClan(clan, 1)!;
            for (int act = 2; act <= 3; act++)
            {
                var later = Rivals.OfClan(clan, act)!;
                Assert.NotEqual(act1.Description, later.Description);
                Assert.False(string.IsNullOrWhiteSpace(later.Description.Es));
                Assert.False(string.IsNullOrWhiteSpace(later.Description.En));
            }
        }
    }

    [Fact]
    public void NoRivalChangedASingleNumberWithTheClans()
    {
        Assert.Equal(NumbersBeforeAdr0165.Count, Rivals.All.Count);
        foreach (var team in Rivals.All)
        {
            Assert.True(NumbersBeforeAdr0165.TryGetValue(team.Id, out string? expected), team.Id);
            Assert.Equal(expected, Fingerprint(team));
        }
    }

    private static string Fingerprint(RivalTeam team)
    {
        var parts = new List<string>(team.Players.Count);
        foreach (var p in team.Players)
        {
            var a = p.Attributes;
            parts.Add(string.Join(
                "|",
                p.Position.ToString(),
                p.Rarity.ToString(),
                p.Level.ToString(System.Globalization.CultureInfo.InvariantCulture),
                string.Join(",", a.Strength, a.Speed, a.Technique, a.Stamina, a.Leash),
                string.Join(",", p.Traits.Select(t => t.ToString())),
                string.Join(",", p.Perks),
                p.StyleTag.ToString()));
        }

        string text = $"{team.Race}|{team.Act}|{team.Difficulty}#" + string.Join(";", parts);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    }
}
