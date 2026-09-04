using Underleague.Sim.Data;

namespace Underleague.Sim.Engine;

/// <summary>
/// Cuerpos con volumen y separación blanda (ADR 0020, RT-016, fase1b-diseno.md §2.1). Hasta la fase 1
/// los jugadores eran puntos sin volumen y catorce podían ocupar la misma coordenada: concentrarse solo
/// tenía coste, nunca masa, bloqueo ni superioridad local. Aquí dos cuerpos que se solapan se empujan.
///
/// <para><b>Orden de resolución</b>, fijo y documentado porque de él depende el resultado:</para>
/// <list type="number">
/// <item><see cref="BeginTick"/> vacía el buffer al principio del tick, <b>antes</b> de que nadie se
/// mueva, porque el empuje de una entrada (<see cref="AddTacklePush"/>) se acumula ya durante el bucle
/// de jugadores y tiene que caer en el mismo buffer que la separación;</item>
/// <item><see cref="Resolve"/> recorre los pares <c>(i, j)</c> con <c>i &lt; j</c> por índice ascendente,
/// que es id ascendente (RT-041), y acumula el desplazamiento de cada par en el buffer <b>sin mover a
/// nadie</b> (esquema de Jacobi);</item>
/// <item>solo al terminar el recorrido se aplican todos los desplazamientos a la vez, cada uno acotado al
/// tope del jugador.</item>
/// </list>
/// <para>Aplicar los empujes sobre la marcha haría que el orden del bucle cambiara el resultado, que es
/// exactamente el sesgo por id que hubo que corregir en la fase 0. Con la acumulación en buffer la suma
/// de un tick es conmutativa y el recorrido alterno por paridad de tick del motor
/// (<c>PlayerInTurnOrder</c>) no puede influir.</para>
///
/// <para>El balón no participa: sigue siendo un punto sin colisión, interceptable por radio (RT-016).</para>
/// </summary>
internal sealed class BodySeparation
{
    private readonly BodiesTuning _tuning;
    private readonly float[] _pushX;
    private readonly float[] _pushY;
    private readonly int[] _capMilli;

    public BodySeparation(BodiesTuning tuning, int playerCount)
    {
        _tuning = tuning;
        _pushX = new float[playerCount];
        _pushY = new float[playerCount];
        _capMilli = new int[playerCount];
    }

    /// <summary>Vacía el buffer y devuelve el tope de cada jugador a <c>bodies.maxPushPerTickMilli</c>.</summary>
    public void BeginTick()
    {
        int cap = _tuning.MaxPushPerTickMilli;
        for (int i = 0; i < _pushX.Length; i++)
        {
            _pushX[i] = 0f;
            _pushY[i] = 0f;
            _capMilli[i] = cap;
        }
    }

    /// <summary>
    /// Empuje de una entrada (§2.1.4). No usa el solape real —una entrada llega desde más lejos de lo que
    /// se solapan dos cuerpos— sino el contacto pleno, la suma de los dos radios, multiplicada por
    /// <c>tacklePushMultiplier</c>. El tope del receptor sube en ese mismo factor: con el tope normal el
    /// multiplicador no se notaría, porque el empuje de una entrada lo supera con creces.
    /// </summary>
    public void AddTacklePush(MatchPlayer tackler, MatchPlayer carrier)
    {
        if (!_tuning.SeparationEnabled)
        {
            return;
        }

        float dx = carrier.Position.X - tackler.Position.X;
        float dy = carrier.Position.Y - tackler.Position.Y;
        float distance = MathF.Sqrt((dx * dx) + (dy * dy));
        Accumulate(tackler, carrier, dx, dy, distance, tackler.BodyRadius + carrier.BodyRadius, _tuning.TacklePushMultiplier);
        _capMilli[carrier.Index] = _tuning.MaxPushPerTickMilli * _tuning.TacklePushMultiplier / 100;
    }

