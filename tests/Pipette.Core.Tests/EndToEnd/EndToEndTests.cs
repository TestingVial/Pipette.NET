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
        Expected("Cart", "A customer can fill a cart and see its total", "e2e", "SampleTests.CartTests", "FillCart_ShowsTotal"),
        Expected("Checkout", "Checkout talks to the payment gateway", "integration", "SampleTests.Checkout.CheckoutTests", "*"),
        Expected("Checkout", "Checkout completes in under a second", "performance", "SampleTests.Checkout.CheckoutTests", "Checkout_IsFast"),
        Expected("Search", "Search answers within 100 ms", "performance", "SampleTests.Checkout.CheckoutTests", "Checkout_IsFast"),
        Expected("Checkout", "A customer can pay for a cart", "e2e", "SampleTests.Checkout.CheckoutTests", "Pay_CompletesOrder"),
        Expected("Catalog", "SearchTests", "integration", "SampleTests.SearchTests", "*"),
        Expected("Search", "Search matches products by name", "unit", "SampleTests.SearchTests", "Search_MatchesByName"),
        Expected("Search", "Search_WithoutDescription", "unit", "SampleTests.SearchTests", "Search_WithoutDescription"),
        Expected("Cart", "Wishlists can be turned into carts", null, "SampleTests.WishlistTests", "*"),
        Expected("Wishlist", "Wishlists keep products for later", null, "SampleTests.WishlistTests", "Add_KeepsProduct"),
    ];

    private static Vial Expected(string name, string description, string? type, string className, string methodName) =>
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
        Assert.StartsWith("{\n  \"schemaVersion\": \"0.0.2\",", fixture.ProjectsOutput);
    }

    [Fact]
    public void StrictMode_WithWarnings_ReturnsExitCode2_AndStillWritesTheFile()
    {
        Assert.Equal(PipetteExitCodes.StrictWarnings, fixture.ProjectsRun.ExitCode);
        Assert.Null(fixture.ProjectsRun.Error);
        Assert.True(File.Exists(fixture.ProjectsRun.OutputPath));
    }

    [Fact]
    public void Warnings_AreOnlyTheNonTestProject()
    {
        foreach (var diagnostics in new[] { fixture.ProjectsDiagnostics, fixture.SolutionDiagnostics })
        {
            var warning = Assert.Single(Warnings(diagnostics));
            Assert.Contains("'NonTestLibrary.csproj' is not a test project", warning, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void MissingDescriptions_AreVerboseNotes()
    {
        foreach (var diagnostics in new[] { fixture.ProjectsDiagnostics, fixture.SolutionDiagnostics })
        {
            var notes = diagnostics.Messages
                .Where(m => m.Severity == PipetteSeverity.Verbose && m.Text.Contains("has no Description", StringComparison.Ordinal))
                .Select(m => m.Text)
                .Order(StringComparer.Ordinal)
                .ToList();
            Assert.Equal(2, notes.Count);
            Assert.StartsWith($"{SearchTests}(16): ", notes[0], StringComparison.Ordinal);
            Assert.EndsWith("on SampleTests.SearchTests.Search_WithoutDescription: 'Search' has no Description; using 'Search_WithoutDescription'.", notes[0], StringComparison.Ordinal);
            Assert.StartsWith($"{SearchTests}(6): ", notes[1], StringComparison.Ordinal);
            Assert.EndsWith("on SampleTests.SearchTests.*: 'Catalog' has no Description; using 'SearchTests'.", notes[1], StringComparison.Ordinal);
        }
    }

    [Fact]
    public void NonTestProject_ContributesNoVials()
    {
        Assert.NotNull(fixture.ProjectsRun.Document);
        Assert.All(fixture.ProjectsRun.Document.Vials, v => Assert.StartsWith("SampleTests.", v.Location.ClassName, StringComparison.Ordinal));
    }

    [Fact]
    public void Output_IsValidAgainstTheSchema_IncludingCustomTypes()
    {
        var results = VialSchema.Evaluate(fixture.ProjectsOutput);

        Assert.True(results.IsValid, string.Join(Environment.NewLine, VialSchema.Errors(results)));
        Assert.Contains("\"type\": \"performance\"", fixture.ProjectsOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void GenericVials_AreWrittenWithoutType()
    {
        var output = JsonNode.Parse(fixture.ProjectsOutput)!;

        var generic = output["vials"]!.AsArray().Where(v => (string?)v!["location"]!["className"] == "SampleTests.WishlistTests").ToList();
        Assert.Equal(2, generic.Count);
        Assert.All(generic, v => Assert.False(v!.AsObject().ContainsKey("type")));
    }

    [Fact]
    public void Output_HasNoFilePath()
    {
        var output = JsonNode.Parse(fixture.ProjectsOutput)!;

        Assert.All(output["vials"]!.AsArray(), v => Assert.Null(v!["location"]!["filePath"]));
    }

    private static List<string> Warnings(PipetteDiagnostics diagnostics) =>
        diagnostics.Messages.Where(m => m.Severity == PipetteSeverity.Warning).Select(m => m.Text).ToList();
}
