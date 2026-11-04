using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Pipette.Core.Model;

namespace Pipette.Core.Output;

/// <summary>Serializes a <see cref="VialDocument"/> to deterministic .vial JSON.</summary>
public static class VialWriter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        IndentSize = 2,
        NewLine = "\n",
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>
    /// Returns a copy of <paramref name="document"/> with lowercased types and vials sorted by
    /// class name, method name and vial name (ordinal), with type and description as tie-breakers.
    /// </summary>
    public static VialDocument Normalize(VialDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var vials = document.Vials
            .Select(v => v with { Type = v.Type.ToLowerInvariant() })
            .OrderBy(v => v.Location.ClassName, StringComparer.Ordinal)
            .ThenBy(v => v.Location.MethodName, StringComparer.Ordinal)
            .ThenBy(v => v.Name, StringComparer.Ordinal)
            .ThenBy(v => v.Type, StringComparer.Ordinal)
            .ThenBy(v => v.Description, StringComparer.Ordinal)
            .ToList();

        return document with { Vials = vials };
    }

    public static string Serialize(VialDocument document) =>
        JsonSerializer.Serialize(Normalize(document), Options) + "\n";

    public static async Task WriteAsync(VialDocument document, Stream stream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var bytes = Utf8NoBom.GetBytes(Serialize(document));
        await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Writes the document to <paramref name="path"/>, creating the parent directory if needed.</summary>
    public static async Task WriteAsync(VialDocument document, string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(fullPath, Serialize(document), Utf8NoBom, cancellationToken).ConfigureAwait(false);
    }
}
