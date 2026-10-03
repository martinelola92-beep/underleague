using Godot;
using Underleague.Game.Data;
using Underleague.Game.Ui;
using Underleague.Game.Ui.Broadcast;

namespace Underleague.Game.Autoload;

/// <summary>
/// Menú de pausa, en cualquier pantalla de la run: Esc (o Start en el mando) lo abre y lo cierra.
/// Cuatro opciones —Resumir, Guardar, Ajustes, Salir al menú principal— y una página de Ajustes con el
/// volumen de música y de efectos y, solo en modo desarrollo (<see cref="GameSettings.DevMode"/>), la
/// pantalla de partido que se abre: la retransmisión 3D o la vista 2D con el log de eventos.
///
/// <para><b>Pausa de verdad.</b> Abrirlo pone <c>SceneTree.Paused</c>: la reproducción del partido, sus
/// tiempos y sus gestos se congelan sin que ninguna pantalla tenga que saber que existe este menú. El
/// menú y el audio (<see cref="AudioManager"/>) llevan <c>ProcessMode.Always</c> y siguen vivos.</para>
///
/// <para><b>Quién se queda con Esc.</b> El menú escucha en <c>_UnhandledInput</c>, que Godot entrega a la
/// escena antes que a los autoloads: una pantalla que usa cancelar para cerrar algo suyo (el cofre de
/// Equipo, una ficha cogida) lo consume y la pausa no se abre. Las que usaban Esc para ir al informe lo
/// ceden (<see cref="IsPauseKey"/>) y conservan el B del mando.</para>
///
/// <para><b>Guardar no cambia el ironman</b> (RT-061): escribe el mismo slot único que el guardado
/// automático y que el cierre de ventana, con lo que ya se ha decidido. Retomar sigue borrándolo.</para>
/// </summary>
public sealed partial class PauseMenu : CanvasLayer
{
    private const float BoxWidth = 380f;
    private const float ButtonWidth = 300f;
    private const float ButtonHeight = 34f;

    private Control _root = null!;
    private Control _main = null!;
    private Control _settings = null!;
    private Button _resume = null!;
    private Button _save = null!;
    private Label _saveStatus = null!;
    private HSlider _music = null!;
    private Label _musicValue = null!;
    private HSlider _effects = null!;
    private Label _effectsValue = null!;
    private CheckButton? _debugView;
    private CheckButton _match2DKey = null!;

    public static PauseMenu? Instance { get; private set; }

    /// <summary>True con el menú a la vista (la run está en pausa).</summary>
    public bool IsOpen => _root.Visible;

