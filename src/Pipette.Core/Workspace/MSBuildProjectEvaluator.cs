using Microsoft.Build.Evaluation;
using Microsoft.Build.Exceptions;
using Pipette.Core.Diagnostics;

namespace Pipette.Core.Workspace;

/// <summary>Evaluates projects in-process to read MSBuild properties and items. Only use after <see cref="MSBuildRegistration.EnsureRegistered"/>.</summary>
internal sealed class MSBuildProjectEvaluator : IDisposable
{
    private readonly ProjectCollection _collection = new();
    private readonly PipetteDiagnostics _diagnostics;

    public MSBuildProjectEvaluator(PipetteDiagnostics diagnostics) => _diagnostics = diagnostics;

    /// <summary>Returns the evaluated <c>IsTestProject</c> value (null when unset) and the PackageReference names, or null when evaluation fails.</summary>
    public (bool? IsTestProject, IReadOnlyList<string> PackageReferences)? Evaluate(string projectPath)
    {
        Project? project = null;
        try
        {
            project = _collection.LoadProject(projectPath);
            var isTestProject = project.GetPropertyValue("IsTestProject");
            var packageReferences = project.GetItems("PackageReference")
                .Select(i => i.EvaluatedInclude)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return (bool.TryParse(isTestProject, out var value) ? value : null, packageReferences);
        }
        catch (InvalidProjectFileException ex)
        {
            _diagnostics.Warn($"Could not evaluate '{projectPath}' with MSBuild: {ex.Message}");
            return null;
        }
        finally
        {
            if (project is not null)
            {
                _collection.UnloadProject(project);
            }
        }
    }

    public void Dispose() => _collection.Dispose();
}
