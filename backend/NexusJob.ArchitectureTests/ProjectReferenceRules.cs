using System.Xml.Linq;
using Xunit;

namespace NexusJob.ArchitectureTests;

/// <summary>
/// Deterministic AD-2 rules 1-3 enforcement on the raw <c>&lt;ProjectReference&gt;</c>
/// graph of every <c>*.csproj</c> under <c>backend/</c>.
///
/// The ArchUnitNET rules and the <c>GetReferencedAssemblies()</c> checks in
/// <see cref="BoundaryRules"/> only see a cross-boundary edge once a <em>type</em>
/// from the target assembly is used: with the story-1.1 stubs essentially empty,
/// the C# compiler elides an unused project reference from the emitted assembly
/// metadata, so a declared-but-not-yet-used bad reference would slip through. This
/// scan reads the project files directly and catches it regardless of type usage.
///
/// A <c>&lt;ProjectReference&gt;</c>-element-only scan naturally ignores the
/// <c>&lt;BoundaryScanProject&gt;</c> items in this test's own csproj (those feed an
/// MSBuild <c>&lt;MSBuild&gt;</c> task, they are not project references).
/// </summary>
public sealed class ProjectReferenceRules
{
    private static readonly IReadOnlyList<BoundaryRuleChecks.Node> Projects = LoadProjects();

    [Fact]
    public void No_module_implementation_project_references_another_modules_implementation_project()
    {
        var offenders = BoundaryRuleChecks.Rule1Offenders(Projects);

        Assert.True(
            offenders.Count == 0,
            $"{BoundaryRules.Rule1} A module implementation project may reference only NexusJob.Modules.*.Contracts. " +
            $"Offending <ProjectReference> element(s): {string.Join(", ", offenders)}");
    }

    [Fact]
    public void No_contracts_project_references_a_non_contracts_project()
    {
        var offenders = BoundaryRuleChecks.Rule2Offenders(Projects);

        Assert.True(
            offenders.Count == 0,
            $"{BoundaryRules.Rule2} Offending <ProjectReference> element(s): {string.Join(", ", offenders)}");
    }

    [Fact]
    public void Only_the_Host_project_references_a_module_implementation_project()
    {
        var offenders = BoundaryRuleChecks.Rule3Offenders(Projects, ModuleAssemblies.HostName);

        Assert.True(
            offenders.Count == 0,
            $"{BoundaryRules.Rule3} Offending <ProjectReference> element(s): {string.Join(", ", offenders)}");
    }

    [Fact]
    public void The_Host_project_references_every_module_implementation_project()
    {
        var host = Projects.SingleOrDefault(p =>
            string.Equals(p.Name, ModuleAssemblies.HostName, StringComparison.Ordinal));

        Assert.True(
            host.Name == ModuleAssemblies.HostName,
            $"AD-10: {ModuleAssemblies.HostName}.csproj was not found under backend/ - the composition root is missing.");

        foreach (var implementationName in ModuleAssemblies.ImplementationNames)
        {
            Assert.True(
                host.References.Contains(implementationName),
                $"NexusJob.Host must declare a <ProjectReference> to {implementationName} (AD-10: the Host is the only composition root).");
        }
    }

    private static IReadOnlyList<BoundaryRuleChecks.Node> LoadProjects()
    {
        var backendDirectory = ModuleAssemblies.BackendDirectory;
        var result = new List<BoundaryRuleChecks.Node>();

        foreach (var path in Directory.EnumerateFiles(backendDirectory, "*.csproj", SearchOption.AllDirectories))
        {
            if (path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            var document = XDocument.Load(path);
            var references = document
                .Descendants()
                .Where(e => e.Name.LocalName == "ProjectReference")
                .Select(e => (string?)e.Attribute("Include"))
                .Where(include => !string.IsNullOrWhiteSpace(include))
                .Select(include => Path.GetFileNameWithoutExtension(include!.Replace('\\', Path.DirectorySeparatorChar)))
                .ToArray();

            result.Add(new BoundaryRuleChecks.Node(Path.GetFileNameWithoutExtension(path), references));
        }

        return result;
    }
}
