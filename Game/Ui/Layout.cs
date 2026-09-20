using Godot;

namespace Underleague.Game.Ui;

/// <summary>
/// Adapta al área lógica real una pantalla maquetada a coordenadas absolutas de <see cref="LegacySize"/>.
/// <para>
/// El juego pasa a 16:9 (<c>project.godot</c>: <c>window/stretch/aspect="expand"</c>, base 1280x800) sin
/// remaquetar las pantallas una por una: en 16:10 (el caso de siempre) el área lógica real
/// (<c>GetViewport().GetVisibleRect().Size</c>) sigue siendo 1280x800 y esto no cambia nada; en 16:9
/// crece en ancho a ~1422x800, así que cada pantalla —salvo la retransmisión, <c>BroadcastScreen</c>, que
/// compone su propio lienzo a partir del alto real (docs/ui/README.md §7)— se fija a su tamaño de
/// siempre y se centra dentro de esa área más ancha, con un fondo por detrás de todo que tapa los
/// laterales para que no queden bandas negras.
/// </para>
/// <para>
/// Reacciona a <c>Viewport.SizeChanged</c> (redimensionar la ventana en marcha) recolocándose, y se
/// desconecta de esa señal en <c>TreeExited</c>: el viewport es del árbol de escena raíz y sobrevive a la
/// pantalla, así que sin esto el delegado seguiría vivo apuntando a un <see cref="Control"/> ya liberado
/// tras el siguiente <c>ChangeSceneToFile</c> (<see cref="Nav.Go"/>).
/// </para>
/// </summary>
public static class Layout
{
    /// <summary>El tamaño con el que están maquetadas todas las pantallas salvo la retransmisión.</summary>
    public static readonly Vector2 LegacySize = new(1280f, 800f);

    /// <summary>Nombre fijo del fondo que añade <see cref="CenterLegacy"/>, para encontrarlo de nuevo tras un <c>Rebuild</c>.</summary>
    private const string FillName = "__LayoutFill";

    /// <summary>Meta que marca que esta pantalla ya está suscrita a <c>Viewport.SizeChanged</c>, para no acumular una suscripción por cada <c>Rebuild</c>.</summary>
    private const string SubscribedMeta = "__LayoutSubscribed";

    /// <summary>
    /// Fija <paramref name="screen"/> a <see cref="LegacySize"/>, centrado en el área lógica real, con un
    /// fondo del color de la pantalla (por defecto <see cref="Style.Background"/>, el que usa
    /// <c>Widgets.Background</c>) detrás de todos sus hijos.
    /// <para>
    /// Pensado para llamarse en cada <c>Build</c>/<c>Rebuild</c>, no solo en <c>_Ready</c>: varias
    /// pantallas (<c>RewardScreen</c>, <c>MarketScreen</c>, <c>NodeScreen</c>...) se repintan enteras
    /// borrando todos sus hijos, y eso incluye el fondo que esta función añadió la vez anterior. Llamarla
    /// de nuevo es barato — vuelve a fijar las mismas anclas y crea un fondo nuevo — y no duplica la
    /// suscripción al cambio de tamaño, que solo se hace una vez por instancia (<see cref="SubscribedMeta"/>).
    /// </para>
    /// </summary>
    public static void CenterLegacy(Control screen, Color? background = null)
    {
        // Anclas fijas arriba-izquierda: con las anclas de siempre (FullRect, heredadas del .tscn) el
        // tamaño lógico de la pantalla seguiría al del viewport en vez de quedarse en 1280x800.
        screen.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
        screen.Size = LegacySize;

        // Voz de pregón (encargo pregon-resto): un solo Theme para que las fuentes y la placa de los
        // botones cambien de una vez en toda pantalla vieja, incluida la que trae nodos ya maquetados en
        // un .tscn (Equipo) — un Theme en la raíz alcanza a los hijos de la escena, llamar a
        // Widgets.Button no. BuildLegacyTheme() cachea el Theme: reasignarlo en cada Rebuild es barato.
        screen.Theme = Widgets.BuildLegacyTheme();

        var fill = new ColorRect
        {
            Name = FillName,
            Color = background ?? Style.Background,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        screen.AddChild(fill);
        screen.MoveChild(fill, 0);

        Reposition(screen);

        if (screen.HasMeta(SubscribedMeta))
        {
            return;
        }

        screen.SetMeta(SubscribedMeta, true);
        var viewport = screen.GetViewport();
        void OnSizeChanged() => Reposition(screen);
        viewport.SizeChanged += OnSizeChanged;
        screen.TreeExited += () => viewport.SizeChanged -= OnSizeChanged;
    }

    /// <summary>
    /// Centra <paramref name="screen"/> en el área lógica real y estira su fondo (buscado por
    /// <see cref="FillName"/>, no por referencia guardada: <c>Rebuild</c> lo recrea) para cubrirla entera.
    /// </summary>
    private static void Reposition(Control screen)
    {
        if (!GodotObject.IsInstanceValid(screen))
        {
            return;
        }

        var area = screen.GetViewport().GetVisibleRect().Size;
        screen.Position = ((area - LegacySize) / 2f).Round();

        // El fondo cubre el área lógica entera, no solo el rectángulo de 1280x800: se coloca en el
        // sistema de coordenadas local de la pantalla (que ya está desplazada por el centrado), así que
        // su posición es el negativo de ese desplazamiento.
        var fill = screen.GetNodeOrNull<ColorRect>(FillName);
        if (fill is not null)
        {
            fill.Position = -screen.Position;
            fill.Size = area;
        }
    }
}
