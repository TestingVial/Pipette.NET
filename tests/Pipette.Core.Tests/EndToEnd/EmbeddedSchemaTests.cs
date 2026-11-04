using System.Text.Json.Nodes;
using Pipette.Core.Model;

namespace Pipette.Core.Tests.EndToEnd;

/// <summary>The embedded schema is the version Pipette writes, and an unmodified copy of the VialSchemas file.</summary>
public class EmbeddedSchemaTests
{
    /// <summary>Overrides where the VialSchemas checkout is; by default it's next to this repository (the workspace layout).</summary>
    private const string VialSchemasDirectoryVariable = "VIALSCHEMAS_DIR";

    [Fact]
    public void EmbeddedSchema_IsTheVersionPipetteWrites()
    {
        var schema = JsonNode.Parse(VialSchema.Text())!;

        Assert.Equal(VialDocument.CurrentSchemaVersion, (string?)schema["properties"]?["schemaVersion"]?["const"]);
        Assert.Equal(
            $"https://raw.githubusercontent.com/TestingVial/VialSchemas/main/schemas/{VialSchema.FileName}",
            (string?)schema["$id"]);
    }

    [Fact]
    public void EmbeddedSchema_IsIdenticalToTheVialSchemasFile()
    {
        var directory = Environment.GetEnvironmentVariable(VialSchemasDirectoryVariable)
            ?? Path.Combine(FixtureGeneration.FindRepositoryRoot(), "..", "VialSchemas");
        var published = Path.Combine(directory, "schemas", VialSchema.FileName);
        Assert.SkipUnless(
            File.Exists(published),
            $"'{published}' not found: clone VialSchemas next to this repository or set {VialSchemasDirectoryVariable}.");

        // Line endings depend on the checkout settings, not on the schema.
        Assert.Equal(Normalize(File.ReadAllText(published)), Normalize(VialSchema.Text()));
    }

    private static string Normalize(string text) => text.ReplaceLineEndings("\n");
}
