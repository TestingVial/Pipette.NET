using System.Text.Json;
using Json.Schema;

namespace Pipette.Core.Tests.EndToEnd;

/// <summary>The VialSchemas v0.0.1 JSON schema, embedded in the test assembly.</summary>
internal static class VialSchema
{
    private const string ResourceName = "Pipette.Core.Tests.Schemas.v0.0.1.json";

    // Building a schema registers its $id globally, so it must only be built once per process.
    private static readonly Lazy<JsonSchema> Schema = new(() =>
    {
        using var stream = typeof(VialSchema).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{ResourceName}' not found.");
        using var reader = new StreamReader(stream);
        return JsonSchema.FromText(reader.ReadToEnd());
    });

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
