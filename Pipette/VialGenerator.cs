using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Pipette;

public static class VialGenerator
{
    private static readonly string[] TestingVialTypes =
    [
        "IntegrationTestVial",
        "IVial",
        "TestingVialAttribute",
        "UnitTestVial",
        "VialAttribute",
        "Vial"
    ];

    public static string GenerateForProject(string projectPath, string vialVersion)
    {
        if (!File.Exists(projectPath))
        {
            throw new FileNotFoundException($"Project not found: {projectPath}");
        }

        var fullProjectPath = Path.GetFullPath(projectPath);
        var projectDirectory = Path.GetDirectoryName(fullProjectPath)!;
        var hasTestingVialReference = ProjectReferencesTestingVial(fullProjectPath);
        var occurrences = new Dictionary<string, int>(StringComparer.Ordinal);

        if (hasTestingVialReference)
        {
            foreach (var file in EnumerateSourceFiles(projectDirectory))
            {
                var source = File.ReadAllText(file);
                foreach (var typeName in TestingVialTypes)
                {
                    var count = Regex.Matches(source, $"\\b{Regex.Escape(typeName)}\\b").Count;
                    if (count == 0)
                    {
                        continue;
                    }

                    occurrences[typeName] = occurrences.TryGetValue(typeName, out var existing) ? existing + count : count;
                }
            }
        }

        var outputPath = Path.ChangeExtension(fullProjectPath, ".vial");
        var lines = new List<string>
        {
            $"vial-version: {vialVersion}",
            $"project: {Path.GetFileName(fullProjectPath)}",
            "occurrences:"
        };

        foreach (var occurrence in occurrences.OrderBy(kvp => kvp.Key, StringComparer.Ordinal))
        {
            lines.Add($"- {occurrence.Key}: {occurrence.Value}");
        }

        File.WriteAllLines(outputPath, lines);
        return outputPath;
    }

    private static bool ProjectReferencesTestingVial(string projectPath)
    {
        var project = XDocument.Load(projectPath);
        return project
            .Descendants()
            .Where(e => e.Name.LocalName == "PackageReference")
            .Any(e => string.Equals((string?)e.Attribute("Include"), "TestingVial.NET", StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<string> EnumerateSourceFiles(string projectDirectory)
    {
        return Directory.EnumerateFiles(projectDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));
    }
}
