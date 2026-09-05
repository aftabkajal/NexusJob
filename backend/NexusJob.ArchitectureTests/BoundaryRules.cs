using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnit;
using Xunit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace NexusJob.ArchitectureTests;

/// <summary>
/// The SM-2 module-boundary gate (AD-2 rules 1-3). Every assertion fails with a
/// message that names the violated rule.
///
/// Two enforcement layers run for each rule:
///   * an ArchUnitNET type-dependency rule (semantic - catches type usage), and
///   * a referenced-assembly reflection check (deterministic - catches a used
///     project reference no matter how deep the usage sits).
///
/// A third layer, on the raw <c>&lt;ProjectReference&gt;</c> graph, lives in
/// <see cref="ProjectReferenceRules"/> and catches an unused (compiler-elided)
/// reference. The offender logic for all three is in <see cref="BoundaryRuleChecks"/>.
/// </summary>
public sealed class BoundaryRules
{
    internal const string Rule1 =
        "AD-2 rule 1: no module implementation may depend on another module's non-Contracts assembly.";
    internal const string Rule2 =
        "AD-2 rule 2: a .Contracts assembly must not depend on any module implementation.";
    internal const string Rule3 =
        "AD-2 rule 3: only NexusJob.Host may reference a module implementation assembly.";

    private static readonly ISet<string> ImplementationNameSet =
        ModuleAssemblies.ImplementationNames.ToHashSet(StringComparer.Ordinal);

    private static readonly Architecture Architecture = new ArchLoader()
        .LoadAssemblies(
            ModuleAssemblies.Implementations()
                .Concat(ModuleAssemblies.Contracts())
                .Append(ModuleAssemblies.Host())
                .ToArray())
        .Build();

    private static IEnumerable<BoundaryRuleChecks.Node> AssemblyGraph(
        IEnumerable<System.Reflection.Assembly> assemblies) =>
        assemblies.Select(assembly => new BoundaryRuleChecks.Node(
            assembly.GetName().Name!,
            assembly.GetReferencedAssemblies().Select(reference => reference.Name!).ToList()));

    // ---- Rule 1 : impl must not depend on another module's impl -------------

    [Fact]
    public void Module_implementation_does_not_depend_on_another_modules_implementation_ArchUnit()
    {
        var implementations = ModuleAssemblies.Implementations();

        foreach (var source in implementations)
        {
            foreach (var target in implementations.Where(a => a != source))
            {
                Types().That().ResideInAssembly(source)
                    .Should().NotDependOnAny(Types().That().ResideInAssembly(target))
                    .Because(Rule1)
                    .Check(Architecture);
            }
        }
    }

    [Fact]
    public void Module_implementation_does_not_reference_another_modules_implementation_assembly()
    {
        var offenders = BoundaryRuleChecks.Rule1ReferenceOffenders(
            AssemblyGraph(ModuleAssemblies.Implementations()), ImplementationNameSet);

        Assert.True(offenders.Count == 0, $"{Rule1} Offending references: {string.Join(", ", offenders)}");
    }

    // ---- Rule 2 : Contracts must not depend on any impl --------------------

    [Fact]
    public void Contracts_assembly_does_not_depend_on_any_implementation_ArchUnit()
    {
        foreach (var contracts in ModuleAssemblies.Contracts())
        {
            foreach (var implementation in ModuleAssemblies.Implementations())
            {
                Types().That().ResideInAssembly(contracts)
                    .Should().NotDependOnAny(Types().That().ResideInAssembly(implementation))
                    .Because(Rule2)
                    .Check(Architecture);
            }
        }
    }

    [Fact]
    public void Contracts_assembly_does_not_reference_any_implementation_assembly()
    {
        var offenders = BoundaryRuleChecks.Rule2ReferenceOffenders(
            AssemblyGraph(ModuleAssemblies.Contracts()), ImplementationNameSet);

        Assert.True(offenders.Count == 0, $"{Rule2} Offending references: {string.Join(", ", offenders)}");
    }

    // ---- Rule 3 : only the Host references an impl ------------------------

    [Fact]
    public void Only_the_Host_references_a_module_implementation_assembly()
    {
        // Every loaded assembly that is neither the Host nor a module
        // implementation, plus this test assembly itself.
        var suspects = ModuleAssemblies.Contracts().Append(typeof(BoundaryRules).Assembly);

        var offenders = BoundaryRuleChecks.Rule3ReferenceOffenders(
            AssemblyGraph(suspects), ImplementationNameSet, ModuleAssemblies.HostName);

        Assert.True(offenders.Count == 0, $"{Rule3} Offending references: {string.Join(", ", offenders)}");
    }

    // ---- Structural sanity : the Host IS the composition root -------------

    [Fact]
    public void Host_references_every_module_implementation()
    {
        var referenced = ModuleAssemblies.Host()
            .GetReferencedAssemblies()
            .Select(a => a.Name)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var implementationName in ModuleAssemblies.ImplementationNames)
        {
            Assert.True(
                referenced.Contains(implementationName),
                $"NexusJob.Host must reference {implementationName} (AD-10: the Host is the only composition root).");
        }
    }
}
