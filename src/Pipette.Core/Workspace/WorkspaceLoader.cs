using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Pipette.Core.Diagnostics;

namespace Pipette.Core.Workspace;

/// <summary>A project opened from one of the resolved input paths.</summary>
public sealed record LoadedProject(string ProjectPath, Project Project, ProjectFacts Facts);

/// <summary>The opened workspace; dispose it once analysis is done.</summary>
public sealed class LoadedWorkspace : IDisposable
{
    private readonly MSBuildWorkspace _workspace;

    internal LoadedWorkspace(MSBuildWorkspace workspace, IReadOnlyList<LoadedProject> projects)
    {
        _workspace = workspace;
        Projects = projects;
    }

    public IReadOnlyList<LoadedProject> Projects { get; }

    public void Dispose() => _workspace.Dispose();
}

/// <summary>Registers MSBuild via MSBuildLocator and opens projects with MSBuildWorkspace.</summary>
public sealed class WorkspaceLoader
{
    private readonly PipetteDiagnostics _diagnostics;

    public WorkspaceLoader(PipetteDiagnostics diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        _diagnostics = diagnostics;
    }

    /// <summary>
    /// Opens every project in <paramref name="projectPaths"/> (referenced projects are loaded but not returned).
    /// Workspace diagnostics are reported as warnings. Multi-targeted projects yield their first target framework only.
    /// </summary>
    /// <exception cref="ProjectLoadException">MSBuild could not be located or a project could not be opened.</exception>
    public async Task<LoadedWorkspace> LoadAsync(IReadOnlyList<string> projectPaths, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(projectPaths);

        MSBuildRegistration.EnsureRegistered();
        return await OpenAsync(projectPaths, cancellationToken).ConfigureAwait(false);
    }

    private async Task<LoadedWorkspace> OpenAsync(IReadOnlyList<string> projectPaths, CancellationToken cancellationToken)
    {
        var workspace = MSBuildWorkspace.Create();
        try
        {
            workspace.RegisterWorkspaceFailedHandler(e =>
                _diagnostics.Warn($"Workspace {e.Diagnostic.Kind.ToString().ToLowerInvariant()}: {e.Diagnostic.Message}"));

            using var evaluator = new MSBuildProjectEvaluator(_diagnostics);
            var loaded = new List<LoadedProject>();
            foreach (var path in projectPaths)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var project = FindOpenProject(workspace.CurrentSolution, path)
                    ?? await OpenProjectAsync(workspace, path, cancellationToken).ConfigureAwait(false);

                var evaluation = evaluator.Evaluate(path);
                var facts = new ProjectFacts(
                    evaluation?.IsTestProject,
                    evaluation?.PackageReferences ?? [],
                    project.MetadataReferences.OfType<PortableExecutableReference>()
                        .Select(r => r.FilePath)
                        .OfType<string>()
                        .ToList());

                loaded.Add(new LoadedProject(path, project, facts));
            }

            return new LoadedWorkspace(workspace, loaded);
        }
        catch
        {
            workspace.Dispose();
            throw;
        }
    }

    private static async Task<Project> OpenProjectAsync(MSBuildWorkspace workspace, string path, CancellationToken cancellationToken)
    {
        try
        {
            return await workspace.OpenProjectAsync(path, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new ProjectLoadException($"Could not open project '{path}': {ex.Message}", ex);
        }
    }

    private static Project? FindOpenProject(Solution solution, string path) =>
        solution.Projects.FirstOrDefault(p => string.Equals(p.FilePath, path, StringComparison.OrdinalIgnoreCase));
}
