# Pipette.NET

`pipette`, a .NET global tool that reads TestingVial.NET attributes from test-project **source code** (Roslyn
`MSBuildWorkspace`, nothing is built or run) and writes a `.vial` JSON file conforming to VialSchemas.

Part of the TestingVial workspace: cross-repo rules, issue tracking and the propagation checklist are in `../CLAUDE.md`
(present when this repo is cloned inside the `TestingVial/workspace` checkout). Every change needs an issue on
<https://github.com/orgs/TestingVial/projects/1> referenced from its PR.

## Build and test

```bash
dotnet restore tests/Fixtures/Fixtures.slnx   # fixtures are analysed, not built; they must be restored
dotnet build
dotnet test                                    # run from this folder: global.json selects Microsoft.Testing.Platform
```

- .NET 10 SDK (`global.json`: 10.0.100, `rollForward: latestFeature`). `Directory.Build.props` sets `TreatWarningsAsErrors`.
- Tests: xunit v3 in `tests/Pipette.Core.Tests`. End-to-end tests run the CLI against `tests/Fixtures` and validate the output
  against the schema copy embedded from `tests/Pipette.Core.Tests/Schemas/` — keep that copy identical to `VialSchemas/schemas/`.

## Layout

`src/Pipette.Cli` (`Program.cs`, argument parsing, exit codes) → `src/Pipette.Core/PipetteRunner.cs`, which runs:

1. `Inputs/` — resolve `.sln`/`.slnx`/`.csproj` arguments.
2. `Workspace/` — `MSBuildRegistration` (MSBuildLocator), `WorkspaceLoader`, `TestProjectDetector` (`IsTestProject` or known test framework references).
3. `Analysis/` — `TestingVialSymbols` (library types by metadata name), `VialCollector`, `VialNameResolver` (`IVial.Name` constant),
   `AttributeConstructorEvaluator` (follows `: base(...)` of custom attributes), `TestTypeMapper` (`Unit`→`unit`, `E2E`/`EndToEnd`→`e2e`, others lowercased).
4. `Model/` + `Output/VialWriter.cs` — the `.vial` document (`VialDocument.CurrentSchemaVersion`) and stable, sorted JSON output.

Diagnostics go through `Diagnostics/PipetteDiagnostics.cs` (warnings/notes to stderr; `--strict` turns warnings into exit code 2).

## Constraints

- **MSBuildLocator**: MSBuild assemblies must come from the installed SDK. Every project excludes `Microsoft.Build.*` runtime
  assets (`ExcludeAssets="runtime"`, see `Directory.Build.props`); don't add a package that brings them in without the same treatment.
- TestingVial.NET is consumed as a **NuGet package** (`1.0.1`) in the tests and fixtures. `docs/testingvial-api.md` documents the
  API Pipette relies on — update it when the library changes.
- Output must stay deterministic (sorted entries) so `.vial` files diff cleanly.
- The README is the user documentation (options, exit codes, attribute mapping); update it with any behaviour change.
