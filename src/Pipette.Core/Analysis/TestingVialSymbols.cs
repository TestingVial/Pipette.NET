using Microsoft.CodeAnalysis;

namespace Pipette.Core.Analysis;

/// <summary>
/// Recognises TestingVial.NET types by namespace and metadata name, so the match works for any assembly version
/// and for the package compiled from source.
/// </summary>
internal static class TestingVialSymbols
{
    public const string Namespace = "TestingVial.NET";
    public const string IVial = "IVial";
    public const string VialAttribute = "VialAttribute";
    public const string TestingVialAttribute = "TestingVialAttribute`1";
    public const string UnitTestVial = "UnitTestVial";
    public const string UnitTestVialGeneric = "UnitTestVial`1";
    public const string IntegrationTestVial = "IntegrationTestVial";
    public const string IntegrationTestVialGeneric = "IntegrationTestVial`1";

    public const string UnitTestType = "Unit";
    public const string IntegrationTestType = "Integration";

    public static bool Is(ITypeSymbol? type, string metadataName) =>
        type is not null
        && type.OriginalDefinition.MetadataName == metadataName
        && type.ContainingType is null
        && type.ContainingNamespace?.ToDisplayString() == Namespace;

    public static bool IsVialAttribute(INamedTypeSymbol? attributeClass)
    {
        for (var current = attributeClass; current is not null; current = current.BaseType)
        {
            if (Is(current, VialAttribute))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns the constructed <c>TestingVialAttribute&lt;T&gt;</c> base of an attribute class, if any.</summary>
    public static INamedTypeSymbol? FindGenericBase(INamedTypeSymbol attributeClass)
    {
        for (var current = attributeClass; current is not null; current = current.BaseType)
        {
            if (Is(current, TestingVialAttribute))
            {
                return current;
            }
        }

        return null;
    }
}
