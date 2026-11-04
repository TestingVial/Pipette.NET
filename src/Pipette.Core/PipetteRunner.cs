using Pipette.Core.Analysis;
using Pipette.Core.Diagnostics;
using Pipette.Core.Inputs;
using Pipette.Core.Model;
using Pipette.Core.Output;
using Pipette.Core.Workspace;

namespace Pipette.Core;

public static class PipetteExitCodes
{
    public const int Success = 0;
    public const int InvalidInputOrLoadFailure = 1;
    public const int StrictWarnings = 2;
}

/// <param name="Inputs">One .sln/.slnx, or one or more .csproj paths.</param>
/// <param name="OutputPath">Destination .vial file; relative paths are resolved against <see cref="WorkingDirectory"/>.</param>
/// <param name="Strict">Exit with <see cref="PipetteExitCodes.StrictWarnings"/> when any warning was reported.</param>
public sealed record PipetteRunOptions(IReadOnlyList<string> Inputs, string OutputPath, bool Strict = false)
{
    public const string DefaultOutputPath = "vials.vial";

    /// <summary>Directory used to resolve relative paths and as the base directory for project inputs. Defaults to the process working directory.</summary>
    public string? WorkingDirectory { get; init; }
}

/// <param name="ExitCode">One of <see cref="PipetteExitCodes"/>.</param>
/// <param name="Document">The written document, or null when the run failed before writing.</param>
/// <param name="OutputPath">Absolute path of the written file, or null when nothing was written.</param>
/// <param name="Error">Why the run failed with <see cref="PipetteExitCodes.InvalidInputOrLoadFailure"/>.</param>
public sealed record PipetteRunResult(int ExitCode, VialDocument? Document, string? OutputPath, string? Error);

/// <summary>Runs the whole pipeline: resolve inputs, load projects, keep test projects, collect vials, write the .vial file.</summary>
public sealed class PipetteRunner
{
    private const string VialAttributeMetadataName = "TestingVial.NET.VialAttribute";

    private readonly PipetteDiagnostics _diagnostics;

    public PipetteRunner(PipetteDiagnostics diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        _diagnostics = diagnostics;
    }

    /// <summary>Generates the document and writes it. Input and load errors are returned as exit code 1, never thrown.</summary>
    public async Task<PipetteRunResult> RunAsync(PipetteRunOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var workingDirectory = options.WorkingDirectory ?? Directory.GetCurrentDirectory();
        VialDocument document;
        try
        {
            document = await GenerateAsync(options.Inputs, workingDirectory, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is InputResolutionException or ProjectLoadException)
        {
            return new PipetteRunResult(PipetteExitCodes.InvalidInputOrLoadFailure, null, null, ex.Message);
        }

        var outputPath = Path.GetFullPath(options.OutputPath, workingDirectory);
        try
        {
            await VialWriter.WriteAsync(document, outputPath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new PipetteRunResult(
                PipetteExitCodes.InvalidInputOrLoadFailure, document, null, $"Could not write '{outputPath}': {ex.Message}");
        }

        var exitCode = options.Strict && _diagnostics.HasWarnings ? PipetteExitCodes.StrictWarnings : PipetteExitCodes.Success;
        return new PipetteRunResult(exitCode, document, outputPath, null);
    }

    /// <summary>Resolves <paramref name="inputs"/>, loads the projects and collects vials from the test projects among them.</summary>
    /// <exception cref="InputResolutionException">The inputs are invalid.</exception>
    /// <exception cref="ProjectLoadException">MSBuild could not be located or a project could not be opened.</exception>
    public async Task<VialDocument> GenerateAsync(IReadOnlyList<string> inputs, string workingDirectory, CancellationToken cancellationToken = default)
    {
        var resolved = new InputResolver(_diagnostics).Resolve(inputs, workingDirectory);
        if (resolved.ProjectPaths.Count == 0)
        {
            return new VialDocument([]);
        }

        using var workspace = await new WorkspaceLoader(_diagnostics).LoadAsync(resolved.ProjectPaths, cancellationToken).ConfigureAwait(false);

        var detector = new TestProjectDetector();
        var collector = new VialCollector(_diagnostics);
        var vials = new List<Vial>();
        foreach (var loaded in workspace.Projects)
        {
            var name = Path.GetFileName(loaded.ProjectPath);
            if (!detector.IsTestProject(loaded.Facts))
            {
                _diagnostics.Warn($"'{name}' is not a test project (no IsTestProject and no known test framework reference); skipping it.");
                continue;
            }

            var compilation = await loaded.Project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
            if (compilation is null)
            {
                _diagnostics.Warn($"Could not get a compilation for '{name}'; skipping it.");
                continue;
            }

            if (compilation.GetTypeByMetadataName(VialAttributeMetadataName) is null)
            {
                _diagnostics.Verbose($"'{name}' does not reference TestingVial.NET; no vials collected.");
                continue;
            }

            vials.AddRange(await collector.CollectAsync(compilation, resolved.BaseDirectory, cancellationToken).ConfigureAwait(false));
        }

        return new VialDocument(vials);
    }
}
