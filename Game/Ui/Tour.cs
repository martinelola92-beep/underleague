using System;
using System.Collections.Generic;
using System.IO;
using Godot;

namespace Underleague.Game.Ui;

/// <summary>
/// Recorrido de capturas del esqueleto jugable. Con <c>--tour</c> el juego se juega solo por las
/// pantallas de inicio, mapa y ojeo, guarda una captura de cada una en <c>Game/screenshots/</c> y sale.
/// <para>
/// Es la única forma de juzgar el resultado visual en este entorno (<c>docs/entorno.md</c>: no hay editor
/// gráfico y <c>--headless</c> no dibuja), y de paso es una prueba de humo de la navegación: si el
/// recorrido llega al ojeo, es que empezar una run, generar el mapa, elegir nodo y construir el partido
/// funcionan de verdad.
/// </para>
/// <para>
/// El paso de cada pantalla lo declara la pantalla misma, con lo que <b>haría el jugador</b> para
/// avanzar: elegir el club, elegir un nodo de partido. No hay un guion escondido en ningún sitio.
/// </para>
/// </summary>
public static class Tour
{
    private const string Flag = "--tour";

    private static readonly HashSet<string> Captured = new();

    /// <summary>Directorio de capturas, el mismo que documenta <c>docs/ui-equipo.md</c>.</summary>
    public const string Directory = "res://screenshots";

    /// <summary>True si el juego se ha arrancado para hacer el recorrido de capturas.</summary>
    public static bool Active => HasArgument(Flag);

    /// <summary>True si se ha arrancado con <c>--screenshots</c> (el recorrido de la pantalla de Equipo).</summary>
    public static bool Screenshots => HasArgument("--screenshots");

    /// <summary>
    /// Captura la pantalla con ese nombre y ejecuta después <paramref name="next"/>, que es lo que el
    /// jugador pulsaría para seguir. Con <paramref name="next"/> a null, el recorrido termina y el juego
    /// se cierra.
    /// </summary>
    public static async void Step(Godot.Node from, string name, Action? next)
    {
        // El recorrido no da vueltas: volver a una pantalla ya capturada es que ha cerrado el circuito y
        // el trabajo está hecho.
        if (!Captured.Add(name))
        {
            from.GetTree().Quit();
            return;
        }

        // Dos fotogramas de proceso y uno de dibujo: la escena acaba de montarse y los Label todavía no
        // han calculado su tamaño en el primero.
        await from.ToSignal(from.GetTree(), SceneTree.SignalName.ProcessFrame);
        await from.ToSignal(from.GetTree(), SceneTree.SignalName.ProcessFrame);
        await from.ToSignal(RenderingServer.Singleton, "frame_post_draw");

        string directory = ProjectSettings.GlobalizePath(Directory);
        System.IO.Directory.CreateDirectory(directory);
        var image = from.GetViewport().GetTexture().GetImage();
        image.SavePng(Path.Combine(directory, name + ".png"));
        GD.Print($"captura: {name}.png");

        if (next is null)
        {
            from.GetTree().Quit();
            return;
        }

        next();
    }

    private static bool HasArgument(string flag)
    {
        foreach (string argument in OS.GetCmdlineArgs())
        {
            if (argument == flag)
            {
                return true;
            }
        }

        foreach (string argument in OS.GetCmdlineUserArgs())
        {
            if (argument == flag)
            {
                return true;
            }
        }

        return false;
    }
}
