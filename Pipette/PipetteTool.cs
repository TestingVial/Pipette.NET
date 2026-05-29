using System.Text.RegularExpressions;

namespace Pipette;

public static class PipetteTool
{
    public static int Run(string[] args, TextWriter? output = null, TextWriter? error = null)
    {
        output ??= Console.Out;
        error ??= Console.Error;

        if (args.Length == 0)
        {
            PrintUsage(error);
            return 1;
        }

        string? solution = null;
        string? project = null;
        var vialVersion = "1.0";

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            if (arg == "--solution")
            {
                if (!TryReadValue(args, ref i, out solution))
                {
                    error.WriteLine("Missing value for --solution.");
                    return 1;
                }
            }
            else if (arg == "--project")
            {
                if (!TryReadValue(args, ref i, out project))
                {
                    error.WriteLine("Missing value for --project.");
                    return 1;
                }
            }
            else if (arg == "--vial-version")
            {
                if (!TryReadValue(args, ref i, out vialVersion))
                {
                    error.WriteLine("Missing value for --vial-version.");
                    return 1;
                }
            }
            else
            {
                error.WriteLine($"Unknown argument '{arg}'.");
                PrintUsage(error);
                return 1;
            }
        }

        if ((solution is null && project is null) || (solution is not null && project is not null))
        {
            error.WriteLine("Specify exactly one of --solution or --project.");
            PrintUsage(error);
            return 1;
        }

        try
        {
            if (project is not null)
            {
                output.WriteLine(VialGenerator.GenerateForProject(project, vialVersion));
                return 0;
            }

            foreach (var projectPath in ParseSolutionProjects(solution!))
            {
                output.WriteLine(VialGenerator.GenerateForProject(projectPath, vialVersion));
            }

            return 0;
        }
        catch (Exception ex)
        {
            error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static bool TryReadValue(string[] args, ref int currentIndex, out string value)
    {
        var valueIndex = currentIndex + 1;
        if (valueIndex >= args.Length)
        {
            value = string.Empty;
            return false;
        }

        value = args[valueIndex];
        currentIndex = valueIndex;
        return true;
    }

    private static IEnumerable<string> ParseSolutionProjects(string solutionPath)
    {
        if (!File.Exists(solutionPath))
        {
            throw new FileNotFoundException($"Solution not found: {solutionPath}");
        }

        var solutionDirectory = Path.GetDirectoryName(Path.GetFullPath(solutionPath))!;
        var regex = new Regex("\"([^\"]+\\.csproj)\"", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        foreach (var line in File.ReadLines(solutionPath))
        {
            var match = regex.Match(line);
            if (!match.Success)
            {
                continue;
            }

            var relativePath = match.Groups[1].Value.Replace('\\', Path.DirectorySeparatorChar);
            yield return Path.GetFullPath(Path.Combine(solutionDirectory, relativePath));
        }
    }

    private static void PrintUsage(TextWriter writer)
    {
        writer.WriteLine("Usage: pipette (--project <path> | --solution <path>) [--vial-version <version>]");
    }
}
