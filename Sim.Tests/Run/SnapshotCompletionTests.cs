using Underleague.Sim.Data;
using Underleague.Sim.Run;
using Underleague.Sim.Run.Save;
using Underleague.Sim.Run.Systems;

namespace Underleague.Sim.Tests.Run;

/// <summary>
/// RT-061, RT-061b, ADR 0163: un guardado de antes de los apodos, la Gaceta y las prótesis se puede retomar.
/// La instantánea de la run sigue mandando en todo lo que no está en la lista de ficheros añadibles.
/// </summary>
public sealed class SnapshotCompletionTests
{
    private static readonly IReadOnlyList<string> Addable = SnapshotCompletion.AddableFiles;

    private static Dictionary<string, string> WithoutAddable(IReadOnlyDictionary<string, string> files)
    {
        var old = new Dictionary<string, string>(files);
        foreach (string path in Addable)
        {
            Assert.True(old.Remove(path), $"el /data actual no trae {path}: la lista de añadibles está desfasada");
        }

        return old;
    }

    /// <summary>El caso real: el guardado no trae los ficheros nuevos, y sin la compleción no se retoma.</summary>
    [Fact]
    public void ASnapshotWithoutTheAddableFilesResumesAfterCompletion()
    {
        var current = TestData.LoadAllFiles();
        var old = WithoutAddable(current);
        var catalog = DataLoader.FromJson(old);
        var state = RunEngine.Start(TestRuns.Setup(), 4242, catalog).WithDataSnapshot(old);

        var loaded = RunSave.Load(RunSave.Save(state));

        // Sin compleción es justo el fallo observado: el cargador exige un fichero que el guardado no trae.
        Assert.ThrowsAny<Exception>(() => StandardRunSystems.FromJson(loaded.DataSnapshot));

        var completed = SnapshotCompletion.Complete(loaded, current);
        var systems = StandardRunSystems.FromJson(completed.DataSnapshot);

        Assert.NotEmpty(systems.Nicknames.All);
        foreach (string path in Addable)
        {
            Assert.Equal(current[path], completed.DataSnapshot[path]);
        }

        // La run sigue siendo la misma: ni un fichero de reglas cambia, ni el estado.
        Assert.Equal(old.Count + Addable.Count, completed.DataSnapshot.Count);
        foreach (var (path, content) in old)
        {
            Assert.Equal(content, completed.DataSnapshot[path]);
        }

        Assert.Equal(loaded.Seed, completed.Seed);
        Assert.Equal(loaded.Roster.Count, completed.Roster.Count);
        Assert.Equal(RunSave.Save(loaded.WithDataSnapshot(completed.DataSnapshot)), RunSave.Save(completed));
    }

    /// <summary>Un fichero que el guardado ya trae no se toca, aunque el /data actual sea distinto (RT-061b).</summary>
    [Fact]
    public void AFileTheSnapshotAlreadyHasIsNeverReplaced()
    {
        var current = TestData.LoadAllFiles();
        var snapshot = new Dictionary<string, string>(current) { ["nicknames/nicknames.json"] = "{\"de la run\":true}" };

        var completed = SnapshotCompletion.Complete(snapshot, current);

        Assert.Equal("{\"de la run\":true}", completed["nicknames/nicknames.json"]);
    }

    /// <summary>
    /// Lo que no está en la lista no se completa jamás: un fichero de reglas ausente sigue siendo un error
    /// explícito, y no un cambio silencioso de las reglas de la run.
    /// </summary>
    [Fact]
    public void AMissingRulesFileIsNotCompleted()
    {
        var current = TestData.LoadAllFiles();
        var snapshot = new Dictionary<string, string>(current);
        Assert.True(snapshot.Remove("economy/economy.json"));

        var completed = SnapshotCompletion.Complete(snapshot, current);

        Assert.Equal(snapshot.Count, completed.Count);
        Assert.False(completed.ContainsKey("economy/economy.json"));
    }
}
