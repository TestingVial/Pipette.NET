namespace Pipette.Core.Model;

/// <summary>Root of a .vial file (VialSchemas v0.0.2).</summary>
public sealed record VialDocument(string SchemaVersion, IReadOnlyList<Vial> Vials)
{
    public const string CurrentSchemaVersion = "0.0.2";

    public VialDocument(IReadOnlyList<Vial> vials)
        : this(CurrentSchemaVersion, vials)
    {
    }
}
