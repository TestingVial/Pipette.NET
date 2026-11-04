using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Pipette.Core.Analysis;

/// <summary>Values that an attribute constructor chain passes to the <c>VialAttribute(name, testType)</c> base.</summary>
/// <param name="Name">Vial name; <see langword="null"/> when it comes from the generic argument or could not be resolved.</param>
/// <param name="NameFromGenericArgument">The chain reaches <c>TestingVialAttribute&lt;T&gt;</c>, so the name is <c>new T().Name</c>.</param>
/// <param name="TestType">Test-type string, or <see langword="null"/> when it could not be resolved.</param>
/// <param name="Failure">Why the chain could not be followed, if it could not.</param>
internal sealed record VialConstructorValues(string? Name, bool NameFromGenericArgument, string? TestType, string? Failure);

/// <summary>
/// Follows an attribute constructor through its <c>: base(...)</c> / <c>: this(...)</c> initializers (or primary
/// constructor base arguments) until it reaches a TestingVial.NET constructor with known semantics.
/// </summary>
internal sealed class AttributeConstructorEvaluator(SemanticModelProvider models)
{
    private const int MaxDepth = 32;

    public VialConstructorValues Evaluate(IMethodSymbol constructor, IReadOnlyList<string?> arguments) =>
        Evaluate(constructor.OriginalDefinition, arguments, depth: 0);

    private VialConstructorValues Evaluate(IMethodSymbol constructor, IReadOnlyList<string?> arguments, int depth)
    {
        if (depth > MaxDepth)
        {
            return Fail("the constructor chain is too deep");
        }

        var type = constructor.ContainingType;
        string? Arg(int ordinal) => ordinal < arguments.Count ? arguments[ordinal] : null;

        if (TestingVialSymbols.Is(type, TestingVialSymbols.VialAttribute))
        {
            return new(Arg(Ordinal(constructor, "name", 0)), false, Arg(Ordinal(constructor, "testType", 1)), null);
        }

        if (TestingVialSymbols.Is(type, TestingVialSymbols.TestingVialAttribute))
        {
            return new(null, true, Arg(Ordinal(constructor, "testType", 0)), null);
        }

        if (constructor.DeclaringSyntaxReferences.Length > 0)
        {
            return EvaluateSource(constructor, arguments, depth);
        }

        if (TestingVialSymbols.Is(type, TestingVialSymbols.UnitTestVial))
        {
            return new(Arg(0), false, TestingVialSymbols.UnitTestType, null);
        }

        if (TestingVialSymbols.Is(type, TestingVialSymbols.UnitTestVialGeneric))
        {
            return new(null, true, TestingVialSymbols.UnitTestType, null);
        }

        if (TestingVialSymbols.Is(type, TestingVialSymbols.IntegrationTestVial))
        {
            return new(Arg(0), false, TestingVialSymbols.IntegrationTestType, null);
        }

        if (TestingVialSymbols.Is(type, TestingVialSymbols.IntegrationTestVialGeneric))
        {
            return new(null, true, TestingVialSymbols.IntegrationTestType, null);
        }

        return Fail($"the constructor of '{type.ToDisplayString()}' is not available in source");
    }

    private VialConstructorValues EvaluateSource(IMethodSymbol constructor, IReadOnlyList<string?> arguments, int depth)
    {
        var syntax = constructor.DeclaringSyntaxReferences[0].GetSyntax();
        if (models.Get(syntax.SyntaxTree) is not { } model)
        {
            return Fail($"the source of '{constructor.ContainingType.ToDisplayString()}' is not part of the analyzed compilations");
        }

        SyntaxNode? callSite;
        ArgumentListSyntax? argumentList;
        switch (syntax)
        {
            case ConstructorDeclarationSyntax { Initializer: { } initializer }:
                callSite = initializer;
                argumentList = initializer.ArgumentList;
                break;
            case TypeDeclarationSyntax typeDeclaration
                when typeDeclaration.BaseList?.Types.OfType<PrimaryConstructorBaseTypeSyntax>().FirstOrDefault() is { } primaryBase:
                callSite = primaryBase;
                argumentList = primaryBase.ArgumentList;
                break;
            default:
                callSite = null;
                argumentList = null;
                break;
        }

        IMethodSymbol? target;
        if (callSite is null)
        {
            target = constructor.ContainingType.BaseType?.InstanceConstructors
                .FirstOrDefault(c => c.Parameters.All(p => p.IsOptional || p.IsParams));
        }
        else
        {
            var symbolInfo = model.GetSymbolInfo(callSite);
            target = (symbolInfo.Symbol ?? symbolInfo.CandidateSymbols.FirstOrDefault()) as IMethodSymbol;
        }

        if (target is null)
        {
            return Fail($"the base constructor called by '{constructor.ContainingType.ToDisplayString()}' could not be resolved");
        }

        target = target.OriginalDefinition;
        var values = new string?[target.Parameters.Length];
        var supplied = new bool[target.Parameters.Length];

        if (argumentList is not null)
        {
            for (var i = 0; i < argumentList.Arguments.Count; i++)
            {
                var argument = argumentList.Arguments[i];
                var parameter = argument.NameColon is { } nameColon
                    ? target.Parameters.FirstOrDefault(p => p.Name == nameColon.Name.Identifier.ValueText)
                    : i < target.Parameters.Length ? target.Parameters[i] : null;
                if (parameter is null)
                {
                    continue;
                }

                values[parameter.Ordinal] = EvaluateExpression(argument.Expression, model, constructor, arguments);
                supplied[parameter.Ordinal] = true;
            }
        }

        foreach (var parameter in target.Parameters)
        {
            if (!supplied[parameter.Ordinal] && parameter.HasExplicitDefaultValue)
            {
                values[parameter.Ordinal] = parameter.ExplicitDefaultValue as string;
            }
        }

        return Evaluate(target, values, depth + 1);
    }

    private static string? EvaluateExpression(
        ExpressionSyntax expression, SemanticModel model, IMethodSymbol constructor, IReadOnlyList<string?> arguments)
    {
        var constant = model.GetConstantValue(expression);
        if (constant.HasValue)
        {
            return constant.Value as string;
        }

        if (model.GetSymbolInfo(expression).Symbol is IParameterSymbol parameter
            && SymbolEqualityComparer.Default.Equals(parameter.ContainingSymbol.OriginalDefinition, constructor)
            && parameter.Ordinal < arguments.Count)
        {
            return arguments[parameter.Ordinal];
        }

        return null;
    }

    private static int Ordinal(IMethodSymbol constructor, string parameterName, int fallback) =>
        constructor.Parameters.FirstOrDefault(p => p.Name == parameterName)?.Ordinal ?? fallback;

    private static VialConstructorValues Fail(string reason) => new(null, false, null, reason);
}
