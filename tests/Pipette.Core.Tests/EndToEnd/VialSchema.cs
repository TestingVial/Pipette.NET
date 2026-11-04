using System.Text.Json;
using Json.Schema;
using Pipette.Core.Model;

namespace Pipette.Core.Tests.EndToEnd;

/// <summary>
/// The VialSchemas schema that Pipette targets (<see cref="VialDocument.CurrentSchemaVersion"/>), embedded in the test
/// assembly from <c>Schemas/</c>.
/// </summary>
internal static class VialSchema
{
    public static string FileName { get; } = $"v{VialDocument.CurrentSchemaVersion}.json";

    private static readonly string ResourceName = $"Pipette.Core.Tests.Schemas.{FileName}";

    // Building a schema registers its $id globally, so it must only be built once per process.
    private static readonly Lazy<JsonSchema> Schema = new(() => JsonSchema.FromText(Text()));

    /// <summary>The embedded schema file, as text.</summary>
    public static string Text()
    {
        using var stream = typeof(VialSchema).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{ResourceName}' not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public static EvaluationResults Evaluate(string json)
    {
        using var document = JsonDocument.Parse(json);
        return Schema.Value.Evaluate(document.RootElement.Clone(), new EvaluationOptions { OutputFormat = OutputFormat.List });
    }

    /// <summary>Every (instance location, failing keyword) pair reported by the evaluation.</summary>
    public static IReadOnlyList<(string InstanceLocation, string Keyword)> Errors(EvaluationResults results) =>
        new[] { results }
            .Concat(results.Details ?? [])
            .Where(r => r.Errors is { Count: > 0 })
            .SelectMany(r => r.Errors!.Keys.Select(keyword => (r.InstanceLocation.ToString(), keyword)))
            .Distinct()
            .ToList();
}
