namespace Underleague.Sim.Model;

/// <summary>
/// Sustitución forzada (ADR 0094, AZ-F): al final del tick <paramref name="Tick"/> —en el que
/// <paramref name="OutPlayerId"/> dejó el campo por lesión o muerte— entra <paramref name="InPlayerId"/>,
/// un jugador de la plantilla que no estaba alineado. Es parte del <b>estado inicial</b> del partido, como
/// la activación manual de un consumible (RF-082, docs/arquitectura.md): volver a ejecutar el partido con
/// la misma lista reproduce exactamente lo mismo (RT-013, RT-024, RT-061). No existen sustituciones
/// voluntarias ni por expulsión: el motor rechaza una sustitución cuyo jugador saliente no haya salido por
/// lesión o muerte en un tick anterior o igual.
/// </summary>
public sealed record Substitution(int Tick, int OutPlayerId, int InPlayerId);

/// <summary>
/// «Sigue jugando» (ADR 0134 apartado E): en el tick <paramref name="Tick"/> el jugador
/// <paramref name="PlayerId"/> sufrió una lesión <b>leve</b> y el jugador humano ha decidido que
/// <b>no deje el campo</b>. Es una tercera respuesta al mismo punto de decisión que responden
/// <see cref="Substitution"/> («entra este») y el rechazo («que se quede el hueco»), y como ellas es parte
/// del <b>estado inicial</b> del partido: volver a ejecutarlo con la misma lista reproduce exactamente lo
/// mismo (RT-013, RT-024).
///
/// <para>El que se queda paga la lesión <b>ya</b>, en vez de al terminar el partido: desde ese tick sus
/// atributos pasan a ser <paramref name="After"/>. Sin ese coste, quedarse sería siempre mejor que
/// sustituir y no habría decisión que tomar.</para>
///
/// <para><b>El motor no calcula ese precio, lo recibe hecho</b>, y es una decisión de diseño, no una
/// comodidad. La penalización de RF-091 es <b>lineal</b> en las lesiones acumuladas (<c>100 − 15·n</c>,
/// <c>RunState.ToDefinition</c>), y el atributo con el que el jugador entra al partido <b>ya trae aplicadas
/// las anteriores</b>: volver a descontarle un 15 % dentro del motor compone multiplicativamente y da otro
/// número —con una sola lesión previa, un atributo de 99 sale 71 en vez de 69, y la diferencia crece con
/// cada lesión—. Desde el atributo del partido no se puede recuperar el de la plantilla, así que el cálculo
/// <b>no puede</b> vivir en el motor. Lo hace <c>/Sim/Run</c> con el mismo <c>ToDefinition</c> de siempre,
/// que ya resuelve la inmunidad <c>ImmunityKind.MinorInjuryPenalty</c> (ADR 0026: el no-muerto se queda
/// entero), el mismo truncamiento entero y el que la correa no se toque. La regla existe en <b>un solo
/// sitio</b>: divergir deja de ser algo que un test vigila y pasa a no ser representable.</para>
///
/// <para>Solo vale para la lesión leve: pedirlo sobre una lesión <b>grave</b> (RF-092) o sobre una muerte es
/// ilegal y el motor lo rechaza con <c>ArgumentException</c>, igual que una sustitución ilegal (RT-032).</para>
/// </summary>
/// <param name="After">
/// Atributos que el jugador pasa a tener desde <paramref name="Tick"/>, ya resueltos por la capa de run.
/// Es la misma idea que <see cref="TeamSetup"/>, que tampoco lleva reglas sino jugadores ya resueltos: aquí
/// simplemente se resuelven en un tick que no es el cero.
/// </param>
public sealed record PlayOn(int Tick, int PlayerId, Attributes After);
