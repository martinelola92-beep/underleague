using System.Collections.Generic;

namespace Underleague.Game.Ui;

/// <summary>
/// Textos de la interfaz por clave (RT-073). Todo lo que el jugador lee sale de aquí o de
/// <c>data/l10n/&lt;idioma&gt;/templates.json</c> a través de <c>Sim.Data.Localization</c>; en la pantalla
/// no hay ni una cadena escrita a mano.
/// <para>
/// El vocabulario del juego —posiciones, atributos, rasgos, etiquetas, relaciones de vínculo— ya vive en
/// <c>data/l10n</c> y se lee del catálogo. Lo que hay en esta tabla es solo el <b>mobiliario de la
/// pantalla</b> (títulos, ayudas de control, etiquetas de campo), que todavía no tiene fichero de datos.
/// La tabla está indexada por clave precisamente para que en fase 4 el cuerpo de <see cref="Get(string)"/>
/// pase a leer <c>data/l10n/&lt;idioma&gt;/ui.json</c> sin tocar ninguna pantalla.
/// </para>
/// </summary>
public static class UiText
{
    private static readonly Dictionary<string, string> Es = new()
    {
        ["ui.team.title"] = "EQUIPO",
        ["ui.team.subtitle"] = "{0} · {1} · plantilla de {2}",
        ["ui.team.roster"] = "PLANTILLA",
        ["ui.team.placeholderClub"] = "club de pruebas",
        ["ui.team.starters"] = "TITULARES",
        ["ui.team.bench"] = "SUPLENTES",
        ["ui.team.pitch"] = "COLOCACIÓN",
        ["ui.team.pitchHint"] = "mitad propia, 8 columnas · el portero no se mueve de su casilla",
        ["ui.team.coverage"] = "COBERTURA DEL EQUIPO",
        ["ui.team.coverageLegend"] = "cubren",
        ["ui.team.coverageButton"] = "Cobertura del equipo (X / C)",
        ["ui.team.coverageHint"] = "jugadores que cubren cada casilla · {0} casillas sin cubrir",
        ["ui.team.coverageHole"] = "hueco",
        ["ui.team.legendZone"] = "zona: aquí estará",
        ["ui.team.legendMargin"] = "margen: hasta aquí llega",
        ["ui.team.legendLink"] = "vínculo",
        ["ui.team.legendCreated"] = "se crea",
        ["ui.team.legendBroken"] = "se rompe",
        ["ui.team.links"] = "VÍNCULOS",
        ["ui.team.lineup"] = "ALINEACIÓN",
        ["ui.team.lineupRow"] = "{0} · {1} · columna {2}, fila {3} · {4} {5}",
        ["ui.team.linkOne"] = "vínculo",
        ["ui.team.linkMany"] = "vínculos",
        ["ui.team.moreChanges"] = "y {0} cambios más en el resto de la alineación",
        ["ui.link.Beside"] = "al lado",
        ["ui.link.Ahead"] = "delante",
        ["ui.link.Behind"] = "detrás",
        ["ui.link.Left"] = "izquierda",
        ["ui.link.Right"] = "derecha",
        ["ui.link.DiagonalAhead"] = "diagonal delante",
        ["ui.link.DiagonalBehind"] = "diagonal detrás",
        ["ui.team.linksNone"] = "sin vínculos: nadie en las casillas contiguas",
        ["ui.team.linkOf"] = "{0}: {1}",
        ["ui.team.created"] = "+ {1} ({0})",
        ["ui.team.broken"] = "- {1} ({0})",
        ["ui.team.moving"] = "MOVIENDO A {0}",
        ["ui.team.dropHint"] = "suelta en una casilla de la mitad propia",
        ["ui.team.selected"] = "SELECCIONADO",
        ["ui.team.nobody"] = "selecciona a un jugador para ver su zona de acción",
        ["ui.card.level"] = "nivel {0}",
        ["ui.card.rarity.Common"] = "común",
        ["ui.card.rarity.Rare"] = "raro",
        ["ui.card.rarity.Legendary"] = "legendario",
        ["ui.card.traits"] = "RASGOS",
        ["ui.card.perks"] = "PERKS",
        ["ui.card.ability"] = "HABILIDAD RACIAL",
        ["ui.card.perkSlot"] = "slot libre",
        ["ui.card.item"] = "OBJETO",
        ["ui.card.itemNone"] = "sin objeto (equipamiento: fase 2)",
        ["ui.card.state"] = "ESTADO",
        ["ui.card.salary"] = "SALARIO",
        ["ui.card.salaryNone"] = "sin salario (economía: fase 2)",
        ["ui.card.links"] = "VÍNCULOS",
        ["ui.card.style"] = "estilo",
        ["ui.card.bench"] = "suplente",
        ["ui.state.Healthy"] = "sano",
        ["ui.state.MinorInjury"] = "lesión leve",
        ["ui.state.SevereInjury"] = "lesión grave",
        ["ui.state.Dead"] = "muerto",
        ["ui.pos.Goalkeeper"] = "POR",
        ["ui.pos.Defender"] = "DEF",
        ["ui.pos.Midfielder"] = "CEN",
        ["ui.pos.Forward"] = "DEL",
        ["ui.input.mouse"] = "RATÓN  clic: seleccionar · arrastrar y soltar: colocar · clic en la ficha: expandir",
        ["ui.input.pad"] = "MANDO  cruceta: mover cursor · A: seleccionar y coger/soltar · B: cancelar · X: cobertura",
    };

    /// <summary>Texto de la clave; si falta, la propia clave (un texto que falta debe verse, no ocultarse).</summary>
    public static string Get(string key) => Es.TryGetValue(key, out string? value) ? value : key;

    /// <summary>Texto de la clave con los argumentos sustituidos en <c>{0}</c>, <c>{1}</c>...</summary>
    public static string Get(string key, params object[] args) =>
        string.Format(System.Globalization.CultureInfo.InvariantCulture, Get(key), args);
}
