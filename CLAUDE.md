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
  against the schema copy embedded from `tests/Pipette.Core.Tests/Schemas/v<CurrentSchemaVersion>.json` — keep it identical to
  `VialSchemas/schemas/` (`EmbeddedSchemaTests` compares them when `../VialSchemas` or `VIALSCHEMAS_DIR` exists).

## CI/CD and releases

`.github/workflows/ci-cd.yml` restores the fixtures, builds, tests (with VialSchemas checked out, so `EmbeddedSchemaTests` runs)
and packs on every pull request and push. On push to `main` it also publishes the tool to nuget.org:

- **Version** `<VersionMajorMinor>.<run number>` (`src/Pipette.Cli/Pipette.Cli.csproj`, currently `0.1`): every merge is a release.
  Bump `VersionMajorMinor` for a breaking change or a new feature line; never set the patch by hand.
- **Authentication**: nuget.org Trusted Publishing (`NuGet/login`, OIDC, no API key). One-time setup by a maintainer: a trusted
  publishing policy on nuget.org for repository `TestingVial/Pipette.NET` and workflow `ci-cd.yml`, and the repository secret
  `NUGET_USER` set to the nuget.org account (profile name, not e-mail) that owns the policy.
- A change that needs an unreleased TestingVial.NET version can't merge until that package is on nuget.org: CI restores from it.

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
- TestingVial.NET is consumed as a **NuGet package** (`1.1.0`) in the tests and fixtures. `docs/testingvial-api.md` documents the
  API Pipette relies on — update it when the library changes.
- Output must stay deterministic (sorted entries) so `.vial` files diff cleanly.
- The README is the user documentation (options, exit codes, attribute mapping); update it with any behaviour change.
