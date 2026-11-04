using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Pipette.Core.Analysis;

/// <summary>Reads the constant value of an <c>IVial.Name</c> implementation from source.</summary>
internal static class VialNameResolver
{
    /// <summary>
    /// Supports an expression body (<c>=&gt; "Cart"</c>), a getter with a single <c>return</c>, and a property
    /// initializer, where the expression is any C# constant (literal, <c>const</c> reference, constant concatenation
    /// or interpolation, <c>nameof</c>).
    /// </summary>
    public static string? TryResolve(SemanticModelProvider models, ITypeSymbol vialType)
    {
        var vialInterface = vialType.AllInterfaces.FirstOrDefault(i => TestingVialSymbols.Is(i, TestingVialSymbols.IVial));
        var nameProperty = vialInterface?.GetMembers("Name").OfType<IPropertySymbol>().FirstOrDefault();
        if (nameProperty is null
            || vialType.FindImplementationForInterfaceMember(nameProperty) is not IPropertySymbol implementation)
        {
            return null;
        }

        foreach (var reference in implementation.DeclaringSyntaxReferences)
        {
            if (reference.GetSyntax() is not PropertyDeclarationSyntax property)
            {
                continue;
            }

            var expression = NameExpression(property);
            if (expression is not null
                && models.Get(property.SyntaxTree)?.GetConstantValue(expression) is { HasValue: true, Value: string value })
            {
                return value;
            }
        }

        return null;
    }

    private static ExpressionSyntax? NameExpression(PropertyDeclarationSyntax property)
    {
        if (property.ExpressionBody is { } expressionBody)
        {
            return expressionBody.Expression;
        }

        var getter = property.AccessorList?.Accessors.FirstOrDefault(a => a.IsKind(SyntaxKind.GetAccessorDeclaration));
        if (getter?.ExpressionBody is { } getterBody)
        {
            return getterBody.Expression;
        }

        if (getter?.Body is { } body)
        {
            var returns = body.DescendantNodes(n => n is not (LocalFunctionStatementSyntax or AnonymousFunctionExpressionSyntax))
                .OfType<ReturnStatementSyntax>()
                .ToList();
            return returns.Count == 1 ? returns[0].Expression : null;
        }

        return property.Initializer?.Value;
    }
}
