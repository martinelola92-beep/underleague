using Underleague.Sim.Data;
using Underleague.Sim.Model;

namespace Underleague.Sim.Run.Systems.Rivals;

/// <summary>Un jugador de un rival estático (RF-015): igual forma que <c>Sim.Generation.TeamGenerator</c> pero escrito a mano, sin RNG.</summary>
/// <param name="StyleTag">
/// Etiqueta de estilo del jugador (ADR 0024). En un rival estático se <b>escribe</b> en vez de sortearse,
/// porque es la mitad de lo que hace reconocible su build (RF-015): los perks de La Muralla exigen
/// <c>Bulwark</c> y los de El Toque exigen <c>Fine</c>, así que sin ella un rival con perks de su línea
/// los fallaría todos. No toca los atributos —que en un rival vienen escritos uno a uno y no generados—,
/// solo las etiquetas que consultan las condiciones.
/// </param>
public sealed record RivalPlayer(
    string Name,
    Position Position,
    Rarity Rarity,
    int Level,
    Attributes Attributes,
    IReadOnlyList<Trait> Traits,
    IReadOnlyList<string> Perks,
    StyleTag StyleTag = StyleTag.Neutral);

/// <summary>
/// Un rival estático por acto (RF-015), cargado de <c>data/rivals/&lt;id&gt;.json</c>. Diez jugadores en
/// el mismo orden que <c>Sim.Generation.TeamGenerator</c> (titulares GK, DEF, DEF, MID, MID, MID, FWD;
/// suplentes DEF, MID, FWD), para poder reutilizar <c>Lineup.Default</c> al construir el
/// <c>TeamSetup</c>. La build "reconocible" de RF-015 es la combinación de raza, sesgo de atributos y
/// perks: no hay un campo de etiqueta de build separado, el propio roster la cuenta.
/// </summary>
/// <param name="Description">
/// Línea de una frase para el informe de ojeo (RF-012b, RF-015): "un rival real, no un bloque de
/// estadísticas". Se escribe a mano, con las mismas reglas que la descripción de una raza o un club
/// (<c>docs/estilo-descripciones.md</c>): no es un efecto, así que RT-035 no aplica.
/// </param>
public sealed record RivalTeam(
    string Id,
    LocalizedName Name,
    LocalizedName Description,
    Race Race,
    int Act,
    int Difficulty,
    IReadOnlyList<RivalPlayer> Players);
