using System.Collections.Generic;
using System.IO;
using Godot;
using Underleague.Game.Autoload;
using Underleague.Game.Ui;
using Underleague.Sim.Model;
using Underleague.Sim.Run;
using Underleague.Sim.Run.Systems.Market;
using Underleague.Sim.Run.View;

namespace Underleague.Game.Screens;

/// <summary>
/// Regenera las capturas de las cuatro pantallas del partido —Partido, Informe, Recompensa y Mercado—
/// jugando una run de verdad con una semilla fija. No es una pantalla del juego: es el equivalente de la
/// secuencia de capturas de <c>TeamScreen</c> para las pantallas que no son la de Equipo, y existe porque
/// sin editor gráfico (<c>docs/entorno.md</c>) la única forma de juzgar la composición es mirarlas.
/// <para>
/// Las pantallas no se falsean: se instancian sus escenas de verdad, con el <see cref="RunController"/>
/// del proyecto y el estado que deja una run jugada hasta ese punto. Los estados que hacen falta un clic
/// —una opción de recompensa elegida, un objeto del mercado abierto— se alcanzan <b>empujando eventos de
/// ratón sintéticos</b> por el mismo camino que la entrada real, no llamando a los métodos por dentro.
/// </para>
/// <code>
/// xvfb-run -a --server-args="-screen 0 1280x800x24" godot --path Game \
///   --rendering-driver opengl3 --audio-driver Dummy res://Scenes/Capturas.tscn
/// </code>
/// </summary>
public partial class CaptureRunner : Control
{
    /// <summary>Semilla fija: las capturas tienen que enseñar siempre la misma run (RT-021).</summary>
    private const ulong Seed = 20260905UL;

    private string _directory = string.Empty;

