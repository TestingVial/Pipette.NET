# Pipette.NET

[![NuGet](https://img.shields.io/nuget/v/Pipette.NET.svg)](https://www.nuget.org/packages/Pipette.NET/)
[![CI/CD](https://github.com/TestingVial/Pipette.NET/actions/workflows/ci-cd.yml/badge.svg)](https://github.com/TestingVial/Pipette.NET/actions/workflows/ci-cd.yml)

Pipette.NET is a .NET global tool (`pipette`) that reads the [TestingVial.NET](https://github.com/TestingVial/TestingVial.NET)
attributes in your test projects and writes a `.vial` file: a JSON catalogue of which tests cover which features, conforming to the
[VialSchemas v0.0.2](https://github.com/TestingVial/VialSchemas/blob/main/schemas/v0.0.2.json) schema.

It analyzes the **source code** with Roslyn (`MSBuildWorkspace`); your projects are never built or executed.

```csharp
public class CartTests
{
    [Fact]
    [UnitTestVial<CartVial>(Description = "Carts can have products added")]
    [UnitTestVial("Product", Description = "Products can be added to a cart")]
    public void AddToCart_AddsProduct() { }
}
```

## Requirements

- **The .NET 10 SDK must be installed on the machine that runs `pipette`.** A runtime is not enough: projects are loaded
  through `MSBuildWorkspace`, which uses the installed SDK's MSBuild (located with `MSBuildLocator`).
- **The projects you analyze must be restored** (`dotnet restore`) before you run `pipette`, so that package references
  such as TestingVial.NET can be resolved. Running a build or `dotnet test` beforehand also restores them.

## Install

Install the `pipette` tool from nuget.org:

```bash
dotnet tool install --global Pipette.NET
```

To update it, run `dotnet tool update --global Pipette.NET`. To install it for one repository only, create a tool manifest
(`dotnet new tool-manifest`) and drop `--global`.

Every push to `main` publishes a new version (`0.1.<build>`). To try a local build instead, pack it and install it from the folder:

```bash
dotnet pack src/Pipette.Cli -c Release
dotnet tool install --global --add-source ./artifacts/nupkg Pipette.NET
```

## Usage

```text
pipette <inputs...> [-o|--output vials.vial] [--strict] [--verbose]
```

| Option | Meaning |
|---|---|
| `inputs` | One `.sln` or `.slnx` file, **or** one or more `.csproj` files. A solution can't be combined with other inputs. |
| `-o`, `--output` | Path of the `.vial` file to write. Defaults to `./vials.vial`. Missing parent directories are created. |
| `--strict` | Exit with code 2 when any warning was reported (the file is still written). |
| `--verbose` | Also print informational notes, such as test projects that don't reference TestingVial.NET. |

Warnings and notes go to standard error; the summary line (`Wrote N vial(s) to ...`) goes to standard output.

```powershell
pipette MyApp.slnx
pipette tests/Shop.Tests/Shop.Tests.csproj tests/Shop.IntegrationTests/Shop.IntegrationTests.csproj -o artifacts/shop.vial
pipette MyApp.sln --strict    # fail the CI step on any warning
```

Only `.csproj` projects inside a solution are analyzed; other project types are skipped. Solution entries that don't exist on disk
are skipped with a warning.

### Exit codes

| Code | Meaning |
|---|---|
| 0 | Success. |
| 1 | Invalid input (missing file, unsupported extension, solution mixed with other inputs), MSBuild/SDK not found, a project that can't be opened, or the output can't be written. |
| 2 | `--strict` was set and at least one warning was reported. |

## How attributes are mapped

Each vial attribute found on a class or method becomes one entry:

| Field | Value |
|---|---|
| `name` | Generic form (`[UnitTestVial<CartVial>]`): the constant returned by `CartVial`'s `IVial.Name` (expression body, getter with a single `return`, property initializer, or `const`). String form (`[UnitTestVial("Cart")]`): the string argument. |
| `description` | The `Description` named argument. When it's missing, empty or whitespace: the method name, or the simple class name (e.g. `CartTests`) for an attribute placed on the class. |
| `type` | Derived from the attribute's test type (see below). |
| `technology` | Always `csharp`. |
| `location.className` | The fully qualified name of the containing type, e.g. `Shop.Tests.CartTests` or `Shop.Tests.Outer.Inner`. |
| `location.methodName` | The method name, or `*` for an attribute placed on the class. |

Entries are sorted by class, method and name, so the output is stable between runs.

**Test types.** `Unit` maps to `unit`, `Integration` to `integration`, and `E2E` or `EndToEnd` (any casing) to `e2e`.
Any other test type is kept and lowercased, so a custom `PerformanceTestVial` produces `performance`; schema v0.0.2 allows
any non-empty `type`.

**Custom attributes** defined in source are supported, in both forms from the TestingVial.NET README:

```csharp
public class PerformanceTestVial<T> : TestingVialAttribute<T> where T : IVial, new()
{ public PerformanceTestVial() : base("Performance") { } }

public class PerformanceTestVial : VialAttribute
{ public PerformanceTestVial(string name) : base(name, "Performance") { } }
```

Pipette follows the constructor's `: base(...)` / `: this(...)` chain (and primary-constructor base arguments) until it reaches a
constant test type, so attributes derived from other custom attributes also work.

**Missing descriptions.** The schema requires a non-empty description, so an attribute without a `Description` (or with an empty
one) gets the method or class name instead, and Pipette reports it as a note (shown with `--verbose`), not a warning, so `--strict`
doesn't fail on it. TestingVial.NET 1.1.0 and later warn about it at compile time (rule `TV0001`).

**Skipped entries.** Attributes whose test type or string name isn't a compile-time constant are skipped with a warning.

**Test-project detection.** A project is analyzed when its evaluated MSBuild property `IsTestProject` is `true` (set by
`Microsoft.NET.Test.Sdk`, MSTest.Sdk and similar), or when it references a known test framework (`xunit`, `xunit.v3`, `NUnit`,
`MSTest`, `MSTest.TestFramework`, `TUnit`). Every other project is discarded with a warning. A test project that doesn't reference
TestingVial.NET produces no entries (reported as a note with `--verbose`).

## Sample output

For the fixture project in `tests/Fixtures/SampleTests` (excerpt of `pipette tests/Fixtures/Fixtures.slnx`):

```json
{
  "schemaVersion": "0.0.2",
  "vials": [
    {
      "name": "Cart",
      "description": "Carts can have products added",
      "type": "unit",
      "technology": "csharp",
      "location": {
        "className": "SampleTests.CartTests",
        "methodName": "AddToCart_AddsProduct"
      }
    },
    {
      "name": "Checkout",
      "description": "Checkout talks to the payment gateway",
      "type": "integration",
      "technology": "csharp",
      "location": {
        "className": "SampleTests.Checkout.CheckoutTests",
        "methodName": "*"
      }
    },
    {
      "name": "Checkout",
      "description": "A customer can pay for a cart",
      "type": "e2e",
      "technology": "csharp",
      "location": {
        "className": "SampleTests.Checkout.CheckoutTests",
        "methodName": "Pay_CompletesOrder"
      }
    }
  ]
}
```

## Known limitations

- **Inherited vials are not propagated.** TestingVial.NET's attributes are `Inherited = true`, but Pipette reports a vial only where
  it's declared: a class-level vial on a base test class is not repeated for derived classes.
- **Non-constant `IVial.Name`.** When the name can't be read as a constant from source (computed at run time, or the vial type
  comes from a compiled assembly instead of source), Pipette uses the vial type's name (e.g. `CartVial`) and warns.
- **Custom attributes from compiled packages are skipped.** The test type of a custom attribute is read from its constructor's
  source; attributes that come from a NuGet package or a DLL reference (rather than a project in the workspace) are skipped with a
  warning. The built-in `UnitTestVial`, `IntegrationTestVial` and `EndToEndTestVial` are always recognized.
- **Only the first target framework is analyzed** for multi-targeted projects.

## Building and testing this repository

```powershell
dotnet build Pipette.NET.slnx
dotnet test --project tests/Pipette.Core.Tests/Pipette.Core.Tests.csproj
```

The SDK version is pinned by `global.json` (.NET 10), and tests run on Microsoft.Testing.Platform with xUnit v3.

- `src/Pipette.Core`: input resolution, workspace loading, test-project detection, attribute collection and the `.vial` writer.
- `src/Pipette.Cli`: the `pipette` command line (System.CommandLine), packaged as the `Pipette.NET` tool.
- `tests/Pipette.Core.Tests`: unit tests over in-memory Roslyn compilations, plus end-to-end tests (`EndToEnd/`) that restore the
  fixture projects, run the whole pipeline once on `tests/Fixtures/Fixtures.slnx` and on the project files, validate the output
  against the embedded v0.0.2 schema with JsonSchema.Net, and run the built CLI. The embedded copy
  (`tests/Pipette.Core.Tests/Schemas/`) must stay identical to the VialSchemas file: `EmbeddedSchemaTests` compares them when
  VialSchemas is cloned next to this repository (or `VIALSCHEMAS_DIR` points at it), and is skipped otherwise.
- `tests/Fixtures`: `SampleTests` (an xUnit project covering generic, string, class-level, multiple-per-method, custom
  `performance` and `e2e` attributes, and missing descriptions on a method and a class) and `NonTestLibrary` (a plain library that must be discarded).
  They are isolated from the repository's `Directory.Build.props` and are not part of `Pipette.NET.slnx`.
- `docs/testingvial-api.md`: the TestingVial.NET 1.0.1 API that the analysis relies on.
