using Godot;

namespace Underleague.Game.Ui.Broadcast;

/// <summary>
/// Placa de banquillo («Banquillo N»): mismo material que la tira, sin escudo ni estado. Su forma vive en
/// <c>BenchPlaque.tscn</c>; el código solo pone el texto. Se crea con <see cref="Create"/>.
/// </summary>
[Tool]
public partial class BenchPlaque : Control
{
    public const float DesignWidth = 150f;
    public const float DesignHeight = 72f;

    private const string ScenePath = "res://Ui/Broadcast/BenchPlaque.tscn";

    private int _count;
    private Label? _label;

    /// <summary>La placa con su escena. La única forma correcta de crearla.</summary>
    public static BenchPlaque Create() => GD.Load<PackedScene>(ScenePath).Instantiate<BenchPlaque>();

    public override void _Ready()
    {
        _label = GetNodeOrNull<Label>("%Texto");
        if (_label is null)
        {
            GD.PushError("BenchPlaque sin su escena: créala con BenchPlaque.Create(), no con new.");
            return;
        }

        if (Engine.IsEditorHint())
        {
            _count = 2;
        }

        Refresh();
    }

    public void SetCount(int count)
    {
        _count = count;
        Refresh();
    }

    private void Refresh()
    {
        if (_label is not null)
        {
            _label.Text = UiText.Get("ui.pregon.bench", _count);
        }
    }
}