    /// <summary>Acumula la separación de todos los pares que se solapan y aplica el buffer entero.</summary>
    public void Resolve(MatchPlayer[] players)
    {
        if (_tuning.SeparationEnabled)
        {
            for (int i = 0; i < players.Length; i++)
            {
                var a = players[i];
                if (!a.OnPitch)
                {
                    continue;
                }

                for (int j = i + 1; j < players.Length; j++)
                {
                    var b = players[j];
                    if (!b.OnPitch)
                    {
                        continue;
                    }

                    float dx = b.Position.X - a.Position.X;
                    float dy = b.Position.Y - a.Position.Y;
                    float radii = a.BodyRadius + b.BodyRadius;
                    float squared = (dx * dx) + (dy * dy);
                    if (squared >= radii * radii)
                    {
                        continue;
                    }

                    float distance = MathF.Sqrt(squared);
                    Accumulate(a, b, dx, dy, distance, radii - distance, 100);
                }
            }
        }

        Apply(players);
    }

    /// <summary>
    /// Acumula en el buffer el desplazamiento de un contacto entre <paramref name="a"/> y
    /// <paramref name="b"/>. El reparto es <b>inverso a la masa</b> (§2.1.2): el ligero se lleva la parte
    /// mayor, así que un orco abre hueco y un elfo sale despedido. Un jugador con
    /// <see cref="MatchPlayer.Immovable"/> recibe desplazamiento 0 y su parte se la lleva entera el otro:
    /// contra un cuerpo inamovible se rebota, no se atraviesa.
    /// </summary>
    private void Accumulate(MatchPlayer a, MatchPlayer b, float dx, float dy, float distance, float overlap, int multiplierPercent)
    {
        bool aMoves = !a.Immovable;
        bool bMoves = !b.Immovable;
        if ((!aMoves && !bMoves) || overlap <= 0f)
        {
            return;
        }

        float nx;
        float ny;
        if (distance <= 0f)
        {
            // Dos cuerpos exactamente en el mismo punto: no hay dirección de separación. Se separan a lo
            // largo del eje X, el de menor índice hacia las columnas bajas. Es arbitrario, pero depende
            // solo del orden de los índices, así que es reproducible.
            nx = 1f;
            ny = 0f;
        }
        else
        {
            nx = dx / distance;
            ny = dy / distance;
        }

        int shareA = b.Mass * 1000 / (a.Mass + b.Mass);
        int shareB = 1000 - shareA;
        if (!aMoves)
        {
            shareA = 0;
            shareB = 1000;
        }
        else if (!bMoves)
        {
            shareA = 1000;
            shareB = 0;
        }

        float push = overlap * multiplierPercent / 100f;
        _pushX[a.Index] -= nx * push * shareA / 1000f;
        _pushY[a.Index] -= ny * push * shareA / 1000f;
        _pushX[b.Index] += nx * push * shareB / 1000f;
        _pushY[b.Index] += ny * push * shareB / 1000f;
    }

    /// <summary>
    /// Aplica de golpe el buffer del tick, acotado por jugador (§2.1.3). El empuje no toca
    /// <see cref="MatchPlayer.Velocity"/>: la velocidad es el desplazamiento <b>propio</b> del jugador y
    /// la usa la anticipación del pase (§3.7); que a uno lo empujen no significa que vaya hacia allí.
    /// Tampoco se acota a la zona de acción: que un empujón te saque de tu zona es justo lo que tiene que
    /// poder pasar, y la utilidad ya paga por volver (§2.2).
    /// </summary>
    private void Apply(MatchPlayer[] players)
    {
        for (int i = 0; i < players.Length; i++)
        {
            float px = _pushX[i];
            float py = _pushY[i];
            if (px == 0f && py == 0f)
            {
                continue;
            }

            var player = players[i];
            if (!player.OnPitch)
            {
                continue;
            }

            float cap = _capMilli[i] / 1000f;
            float length = MathF.Sqrt((px * px) + (py * py));
            if (length > cap)
            {
                float scale = cap / length;
                px *= scale;
                py *= scale;
            }

            var next = Utility.ClampToPitch(new Vec2(player.Position.X + px, player.Position.Y + py));
            if (!player.IsOutfield)
            {
                next = Utility.ClampToArea(next, player.Team);
            }

            player.Position = next;
        }
    }
}
