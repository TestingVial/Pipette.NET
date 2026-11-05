namespace Pipette.Core.Model;

/// <summary>A single vial entry.</summary>
/// <param name="Type">
/// Lowercased test type. Known values are <c>unit</c>, <c>integration</c> and <c>e2e</c>;
/// custom test types (e.g. <c>performance</c>) are kept as-is. <see langword="null"/> for generic vials
/// (<c>[TestingVial&lt;T&gt;]</c>), which are written without <c>type</c>.
/// </param>
public sealed record Vial(
    string Name,
    string Description,
    string? Type,
    string Technology,
    SourceLocation Location)
{
    public const string CSharpTechnology = "csharp";
}
