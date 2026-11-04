namespace Pipette.Core.Inputs;

/// <summary>The command-line inputs cannot be turned into a list of projects.</summary>
public sealed class InputResolutionException : Exception
{
    public InputResolutionException(string message)
        : base(message)
    {
    }

    public InputResolutionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
