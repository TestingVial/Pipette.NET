namespace Pipette.Core.Inputs;

/// <param name="BaseDirectory">Directory that vial file paths are made relative to: the solution folder, or the working directory for project inputs.</param>
/// <param name="ProjectPaths">Absolute, de-duplicated .csproj paths in input order.</param>
/// <param name="SolutionPath">Absolute path of the solution input, or <see langword="null"/> for project inputs.</param>
public sealed record ResolvedInputs(string BaseDirectory, IReadOnlyList<string> ProjectPaths, string? SolutionPath);
