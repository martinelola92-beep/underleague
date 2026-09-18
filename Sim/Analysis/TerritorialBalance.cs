namespace Underleague.Sim.Analysis;

/// <summary>
/// Balance territorial de un lote, <b>relativo al equipo del portador</b> (§32): cuánto más se juega en el
/// tercio rival que en el propio.
///
/// <para>Existe porque <see cref="MatchMetrics.BallThirdMaxShare"/> —la métrica primaria que el
/// clasificador asigna hoy a los efectos de geometría— es un <b>máximo</b> y por tanto no tiene dirección:
/// acampar atrás y acampar arriba dan el mismo valor (§30.3). Un efecto de geometría actúa justo sobre esa
/// dirección, así que la métrica no puede representarlo.</para>
///
/// <para>No necesita traza: sale de <c>BallThird0/1/2</c>, que ya viaja en cada
/// <see cref="MatchSummary"/>. Lo único que hay que aportar es en qué equipo juega el portador, porque los
/// tercios del informe son ABSOLUTOS (<c>MatchEngine.cs:3058-3061</c>) mientras que "propio" y "rival"
/// dependen del lado: el equipo 0 ataca hacia el tercio 2 y el 1 hacia el 0
/// (<c>Pitch.GoalCenter</c>/<c>Pitch.ZoneOf</c>).</para>
///
/// <para><b>Esta clase no decide nada</b>: no la consulta ningún veredicto de cribado, no tiene banda, y
/// no sustituye a ninguna métrica del protocolo. Es un instrumento de medida, y convertirla en criterio
/// sería una decisión de protocolo aparte.</para>
/// </summary>
public static class TerritorialBalance
{
    /// <summary>
    /// Porcentaje de ticks de balón en el tercio rival menos el porcentaje en el tercio propio, para un
    /// partido visto desde <paramref name="carrierTeam"/>. Positivo = se juega más en campo contrario.
    /// Rango −100..+100; 0 = simétrico.
    /// </summary>
    public static double ForMatch(MatchSummary match, int carrierTeam)
    {
        long total = (long)match.BallThird0 + match.BallThird1 + match.BallThird2;
        if (total <= 0)
        {
            return 0.0;
        }

        // El equipo 0 defiende el tercio 0 y ataca el 2; el equipo 1, al revés.
        long own = carrierTeam == 0 ? match.BallThird0 : match.BallThird2;
        long opposing = carrierTeam == 0 ? match.BallThird2 : match.BallThird0;
        return 100.0 * (opposing - own) / total;
    }

    /// <summary>
    /// Igual, agregado sobre un lote: suma los ticks de todos los partidos antes de dividir, para que un
    /// partido corto no pese lo mismo que uno largo. <paramref name="carrierTeamOf"/> devuelve el equipo
    /// del portador en cada partido (en el harness emparejado alterna con la dirección).
    /// </summary>
    public static double ForBatch(IReadOnlyList<MatchSummary> matches, Func<int, int> carrierTeamOf)
    {
        ArgumentNullException.ThrowIfNull(matches);
        ArgumentNullException.ThrowIfNull(carrierTeamOf);

        long own = 0, opposing = 0, total = 0;
        for (int i = 0; i < matches.Count; i++)
        {
            var match = matches[i];
            int team = carrierTeamOf(i);
            own += team == 0 ? match.BallThird0 : match.BallThird2;
            opposing += team == 0 ? match.BallThird2 : match.BallThird0;
            total += (long)match.BallThird0 + match.BallThird1 + match.BallThird2;
        }

        return total <= 0 ? 0.0 : 100.0 * (opposing - own) / total;
    }
}
