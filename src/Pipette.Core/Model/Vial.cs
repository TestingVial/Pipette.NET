namespace Pipette.Core.Model;

/// <summary>A single vial entry.</summary>
/// <param name="Type">
/// Lowercased test type. Known values are <c>unit</c>, <c>integration</c> and <c>e2e</c>;
/// custom test types (e.g. <c>performance</c>) are kept as-is.
/// </param>
public sealed record Vial(
    string Name,
    string Description,
    string Type,
    string Technology,
    SourceLocation Location)
{
    public const string CSharpTechnology = "csharp";
}
