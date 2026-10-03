using System.IO;
using System.Linq;
using Godot;
using Underleague.Game.Autoload;
using Underleague.Game.Ui;
using Underleague.Sim.Model;
using Underleague.Sim.Run;
using Underleague.Sim.Run.Systems.Rewards;

namespace Underleague.Game.Screens;

/// <summary>
/// Capturas del dorsal fijo (BX-4) y de «fichar con la plantilla llena soltando a uno propio» (BX-5): la recompensa de jugador con el panel de
/// cambio y el mercado con un fichaje elegido y el jugador a soltar marcado en «Vender». Sobre una run de verdad con la
/// plantilla llena (10 de 10).
/// <code>
/// xvfb-run -a --server-args="-screen 0 1280x800x24" godot --path Game \
///   --rendering-driver opengl3 --audio-driver Dummy res://Scenes/CapturasCambio.tscn
/// </code>
/// </summary>
public partial class SwapCaptureRunner : Control
{
    private string _directory = string.Empty;

    public override void _Ready()
    {
        Nav.Suppressed = true;
        _directory = ProjectSettings.GlobalizePath("res://screenshots");
        Directory.CreateDirectory(_directory);
        _ = Capture();
    }

    private async System.Threading.Tasks.Task Capture()
    {
        var run = RunController.Instance;
        if (run is null)
        {
            GD.PushError("no hay RunController: la escena de capturas necesita el autoload del proyecto");
            GetTree().Quit(1);
            return;
        }

        int failures = 0;

        // 00. BX-1: la guardada podada a cuatro por las bajas; Equipo enseña el once que juega, con el relleno marcado.
        run.NewRun("orc_ironworks", Race.Orc, 1UL);
        run.SeedForCapture(state => state.WithLineup(new Lineup(state.Lineup.Slots.Take(4).ToList())));
        var filled = await Show("res://Scenes/Equipo.tscn");
        await Save("equipo-relleno");
        Drop(filled);

        // 0. BX-4: la plantilla de una run con el dorsal fijo de cada jugador en su fila.
        run.NewRun("orc_ironworks", Race.Orc, 1UL);
        var team = await Show("res://Scenes/Equipo.tscn");
        await Save("equipo-dorsal");
        Drop(team);

        // 1. Recompensa de jugador con la plantilla llena: se busca una semilla cuya recompensa de élite ofrezca uno.
        int optionIndex = -1;
        for (ulong seed = 1; seed <= 80 && optionIndex < 0; seed++)
        {
            run.NewRun("orc_ironworks", Race.Orc, seed);
            run.SeedForCapture(state =>
            {
                state = state.WithNewPlayer(state.Roster[0] with { Id = -1 });
                foreach (var node in state.CurrentMap.Nodes)
                {
                    if (node.Kind == NodeKind.EliteMatch)
                    {
                        return state.WithPendingNode(node.Id);
                    }
                }

                return state;
            });

            if (run.Reward() is { } view)
            {
                for (int i = 0; i < view.Options.Count; i++)
                {
                    if (view.Options[i] is { Kind: Sim.Run.View.RewardKind.Player })
                    {
                        optionIndex = i;
                    }
                }
            }
        }

        if (optionIndex < 0)
        {
            GD.PushError("ninguna semilla de 1..80 ofrece un jugador de recompensa de élite");
            failures++;
        }
        else
        {
            var reward = await Show("res://Scenes/Recompensa.tscn");
            var card = FindOptionCard(reward, optionIndex);
            if (card is not null)
            {
                await Click(card.GetGlobalRect().GetCenter());
            }

            await Save("cambio-recompensa");
            Drop(reward);
        }

        // 2. Mercado con la plantilla llena: un fichaje elegido y a quién soltar marcado en «Vender».
        for (ulong seed = 1; seed <= 80; seed++)
        {
            run.NewRun("orc_ironworks", Race.Orc, seed);
            SeedMarket(run);
            if (run.Market() is { } offered && System.Linq.Enumerable.Any(offered.Players, row => row.Category == Sim.Run.Systems.Market.MarketCategories.Player))
            {
                break;
            }
        }

        var market = await Show("res://Scenes/Mercado.tscn");
        var offer = FindRecruitCard(market);
        await MarketShot(market, offer);
        Drop(market);
        Nav.Suppressed = false;
        GetTree().Quit(failures);
    }

    private static void SeedMarket(RunController run)
    {
        run.SeedForCapture(state =>
        {
            state = state.WithNewPlayer(state.Roster[0] with { Id = -1 }).WithGold(400);

            // El jugador que se suelta ya ha jugado (se vende): uno que no, se descartaría sin cobrar.
            state = state.WithPlayer(state.Roster[3] with { Experience = 60 });
            foreach (var node in state.CurrentMap.Nodes)
            {
                if (node.Kind == NodeKind.Market)
                {
                    return state.WithPendingNode(node.Id);
                }
            }

            return state;
        });
    }

    private async System.Threading.Tasks.Task MarketShot(Node market, OptionCard? offer)
    {
        if (offer is null)
        {
            GD.PushError("el mercado de esta semilla no ofrece ningún fichaje");
        }
        else
        {
            await Click(offer.GetGlobalRect().GetCenter());
            var sellers = new System.Collections.Generic.List<OptionCard>();
            CollectSellCards(market, sellers);
            if (sellers.Count > 3)
            {
                await Click(sellers[3].GetGlobalRect().GetCenter());
            }

            await Save("cambio-mercado");
        }
    }

    private static OptionCard? FindOptionCard(Node root, int index)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is OptionCard card && !card.HasMeta("marketCategory") && card.Index == index)
            {
                return card;
            }

            if (FindOptionCard(child, index) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>El primer fichaje de la columna de jugadores (la de más a la izquierda).</summary>
    private static OptionCard? FindRecruitCard(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is OptionCard card && !card.HasMeta("marketCategory") && card.Index == 0 && card.GetGlobalRect().Position.X < 400f)
            {
                return card;
            }

            if (FindRecruitCard(child) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>Las cartas de la lista «Vender» (la columna de la derecha, sin metadatos de oferta), de arriba abajo.</summary>
    private static void CollectSellCards(Node root, System.Collections.Generic.List<OptionCard> into)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is OptionCard card && !card.HasMeta("marketCategory") && card.GetGlobalRect().Position.X > 880f)
            {
                into.Add(card);
            }

            CollectSellCards(child, into);
        }
    }

    private async System.Threading.Tasks.Task Click(Vector2 at)
    {
        Input.WarpMouse(at);
        Input.ParseInputEvent(new InputEventMouseMotion { Position = at, GlobalPosition = at });
        Input.ParseInputEvent(new InputEventMouseButton
        {
            ButtonIndex = MouseButton.Left,
            Pressed = true,
            Position = at,
            GlobalPosition = at,
            ButtonMask = MouseButtonMask.Left,
        });
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = at, GlobalPosition = at });
        for (int i = 0; i < 3; i++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    private async System.Threading.Tasks.Task<Node> Show(string path)
    {
        var instance = GD.Load<PackedScene>(path).Instantiate();
        AddChild(instance);
        for (int i = 0; i < 4; i++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        return instance;
    }

    private void Drop(Node instance)
    {
        RemoveChild(instance);
        instance.QueueFree();
    }

    private async System.Threading.Tasks.Task Save(string name)
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(RenderingServer.Singleton, "frame_post_draw");
        GetViewport().GetTexture().GetImage().SavePng(Path.Combine(_directory, name + ".png"));
        GD.Print($"captura: {name}.png");
    }
}
