using Underleague.Sim.Run;

namespace Underleague.Sim.Tests.Run;

/// <summary>
/// BE-F (docs/pendientes/BE-F.md): las dos preguntas sobre un nodo son distintas y no deben volver a
/// confundirse. <c>IsMatch</c> responde <b>"¿aquí se juega?"</b>; <c>IsCatalogRivalMatch</c> responde
/// <b>"¿enfrente hay un clan del catálogo, o sea, significa algo el <c>OpponentId</c> guardado?"</b>.
///
/// <para>El jefe es el único punto donde las dos difieren, y es justo el que ha estado a punto de morder
/// tres veces en un día: su nodo guarda un <c>OpponentId</c> fantasma porque el equipo lo construye
/// <c>BossRunSystems</c> desde <c>data/bosses/</c>. Este test existe para que la diferencia esté escrita
/// como contrato y no como comentario.</para>
/// </summary>
public sealed class NodeKindsTests
{
    [Theory]
    [InlineData(NodeKind.LeagueMatch)]
    [InlineData(NodeKind.EliteMatch)]
    [InlineData(NodeKind.Boss)]
    public void EveryKindThatIsPlayedCountsAsAMatch(NodeKind kind) => Assert.True(NodeKinds.IsMatch(kind));

    [Theory]
    [InlineData(NodeKind.LeagueMatch)]
    [InlineData(NodeKind.EliteMatch)]
    public void LeagueAndEliteAreThePlayedMatchesWithACatalogRival(NodeKind kind) =>
        Assert.True(NodeKinds.IsCatalogRivalMatch(kind));

    /// <summary>El jefe se juega, pero su <c>OpponentId</c> no significa nada: es toda la razón de BE-F.</summary>
    [Fact]
    public void TheBossIsPlayedButHasNoCatalogRival()
    {
        Assert.True(NodeKinds.IsMatch(NodeKind.Boss));
        Assert.False(NodeKinds.IsCatalogRivalMatch(NodeKind.Boss));
    }

    /// <summary>
    /// Lo que no se juega no es ninguna de las dos cosas. Recorre el enum entero, así que un
    /// <see cref="NodeKind"/> nuevo que alguien añada mañana entra por aquí y obliga a decidir.
    /// </summary>
    [Fact]
    public void NothingThatIsNotPlayedIsEitherKindOfMatch()
    {
        foreach (NodeKind kind in Enum.GetValues<NodeKind>())
        {
            if (kind is NodeKind.LeagueMatch or NodeKind.EliteMatch or NodeKind.Boss)
            {
                continue;
            }

            Assert.False(NodeKinds.IsMatch(kind), $"{kind} no se juega");
            Assert.False(NodeKinds.IsCatalogRivalMatch(kind), $"{kind} no se juega");
        }
    }

    /// <summary>
    /// Un partido con rival de catálogo es siempre un partido. Si alguien rompiera esta implicación, los
    /// dos predicados habrían dejado de ser el mismo conjunto menos el jefe.
    /// </summary>
    [Fact]
    public void ACatalogRivalMatchIsAlwaysAMatch()
    {
        foreach (NodeKind kind in Enum.GetValues<NodeKind>())
        {
            if (NodeKinds.IsCatalogRivalMatch(kind))
            {
                Assert.True(NodeKinds.IsMatch(kind), $"{kind} tiene rival de catálogo pero no se juega");
            }
        }
    }
}
