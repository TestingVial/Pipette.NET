using Microsoft.Build.Locator;

namespace Pipette.Core.Workspace;

/// <summary>
/// Registers the installed .NET SDK's MSBuild with <see cref="MSBuildLocator"/>. Must run before any
/// Microsoft.Build type is loaded, so code touching those types lives in other classes and methods.
/// </summary>
public static class MSBuildRegistration
{
    private static readonly Lock Gate = new();

    /// <exception cref="ProjectLoadException">No usable .NET SDK was found.</exception>
    public static void EnsureRegistered()
    {
        lock (Gate)
        {
            if (MSBuildLocator.IsRegistered)
            {
                return;
            }

            try
            {
                MSBuildLocator.RegisterDefaults();
            }
            catch (InvalidOperationException ex)
            {
                throw new ProjectLoadException(
                    $"Could not locate MSBuild. Pipette needs the .NET SDK installed to load projects: {ex.Message}", ex);
            }
        }
    }
}