    public override void _Ready()
    {
        // BA-L2: las pantallas se instancian como hijas de este nodo, así que si una de ellas navega
        // (Nav.Go/Nav.Route) cambia la escena RAÍZ y destruye este arnés a mitad del recorrido. Se silencia
        // la navegación mientras dura la captura; se devuelve al estado normal al terminar.
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

        run.NewRun("orc_ironworks", Race.Orc, Seed);

        int matchNode = FirstOfKind(run, node => node.IsMatch);
        if (matchNode < 0)
        {
            GD.PushError("la run no ofrece ningún nodo de partido en el arranque del acto 1");
            GetTree().Quit(1);
            return;
        }

        run.SelectedNodeId = matchNode;

        // 1. Partido: el campo con las fichas en juego. Se llega a un instante concreto arrastrando la barra
        //    de reproducción —no dejando correr el reloj— para que la captura sea siempre la misma. El
        //    instante se elige con la traza en la mano: uno en el que alguien lleva el balón, para que la
        //    captura enseñe también el halo del poseedor.
        var match = await Show("res://Scenes/Partido.tscn", frames: 8);
        var pitch = FindPitch(match);
        var trace = run.Playback?.Trace;

        int shown = trace is { FrameCount: > 0 } ? WithCarrier(trace) : -1;
        if (trace is not null && shown >= 0)
        {
            await Click(new Vector2(918f + (340f * shown / (trace.FrameCount - 1)), 551f));
        }

        await Save("partido");

        // 2. El mismo campo con un jugador seguido y su correa encima (ADR 0028, ADR 0029). La ficha se
        //    pulsa en el píxel exacto donde la traza dice que está: es un clic de ratón de verdad, pero
        //    apuntado con el dato en vez de a ojo, para que la captura no dependa de la suerte.
        if (pitch is not null && trace is { FrameCount: > 0 })
        {
            int frame = Mathf.Clamp(pitch.Frame, 0, trace.FrameCount - 1);
            int index = trace.BallOwnerAt(frame);
            if (index < 0)
            {
                index = Nearest(trace, frame);
            }

            await Click(pitch.GlobalPosition + pitch.PixelOf(trace.PositionAt(frame, index)));
            await Click(new Vector2(1215f, 583f));
            GD.Print($"campo: tick {trace.TickAt(frame)}, seguido {trace.Players[index].Name} (dorsal {trace.Players[index].Number})");
        }

        await Save("partido-correa");

        // 2b. El marcaje (ADR 0022). Se busca un instante en el que alguien esté de verdad yendo a por su
        //     par —la línea continua— porque MarkOpponent gana la tabla de utilidad muy de tarde en tarde:
        //     dejar la captura al azar habría enseñado solo las asignaciones punteadas.
        if (pitch is not null && trace is { FrameCount: > 0 })
        {
            await Click(new Vector2(1215f, 583f));   // apagar la correa: aquí el sujeto es el marcaje
            int marker = -1;
            int at = Marking(trace, out marker);
            if (at >= 0)
            {
                await Click(new Vector2(918f + (340f * at / (trace.FrameCount - 1)), 551f));
                int frame = Mathf.Clamp(pitch.Frame, 0, trace.FrameCount - 1);
                await Click(pitch.GlobalPosition + pitch.PixelOf(trace.PositionAt(frame, marker)));
                GD.Print($"marcaje: tick {trace.TickAt(frame)}, {trace.Players[marker].Name} marca a "
                    + $"{(trace.MarkTargetAt(frame, marker) >= 0 ? trace.Players[trace.MarkTargetAt(frame, marker)].Name : "nadie")}");
            }
            else
            {
                GD.PushWarning("ningún jugador elige MarkOpponent en todo el partido de las capturas");
            }

            await Save("partido-marcaje");
        }

        // 2b-bis. C9: el aviso de perk activado. Se busca el primer aviso que no caiga en el saque —los
        //         perks de MATCH_START se cobran todos en el tick 1 y ahí no se distingue nada— y se para
        //         la reproducción DENTRO de su segundo de vida. Si el partido de las capturas no activa
        //         ningún perk, se dice: la captura que falta es un dato, no un fallo silencioso.
        if (pitch is not null && trace is { FrameCount: > 0 })
        {
            GD.Print($"avisos de perk en el partido: {pitch.Flashes.Count}");
            // Se prefiere un aviso que NO sea del saque —ahí se cobran de golpe todos los perks de
            // MATCH_START y la captura no distingue una habilidad de la ceremonia inicial—, pero si el
            // partido no tiene ninguno, el del saque también sirve: lo que se está probando es que el
            // cartel se pinta donde y cuando toca.
            int chosen = -1;
            for (int i = 0; i < pitch.Flashes.Count; i++)
            {
                if (pitch.Flashes[i].Frame > 0)
                {
                    chosen = i;
                    break;
                }
            }

            if (chosen < 0 && pitch.Flashes.Count > 0)
            {
                chosen = 0;
                GD.PushWarning("ningún perk se activa fuera del saque: la captura del aviso es la del saque");
            }

            int at = -1;
            if (chosen >= 0)
            {
                var flash = pitch.Flashes[chosen];
                at = flash.Frame + (MatchFlashView.DurationFrames / 3);
                GD.Print($"aviso: tick {trace.TickAt(flash.Frame)}, "
                    + $"{trace.Players[flash.Player].Name} activa '{flash.Name}'");
            }

            if (at >= 0)
            {
                await Click(new Vector2(918f + (340f * Mathf.Min(at, trace.FrameCount - 1) / (trace.FrameCount - 1)), 551f));
                await Save("partido-perk");
            }
            else
            {
                GD.PushWarning("el partido de las capturas no activa ningún perk: no hay captura del aviso");
            }
        }

        // 2c. ADR 0102: la prueba de geometría en 3D. Cápsulas grises a las proporciones de RA-002, cámara
        //     ortográfica fija en tres cuartos y sombra direccional desde arriba a la izquierda (RA-005,
        //     RA-008). Las cuatro imágenes salen del MISMO fotograma —la reproducción está parada desde el
        //     arrastre de la barra— para que lo único que cambie entre ellas sea lo que se está juzgando.
        var pitch3d = FindPitch3D(match);
        if (pitch3d is not null && trace is { FrameCount: > 0 })
        {
            await Click(new Vector2(1233f, 170f));   // el interruptor 2D/3D, por el mismo camino que un jugador
            if (!pitch3d.Visible)
            {
                GD.PushWarning("el interruptor 2D/3D no respondió al clic: se fuerza la vista para no perder la captura");
                pitch3d.Visible = true;
                if (pitch is not null)
                {
                    pitch.Visible = false;
                }
            }

            await Settle(6);
            await Save("partido-3d");

            // El barrido de elevación: es la palanca barata del ADR 0102 si en tres cuartos no se lee.
            foreach (int degrees in new[] { 30, 60 })
            {
                pitch3d.Elevation = degrees;
                await Settle(4);
                await Save("partido-3d-angulo-" + degrees.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }

            pitch3d.Elevation = 60f;

            // Modo silueta: RA-002 al pie de la letra, «toda raza debe ser reconocible en blanco y negro».
            await Click(new Vector2(1233f, 198f));
            if (!pitch3d.SilhouetteMode)
            {
                GD.PushWarning("el interruptor de silueta no respondió al clic: se fuerza el modo");
                pitch3d.SilhouetteMode = true;
            }

            await Settle(6);
            await Save("partido-3d-silueta");

            // La captura que decide la prueba: las cinco razas de lanzamiento a la vez, en silueta. El
            // partido de verdad enfrenta a dos razas, así que aquí se reparten las cinco entre las fichas
            // —solo en la vista, ni la traza ni el resultado se enteran— para poder compararlas juntas.
            pitch3d.ForceRaceParade(run.Catalog!);
            await Settle(6);
            await Save("partido-3d-razas");
            GD.Print("3d: cinco razas repartidas por dorsal (enano, elfo, humano, orco, no-muerto)");
        }
        else
        {
            GD.PushWarning("no se encontró la vista 3D del campo: no hay capturas de la prueba de geometría");
        }

        Drop(match);

        // 3. Informe post-partido.
        var report = await Show("res://Scenes/Informe.tscn");
        await Save("informe");
        Drop(report);

        // 4. Recompensa, con la primera opción elegida para que se vea la asignación a un jugador.
        var reward = await Show("res://Scenes/Recompensa.tscn");
        await Click(new Vector2(60f, 96f));
        await Save("recompensa");

        Drop(reward);

        // Prueba de humo de la asignación: se cobra la recompensa de verdad con el portador que la vista
        // declara elegible. No se hace pulsando el botón porque cobrar la última elección del nodo lo
        // cierra y navega al mapa, y eso se llevaría por delante a esta escena a mitad del recorrido.
        int perksBefore = PerkCount(run);
        var view = run.Reward();
        if (view is { Options.Count: > 0 })
        {
            for (int i = 0; i < view.Options.Count; i++)
            {
                var option = view.Options[i];
                if (option.Block != Sim.Run.View.RewardBlock.None)
                {
                    continue;
                }

                run.Apply(new ChooseReward(i, option.Carriers.Count > 0 ? option.Carriers[0].PlayerId : -1));
                break;
            }
        }

        GD.Print($"recompensa cobrada: perks {perksBefore} -> {PerkCount(run)}");

        // 5. Mercado: se rechazan las recompensas pendientes, se cierra el nodo y se camina hasta la
        //    tienda, que RF-011b garantiza a dos saltos como máximo.
        ResolveRewards(run);
        int marketNode = WalkToMarket(run);
        if (marketNode < 0)
        {
            GD.PushError("no se alcanzó ningún nodo de mercado en el acto 1");
        }
        else
        {
            var market = await Show("res://Scenes/Mercado.tscn");

            // Encargo mercado-arrastrar (UI-010): la ficha de la plantilla también se puede ampliar aquí,
            // igual que en Equipo — un clic sobre el primer titular, por el mismo camino que un jugador de
            // verdad, para que la captura base enseñe el retrato en medallón grande y las secciones
            // completas, no solo la tira colapsada. Se vuelve a colapsar antes de seguir: el resto de la
            // secuencia busca cartas por posición real en el árbol (<see cref="FindPlayerCard"/>) y una
            // ficha ampliada empuja a las de abajo fuera del scroll visible.
            int firstRosterId = run.State is { Roster.Count: > 0 } ? run.State.Roster[0].Id : -1;
            if (firstRosterId >= 0 && FindPlayerCard(market, firstRosterId) is { } firstCard)
            {
                await Click(firstCard.GetGlobalRect().GetCenter());
                await Save("mercado");

                if (FindPlayerCard(market, firstRosterId) is { } expandedCard)
                {
                    await Click(expandedCard.GetGlobalRect().GetCenter());
                }
            }
            else
            {
                GD.PushWarning("no se encontró la ficha del primer jugador: la captura base del Mercado no enseña ninguna ficha ampliada");
                await Save("mercado");
            }

            // Encargo mercado-arrastrar (UI-001, UI-006): un arrastre de verdad, no un clic. Se busca el
            // primer perk que se pueda pagar y que tenga a quién dárselo (Sim.Run.View.MarketRow.Carriers,
            // la misma lista que valida la compra), se localiza su carta y la ficha de su primer portador
            // por posición real en el árbol —nunca por pixel a ciegas— y se arrastra la una hasta la otra.
            var marketView = run.Market();
            int perkIndex = -1;
            int carrierId = -1;
            if (marketView is not null)
            {
                foreach (var perk in marketView.Perks)
                {
                    if (perk.Affordable && perk.Carriers.Count > 0)
                    {
                        perkIndex = perk.Index;
                        carrierId = perk.Carriers[0].PlayerId;
                        break;
                    }
                }
            }

            if (perkIndex < 0)
            {
                GD.PushWarning("ningún perk del mercado tiene comprador elegible: no hay captura de arrastre");
            }
            else
            {
                var offerCard = FindOfferCard(market, MarketCategories.Perk, perkIndex);
                var targetCard = FindPlayerCard(market, carrierId);
                if (targetCard is null || offerCard is null)
                {
                    GD.PushWarning("no se localizó la carta del perk o la ficha de su portador: no hay captura de arrastre");
                }
                else
                {
                    var cardCenter = offerCard.GetGlobalRect().GetCenter();
                    var targetCenter = targetCard.GetGlobalRect().GetCenter();
                    int goldBefore = run.State!.Gold;

                    await Press(cardCenter);
                    await Move(targetCenter);
                    await Save("mercado-arrastre");

                    await Release(targetCenter);
                    await Save("mercado-asignado");
                    GD.Print($"arrastre: perk #{perkIndex} -> jugador {carrierId}, oro {goldBefore} -> {run.State!.Gold}");
                }
            }

            Drop(market);
        }

        Nav.Suppressed = false;
        GetTree().Quit();
    }

    /// <summary>Instancia la escena de una pantalla y espera a que se estabilice.</summary>
    private async System.Threading.Tasks.Task<Node> Show(string path, int frames = 4)
    {
        var scene = GD.Load<PackedScene>(path);
        var instance = scene.Instantiate();
        AddChild(instance);
        for (int i = 0; i < frames; i++)
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

    /// <summary>Deja pasar unos fotogramas para que la escena 3D aplique el cambio antes de la captura.</summary>
    private async System.Threading.Tasks.Task Settle(int frames)
    {
        for (int i = 0; i < frames; i++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    /// <summary>Un clic de ratón por el mismo camino que la entrada real, para no llamar a nada por dentro.</summary>
    private async System.Threading.Tasks.Task Click(Vector2 at)
    {
        var physical = ToPhysical(at);
        GetViewport().PushInput(new InputEventMouseButton
        {
            Position = physical,
            GlobalPosition = physical,
            ButtonIndex = MouseButton.Left,
            Pressed = true,
        });
        GetViewport().PushInput(new InputEventMouseButton
        {
            Position = physical,
            GlobalPosition = physical,
            ButtonIndex = MouseButton.Left,
            Pressed = false,
        });

        for (int i = 0; i < 4; i++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    /// <summary>Empieza un arrastre: solo el botón abajo, por el mismo camino que un clic de verdad.</summary>
    private async System.Threading.Tasks.Task Press(Vector2 at)
    {
        var physical = ToPhysical(at);
        GetViewport().PushInput(new InputEventMouseButton
        {
            Position = physical,
            GlobalPosition = physical,
            ButtonIndex = MouseButton.Left,
            Pressed = true,
        });

        for (int i = 0; i < 2; i++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    /// <summary>Mueve el ratón sin soltar: dispara los <c>MouseEntered</c>/<c>MouseExited</c> de las cartas por las que pasa.</summary>
    private async System.Threading.Tasks.Task Move(Vector2 at)
    {
        var physical = ToPhysical(at);
        GetViewport().PushInput(new InputEventMouseMotion { Position = physical, GlobalPosition = physical });

        for (int i = 0; i < 4; i++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    /// <summary>Suelta donde esté el ratón en ese instante: el gesto que cierra un arrastre de verdad.</summary>
    private async System.Threading.Tasks.Task Release(Vector2 at)
    {
        var physical = ToPhysical(at);
        GetViewport().PushInput(new InputEventMouseButton
        {
            Position = physical,
            GlobalPosition = physical,
            ButtonIndex = MouseButton.Left,
            Pressed = false,
        });

        for (int i = 0; i < 4; i++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    /// <summary>
    /// <c>Viewport.PushInput</c> inyecta al mismo nivel que un evento real del sistema de ventanas, así
    /// que espera <b>pixeles físicos de la ventana</b> — mientras que <c>Control.GetGlobalRect()</c> (de
    /// donde salen todos los puntos que clica esta escena) da coordenadas en el lienzo lógico que
    /// <c>window/stretch/mode="canvas_items"</c> escala para pintar. A resolución 1280x800 (la de siempre
    /// en estas capturas) los dos espacios coinciden y el desajuste no se nota; a 1920x1080 (16:9, la que
    /// pide el encargo <c>mercado-arrastrar</c>) el lienzo lógico crece a ~1422x800 (<c>Layout.LegacySize</c>
    /// más el sobreancho de 16:9) y hace falta este factor, o el clic cae varias cartas a un lado de la
    /// que se ve en pantalla.
    /// </summary>
    private Vector2 ToPhysical(Vector2 logical)
    {
        var visible = GetViewport().GetVisibleRect().Size;
        var window = (Vector2)DisplayServer.WindowGetSize();
        if (visible.X <= 0f || visible.Y <= 0f)
        {
            return logical;
        }

        return new Vector2(logical.X * window.X / visible.X, logical.Y * window.Y / visible.Y);
    }

    /// <summary>
    /// La carta de un artículo arrastrable del Mercado, por categoría e índice de dato —los metadatos que
    /// <c>MarketScreen.DraggableColumn</c> deja en cada carta—, nunca por una franja de pixel adivinada: el
    /// área lógica real crece en 16:9 y <c>Layout</c> centra la pantalla dentro de ella (docs/entorno.md),
    /// así que dos columnas pueden solaparse en X según la resolución de captura.
    /// </summary>
    private static OptionCard? FindOfferCard(Node root, string category, int index)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is OptionCard card
                && card.HasMeta("marketCategory")
                && card.GetMeta("marketCategory").AsString() == category
                && card.GetMeta("marketIndex").AsInt32() == index)
            {
                return card;
            }

            if (FindOfferCard(child, category, index) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>La ficha de ese jugador en el árbol vivo, sea cual sea la pantalla que la contenga.</summary>
    private static PlayerCard? FindPlayerCard(Node root, int playerId)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is PlayerCard card && card.PlayerId == playerId)
            {
                return card;
            }

            if (FindPlayerCard(child, playerId) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    private async System.Threading.Tasks.Task Save(string name)
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(RenderingServer.Singleton, "frame_post_draw");
        var image = GetViewport().GetTexture().GetImage();
        image.SavePng(Path.Combine(_directory, name + ".png"));
        GD.Print($"captura: {name}.png");
    }

    /// <summary>
    /// Primer fotograma pasada la hora de juego en el que alguien lleva el balón: el instante que mejor
    /// enseña de qué va la pantalla, con poseedor, presión alrededor y el marcador ya movido.
    /// </summary>
    private static int WithCarrier(Underleague.Sim.Engine.MatchTrace trace)
    {
        for (int frame = trace.FrameCount * 3 / 5; frame < trace.FrameCount; frame++)
        {
            if (trace.BallOwnerAt(frame) >= 0)
            {
                return frame;
            }
        }

        return trace.FrameCount / 2;
    }

    /// <summary>
    /// Primer fotograma en el que un jugador lleva y seguirá llevando <c>MarkOpponent</c> un buen rato, y
    /// quién es. Se pide una racha de 20 ticks porque la barra de reproducción mide 340 px para 1.200
    /// fotogramas: el clic cae cerca del fotograma pedido, no exactamente en él.
    /// </summary>
    private static int Marking(Underleague.Sim.Engine.MatchTrace trace, out int marker)
    {
        const int Run = 20;
        for (int frame = trace.FrameCount / 5; frame < trace.FrameCount - Run; frame++)
        {
            for (int player = 0; player < trace.Players.Count; player++)
            {
                if (!IsMarking(trace, frame, player))
                {
                    continue;
                }

                bool holds = true;
                for (int ahead = 1; ahead <= Run && holds; ahead++)
                {
                    holds = IsMarking(trace, frame + ahead, player);
                }

                if (holds)
                {
                    marker = player;
                    return frame + (Run / 2);
                }
            }
        }

        marker = -1;
        return -1;
    }

    private static bool IsMarking(Underleague.Sim.Engine.MatchTrace trace, int frame, int player) =>
        trace.OnPitchAt(frame, player)
        && trace.ActionAt(frame, player) == Underleague.Sim.Engine.PlayerAction.MarkOpponent
        && trace.MarkTargetAt(frame, player) >= 0;

    /// <summary>El campo del partido dentro de la pantalla instanciada, o null si no está.</summary>
    private static MatchPitchView? FindPitch(Node screen)
    {
        foreach (var child in screen.GetChildren())
        {
            if (child is MatchPitchView pitch)
            {
                return pitch;
            }
        }

        return null;
    }

    /// <summary>La vista 3D del campo dentro de la pantalla instanciada, o null si no está (ADR 0102).</summary>
    private static MatchPitchView3D? FindPitch3D(Node screen)
    {
        foreach (var child in screen.GetChildren())
        {
            if (child is MatchPitchView3D view)
            {
                return view;
            }
        }

        return null;
    }

    /// <summary>Jugador más cercano al balón cuando no lo lleva nadie: siempre hay una ficha que pulsar.</summary>
    private static int Nearest(Underleague.Sim.Engine.MatchTrace trace, int frame)
    {
        var ball = trace.BallAt(frame);
        int best = 0;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < trace.Players.Count; i++)
        {
            if (!trace.OnPitchAt(frame, i))
            {
                continue;
            }

            float distance = Underleague.Sim.Engine.Vec2.Distance(trace.PositionAt(frame, i), ball);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = i;
            }
        }

        return best;
    }

    /// <summary>Perks asignados en toda la plantilla: la forma más simple de ver si una recompensa entró.</summary>
    private static int PerkCount(RunController run)
    {
        int count = 0;
        foreach (var player in run.State!.Roster)
        {
            count += player.Perks.Count;
        }

        return count;
    }

    private static int FirstOfKind(RunController run, System.Func<MapNode, bool> predicate)
    {
        foreach (var node in run.Available())
        {
            if (predicate(node))
            {
                return node.Id;
            }
        }

        return -1;
    }

    /// <summary>Rechaza las recompensas que el nodo de partido haya dejado abiertas y cierra el nodo.</summary>
    private static void ResolveRewards(RunController run)
    {
        for (int guard = 0; guard < 4 && run.Reward() is not null; guard++)
        {
            run.Apply(new DeclineReward());
        }

        if (run.State is { Phase: RunPhase.NodeOpen, PendingNodeId: >= 0 })
        {
            run.Apply(new LeaveNode());
        }
    }

    /// <summary>
    /// Camina por el mapa hasta el primer nodo de mercado. RF-011b garantiza uno a dos saltos, así que el
    /// paseo es corto; los nodos intermedios se resuelven y se cierran sin tocar nada.
    /// </summary>
    private static int WalkToMarket(RunController run)
    {
        for (int hop = 0; hop < 6; hop++)
        {
            var available = new List<MapNode>(run.Available());
            if (available.Count == 0)
            {
                return -1;
            }

            foreach (var node in available)
            {
                if (node.Kind == NodeKind.Market)
                {
                    run.Enter(node.Id);
                    return node.Id;
                }
            }

            run.Enter(available[0].Id);
            ResolveRewards(run);
            if (run.Outcome().IsOver)
            {
                return -1;
            }
        }

        return -1;
    }
}
