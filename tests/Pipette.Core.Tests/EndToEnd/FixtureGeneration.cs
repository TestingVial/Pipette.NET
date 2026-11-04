using Pipette.Core.Diagnostics;
using Pipette.Core.Model;

namespace Pipette.Core.Tests.EndToEnd;

[CollectionDefinition(Name)]
public sealed class EndToEndCollection : ICollectionFixture<FixtureGeneration>
{
    public const string Name = "EndToEnd";
}

/// <summary>
/// Restores the projects under <c>tests/Fixtures</c> and runs Pipette on them once per test run, since loading
/// projects with MSBuildWorkspace is slow. Runs both the project-list input (strict) and the solution input.
/// </summary>
public sealed class FixtureGeneration : IAsyncLifetime
{
    private static readonly TimeSpan RestoreTimeout = TimeSpan.FromMinutes(5);

    public static string FixturesDirectory { get; } = Path.Combine(FindRepositoryRoot(), "tests", "Fixtures");

    public static string SolutionPath => Path.Combine(FixturesDirectory, "Fixtures.slnx");

    public static string SampleTestsProject => Path.Combine(FixturesDirectory, "SampleTests", "SampleTests.csproj");

    public static string NonTestLibraryProject => Path.Combine(FixturesDirectory, "NonTestLibrary", "NonTestLibrary.csproj");

    private string? _outputDirectory;

    /// <summary>Result of <c>RunAsync</c> with both fixture .csproj files as inputs and <c>Strict = true</c>.</summary>
    public PipetteRunResult ProjectsRun { get; private set; } = null!;

    public PipetteDiagnostics ProjectsDiagnostics { get; } = new();

    /// <summary>Content of the .vial file written by <see cref="ProjectsRun"/>.</summary>
    public string ProjectsOutput { get; private set; } = null!;

    /// <summary>Result of <c>GenerateAsync</c> with <c>Fixtures.slnx</c> as the only input.</summary>
    public VialDocument SolutionDocument { get; private set; } = null!;

    public PipetteDiagnostics SolutionDiagnostics { get; } = new();

    public async ValueTask InitializeAsync()
    {
        await RestoreAsync(SolutionPath);

        _outputDirectory = Directory.CreateTempSubdirectory("pipette-e2e-").FullName;
        var options = new PipetteRunOptions(
            [Path.Combine("SampleTests", "SampleTests.csproj"), NonTestLibraryProject],
            Path.Combine(_outputDirectory, "vials.vial"),
            Strict: true)
        {
            WorkingDirectory = FixturesDirectory,
        };
        ProjectsRun = await new PipetteRunner(ProjectsDiagnostics).RunAsync(options);
        ProjectsOutput = ProjectsRun.OutputPath is { } path ? await File.ReadAllTextAsync(path) : "";

        SolutionDocument = await new PipetteRunner(SolutionDiagnostics).GenerateAsync(["Fixtures.slnx"], FixturesDirectory);
    }

    public ValueTask DisposeAsync()
    {
        if (_outputDirectory is not null && Directory.Exists(_outputDirectory))
        {
            Directory.Delete(_outputDirectory, recursive: true);
        }

        return ValueTask.CompletedTask;
    }

    private static async Task RestoreAsync(string path)
    {
        var result = await DotnetProcess.RunAsync(["restore", path, "--nologo"], FixturesDirectory, RestoreTimeout);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"'dotnet restore {path}' failed with exit code {result.ExitCode}:{Environment.NewLine}{result.Output}{result.Error}");
        }
    }

    internal static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Pipette.NET.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException($"Could not find Pipette.NET.slnx above '{AppContext.BaseDirectory}'.");
    }
}
