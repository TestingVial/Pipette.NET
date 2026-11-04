using Microsoft.CodeAnalysis;
using Pipette.Core.Diagnostics;
using Pipette.Core.Model;

namespace Pipette.Core.Analysis;

/// <summary>Finds TestingVial.NET attributes in a Roslyn compilation and maps them to vials.</summary>
public sealed class VialCollector(PipetteDiagnostics diagnostics)
{
    private const string DescriptionProperty = "Description";

    private static readonly SymbolDisplayFormat ClassNameFormat = new(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters);

    /// <param name="baseDirectory">File paths in warnings are made relative to it, with forward slashes.</param>
    public Task<IReadOnlyList<Vial>> CollectAsync(
        Compilation compilation, string baseDirectory, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(compilation);
        ArgumentNullException.ThrowIfNull(baseDirectory);

        var models = new SemanticModelProvider(compilation);
        var context = new CollectionContext(models, baseDirectory, new AttributeConstructorEvaluator(models));
        var vials = new List<Vial>();
        foreach (var type in SourceTypes(compilation.Assembly.GlobalNamespace))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var className = type.ToDisplayString(ClassNameFormat);

            Collect(context, type, className, SourceLocation.ClassLevel, type.Name, vials);
            foreach (var method in type.GetMembers().OfType<IMethodSymbol>())
            {
                if (method.MethodKind == MethodKind.Ordinary)
                {
                    Collect(context, method, className, method.Name, method.Name, vials);
                }
            }
        }

        return Task.FromResult<IReadOnlyList<Vial>>(vials);
    }

    /// <param name="fallbackDescription">Used when an attribute has no Description: the method name, or the simple class name.</param>
    private void Collect(
        CollectionContext context, ISymbol symbol, string className, string methodName, string fallbackDescription, List<Vial> vials)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            if (attribute.AttributeClass is not { TypeKind: not TypeKind.Error } attributeClass
                || !TestingVialSymbols.IsVialAttribute(attributeClass)
                || attribute.ApplicationSyntaxReference is not { } application)
            {
                continue;
            }

            var filePath = RelativePath(context.BaseDirectory, application.SyntaxTree.FilePath);
            var where = $"{filePath}({application.SyntaxTree.GetLineSpan(application.Span).StartLinePosition.Line + 1}): " +
                        $"[{attributeClass.ToDisplayString()}] on {className}.{methodName}";

            var location = new SourceLocation(className, methodName);
            if (TryCreate(context, attribute, attributeClass, location, fallbackDescription, where) is { } vial)
            {
                vials.Add(vial);
            }
        }
    }

    private Vial? TryCreate(
        CollectionContext context,
        AttributeData attribute,
        INamedTypeSymbol attributeClass,
        SourceLocation location,
        string fallbackDescription,
        string where)
    {
        if (attribute.AttributeConstructor is not { } constructor)
        {
            diagnostics.Warn($"{where}: the attribute constructor could not be resolved; entry skipped.");
            return null;
        }

        var values = context.Evaluator.Evaluate(constructor, attribute.ConstructorArguments.Select(AsString).ToList());
        if (values.Failure is not null)
        {
            diagnostics.Warn($"{where}: {values.Failure}; entry skipped.");
            return null;
        }

        if (string.IsNullOrWhiteSpace(values.TestType))
        {
            diagnostics.Warn($"{where}: the test type is not a constant string that can be read from source; entry skipped.");
            return null;
        }

        var name = values.NameFromGenericArgument ? ResolveGenericName(context, attributeClass, where) : values.Name;
        if (string.IsNullOrWhiteSpace(name))
        {
            diagnostics.Warn($"{where}: the vial name is missing or not a constant string; entry skipped.");
            return null;
        }

        var description = attribute.NamedArguments
            .Where(a => a.Key == DescriptionProperty)
            .Select(a => AsString(a.Value))
            .LastOrDefault();
        if (string.IsNullOrWhiteSpace(description))
        {
            // Not a warning: TestingVial.NET's analyzer (TV0001) already warns about it where the attribute is written.
            diagnostics.Verbose($"{where}: '{name}' has no Description; using '{fallbackDescription}'.");
            description = fallbackDescription;
        }

        return new Vial(name, description, TestTypeMapper.Map(values.TestType), Vial.CSharpTechnology, location);
    }

    private string? ResolveGenericName(CollectionContext context, INamedTypeSymbol attributeClass, string where)
    {
        if (TestingVialSymbols.FindGenericBase(attributeClass)?.TypeArguments.FirstOrDefault() is not { } vialType)
        {
            return null;
        }

        var name = VialNameResolver.TryResolve(context.Models, vialType);
        if (name is not null)
        {
            return name;
        }

        diagnostics.Warn($"{where}: '{vialType.ToDisplayString()}.Name' is not a constant string; using the type name '{vialType.Name}'.");
        return vialType.Name;
    }

    private static string? AsString(TypedConstant constant) =>
        constant.Kind is TypedConstantKind.Error or TypedConstantKind.Array ? null : constant.Value as string;

    private static IEnumerable<INamedTypeSymbol> SourceTypes(INamespaceSymbol ns)
    {
        foreach (var member in ns.GetMembers())
        {
            var nested = member is INamespaceSymbol childNamespace
                ? SourceTypes(childNamespace)
                : NestedTypes((INamedTypeSymbol)member);
            foreach (var type in nested)
            {
                yield return type;
            }
        }
    }

    private static IEnumerable<INamedTypeSymbol> NestedTypes(INamedTypeSymbol type)
    {
        yield return type;
        foreach (var nested in type.GetTypeMembers().SelectMany(NestedTypes))
        {
            yield return nested;
        }
    }

    private static string RelativePath(string baseDirectory, string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            return filePath;
        }

        var relative = Path.IsPathRooted(filePath) && baseDirectory.Length > 0
            ? Path.GetRelativePath(baseDirectory, filePath)
            : filePath;
        return relative.Replace('\\', '/');
    }

    private sealed record CollectionContext(SemanticModelProvider Models, string BaseDirectory, AttributeConstructorEvaluator Evaluator);
}
