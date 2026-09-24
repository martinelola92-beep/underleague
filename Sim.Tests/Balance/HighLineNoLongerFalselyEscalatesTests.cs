using Underleague.Sim.Analysis;
using Underleague.Sim.Data;
using Xunit.Abstractions;

namespace Underleague.Sim.Tests.Balance;

/// <summary>
/// Regresión de integración de §29: <c>high_line</c> deja de escalar por la contradicción de signo
/// espuria. Escrito ANTES del cambio, donde falla; pasa después.
///
/// <para><b>Lo que este test NO afirma</b>: que <c>high_line</c> pase a <c>SCREENING_PASS</c>. Solo
/// comprueba que desaparece el motivo espurio de <c>DESIGN_ESCALATION</c>; el resto de reglas del
/// screening (exposición, seguridad, potencia) sigue decidiendo igual que antes.</para>
/// </summary>
public sealed class HighLineNoLongerFalselyEscalatesTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();

    private readonly ITestOutputHelper _output;
    public HighLineNoLongerFalselyEscalatesTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void HighLineDoesNotEscalateForASignContradiction()
    {
        var perk = Catalog.Perks.All.Single(p => p.Id == "high_line");
        var result = ScreeningRunner.RunPerk(Catalog, perk, seed: 1);

        _output.WriteLine($"high_line -> {result.DisplayState}");
        _output.WriteLine($"  delta={result.PrimaryDelta} exposición={result.ExposureFraction:P1}");
        _output.WriteLine($"  motivo: {result.Reason}");

        Assert.NotEqual(BalanceState.DesignReview, result.FinalState);

        // La frase que SOLO aparece en la escalada espuria por contradicción de signo. No se puede buscar
        // "dirección" a secas: el mensaje correcto la usa al decir "sin comprobación de dirección".
        Assert.DoesNotContain("contraria a la", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ThePerkStillGoesThroughEveryOtherScreeningRule()
    {
        // Lo que este test afirma es que el perk SIGUE PASANDO por el resto del cribado: que produce una
        // medida y un estado definido, no que la medida valga tal cosa.
        //
        // REPLANTEADO (ADR 0139): fijaba además que el delta fuera NEGATIVO, «como en §23.5». Eso no es una
        // regla del sistema sino una medición del motor tal y como estaba aquel día, y el Gameplay AI
        // Foundations Pass cambia el motor a propósito —percepción compartida, proteger, despejar, juego
        // aéreo—, así que el signo puede cambiar sin que nada esté roto. Volver a clavar el signo nuevo
        // sería repetir el error con otra cifra. El valor se imprime para que la fase de balance lo lea.
        var perk = Catalog.Perks.All.Single(p => p.Id == "high_line");
        var result = ScreeningRunner.RunPerk(Catalog, perk, seed: 1);

        Assert.NotNull(result.PrimaryDelta);
        Assert.True(Enum.IsDefined(result.FinalState));
        _output.WriteLine($"estado final tras la corrección: {result.DisplayState} (delta {result.PrimaryDelta:F4})");
    }
}
