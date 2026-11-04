# TestingVial.NET 1.1.0 public API

From the TestingVial.NET 1.1.0 sources (the nupkg ships net8.0, net9.0 and net10.0, no dependencies, plus the `TV0001` analyzer).
`AssemblyVersion` is `<major>.0.0.0` (1.0.0.0 for every 1.x; 1.0.1 had 0.0.1.0) — match by type name, never by assembly version.

Everything lives in a single namespace: **`TestingVial.NET`**.

## Types

```csharp
namespace TestingVial.NET;

public interface IVial
{
    string Name { get; }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true, AllowMultiple = true)]
public abstract class VialAttribute : Attribute
{
    public string VialName { get; }              // NOT "Name"
    public string? TestType { get; }             // null for generic vials
    public string? Description { get; init; }    // named argument, optional (nullable)

    protected VialAttribute(string name);                    // generic vial, no test type
    protected VialAttribute(string name, string testType);   // throw on null/whitespace
}

public class TestingVialAttribute<T> : VialAttribute where T : IVial, new()   // abstract before 1.1.0
{
    public TestingVialAttribute() : base(new T().Name);                       // generic vial: [TestingVial<CartVial>]
    protected TestingVialAttribute(string testType) : base(new T().Name, testType);   // public before 1.1.0
}

public class TestingVialAttribute : VialAttribute                             // generic vial: [TestingVial("Cart")]
{
    public TestingVialAttribute(string name) : base(name);
}

public class UnitTestVial<T> : TestingVialAttribute<T> where T : IVial, new()
{
    public UnitTestVial() : base("Unit");
}

public class UnitTestVial : VialAttribute
{
    public UnitTestVial(string name) : base(name, "Unit");
}

public class IntegrationTestVial<T> : TestingVialAttribute<T> where T : IVial, new()
{
    public IntegrationTestVial() : base("Integration");
}

public class IntegrationTestVial : VialAttribute
{
    public IntegrationTestVial(string name) : base(name, "Integration");
}

public class EndToEndTestVial<T> : TestingVialAttribute<T> where T : IVial, new()
{
    public EndToEndTestVial() : base("E2E");
}

public class EndToEndTestVial : VialAttribute
{
    public EndToEndTestVial(string name) : base(name, "E2E");
}
```

Metadata names (for `Compilation.GetTypeByMetadataName`):

| Type | Metadata name |
|---|---|
| `IVial` | `TestingVial.NET.IVial` |
| `VialAttribute` | `TestingVial.NET.VialAttribute` |
| `TestingVialAttribute<T>` / `TestingVialAttribute` | ``TestingVial.NET.TestingVialAttribute`1`` / `TestingVial.NET.TestingVialAttribute` |
| `UnitTestVial` / `UnitTestVial<T>` | `TestingVial.NET.UnitTestVial` / ``TestingVial.NET.UnitTestVial`1`` |
| `IntegrationTestVial` / `IntegrationTestVial<T>` | `TestingVial.NET.IntegrationTestVial` / ``TestingVial.NET.IntegrationTestVial`1`` |
| `EndToEndTestVial` / `EndToEndTestVial<T>` | `TestingVial.NET.EndToEndTestVial` / ``TestingVial.NET.EndToEndTestVial`1`` |

## Test-type strings

Exact strings passed to the base constructor: **`"Unit"`**, **`"Integration"`** and **`"E2E"`** (added in 1.1.0).
Custom attributes pick any string (README example: `"Performance"`).

## Facts that matter for Pipette's analysis

- **Detection:** every vial attribute ultimately derives from `TestingVial.NET.VialAttribute`
  (`TestingVialAttribute<T>` derives from it too). Walking `AttributeClass.BaseType` up to `VialAttribute` is enough.
- **No `AttributeUsage` on subclasses:** `AllowMultiple = true` and `Inherited = true` are inherited from `VialAttribute`.
  So several vial attributes can sit on one method or class. Roslyn's `ISymbol.GetAttributes()` returns only the
  attributes declared directly on the symbol, not inherited ones. The plan emits vials at their declaring location.
- **Name, generic form:** `T` is the generic type argument. The attribute ctor has no `name` argument, and the name
  comes from `new T().Name` at run time. In Roslyn: `attributeData.AttributeClass` is the constructed type,
  for example `UnitTestVial<CartVial>`. Walk up to the constructed `TestingVialAttribute<T>` base and take
  `TypeArguments[0]` (or take `AttributeClass.TypeArguments[0]` directly for the built-ins). Then resolve
  `T`'s `IVial.Name` implementation from source, which can be an expression-bodied property (`=> "Cart"`), a getter
  `return`, or a `const`.
- **Name, string form:** for the built-ins it's `ConstructorArguments[0]` (`string name`).
- **Test type:** this is not in attribute data. It's a constant in the subclass ctor's `: base(...)` initializer:
  - Built-ins: map by original definition `UnitTestVial`/`UnitTestVial<T>` → `"Unit"`, `IntegrationTestVial`/`IntegrationTestVial<T>` → `"Integration"`, `EndToEndTestVial`/`EndToEndTestVial<T>` → `"E2E"`.
  - Generic vials have none: `TestingVialAttribute<T>()`, `TestingVialAttribute(name)` and `VialAttribute(name)`, or a custom
    attribute whose chain ends in one of them (including an implicit `base()` call). Pipette writes them without `type`.
  - Custom generic (`: TestingVialAttribute<T>`): `base(testType)`, which is the **1st** (only) argument.
  - Custom non-generic (`: VialAttribute`): `base(name, testType)`, which is the **2nd** argument, and the 1st is usually the ctor's `name` parameter.
  - A custom attribute may derive from another custom attribute. Follow the chain until you reach a constant.
- **Description:** this is the named argument `Description` (`attributeData.NamedArguments`). It can be missing or null.
  The schema requires `minLength: 1`, so Pipette uses the method name (or the simple class name for a class-level attribute)
  and reports a verbose note. From 1.1.0 the package's analyzer warns about it (`TV0001`).
- Example of a custom attribute, from the package README:
  ```csharp
  public class PerformanceTestVial<T> : TestingVialAttribute<T> where T : IVial, new()
  { public PerformanceTestVial() : base("Performance") { } }

  public class PerformanceTestVial : VialAttribute
  { public PerformanceTestVial(string name) : base(name, "Performance") { } }
  ```
