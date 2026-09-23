using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Tests.Perks;

namespace Underleague.Sim.Tests.Engine;

/// <summary>
/// ADR 0135, paso 1: el balón tiene altura, cae, bota y acaba parándose — y un balón rápido no se le
/// escapa a quien podría recogerlo.
///
/// <para><b>Por qué estos tests y no el lote.</b> En este paso <b>nada del partido usa todavía la
/// altura</b>: los tiros siguen saliendo rasos, así que el lote de `/Balance` es byte a byte idéntico al
/// baseline y no demuestra que la física funcione, sólo que no estorba. Quien la demuestra es esto:
/// se le da altura al balón a mano y se comprueba que se comporta.</para>
/// </summary>
public sealed class BallHeightTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();

    private static MatchEngine Engine() => TestPerks.Engine(Catalog, TestMatches.Reference(Catalog, 1));

    /// <summary>Lo primero y más aburrido: sin impulso vertical, el balón se queda en el suelo.</summary>
    [Fact]
    public void ABallOnTheGroundStaysOnTheGround()
    {
        var engine = Engine();
        engine.SetLooseForTest(new Vec2(8f, 3.5f), new Vec2(0.2f, 0f), velocityZ: 0f);

        for (int i = 0; i < 60; i++)
        {
            engine.StepBallForTest();
            Assert.Equal(0f, engine.BallHeightForTest);
        }
    }

    /// <summary>Con impulso hacia arriba sube, se para arriba y vuelve a bajar: una parábola, no una recta.</summary>
    [Fact]
    public void ABallHitUpwardsRisesAndFalls()
    {
        var engine = Engine();
        engine.SetLooseForTest(new Vec2(8f, 3.5f), new Vec2(0f, 0f), velocityZ: 0.25f);

        float peak = 0f;
        var heights = new List<float>();
        for (int i = 0; i < 40; i++)
        {
            engine.StepBallForTest();
            heights.Add(engine.BallHeightForTest);
            peak = MathF.Max(peak, engine.BallHeightForTest);
        }

        Assert.True(peak > 0.5f, $"el balón apenas se levantó del suelo: pico {peak:F3} casillas");
        int peakIndex = heights.IndexOf(peak);
        Assert.True(peakIndex > 0, "el balón ya estaba en su punto más alto en el primer tick: no subió");
        Assert.True(heights[^1] < peak, "el balón nunca bajó del pico");
    }

    /// <summary>
    /// Bota, cada bote es más bajo que el anterior, y <b>acaba quieto</b>. Lo último es lo que importa: sin
    /// el corte, un balón botaría infinitas veces cada vez más bajo y nunca se pararía.
    /// </summary>
    [Fact]
    public void ABouncingBallLosesHeightAndComesToRest()
    {
        var engine = Engine();
        engine.SetLooseForTest(new Vec2(8f, 3.5f), new Vec2(0f, 0f), velocityZ: 0.4f);

        var peaks = new List<float>();
        float current = 0f;
        bool rising = true;
        for (int i = 0; i < 400; i++)
        {
            engine.StepBallForTest();
            float h = engine.BallHeightForTest;
            if (rising && h < current)
            {
                peaks.Add(current);
                rising = false;
            }
            else if (!rising && h > current)
            {
                rising = true;
            }

            current = h;
        }

        Assert.True(peaks.Count >= 2, $"el balón no llegó a botar dos veces (picos: {peaks.Count})");
        for (int i = 1; i < peaks.Count; i++)
        {
            Assert.True(peaks[i] < peaks[i - 1], $"el bote {i} ({peaks[i]:F3}) no fue más bajo que el anterior ({peaks[i - 1]:F3})");
        }

        Assert.Equal(0f, engine.BallHeightForTest);
    }

    /// <summary>
    /// <b>El riesgo que motivó el barrido.</b> Un balón que cruza entero el radio de recogida entre dos
    /// ticks tiene que poder cogerse igual. Medir contra el punto final lo haría incogible: pasaría de
    /// largo por encima de un jugador quieto sin que nadie lo tocara.
    /// </summary>
    [Fact]
    public void AFastBallIsNotMissedByThePlayerItFliesPast()
    {
        var engine = Engine();
        var victim = engine.PlayerById(3)!;
        var at = victim.Position;

        // Sale dos casillas a la izquierda y viaja a 1,4 casillas por tick: en un solo tick pasa de largo,
        // porque el radio de recogida es 0,5.
        engine.SetLooseForTest(new Vec2(at.X - 2f, at.Y), new Vec2(1.4f, 0f), velocityZ: 0f);

        bool pickedUp = false;
        for (int i = 0; i < 4 && !pickedUp; i++)
        {
            engine.StepBallForTest();
            pickedUp = engine.BallOwnerIdForTest >= 0;
        }

        Assert.True(pickedUp, "el balón pasó por encima de un jugador y nadie pudo cogerlo");
    }

    /// <summary>Aparcar el balón lo devuelve al suelo y le quita el impulso: una reanudación empieza en calma.</summary>
    [Fact]
    public void ParkingTheBallResetsItsHeight()
    {
        var engine = Engine();
        engine.SetLooseForTest(new Vec2(8f, 3.5f), new Vec2(0f, 0f), velocityZ: 0.4f);
        engine.StepBallForTest();
        Assert.True(engine.BallHeightForTest > 0f);

        engine.ParkBallForTest(new Vec2(4f, 2f));

        Assert.Equal(0f, engine.BallHeightForTest);
        engine.StepBallForTest();
        Assert.Equal(0f, engine.BallHeightForTest);
    }
}
