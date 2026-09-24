using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;

namespace Underleague.Sim.Tests.Engine;

/// <summary>
/// <b>El criterio de éxito del Gameplay AI Foundations Pass</b>, escrito como test.
///
/// <para>El encargo del revisor lo dice sin ambigüedad: el éxito de este pass <b>no</b> es que los runs den
/// un tanto por ciento de pases, de tiros o de entradas. Es que <i>«el motor tenga suficientes mecanismos
/// para que una secuencia de juego de fútbol pueda emerger de las decisiones de los jugadores»</i>.</para>
///
/// <para>Así que esto no mide frecuencias: comprueba que <b>cada eslabón de la cadena existe y ocurre en
/// partidos reales</b>. Si alguno faltara, la cadena sería imposible por construcción — que es exactamente
/// la situación de la que venía el motor, con catorce de veintiséis eventos que no se emitían nunca y un
/// portero que atrapaba siempre.</para>
///
/// <para>Deliberadamente no afirma que la cadena ocurra <b>entera y seguida</b> en un partido concreto: eso
/// depende del azar y convertiría un criterio de diseño en una lotería. Afirma que <b>ninguna pieza
/// falta</b>, que es lo que se puede demostrar y lo que el encargo pide.</para>
/// </summary>
public sealed class EmergentChainTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();

    private const int Matches = 200;

    /// <summary>
    /// Los eslabones de la cadena larga del encargo: <i>portero atrapa → busca compañero libre → juega
    /// corto → el compañero recibe bajo presión → protege → apoyo → pase → ataque a la profundidad → pase
    /// profundo → despeje → balón aéreo → duelo → rechace → segunda jugada → tiro → parada → rechace →
    /// remate</i>.
    /// </summary>
    [Fact]
    public void TodosLosEslabonesDeLaCadenaOcurrenEnPartidosReales()
    {
        var census = new UtilityCensus();

        bool held = false, parried = false, toCorner = false;
        bool clearance = false, aerialDuel = false, cross = false, throughPass = false;
        bool penalty = false, mob = false, injuryWithCulprit = false;

        int clearances = 0, duels = 0, parries = 0;

        for (ulong seed = 1; seed <= Matches; seed++)
        {
            var result = Simulator.Run(
                TestMatches.Reference(Catalog, seed), seed, Catalog,
                SimConfig.Default with { CollectLog = true, Census = census });

            var report = result.Report;
            clearances += report.Clearances[0] + report.Clearances[1];
            duels += report.AerialDuels[0] + report.AerialDuels[1];
            parries += report.SavesParried[0] + report.SavesParried[1];

            toCorner |= report.SavesToCorner[0] + report.SavesToCorner[1] > 0;
            clearance |= report.Clearances[0] + report.Clearances[1] > 0;
            aerialDuel |= report.AerialDuels[0] + report.AerialDuels[1] > 0;
            parried |= report.SavesParried[0] + report.SavesParried[1] > 0;
            cross |= report.Crosses[0] + report.Crosses[1] > 0;
            throughPass |= report.ThroughPasses[0] + report.ThroughPasses[1] > 0;

            foreach (var e in result.Events)
            {
                held |= e.Type == EventType.Save && e.Detail == "held";
                mob |= e.Type == EventType.MobStart;
                injuryWithCulprit |= e.Type == EventType.Injury && e.Opponent >= 0;
                // Cualquier evento emitido durante la fase de penalti delata que hubo uno: la falta que
                // lo provoca se emite ANTES de que la fase cambie, así que buscarla por ahí no vale.
                penalty |= e.Phase == MatchPhase.Penalty;
            }
        }

        // --- lo que hace el portero: las tres ramas de una parada (ADR 0141) ---
        Assert.True(held, "el portero no blocó ni una sola vez: la rama de atrapar no existe");
        Assert.True(parried, "no hubo ni un rechace: sin él no hay segunda jugada");
        Assert.True(toCorner, "no se desvió ni un balón a córner");

        // --- el balón deja el suelo y se disputa (ADR 0139) ---
        Assert.True(clearance, "nadie despejó: el balón aéreo no tiene de dónde salir");
        Assert.True(aerialDuel, "ningún duelo aéreo: AERIAL_DUEL vuelve a ser un evento teórico");
        Assert.True(cross, "ningún centro");

        // --- asociación (ADR 0138, 0144) ---
        Assert.True(throughPass, "ningún pase en profundidad");
        Assert.True(
            census.ChosenTotal(PlayerAction.OfferSupport) > 0,
            "nadie se ofreció en corto a un compañero apretado: la descarga sigue muerta");
        Assert.True(
            census.ChosenTotal(PlayerAction.Shield) > 0,
            "nadie protegió el balón: el portador sigue sin respuesta a la presión");
        Assert.True(
            census.ChosenTotal(PlayerAction.Clear) > 0,
            "nadie eligió despejar: la acción existe pero no se decide nunca");

        // --- las reglas de identidad (ADR 0143, 0145) ---
        Assert.True(penalty, "ningún penalti señalado en la muestra");
        Assert.True(mob, "ninguna turba: la fase sin árbitro no llega a existir");
        Assert.True(injuryWithCulprit, "ninguna lesión con culpable: la represalia no tendría a quién apuntar");

        // La cadena no es una rareza estadística: las piezas nuevas ocurren varias veces por muestra.
        Assert.True(clearances > Matches / 10, $"despejes demasiado raros para encadenar nada: {clearances} en {Matches} partidos");
        Assert.True(duels > Matches / 10, $"duelos aéreos demasiado raros: {duels} en {Matches} partidos");
        Assert.True(parries > Matches / 20, $"rechaces demasiado raros: {parries} en {Matches} partidos");
    }

    /// <summary>
    /// La segunda cadena del encargo: <i>0-1 y quedan segundos → el equipo pasa a ofensivo → sube el riesgo
    /// → el portero sale → balón largo → duelo → segunda jugada → ocasión</i>.
    ///
    /// <para>Aquí lo que se comprueba es que las <b>dos piezas que la hacen posible</b> existen y están
    /// conectadas: que la urgencia llega al máximo con el marcador en contra y el tiempo consumido, y que a
    /// esa urgencia el portero puede salir. Sin cualquiera de las dos, la cadena es imposible.</para>
    /// </summary>
    [Fact]
    public void LaCadenaDeLaRemontadaTieneSusDosPiezas()
    {
        int urgency = MatchEngine.UrgencyPercent(
            goalDifference: -1,
            elapsedPercent: 100,
            perGoalPercent: Catalog.Ai.Context.UrgencyPerGoalPercent);

        Assert.True(urgency > 0, "ir perdiendo en el último minuto no genera ninguna urgencia");
        Assert.True(
            urgency >= Catalog.Tuning.Goalkeeper.ExitUrgencyPercent,
            $"con un gol en contra y el tiempo cumplido la urgencia es {urgency}, por debajo del umbral de "
            + $"salida del portero ({Catalog.Tuning.Goalkeeper.ExitUrgencyPercent}): la remontada no puede llegar a su última pieza");
    }
}
