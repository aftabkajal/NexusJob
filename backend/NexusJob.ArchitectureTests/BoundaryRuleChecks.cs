using Xunit;

namespace NexusJob.ArchitectureTests;

/// <summary>
/// Pure offender computations for AD-2 rules 1-3 over injected inputs, so the
/// real-tree facts (<see cref="BoundaryRules"/>, <see cref="ProjectReferenceRules"/>)
/// and the synthetic-graph fixture facts below exercise the same code paths.
///
/// Two graph shapes:
///  * <see cref="Rule1Offenders"/>/2/3 take a <c>&lt;ProjectReference&gt;</c> graph
///    whose edges are all solution projects - "impl may reference only .Contracts".
///  * <see cref="Rule1ReferenceOffenders"/>/2/3 take an assembly-reference graph
///    (which also contains framework assemblies) plus the set of implementation
///    assembly names, and only judge edges into that set.
/// </summary>
internal static class BoundaryRuleChecks
{
    internal readonly record struct Node(string Name, IReadOnlyList<string> References);

    internal static bool IsContracts(string name) =>
        name.StartsWith("NexusJob.Modules.", StringComparison.Ordinal)
        && name.EndsWith(".Contracts", StringComparison.Ordinal);

    internal static bool IsImplementation(string name) =>
        name.StartsWith("NexusJob.Modules.", StringComparison.Ordinal)
        && !name.EndsWith(".Contracts", StringComparison.Ordinal);

    // ---- Project-reference graph -------------------------------------------

    /// <summary>Rule 1: a module implementation project may reference only <c>*.Contracts</c>.</summary>
    internal static IReadOnlyList<string> Rule1Offenders(IEnumerable<Node> graph) =>
        graph
            .Where(node => IsImplementation(node.Name))
            .SelectMany(node => node.References
                .Where(reference => !IsContracts(reference))
                .Select(reference => $"{node.Name} -> {reference}"))
            .ToList();

    /// <summary>Rule 2: a <c>.Contracts</c> project may reference only other <c>.Contracts</c>.</summary>
    internal static IReadOnlyList<string> Rule2Offenders(IEnumerable<Node> graph) =>
        graph
            .Where(node => IsContracts(node.Name))
            .SelectMany(node => node.References
                .Where(reference => !IsContracts(reference))
                .Select(reference => $"{node.Name} -> {reference}"))
            .ToList();

    /// <summary>Rule 3: only the Host project may reference a module implementation project.</summary>
    internal static IReadOnlyList<string> Rule3Offenders(IEnumerable<Node> graph, string hostName) =>
        graph
            .Where(node => !string.Equals(node.Name, hostName, StringComparison.Ordinal))
            .SelectMany(node => node.References
                .Where(IsImplementation)
                .Select(reference => $"{node.Name} -> {reference}"))
            .ToList();

    // ---- Assembly-reference graph (judged only against the impl-name set) ---

    internal static IReadOnlyList<string> Rule1ReferenceOffenders(
        IEnumerable<Node> graph, ISet<string> implementationNames) =>
        graph
            .Where(node => implementationNames.Contains(node.Name))
            .SelectMany(node => node.References
                .Where(reference => reference != node.Name && implementationNames.Contains(reference))
                .Select(reference => $"{node.Name} -> {reference}"))
            .ToList();

    internal static IReadOnlyList<string> Rule2ReferenceOffenders(
        IEnumerable<Node> graph, ISet<string> implementationNames) =>
        graph
            .Where(node => IsContracts(node.Name))
            .SelectMany(node => node.References
                .Where(implementationNames.Contains)
                .Select(reference => $"{node.Name} -> {reference}"))
            .ToList();

    internal static IReadOnlyList<string> Rule3ReferenceOffenders(
        IEnumerable<Node> graph, ISet<string> implementationNames, string hostName) =>
        graph
            .Where(node => !string.Equals(node.Name, hostName, StringComparison.Ordinal))
            .SelectMany(node => node.References
                .Where(implementationNames.Contains)
                .Select(reference => $"{node.Name} -> {reference}"))
            .ToList();

    internal static string Message(string rule, IReadOnlyList<string> offenders) =>
        $"{rule} Offending edge(s): {string.Join(", ", offenders)}";
}

/// <summary>
/// Fixture facts: feed a synthetic graph that contains each forbidden edge and
/// assert the offender list is non-empty and its message names the rule. Proves
/// the rule logic actually rejects a bad edge - the real-tree facts only ever
/// see a clean graph.
/// </summary>
public sealed class BoundaryRuleChecksTests
{
    private const string Host = "NexusJob.Host";

    private static readonly HashSet<string> ImplementationNames =
        new(StringComparer.Ordinal) { "NexusJob.Modules.Alpha", "NexusJob.Modules.Beta" };

    private static readonly BoundaryRuleChecks.Node[] ForbiddenProjectGraph =
    [
        new(Host, ["NexusJob.Modules.Alpha", "NexusJob.Modules.Beta"]),
        // impl -> another module's impl (rule 1, and also rule 3)
        new("NexusJob.Modules.Alpha", ["NexusJob.Modules.Alpha.Contracts", "NexusJob.Modules.Beta"]),
        new("NexusJob.Modules.Beta", ["NexusJob.Modules.Beta.Contracts"]),
        // .Contracts -> an impl (rule 2, and also rule 3)
        new("NexusJob.Modules.Alpha.Contracts", ["NexusJob.Modules.Beta"]),
        // non-Host project -> an impl (rule 3)
        new("NexusJob.SomeOtherProject", ["NexusJob.Modules.Alpha"]),
    ];