    /// <summary>
    /// La tecla de pausa: Esc, sin repetición. Las pantallas que responden a <c>ui_cancel</c> —que en Godot
    /// incluye Esc— la excluyen cuando la acción suya no debe ganarle a la pausa.
    /// </summary>
    public static bool IsPauseKey(InputEvent @event) =>
        @event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape };

    private static bool IsPauseEvent(InputEvent @event) =>
        IsPauseKey(@event) || @event is InputEventJoypadButton { Pressed: true, ButtonIndex: JoyButton.Start };

    public override void _Ready()
    {
        Instance = this;
        Layer = 100;
        ProcessMode = ProcessModeEnum.Always;

        // Los ajustes se aplican al arrancar, no al abrir el menú: el volumen elegido vale desde la
        // primera nota de la pantalla de inicio.
        GameSettings.Load();

        Build();
        _root.Visible = false;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!IsPauseEvent(@event))
        {
            return;
        }

        if (IsOpen)
        {
            if (_settings.Visible)
            {
                ShowMain();
            }
            else
            {
                Close();
            }
        }
        else if (CanOpen())
        {
            Open();
        }
        else
        {
            return;
        }

        GetViewport().SetInputAsHandled();
    }

    /// <summary>Abre el menú y pausa el árbol. No hace nada en la pantalla de inicio (no hay run que pausar).</summary>
    public void Open()
    {
        var run = RunController.Instance;
        bool live = run is { HasRun: true } && !run.IsOverNow;
        _save.Disabled = !live;
        _saveStatus.Text = UiText.Get("ui.pause.autosave");
        _saveStatus.AddThemeColorOverride("font_color", Style.TextDim);

        GetTree().Paused = true;
        _root.Visible = true;
        ShowMain();
    }

    /// <summary>Cierra el menú y reanuda. Si en Ajustes se cambió la vista del partido estando en uno, cambia de pantalla ahora.</summary>
    public void Close()
    {
        _root.Visible = false;
        GetTree().Paused = false;

        string current = CurrentScene();
        if ((current == Nav.Match || current == Nav.MatchDebug) && current != Nav.MatchView)
        {
            // El mismo salto que F3: las dos pantallas leen la misma RunController.Playback, no se vuelve
            // a jugar nada.
            Nav.Go(this, Nav.MatchView);
        }
    }

    /// <summary>Muestra la página de Ajustes (también la usa el arnés de capturas).</summary>
    public void ShowSettings()
    {
        _main.Visible = false;
        _settings.Visible = true;
        _music.SetValueNoSignal(GameSettings.MusicVolume * 100f);
        _effects.SetValueNoSignal(GameSettings.EffectsVolume * 100f);
        _match2DKey.SetPressedNoSignal(GameSettings.Match2DKey);
        _debugView?.SetPressedNoSignal(GameSettings.DebugMatchView);
        RefreshVolumeLabels();
        _music.GrabFocus();
    }

    private void ShowMain()
    {
        _settings.Visible = false;
        _main.Visible = true;
        _resume.GrabFocus();
    }

    private bool CanOpen()
    {
        string current = CurrentScene();
        return current.Length > 0 && current != Nav.Start;
    }

    private string CurrentScene() => GetTree().CurrentScene?.SceneFilePath ?? string.Empty;

    // ------------------------------------------------------------------ acciones

    private void SaveRun()
    {
        var run = RunController.Instance;
        if (run is not { HasRun: true } || run.IsOverNow)
        {
            return;
        }

        run.Save();
        _saveStatus.Text = UiText.Get("ui.pause.saved");
        _saveStatus.AddThemeColorOverride("font_color", Pregon.Wax);
    }

    private void QuitToMenu()
    {
        var run = RunController.Instance;
        _root.Visible = false;
        GetTree().Paused = false;

        if (run is { HasRun: true })
        {
            // Una run terminada ya no tiene slot (Save lo borra): salir es cerrarla, como en FinDeRun.
            if (run.IsOverNow)
            {
                run.Abandon();
            }
            else
            {
                run.LeaveToMenu();
            }
        }

        Nav.Go(this, Nav.Start);
    }

    // ------------------------------------------------------------------ construcción

    private void Build()
    {
        _root = new Control { MouseFilter = Control.MouseFilterEnum.Stop };
        _root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _root.Theme = Widgets.BuildLegacyTheme();
        AddChild(_root);

        // Cortina: oscurece la pantalla de debajo y se come los clics, para que nada de ella responda
        // con el menú abierto.
        var veil = new ColorRect { Color = new Color(0f, 0f, 0f, 0.6f), MouseFilter = Control.MouseFilterEnum.Stop };
        veil.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(veil);

        var center = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(center);

        _main = BuildMain();
        center.AddChild(_main);

        _settings = BuildSettings();
        center.AddChild(_settings);
    }

    private Control BuildMain()
    {
        const float height = 290f;
        var box = Box(height);
        Widgets.Panel(box, new Rect2(0f, 0f, BoxWidth, height));
        Widgets.Title(box, UiText.Get("ui.pause.title"), new Vector2(24f, 16f), BoxWidth - 48f);

        float x = (BoxWidth - ButtonWidth) / 2f;
        float y = 70f;
        _resume = Widgets.Button(box, UiText.Get("ui.pause.resume"), new Rect2(x, y, ButtonWidth, ButtonHeight));
        _resume.Pressed += Close;
        y += ButtonHeight + 10f;

        _save = Widgets.Button(box, UiText.Get("ui.pause.save"), new Rect2(x, y, ButtonWidth, ButtonHeight));
        _save.Pressed += SaveRun;
        y += ButtonHeight + 4f;

        _saveStatus = Widgets.Body(box, string.Empty, new Vector2(x, y), ButtonWidth, Style.TextDim);
        _saveStatus.HorizontalAlignment = HorizontalAlignment.Center;
        y += 26f;

        var settings = Widgets.Button(box, UiText.Get("ui.pause.settings"), new Rect2(x, y, ButtonWidth, ButtonHeight));
        settings.Pressed += ShowSettings;
        y += ButtonHeight + 10f;

        var quit = Widgets.Button(box, UiText.Get("ui.pause.quit"), new Rect2(x, y, ButtonWidth, ButtonHeight));
        quit.Pressed += QuitToMenu;

        return box;
    }

    private Control BuildSettings()
    {
        bool dev = GameSettings.DevMode;
        float height = dev ? 408f : 302f;
        var box = Box(height);
        Widgets.Panel(box, new Rect2(0f, 0f, BoxWidth, height));
        Widgets.Title(box, UiText.Get("ui.pause.settings"), new Vector2(24f, 16f), BoxWidth - 48f);

        float y = 70f;
        (_music, _musicValue) = VolumeRow(box, UiText.Get("ui.settings.music"), y);
        _music.ValueChanged += value =>
        {
            GameSettings.SetMusicVolume((float)value / 100f);
            RefreshVolumeLabels();
        };
        y += 54f;

        (_effects, _effectsValue) = VolumeRow(box, UiText.Get("ui.settings.effects"), y);
        _effects.ValueChanged += value =>
        {
            GameSettings.SetEffectsVolume((float)value / 100f);
            RefreshVolumeLabels();
        };
        y += 60f;

        _match2DKey = new CheckButton
        {
            Text = UiText.Get("ui.settings.match2dKey"),
            Position = new Vector2(20f, y),
            Size = new Vector2(BoxWidth - 40f, 30f),
        };
        StyleCheck(_match2DKey);
        _match2DKey.Toggled += GameSettings.SetMatch2DKey;
        box.AddChild(_match2DKey);
        y += 32f;
        Widgets.Body(box, UiText.Get("ui.settings.match2dKeyHint"), new Vector2(24f, y), BoxWidth - 48f, Style.TextDim);
        y += 30f;

        if (dev)
        {
            Widgets.Section(box, UiText.Get("ui.settings.dev"), new Vector2(24f, y), BoxWidth - 48f);
            y += 22f;
            _debugView = new CheckButton
            {
                Text = UiText.Get("ui.settings.debugView"),
                Position = new Vector2(20f, y),
                Size = new Vector2(BoxWidth - 40f, 30f),
            };
            StyleCheck(_debugView);
            _debugView.Toggled += GameSettings.SetDebugMatchView;
            box.AddChild(_debugView);
            y += 32f;
            Widgets.Body(box, UiText.Get("ui.settings.debugViewHint"), new Vector2(24f, y), BoxWidth - 48f, Style.TextDim);
            y += 46f;
        }

        var back = Widgets.Button(box, UiText.Get("ui.nav.back"), new Rect2((BoxWidth - ButtonWidth) / 2f, y, ButtonWidth, ButtonHeight));
        back.Pressed += ShowMain;

        return box;
    }

    private static void StyleCheck(CheckButton check)
    {
        check.AddThemeFontSizeOverride("font_size", Style.TextSmall);
        check.AddThemeColorOverride("font_color", Style.Text);
        check.AddThemeColorOverride("font_pressed_color", Style.Text);
        check.AddThemeColorOverride("font_hover_color", Style.Text);
        check.AddThemeColorOverride("font_focus_color", Style.Text);
    }

    private static (HSlider Slider, Label Value) VolumeRow(Control box, string label, float y)
    {
        Widgets.Section(box, label, new Vector2(24f, y), 200f);
        var value = Widgets.Body(box, string.Empty, new Vector2(BoxWidth - 84f, y), 60f);
        value.HorizontalAlignment = HorizontalAlignment.Right;

        var slider = new HSlider
        {
            MinValue = 0,
            MaxValue = 100,
            Step = 5,
            Position = new Vector2(24f, y + 22f),
            Size = new Vector2(BoxWidth - 48f, 20f),
        };

        // Pista de tinta y relleno en rubrica, como el resto del pergamino; el gris del tema por defecto
        // parecía un control de otro juego.
        slider.AddThemeStyleboxOverride("slider", Track(Style.Line));
        slider.AddThemeStyleboxOverride("grabber_area", Track(Pregon.Wax));
        slider.AddThemeStyleboxOverride("grabber_area_highlight", Track(Pregon.Wax));
        box.AddChild(slider);
        return (slider, value);
    }

    private static StyleBoxFlat Track(Color color) => new()
    {
        BgColor = color,
        ContentMarginTop = 3f,
        ContentMarginBottom = 3f,
        CornerRadiusTopLeft = 3,
        CornerRadiusTopRight = 3,
        CornerRadiusBottomLeft = 3,
        CornerRadiusBottomRight = 3,
    };

    private void RefreshVolumeLabels()
    {
        _musicValue.Text = Percent(_music.Value);
        _effectsValue.Text = Percent(_effects.Value);
    }

    private static string Percent(double value) =>
        ((int)System.Math.Round(value)).ToString(System.Globalization.CultureInfo.InvariantCulture) + " %";

    /// <summary>Caja de tamaño fijo, maquetada a coordenadas absolutas como el resto de pantallas; la centra el CenterContainer.</summary>
    private static Control Box(float height)
    {
        var box = new Control
        {
            CustomMinimumSize = new Vector2(BoxWidth, height),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        return box;
    }
}
