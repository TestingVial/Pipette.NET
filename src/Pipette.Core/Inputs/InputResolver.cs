using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Pipette.Core.Diagnostics;

namespace Pipette.Core.Inputs;

/// <summary>Expands .sln, .slnx and .csproj inputs into a de-duplicated list of project paths.</summary>
public sealed partial class InputResolver
{
    private static readonly StringComparer PathComparer =
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private readonly PipetteDiagnostics _diagnostics;

    public InputResolver(PipetteDiagnostics diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        _diagnostics = diagnostics;
    }

    /// <summary>
    /// Accepts one .sln/.slnx file, or one or more .csproj files. Relative inputs are resolved against
    /// <paramref name="currentDirectory"/>, which is also the base directory for project inputs.
    /// </summary>
    /// <exception cref="InputResolutionException">An input is missing, has an unsupported extension, or the combination is invalid.</exception>
    public ResolvedInputs Resolve(IReadOnlyList<string> inputs, string currentDirectory)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentDirectory);

        if (inputs.Count == 0)
        {
            throw new InputResolutionException("No input given. Pass a .sln, a .slnx, or one or more .csproj files.");
        }

        var fullPaths = new List<string>();
        foreach (var input in inputs)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                throw new InputResolutionException("An empty input path was given.");
            }

            var fullPath = Path.GetFullPath(input, currentDirectory);
            var kind = GetKind(fullPath)
                ?? throw new InputResolutionException(
                    $"Unsupported input '{input}'. Expected a .sln, .slnx or .csproj file.");

            if (!File.Exists(fullPath))
            {
                throw new InputResolutionException($"{DescribeKind(kind)} '{input}' does not exist.");
            }

            fullPaths.Add(fullPath);
        }

        var solutions = fullPaths.Where(p => GetKind(p) != InputKind.Project).Distinct(PathComparer).ToList();
        if (solutions.Count > 0)
        {
            if (fullPaths.Count > 1 && (solutions.Count > 1 || fullPaths.Any(p => GetKind(p) == InputKind.Project)))
            {
                throw new InputResolutionException(
                    "A solution must be the only input. Pass a single .sln/.slnx, or one or more .csproj files.");
            }

            return ResolveSolution(solutions[0]);
        }

        var projects = Deduplicate(fullPaths);
        return new ResolvedInputs(Path.GetFullPath(currentDirectory), projects, SolutionPath: null);
    }

    private ResolvedInputs ResolveSolution(string solutionPath)
    {
        var solutionDirectory = Path.GetDirectoryName(solutionPath)!;
        IReadOnlyList<string> entries;
        try
        {
            var content = File.ReadAllText(solutionPath);
            entries = GetKind(solutionPath) == InputKind.Slnx ? ParseSlnx(content) : ParseSln(content);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or XmlException)
        {
            throw new InputResolutionException($"Could not read solution '{solutionPath}': {ex.Message}", ex);
        }

        var projects = new List<string>();
        foreach (var entry in entries)
        {
            var projectPath = Path.GetFullPath(NormalizeSeparators(entry), solutionDirectory);
            if (!string.Equals(Path.GetExtension(projectPath), ".csproj", StringComparison.OrdinalIgnoreCase))
            {
                _diagnostics.Verbose($"Skipping '{entry}' in '{Path.GetFileName(solutionPath)}': only .csproj projects are analyzed.");
                continue;
            }

            if (!File.Exists(projectPath))
            {
                _diagnostics.Warn($"Project '{entry}' listed in '{Path.GetFileName(solutionPath)}' does not exist; skipping it.");
                continue;
            }

            projects.Add(projectPath);
        }

        if (projects.Count == 0)
        {
            _diagnostics.Warn($"Solution '{Path.GetFileName(solutionPath)}' contains no C# projects.");
        }

        return new ResolvedInputs(solutionDirectory, Deduplicate(projects), solutionPath);
    }

    /// <summary>Returns the project paths (as written, relative to the solution) listed in a .sln file. Solution folders are excluded.</summary>
    internal static IReadOnlyList<string> ParseSln(string content)
    {
        var result = new List<string>();
        foreach (Match match in SlnProjectLine().Matches(content))
        {
            var typeGuid = match.Groups["type"].Value;
            if (string.Equals(typeGuid, SolutionFolderTypeGuid, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            result.Add(match.Groups["path"].Value);
        }

        return result;
    }

    /// <summary>Returns the project paths (as written, relative to the solution) listed in a .slnx file.</summary>
    internal static IReadOnlyList<string> ParseSlnx(string content)
    {
        var document = XDocument.Parse(content);
        return document.Descendants()
            .Where(e => e.Name.LocalName == "Project")
            .Select(e => (string?)e.Attribute("Path"))
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p!)
            .ToList();
    }

    private static List<string> Deduplicate(IEnumerable<string> paths) =>
        paths.Distinct(PathComparer).ToList();

    private static string NormalizeSeparators(string path) =>
        path.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);

    private static InputKind? GetKind(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".sln" => InputKind.Sln,
        ".slnx" => InputKind.Slnx,
        ".csproj" => InputKind.Project,
        _ => null,
    };

    private static string DescribeKind(InputKind kind) => kind == InputKind.Project ? "Project" : "Solution";

    private const string SolutionFolderTypeGuid = "{2150E333-8FDC-42A3-9474-1A3956D46DE8}";

    [GeneratedRegex(@"^\s*Project\(""(?<type>\{[^}]+\})""\)\s*=\s*""[^""]*""\s*,\s*""(?<path>[^""]+)""", RegexOptions.Multiline)]
    private static partial Regex SlnProjectLine();

    private enum InputKind
    {
        Sln,
        Slnx,
        Project,
    }
}
