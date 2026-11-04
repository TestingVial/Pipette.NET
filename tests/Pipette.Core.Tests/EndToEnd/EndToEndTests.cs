using System.Text.Json.Nodes;
using Pipette.Core.Diagnostics;
using Pipette.Core.Model;
using Pipette.Core.Output;

namespace Pipette.Core.Tests.EndToEnd;

[Collection(EndToEndCollection.Name)]
public class EndToEndTests(FixtureGeneration fixture)
{
    private const string SearchTests = "SampleTests/SearchTests.cs";

    /// <summary>All entries the fixture must produce, in the writer's sort order.</summary>
    private static readonly Vial[] ExpectedVials =
    [
        Expected("Cart", "Carts can have products added", "unit", "SampleTests.CartTests", "AddToCart_AddsProduct"),
        Expected("Product", "Products can be added to a cart", "unit", "SampleTests.CartTests", "AddToCart_AddsProduct"),
        Expected("Checkout", "Checkout talks to the payment gateway", "integration", "SampleTests.Checkout.CheckoutTests", "*"),
        Expected("Checkout", "Checkout completes in under a second", "performance", "SampleTests.Checkout.CheckoutTests", "Checkout_IsFast"),
        Expected("Search", "Search answers within 100 ms", "performance", "SampleTests.Checkout.CheckoutTests", "Checkout_IsFast"),
        Expected("Checkout", "A customer can pay for a cart", "e2e", "SampleTests.Checkout.CheckoutTests", "Pay_CompletesOrder"),
        Expected("Search", "Search matches products by name", "unit", "SampleTests.SearchTests", "Search_MatchesByName"),
    ];

    private static readonly string[] SchemaTypes = ["unit", "integration", "e2e"];

    private static Vial Expected(string name, string description, string type, string className, string methodName) =>
        new(name, description, type, Vial.CSharpTechnology, new SourceLocation(className, methodName));

    [Fact]
    public void ProjectInputs_ProduceExactlyTheExpectedVials()
    {
        Assert.NotNull(fixture.ProjectsRun.Document);
        Assert.Equal(ExpectedVials, VialWriter.Normalize(fixture.ProjectsRun.Document).Vials);
    }

    [Fact]
    public void SolutionInput_ProducesTheSameVials()
    {
        Assert.Equal(ExpectedVials, VialWriter.Normalize(fixture.SolutionDocument).Vials);
    }

    [Fact]
    public void WrittenFile_IsTheSerializedDocument()
    {
        Assert.NotNull(fixture.ProjectsRun.Document);
        Assert.Equal(VialWriter.Serialize(fixture.ProjectsRun.Document), fixture.ProjectsOutput);
        Assert.StartsWith("{\n  \"schemaVersion\": \"0.0.1\",", fixture.ProjectsOutput);
    }

    [Fact]
    public void StrictMode_WithWarnings_ReturnsExitCode2_AndStillWritesTheFile()
    {
        Assert.Equal(PipetteExitCodes.StrictWarnings, fixture.ProjectsRun.ExitCode);
        Assert.Null(fixture.ProjectsRun.Error);
        Assert.True(File.Exists(fixture.ProjectsRun.OutputPath));
    }

    [Fact]
    public void Warnings_AreTheNonTestProjectAndTheMissingDescription()
    {
        foreach (var diagnostics in new[] { fixture.ProjectsDiagnostics, fixture.SolutionDiagnostics })
        {
            var warnings = Warnings(diagnostics);
            Assert.Equal(2, warnings.Count);
            Assert.Contains(warnings, w => w.Contains("'NonTestLibrary.csproj' is not a test project", StringComparison.Ordinal));
            Assert.Contains(warnings, w =>
                w.Contains($"{SearchTests}(15)", StringComparison.Ordinal)
                && w.Contains("SampleTests.SearchTests.Search_WithoutDescription", StringComparison.Ordinal)
                && w.Contains("'Search' has no Description; entry skipped", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void NonTestProject_ContributesNoVials()
    {
        Assert.NotNull(fixture.ProjectsRun.Document);
        Assert.All(fixture.ProjectsRun.Document.Vials, v => Assert.StartsWith("SampleTests.", v.Location.ClassName, StringComparison.Ordinal));
    }

    [Fact]
    public void Output_OmitsFilePath_WhichIsTheOnlyLocationGapAgainstSchema()
    {
        // Known gap: v0.0.1 requires location.filePath, which Pipette no longer emits.
        var output = JsonNode.Parse(fixture.ProjectsOutput)!;
        Assert.All(output["vials"]!.AsArray(), v => Assert.Null(v!["location"]!["filePath"]));

        Assert.False(VialSchema.Evaluate(fixture.ProjectsOutput).IsValid);

        var patched = WithPlaceholderFilePath(fixture.ProjectsOutput);
        foreach (var vial in patched["vials"]!.AsArray().Where(v => (string?)v!["type"] == "performance"))
        {
            vial!["type"] = "unit";
        }

        var results = VialSchema.Evaluate(patched.ToJsonString());
        Assert.True(results.IsValid, string.Join(Environment.NewLine, VialSchema.Errors(results)));
    }

    [Fact]
    public void Output_WithSchemaTypesOnly_IsValidAgainstSchema()
    {
        Assert.NotNull(fixture.ProjectsRun.Document);
        var document = fixture.ProjectsRun.Document with
        {
            Vials = fixture.ProjectsRun.Document.Vials.Where(v => SchemaTypes.Contains(v.Type)).ToList(),
        };
        Assert.Equal(["e2e", "integration", "unit"], document.Vials.Select(v => v.Type).Distinct().Order());

        var results = VialSchema.Evaluate(WithPlaceholderFilePath(VialWriter.Serialize(document)).ToJsonString());

        Assert.True(results.IsValid, string.Join(Environment.NewLine, VialSchema.Errors(results)));
    }

    [Fact]
    public void Output_WithCustomPerformanceType_FailsSchemaOnlyOnTypeEnum()
    {
        // Known gap: v0.0.1 only allows unit/integration/e2e, while Pipette keeps custom test types lowercased.
        var patched = WithPlaceholderFilePath(fixture.ProjectsOutput);
        var results = VialSchema.Evaluate(patched.ToJsonString());

        Assert.False(results.IsValid);
        Assert.Equal(
            [("/vials/3/type", "enum"), ("/vials/4/type", "enum")],
            VialSchema.Errors(results).Where(e => e.Keyword == "enum").Order());

        foreach (var vial in patched["vials"]!.AsArray().Where(v => (string?)v!["type"] == "performance"))
        {
            vial!["type"] = "unit";
        }

        var patchedResults = VialSchema.Evaluate(patched.ToJsonString());
        Assert.True(patchedResults.IsValid, string.Join(Environment.NewLine, VialSchema.Errors(patchedResults)));
    }

    private static JsonNode WithPlaceholderFilePath(string json)
    {
        var node = JsonNode.Parse(json)!;
        foreach (var vial in node["vials"]!.AsArray())
        {
            vial!["location"]!["filePath"] = "placeholder.cs";
        }

        return node;
    }

    private static List<string> Warnings(PipetteDiagnostics diagnostics) =>
        diagnostics.Messages.Where(m => m.Severity == PipetteSeverity.Warning).Select(m => m.Text).ToList();
}
