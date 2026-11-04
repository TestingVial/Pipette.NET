namespace Pipette.Core.Diagnostics;

public enum PipetteSeverity
{
    Verbose,
    Warning,
}

public sealed record PipetteMessage(PipetteSeverity Severity, string Text);

/// <summary>Collects verbose notes and warnings produced while resolving, loading and analyzing projects.</summary>
public sealed class PipetteDiagnostics
{
    private readonly List<PipetteMessage> _messages = [];

    public IReadOnlyList<PipetteMessage> Messages => _messages;

    public bool HasWarnings => _messages.Any(m => m.Severity == PipetteSeverity.Warning);

    public void Warn(string text) => _messages.Add(new PipetteMessage(PipetteSeverity.Warning, text));

    public void Verbose(string text) => _messages.Add(new PipetteMessage(PipetteSeverity.Verbose, text));
}
