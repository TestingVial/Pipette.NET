# TestingVial.NET 1.0.1 public API

Decompiled from `lib/net10.0/TestingVial.NET.dll` (nupkg also ships net8.0 and net9.0; no dependencies).
Note: `AssemblyVersion` is `0.0.1.0` even though the package version is 1.0.1 — match by type name, never by assembly version.

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
    public string TestType { get; }
    public string? Description { get; init; }    // named argument, optional (nullable)

    protected VialAttribute(string name, string testType);   // throws on null/whitespace
}

public abstract class TestingVialAttribute<T> : VialAttribute where T : IVial, new()
{
    public TestingVialAttribute(string testType) : base(new T().Name, testType);
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
```

Metadata names (for `Compilation.GetTypeByMetadataName`):

| Type | Metadata name |
|---|---|
| `IVial` | `TestingVial.NET.IVial` |
| `VialAttribute` | `TestingVial.NET.VialAttribute` |
| `TestingVialAttribute<T>` | ``TestingVial.NET.TestingVialAttribute`1`` |
| `UnitTestVial` / `UnitTestVial<T>` | `TestingVial.NET.UnitTestVial` / ``TestingVial.NET.UnitTestVial`1`` |
| `IntegrationTestVial` / `IntegrationTestVial<T>` | `TestingVial.NET.IntegrationTestVial` / ``TestingVial.NET.IntegrationTestVial`1`` |

## Test-type strings

Exact strings passed to the base constructor: **`"Unit"`** and **`"Integration"`**. There is no built-in E2E attribute.
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
  - Built-ins: map by original definition `UnitTestVial`/`UnitTestVial<T>` → `"Unit"`, `IntegrationTestVial`/`IntegrationTestVial<T>` → `"Integration"`.
  - Custom generic (`: TestingVialAttribute<T>`): `base(testType)`, which is the **1st** (only) argument.
  - Custom non-generic (`: VialAttribute`): `base(name, testType)`, which is the **2nd** argument, and the 1st is usually the ctor's `name` parameter.
  - A custom attribute may derive from another custom attribute. Follow the chain until you reach a constant.
- **Description:** this is the named argument `Description` (`attributeData.NamedArguments`). It can be missing or null.
  The schema requires `minLength: 1`, so skip the entry with a warning.
- Example of a custom attribute, from the package README:
  ```csharp
  public class PerformanceTestVial<T> : TestingVialAttribute<T> where T : IVial, new()
  { public PerformanceTestVial() : base("Performance") { } }

  public class PerformanceTestVial : VialAttribute
  { public PerformanceTestVial(string name) : base(name, "Performance") { } }
  ```
