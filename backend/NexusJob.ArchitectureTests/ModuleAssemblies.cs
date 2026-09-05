using System.Reflection;

namespace NexusJob.ArchitectureTests;

/// <summary>
/// Loads the module + Host assemblies from their build-output paths, without a
/// compile-time project reference (see the spec's Design Notes).
/// </summary>
internal static class ModuleAssemblies
{
    internal const string HostName = "NexusJob.Host";

    internal static readonly string[] ImplementationNames =
    [
        "NexusJob.Modules.Identity",
        "NexusJob.Modules.JobPostings",
        "NexusJob.Modules.Applications",
    ];

    internal static readonly string[] ContractNames =
    [
        "NexusJob.Modules.Identity.Contracts",
        "NexusJob.Modules.JobPostings.Contracts",
        "NexusJob.Modules.Applications.Contracts",
    ];

    private static readonly Lazy<IReadOnlyDictionary<string, Assembly>> LoadedAssemblies =
        new(LoadAll);

    internal static Assembly Get(string simpleName) => LoadedAssemblies.Value[simpleName];

    internal static IReadOnlyList<Assembly> Implementations()
        => ImplementationNames.Select(Get).ToArray();

    internal static IReadOnlyList<Assembly> Contracts()
        => ContractNames.Select(Get).ToArray();

    internal static Assembly Host() => Get(HostName);

    /// <summary>
    /// Absolute path to the <c>backend/</c> directory that contains every project.
    /// Found by walking up from the running test assembly to the first directory
    /// that contains <c>NexusJob.sln</c>, so it is correct regardless of a RID
    /// segment, a publish layout, or a differing build configuration.
    /// </summary>
    internal static string BackendDirectory { get; } = ResolveBackendDirectory();

    private static string ResolveBackendDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "NexusJob.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException(
            $"Could not locate NexusJob.sln by walking up from '{AppContext.BaseDirectory}'.");
    }

    private static IReadOnlyDictionary<string, Assembly> LoadAll()
    {
        // The test assembly lives under .../bin/<config>/<tfm>[/<rid>]/. Reuse
        // that tail to find each sibling's matching build output; otherwise fall
        // back to the most recently written copy anywhere under the project bin/.
        var baseDirectory = AppContext.BaseDirectory;
        var binMarker = $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}";
        var binIndex = baseDirectory.LastIndexOf(binMarker, StringComparison.Ordinal);
        var outputTail = binIndex >= 0
            ? baseDirectory[(binIndex + binMarker.Length)..]
                .Trim(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            : null;

        var result = new Dictionary<string, Assembly>(StringComparer.Ordinal);
        foreach (var name in ImplementationNames.Concat(ContractNames).Append(HostName))
        {
            var projectBin = Path.Combine(BackendDirectory, name, "bin");

            string? dllPath = null;
            if (outputTail is not null)
            {
                var candidate = Path.Combine(projectBin, outputTail, name + ".dll");
                if (File.Exists(candidate))
                {
                    dllPath = candidate;
                }
            }

            dllPath ??= Directory.Exists(projectBin)
                ? Directory
                    .EnumerateFiles(projectBin, name + ".dll", SearchOption.AllDirectories)
                    .OrderByDescending(File.GetLastWriteTimeUtc)
                    .FirstOrDefault()
                : null;

            if (dllPath is null)
            {
                throw new FileNotFoundException(
                    $"No build output for '{name}' under '{projectBin}'. Run `dotnet build backend/NexusJob.sln` first.");
            }

            result[name] = Assembly.LoadFrom(dllPath);
        }

        return result;
    }
}
