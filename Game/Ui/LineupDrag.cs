using Godot;

namespace Underleague.Game.Ui;

/// <summary>
/// El arrastre de un jugador en la pantalla de Equipo (BX-3): lo que viaja con el ratón entre la columna de la
/// plantilla y la pizarra del campo con el patrón de Godot (<c>_GetDragData</c> / <c>_CanDropData</c> /
/// <c>_DropData</c>). Es solo el sobre: <b>qué pasa al soltar lo decide <c>/Sim</c></b>
/// (<c>PlacementView.WithPlayerAt</c>) a través de <c>TeamState.Move</c>, igual que el clic y el mando (UI-006), que
/// siguen valiendo como alternativa.
/// </summary>
public static class LineupDrag
{
    private const string Kind = "lineupPlayer";

    /// <summary>El sobre de un jugador arrastrado.</summary>
    public static Variant Pack(int playerId)
    {
        var data = new Godot.Collections.Dictionary { ["kind"] = Kind, ["id"] = playerId };
        return data;
    }

    /// <summary>True si lo que se arrastra es un jugador de la alineación; <paramref name="playerId"/> es el suyo.</summary>
    public static bool TryUnpack(Variant data, out int playerId)
    {
        playerId = -1;
        if (data.VariantType != Variant.Type.Dictionary)
        {
            return false;
        }

        var dictionary = data.AsGodotDictionary();
        if (!dictionary.ContainsKey("kind") || dictionary["kind"].AsString() != Kind || !dictionary.ContainsKey("id"))
        {
            return false;
        }

        playerId = dictionary["id"].AsInt32();
        return true;
    }

    /// <summary>La etiqueta que acompaña al ratón mientras se arrastra: el nombre, sobre papel.</summary>
    public static Control Preview(string name)
    {
        var label = new Label { Text = name };
        label.AddThemeColorOverride("font_color", Knavall.Ink.Black);
        label.AddThemeFontOverride("font", Knavall.Ink.Heavy);
        label.AddThemeFontSizeOverride("font_size", 18);
        var panel = new PanelContainer { Modulate = new Color(1f, 1f, 1f, 0.92f), MouseFilter = Control.MouseFilterEnum.Ignore };
        var style = new StyleBoxFlat { BgColor = Knavall.Ink.Paper, BorderColor = Knavall.Ink.Red };
        style.SetBorderWidthAll(2);
        style.SetContentMarginAll(6f);
        panel.AddThemeStyleboxOverride("panel", style);
        panel.AddChild(label);
        return panel;
    }
}
