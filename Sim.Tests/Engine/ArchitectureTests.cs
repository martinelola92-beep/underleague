using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Underleague.Sim.Tests.Engine;

/// <summary>
/// Comprueba, escaneando los metadatos del ensamblado compilado, que Underleague.Sim respeta
/// las fronteras de arquitectura de RT-011 (sin Godot) y RT-012/RT-021 (sin E/S, reloj,
/// aleatoriedad compartida ni paralelismo no determinista).
/// </summary>
public sealed class ArchitectureTests
{
    private static readonly string AssemblyPath = Path.Combine(AppContext.BaseDirectory, "Underleague.Sim.dll");

    /// <summary>Nombres completos de tipos del BCL prohibidos en /Sim (RT-012, RT-021).</summary>
    private static readonly string[] ForbiddenTypeNames =
    {
        "System.IO.File",
        "System.IO.FileStream",
        "System.IO.Directory",
        "System.Random",
        "System.DateTime",
        "System.DateTimeOffset",
        "System.Guid",
        "System.Diagnostics.Stopwatch",
        "System.Threading.Tasks.Parallel",
        "System.Environment",
    };

    [Fact]
    public void NoReferenceToGodotAssemblies()
    {
        using var peReader = OpenAssembly();
        var metadata = peReader.GetMetadataReader();

        var offenders = new List<string>();
        foreach (var handle in metadata.AssemblyReferences)
        {
            var reference = metadata.GetAssemblyReference(handle);
            var name = metadata.GetString(reference.Name);
            if (name.Contains("Godot", StringComparison.OrdinalIgnoreCase))
            {
                offenders.Add(name);
            }
        }

        Assert.True(
            offenders.Count == 0,
            "Underleague.Sim no debe referenciar ensamblados de Godot (RT-011). Encontrados: "
                + string.Join(", ", offenders));
    }

    [Fact]
    public void NoReferenceToForbiddenFrameworkTypes()
    {
        using var peReader = OpenAssembly();
        var metadata = peReader.GetMetadataReader();

        var offenders = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var handle in metadata.TypeReferences)
        {
            var typeReference = metadata.GetTypeReference(handle);
            var ns = metadata.GetString(typeReference.Namespace);
            var name = metadata.GetString(typeReference.Name);
            var fullName = string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";

            // System.Environment se comprueba por miembro (ver abajo): el compilador inyecta
            // Environment.CurrentManagedThreadId en todo iterador con yield return.
            if (fullName != "System.Environment" && Array.IndexOf(ForbiddenTypeNames, fullName) >= 0)
            {
                offenders.Add(fullName);
            }
        }

        foreach (var handle in metadata.MemberReferences)
        {
            var member = metadata.GetMemberReference(handle);
            if (member.Parent.Kind != HandleKind.TypeReference)
            {
                continue;
            }

            var parent = metadata.GetTypeReference((TypeReferenceHandle)member.Parent);
            var parentName = $"{metadata.GetString(parent.Namespace)}.{metadata.GetString(parent.Name)}";
            var memberName = metadata.GetString(member.Name);
            if (parentName == "System.Environment" && memberName != "get_CurrentManagedThreadId")
            {
                offenders.Add($"{parentName}.{memberName}");
            }
        }

        Assert.True(
            offenders.Count == 0,
            "Underleague.Sim no debe referenciar tipos no deterministas o de E/S prohibidos en /Sim "
                + "(RT-012, RT-013, RT-021). Tipos prohibidos encontrados: "
                + string.Join(", ", offenders));
    }

    private static PEReader OpenAssembly()
    {
        Assert.True(
            File.Exists(AssemblyPath),
            $"No se encontró el ensamblado compilado de Underleague.Sim en '{AssemblyPath}'. "
                + "Este test lee los metadatos del binario, no el código fuente.");

        var stream = File.OpenRead(AssemblyPath);
        return new PEReader(stream);
    }
}
