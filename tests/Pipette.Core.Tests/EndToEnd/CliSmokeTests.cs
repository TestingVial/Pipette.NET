using System.Reflection;

namespace Pipette.Core.Tests.EndToEnd;

/// <summary>Runs the built <c>pipette</c> CLI (built through the test project's reference to Pipette.Cli).</summary>
[Collection(EndToEndCollection.Name)]
public class CliSmokeTests(FixtureGeneration fixture)
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(5);

    private static readonly string Configuration =
        typeof(CliSmokeTests).Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration ?? "Debug";

    [Fact]
    public async Task Cli_WritesTheSameFileAsTheRunner_AndReportsWarnings()
    {
        var outputDirectory = Directory.CreateTempSubdirectory("pipette-cli-").FullName;
        try
        {
            var output = Path.Combine(outputDirectory, "out.vial");
            var cliProject = Path.Combine(FixtureGeneration.FindRepositoryRoot(), "src", "Pipette.Cli", "Pipette.Cli.csproj");

            var result = await DotnetProcess.RunAsync(
                ["run", "--project", cliProject, "--no-build", "-c", Configuration, "--",
                    FixtureGeneration.SampleTestsProject, FixtureGeneration.NonTestLibraryProject, "-o", output],
                FixtureGeneration.FixturesDirectory,
                Timeout,
                TestContext.Current.CancellationToken);

            Assert.True(result.ExitCode == PipetteExitCodes.Success, $"exit {result.ExitCode}{Environment.NewLine}{result.Output}{result.Error}");
            Assert.Contains("Wrote 7 vial(s)", result.Output, StringComparison.Ordinal);
            Assert.Contains("warning: 'NonTestLibrary.csproj' is not a test project", result.Error, StringComparison.Ordinal);
            Assert.Equal(fixture.ProjectsOutput, await File.ReadAllTextAsync(output, TestContext.Current.CancellationToken));
        }
        finally
        {
            Directory.Delete(outputDirectory, recursive: true);
        }
    }
}
