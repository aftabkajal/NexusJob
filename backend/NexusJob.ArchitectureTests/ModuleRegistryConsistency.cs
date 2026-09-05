using Xunit;

namespace NexusJob.ArchitectureTests;

/// <summary>
/// Guards against a new <c>NexusJob.Modules.*</c> project being added to the
/// solution but not registered in the architecture-test lookups
/// (<see cref="ModuleAssemblies.ImplementationNames"/>,
/// <see cref="ModuleAssemblies.ContractNames"/>,
/// <see cref="RawSqlSchemaScan.ModuleSchemas"/>) - which would leave it silently
/// unchecked by the boundary and raw-SQL scans.
/// </summary>
public sealed class ModuleRegistryConsistency
{
    [Fact]
    public void Every_module_project_on_disk_is_registered_in_the_architecture_test_lookups()
    {
        var missing = new List<string>();

        foreach (var directory in Directory.EnumerateDirectories(ModuleAssemblies.BackendDirectory, "NexusJob.Modules.*"))
        {
            var name = Path.GetFileName(directory);

            // Only count a directory that actually holds its project file.
            if (!File.Exists(Path.Combine(directory, name + ".csproj")))
            {
                continue;
            }

            if (name.EndsWith(".Contracts", StringComparison.Ordinal))
            {
                if (!ModuleAssemblies.ContractNames.Contains(name))
                {
                    missing.Add($"{name} is missing from ModuleAssemblies.ContractNames");
                }
            }
            else
            {
                if (!ModuleAssemblies.ImplementationNames.Contains(name))
                {
                    missing.Add($"{name} is missing from ModuleAssemblies.ImplementationNames");
                }

                if (!RawSqlSchemaScan.ModuleSchemas.ContainsKey(name))
                {
                    missing.Add($"{name} is missing from RawSqlSchemaScan.ModuleSchemas");
                }
            }
        }

        Assert.True(
            missing.Count == 0,
            "A new NexusJob.Modules.* project is not registered in the architecture-test lookups, so it is "
            + "not covered by the boundary / raw-SQL scans: " + string.Join("; ", missing));
    }
}
