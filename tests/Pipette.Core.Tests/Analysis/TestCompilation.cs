using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Pipette.Core.Analysis;
using Pipette.Core.Diagnostics;
using Pipette.Core.Model;
using TestingVial.NET;

namespace Pipette.Core.Tests.Analysis;

internal static class TestCompilation
{
    public static readonly string BaseDirectory = Path.Combine(Path.GetTempPath(), "pipette-repo");

    private static readonly Lazy<IReadOnlyList<MetadataReference>> LazyFrameworkReferences = new(() =>
    {
        var runtimeDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        return ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(path => string.Equals(Path.GetDirectoryName(path), runtimeDirectory, StringComparison.OrdinalIgnoreCase))
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToList();
    });

    /// <summary>The shared framework assemblies, without TestingVial.NET.</summary>
    public static IReadOnlyList<MetadataReference> FrameworkReferences => LazyFrameworkReferences.Value;

    public static MetadataReference TestingVialReference { get; } =
        MetadataReference.CreateFromFile(typeof(IVial).Assembly.Location);

    /// <param name="files">Paths relative to <see cref="BaseDirectory"/> and their source text.</param>
    public static CSharpCompilation Create(
        IEnumerable<(string Path, string Source)> files, IEnumerable<MetadataReference>? extraReferences = null, bool allowErrors = false)
    {
        var trees = files.Select(f => CSharpSyntaxTree.ParseText(
            f.Source,
            new CSharpParseOptions(LanguageVersion.Latest),
            Path.Combine(BaseDirectory, f.Path.Replace('/', Path.DirectorySeparatorChar))));

        var compilation = CSharpCompilation.Create(
            "TestAssembly" + Guid.NewGuid().ToString("N"),
            trees,
            FrameworkReferences.Append(TestingVialReference).Concat(extraReferences ?? []),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        if (!allowErrors)
        {
            var errors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
            Assert.True(errors.Count == 0, string.Join(Environment.NewLine, errors));
        }

        return compilation;
    }

    /// <summary>Emits the compilation and references it as a plain metadata image, with no source behind it.</summary>
    public static MetadataReference EmitToImageReference(this Compilation compilation)
    {
        using var stream = new MemoryStream();
        var result = compilation.Emit(stream, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
        return MetadataReference.CreateFromImage(stream.ToArray());
    }

    public static CSharpCompilation Create(string source) => Create([("Tests/SampleTests.cs", source)]);

    public static async Task<(IReadOnlyList<Vial> Vials, PipetteDiagnostics Diagnostics)> CollectAsync(Compilation compilation)
    {
        var diagnostics = new PipetteDiagnostics();
        var vials = await new VialCollector(diagnostics).CollectAsync(compilation, BaseDirectory, TestContext.Current.CancellationToken);
        return (vials, diagnostics);
    }

    public static Task<(IReadOnlyList<Vial> Vials, PipetteDiagnostics Diagnostics)> CollectAsync(string source) =>
        CollectAsync(Create(source));

    public static IReadOnlyList<string> Warnings(this PipetteDiagnostics diagnostics) =>
        diagnostics.Messages.Where(m => m.Severity == PipetteSeverity.Warning).Select(m => m.Text).ToList();
}
