using System.Text.Json;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Pipette;

public static class VialGenerator
{
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
            occurrences = FindTestingVialAttributeUsages(fullProjectPath, projectDirectory);
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

    private static Dictionary<string, int> FindTestingVialAttributeUsages(string projectPath, string projectDirectory)
    {
        var trees = EnumerateSourceFiles(projectDirectory)
            .Select(file => CSharpSyntaxTree.ParseText(File.ReadAllText(file), path: file))
            .ToList();

        var metadataReferences = GetMetadataReferences(projectPath);
        var compilation = CSharpCompilation.Create(
            "PipetteVialScan",
            trees,
            metadataReferences,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var ivialSymbol = compilation.GetTypeByMetadataName("TestingVial.NET.IVial");
        if (ivialSymbol is null)
        {
            return new Dictionary<string, int>(StringComparer.Ordinal);
        }

        var occurrences = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var tree in trees)
        {
            var semanticModel = compilation.GetSemanticModel(tree);
            var root = tree.GetRoot();

            foreach (var attribute in root.DescendantNodes().OfType<AttributeSyntax>())
            {
                if (semanticModel.GetTypeInfo(attribute).Type is not INamedTypeSymbol attributeType)
                {
                    continue;
                }

                if (!string.Equals(attributeType.ContainingAssembly?.Name, "TestingVial.NET", StringComparison.Ordinal))
                {
                    continue;
                }

                if (!IsVialAttributeUsage(attributeType, ivialSymbol))
                {
                    continue;
                }

                occurrences[attributeType.Name] = occurrences.TryGetValue(attributeType.Name, out var existing) ? existing + 1 : 1;
            }
        }

        return occurrences;
    }

    private static bool IsVialAttributeUsage(INamedTypeSymbol attributeType, INamedTypeSymbol ivialSymbol)
    {
        if (!InheritsFromAttribute(attributeType))
        {
            return false;
        }

        if (ImplementsIVial(attributeType, ivialSymbol))
        {
            return true;
        }

        foreach (var typeArgument in attributeType.TypeArguments.OfType<INamedTypeSymbol>())
        {
            if (ImplementsIVial(typeArgument, ivialSymbol))
            {
                return true;
            }
        }

        if (!attributeType.IsGenericType)
        {
            return false;
        }

        foreach (var typeParameter in attributeType.OriginalDefinition.TypeParameters)
        {
            foreach (var constraintType in typeParameter.ConstraintTypes)
            {
                if (constraintType is INamedTypeSymbol namedConstraint && ImplementsIVial(namedConstraint, ivialSymbol))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool InheritsFromAttribute(INamedTypeSymbol symbol)
    {
        for (var current = symbol; current is not null; current = current.BaseType)
        {
            if (string.Equals(current.ToDisplayString(), "System.Attribute", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ImplementsIVial(INamedTypeSymbol symbol, INamedTypeSymbol ivialSymbol)
    {
        return SymbolEqualityComparer.Default.Equals(symbol, ivialSymbol)
            || symbol.AllInterfaces.Any(@interface => SymbolEqualityComparer.Default.Equals(@interface, ivialSymbol));
    }

    private static IEnumerable<MetadataReference> GetMetadataReferences(string projectPath)
    {
        var references = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var trustedPlatformAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;

        if (!string.IsNullOrWhiteSpace(trustedPlatformAssemblies))
        {
            foreach (var assembly in trustedPlatformAssemblies.Split(Path.PathSeparator))
            {
                if (File.Exists(assembly))
                {
                    references.Add(assembly);
                }
            }
        }

        references.Add(ResolveTestingVialAssemblyPath(projectPath));

        return references.Select(reference => MetadataReference.CreateFromFile(reference));
    }

    private static string ResolveTestingVialAssemblyPath(string projectPath)
    {
        var projectDirectory = Path.GetDirectoryName(projectPath)!;
        var assetsPath = Path.Combine(projectDirectory, "obj", "project.assets.json");
        if (!File.Exists(assetsPath))
        {
            throw new FileNotFoundException($"NuGet assets file not found: {assetsPath}");
        }

        using var stream = File.OpenRead(assetsPath);
        using var document = JsonDocument.Parse(stream);
        var root = document.RootElement;

        var packageNameWithVersion = root.GetProperty("libraries")
            .EnumerateObject()
            .Select(property => property.Name)
            .FirstOrDefault(name => name.StartsWith("TestingVial.NET/", StringComparison.OrdinalIgnoreCase));

        if (packageNameWithVersion is null)
        {
            throw new InvalidOperationException("Could not resolve TestingVial.NET package metadata.");
        }

        var packageVersion = packageNameWithVersion.Split('/', 2)[1];
        var packageRoot = root.GetProperty("packageFolders").EnumerateObject().First().Name;

        foreach (var target in root.GetProperty("targets").EnumerateObject())
        {
            if (!target.Value.TryGetProperty(packageNameWithVersion, out var packageTarget))
            {
                continue;
            }

            if (!packageTarget.TryGetProperty("compile", out var compileElement))
            {
                continue;
            }

            var relativeAssemblyPath = compileElement.EnumerateObject()
                .Select(property => property.Name)
                .FirstOrDefault(path => path.EndsWith("TestingVial.NET.dll", StringComparison.OrdinalIgnoreCase));

            if (relativeAssemblyPath is null)
            {
                continue;
            }

            var assemblyPath = Path.Combine(
                packageRoot,
                "testingvial.net",
                packageVersion,
                relativeAssemblyPath.Replace('/', Path.DirectorySeparatorChar));

            if (File.Exists(assemblyPath))
            {
                return assemblyPath;
            }
        }

        throw new FileNotFoundException("Could not locate TestingVial.NET assembly from NuGet assets.");
    }
}
