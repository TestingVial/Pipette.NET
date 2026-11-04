namespace Pipette.Core.Workspace;

/// <param name="IsTestProject">The evaluated MSBuild <c>IsTestProject</c> property, or null when unset or unknown.</param>
/// <param name="PackageReferences">Names of the project's PackageReference items.</param>
/// <param name="MetadataReferences">File paths (or names) of the compilation's metadata references.</param>
public sealed record ProjectFacts(
    bool? IsTestProject,
    IReadOnlyCollection<string> PackageReferences,
    IReadOnlyCollection<string> MetadataReferences);

/// <summary>Decides whether a loaded project is a test project.</summary>
public sealed class TestProjectDetector
{
    /// <summary>Test framework packages that mark a project as a test project when <c>IsTestProject</c> is not set.</summary>
    public static IReadOnlyList<string> TestFrameworkPackages { get; } =
        ["xunit", "xunit.v3", "NUnit", "MSTest.TestFramework", "MSTest", "TUnit"];

    /// <summary>Assembly names shipped by <see cref="TestFrameworkPackages"/>, matched against metadata references.</summary>
    public static IReadOnlyList<string> TestFrameworkAssemblies { get; } =
    [
        "xunit.core",
        "xunit.v3.core",
        "nunit.framework",
        "Microsoft.VisualStudio.TestPlatform.TestFramework",
        "MSTest.TestFramework",
        "TUnit.Core",
    ];

    /// <summary>
    /// <c>IsTestProject == true</c> wins; otherwise a reference to a known test framework package or assembly.
    /// An explicit <c>IsTestProject == false</c> still falls back to references, since some SDKs only set it on test projects.
    /// </summary>
    public bool IsTestProject(ProjectFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);

        if (facts.IsTestProject == true)
        {
            return true;
        }

        if (facts.PackageReferences.Any(p => TestFrameworkPackages.Contains(p, StringComparer.OrdinalIgnoreCase)))
        {
            return true;
        }

        return facts.MetadataReferences
            .Select(Path.GetFileNameWithoutExtension)
            .Any(name => name is not null && TestFrameworkAssemblies.Contains(name, StringComparer.OrdinalIgnoreCase));
    }
}
