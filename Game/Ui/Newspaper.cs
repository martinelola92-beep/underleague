using System.Collections.Generic;
using Godot;
using Underleague.Game.Ui.Broadcast;
using Underleague.Sim.Model;
using Underleague.Sim.Run;

namespace Underleague.Game.Ui;

/// <summary>
/// El periódico deportivo de humor negro apoyado bajo el mapa (encargo mapa-pregon, RA-025): solo texto,
/// nada de ilustraciones (decisión del revisor). Cabecera y lema fijos; un titular y dos breves de
/// relleno salen de un repertorio de broma; los otros cuatro breves reaccionan al estado real de la run
/// —bajas, oro, marca de partidos y lesiones graves—, con su variante alternativa cuando el dato no da
/// para nada (cero bajas, caja a cero...), para que nunca quede un hueco.
/// <para>
/// <b>Determinista, nunca <c>System.Random</c></b>: el titular, el orden de los breves de relleno y el
/// giro de la hoja salen de <see cref="Pregon.Jitter"/> sobre una combinación de la semilla de la run, el
/// acto y el nodo actual (<see cref="Configure"/>) — el mismo periódico para el mismo estado, siempre.
/// </para>
/// </summary>
public partial class Newspaper : Control
{
    private static readonly Color Newsprint = new("d8d2c2");
    private static readonly Color NewsprintEdge = new("9a8f74");

    private float _headerBottom;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        PivotOffset = Size / 2f;
    }

    public override void _Draw()
    {
        if (Size.X <= 0f || Size.Y <= 0f)
        {
            return;
        }

        int seed = (int)((Position.X * 11f) + (Position.Y * 17f));
        Pregon.DrawParchment(this, Vector2.Zero, Size.X, Size.Y, Newsprint, NewsprintEdge, seed, amplitude: 2.5f);
        if (_headerBottom > 0f)
        {
            DrawLine(new Vector2(14f, _headerBottom), new Vector2(Size.X - 14f, _headerBottom), NewsprintEdge, 1.2f);
        }
    }

    /// <summary>
    /// Reconstruye la hoja entera para <paramref name="state"/>: cabecera, titular y los seis breves.
    /// Se llama en cada <c>Refresh</c> de <c>MapScreen</c>, igual que <c>BuildChoices</c>: es más simple
    /// rehacer los seis breves que llevar la cuenta de cuál cambió.
    /// </summary>
    public void Configure(RunState state)
    {
        foreach (var child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }

        int baseSeed = unchecked((int)state.Seed ^ (state.Act * 97) ^ ((state.CurrentNodeId >= 0 ? state.CurrentNodeId : 0) * 131));
        RotationDegrees = Pregon.Jitter(baseSeed, 1.5f);
        PivotOffset = Size / 2f;

        float width = Size.X - 28f;
        var masthead = EssentialLabel.Title(this, UiText.Get("ui.newspaper.masthead"), new Vector2(14f, 6f), width, Pregon.Wax, size: 17);
        masthead.HorizontalAlignment = HorizontalAlignment.Center;

        var lema = EssentialLabel.Body(this, UiText.Get("ui.newspaper.lema"), new Vector2(14f, masthead.Position.Y + masthead.Size.Y + 2f), width, Style.TextDim);
        lema.HorizontalAlignment = HorizontalAlignment.Center;

        _headerBottom = lema.Position.Y + lema.Size.Y + 6f;

        int headlineIndex = Pick(baseSeed, salt: 1, count: 6);
        var headline = EssentialLabel.Title(this, UiText.Get("ui.newspaper.headline." + headlineIndex), new Vector2(14f, _headerBottom + 8f), width, Style.Text, size: 15);
        headline.HorizontalAlignment = HorizontalAlignment.Center;

        float columnsTop = headline.Position.Y + headline.Size.Y + 8f;
        float columnWidth = (width - 16f) / 2f;

        var columnA = new VBoxContainer
        {
            Position = new Vector2(14f, columnsTop),
            Size = new Vector2(columnWidth, Size.Y - columnsTop - 8f),
        };
        columnA.AddThemeConstantOverride("separation", 6);
        AddChild(columnA);

        var columnB = new VBoxContainer
        {
            Position = new Vector2(14f + columnWidth + 16f, columnsTop),
            Size = new Vector2(columnWidth, Size.Y - columnsTop - 8f),
        };
        columnB.AddThemeConstantOverride("separation", 6);
        AddChild(columnB);

        var briefs = Briefs(state, baseSeed);
        for (int i = 0; i < briefs.Count; i++)
        {
            var column = i % 2 == 0 ? columnA : columnB;
            EssentialLabel.Body(column, briefs[i], Vector2.Zero, columnWidth, Style.Text);
        }

        QueueRedraw();
    }

    /// <summary>
    /// Los seis breves, en el orden en que se reparten a las dos columnas: bajas, relleno, oro, relleno,
    /// marca de partidos, lesiones graves. Cuatro salen del estado real; los otros dos, del repertorio de
    /// broma (RA-025), sin repetirse entre sí.
    /// </summary>
    private static List<string> Briefs(RunState state, int baseSeed)
    {
        int deaths = RunSummary.Fallen(state).Count;
        int gold = state.Gold;
        int won = RunSummary.MatchesWon(state);
        int played = RunSummary.MatchesPlayed(state);
        int lost = played - won;
        int severe = 0;
        for (int i = 0; i < state.Roster.Count; i++)
        {
            if (state.Roster[i].PhysicalState == PhysicalState.SevereInjury)
            {
                severe++;
            }
        }

        int fillerA = Pick(baseSeed, salt: 10, count: 8);
        int fillerB = Pick(baseSeed, salt: 11, count: 8);
        if (fillerB == fillerA)
        {
            fillerB = (fillerB + 1) % 8;
        }

        return new List<string>
        {
            deaths > 0
                ? UiText.Get("ui.newspaper.brief.deaths", deaths)
                : UiText.Get("ui.newspaper.brief.deathsAlt." + Pick(baseSeed, salt: 20, count: 2)),
            UiText.Get("ui.newspaper.filler." + fillerA),
            gold > 0
                ? UiText.Get("ui.newspaper.brief.gold", gold)
                : UiText.Get("ui.newspaper.brief.goldAlt." + Pick(baseSeed, salt: 21, count: 2)),
            UiText.Get("ui.newspaper.filler." + fillerB),
            played > 0
                ? UiText.Get("ui.newspaper.brief.matches", won, lost)
                : UiText.Get("ui.newspaper.brief.matchesAlt." + Pick(baseSeed, salt: 22, count: 2)),
            severe > 0
                ? UiText.Get("ui.newspaper.brief.severe", severe)
                : UiText.Get("ui.newspaper.brief.severeAlt." + Pick(baseSeed, salt: 23, count: 2)),
        };
    }

    /// <summary>
    /// Índice determinista en <c>[0, count)</c> a partir de <paramref name="baseSeed"/> y una
    /// <paramref name="salt"/> por ranura (titular, cada breve de relleno, cada variante alternativa),
    /// para que dos ranuras del mismo periódico no acaben eligiendo por las mismas cuentas. Reutiliza
    /// <see cref="Pregon.Jitter"/> —nunca <c>System.Random</c>— igual que el resto de <c>Game/Ui</c>.
    /// </summary>
    private static int Pick(int baseSeed, int salt, int count)
    {
        float h = Pregon.Jitter(baseSeed + (salt * 7919), 1f);
        float t = (h + 1f) * 0.5f;
        return Mathf.Clamp((int)(t * count), 0, count - 1);
    }
}
