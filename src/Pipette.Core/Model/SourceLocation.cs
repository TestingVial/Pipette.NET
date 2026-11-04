namespace Pipette.Core.Model;

/// <param name="ClassName">Fully qualified name of the containing type.</param>
/// <param name="MethodName">Method name, or <see cref="ClassLevel"/> for a class-level vial.</param>
public sealed record SourceLocation(string ClassName, string MethodName)
{
    public const string ClassLevel = "*";
}