    private static readonly BoundaryRuleChecks.Node[] CleanProjectGraph =
    [
        new(Host, ["NexusJob.Modules.Alpha", "NexusJob.Modules.Beta"]),
        new("NexusJob.Modules.Alpha", ["NexusJob.Modules.Alpha.Contracts", "NexusJob.Modules.Beta.Contracts"]),
        new("NexusJob.Modules.Beta", ["NexusJob.Modules.Beta.Contracts"]),
        new("NexusJob.Modules.Alpha.Contracts", []),
        new("NexusJob.Modules.Beta.Contracts", []),
    ];

    // Assembly graph also carries framework references, which must be ignored.
    private static readonly BoundaryRuleChecks.Node[] ForbiddenAssemblyGraph =
    [
        new("NexusJob.Modules.Alpha", ["System.Runtime", "NexusJob.Modules.Beta"]),
        new("NexusJob.Modules.Alpha.Contracts", ["System.Runtime", "NexusJob.Modules.Beta"]),
        new("NexusJob.ArchitectureTests", ["xunit.core", "NexusJob.Modules.Alpha"]),
    ];

    [Fact]
    public void Rule1_rejects_an_implementation_to_implementation_project_edge()
    {
        var offenders = BoundaryRuleChecks.Rule1Offenders(ForbiddenProjectGraph);

        Assert.NotEmpty(offenders);
        Assert.Contains("NexusJob.Modules.Alpha -> NexusJob.Modules.Beta", offenders);
        Assert.Contains(BoundaryRules.Rule1, BoundaryRuleChecks.Message(BoundaryRules.Rule1, offenders));
    }

    [Fact]
    public void Rule2_rejects_a_contracts_to_implementation_project_edge()
    {
        var offenders = BoundaryRuleChecks.Rule2Offenders(ForbiddenProjectGraph);

        Assert.NotEmpty(offenders);
        Assert.Contains("NexusJob.Modules.Alpha.Contracts -> NexusJob.Modules.Beta", offenders);
        Assert.Contains(BoundaryRules.Rule2, BoundaryRuleChecks.Message(BoundaryRules.Rule2, offenders));
    }

    [Fact]
    public void Rule3_rejects_a_non_Host_to_implementation_project_edge()
    {
        var offenders = BoundaryRuleChecks.Rule3Offenders(ForbiddenProjectGraph, Host);

        Assert.NotEmpty(offenders);
        Assert.Contains("NexusJob.SomeOtherProject -> NexusJob.Modules.Alpha", offenders);
        Assert.Contains("NexusJob.Modules.Alpha.Contracts -> NexusJob.Modules.Beta", offenders);
        Assert.Contains(BoundaryRules.Rule3, BoundaryRuleChecks.Message(BoundaryRules.Rule3, offenders));
    }

    [Fact]
    public void Reference_form_rules_reject_the_forbidden_assembly_edges_and_ignore_framework_refs()
    {
        var rule1 = BoundaryRuleChecks.Rule1ReferenceOffenders(ForbiddenAssemblyGraph, ImplementationNames);
        var rule2 = BoundaryRuleChecks.Rule2ReferenceOffenders(ForbiddenAssemblyGraph, ImplementationNames);
        var rule3 = BoundaryRuleChecks.Rule3ReferenceOffenders(ForbiddenAssemblyGraph, ImplementationNames, Host);

        Assert.Contains("NexusJob.Modules.Alpha -> NexusJob.Modules.Beta", rule1);
        Assert.Contains("NexusJob.Modules.Alpha.Contracts -> NexusJob.Modules.Beta", rule2);
        Assert.Contains("NexusJob.ArchitectureTests -> NexusJob.Modules.Alpha", rule3);

        Assert.DoesNotContain(rule1, o => o.Contains("System.Runtime", StringComparison.Ordinal));
        Assert.Contains(BoundaryRules.Rule1, BoundaryRuleChecks.Message(BoundaryRules.Rule1, rule1));
        Assert.Contains(BoundaryRules.Rule2, BoundaryRuleChecks.Message(BoundaryRules.Rule2, rule2));
        Assert.Contains(BoundaryRules.Rule3, BoundaryRuleChecks.Message(BoundaryRules.Rule3, rule3));
    }

    [Fact]
    public void All_rules_accept_a_clean_graph()
    {
        Assert.Empty(BoundaryRuleChecks.Rule1Offenders(CleanProjectGraph));
        Assert.Empty(BoundaryRuleChecks.Rule2Offenders(CleanProjectGraph));
        Assert.Empty(BoundaryRuleChecks.Rule3Offenders(CleanProjectGraph, Host));

        var cleanAssemblyGraph = new[]
        {
            new BoundaryRuleChecks.Node("NexusJob.Modules.Alpha", ["System.Runtime", "NexusJob.Modules.Alpha.Contracts"]),
            new BoundaryRuleChecks.Node("NexusJob.Modules.Alpha.Contracts", ["System.Runtime"]),
        };
        Assert.Empty(BoundaryRuleChecks.Rule1ReferenceOffenders(cleanAssemblyGraph, ImplementationNames));
        Assert.Empty(BoundaryRuleChecks.Rule2ReferenceOffenders(cleanAssemblyGraph, ImplementationNames));
        Assert.Empty(BoundaryRuleChecks.Rule3ReferenceOffenders(cleanAssemblyGraph, ImplementationNames, Host));
    }
}
