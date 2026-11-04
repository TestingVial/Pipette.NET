namespace Pipette.Core.Workspace;

/// <summary>MSBuild could not be located or a project could not be opened.</summary>
public sealed class ProjectLoadException : Exception
{
    public ProjectLoadException(string message)
        : base(message)
    {
    }

    public ProjectLoadException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
