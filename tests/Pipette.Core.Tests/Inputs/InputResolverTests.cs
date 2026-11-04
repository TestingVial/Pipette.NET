using Pipette.Core.Diagnostics;
using Pipette.Core.Inputs;

namespace Pipette.Core.Tests.Inputs;

public sealed class InputResolverTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("pipette-inputs-").FullName;
    private readonly PipetteDiagnostics _diagnostics = new();

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string CreateFile(string relativePath, string content = "<Project />")
    {
        var path = Path.GetFullPath(relativePath, _root);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    private ResolvedInputs Resolve(params string[] inputs) => new InputResolver(_diagnostics).Resolve(inputs, _root);

    [Fact]
    public void Projects_AreDeduplicatedByFullPath_AndUseWorkingDirectoryAsBase()
    {
        var a = CreateFile("A/A.csproj");
        var b = CreateFile("B/B.csproj");

        var resolved = Resolve("A/A.csproj", b, Path.Combine("B", "..", "A", "A.csproj"), "./A/A.csproj");

        Assert.Equal([a, b], resolved.ProjectPaths);
        Assert.Equal(_root, resolved.BaseDirectory);
        Assert.Null(resolved.SolutionPath);
    }

    [Fact]
    public void Sln_ListsCsprojEntries_SkipsFoldersAndOtherLanguages_WarnsOnMissing()
    {
        var a = CreateFile("src/A/A.csproj");
        CreateFile("src/F/F.fsproj");
        var sln = CreateFile("Repo.sln", """
            Microsoft Visual Studio Solution File, Format Version 12.00
            Project("{2150E333-8FDC-42A3-9474-1A3956D46DE8}") = "src", "src", "{11111111-1111-1111-1111-111111111111}"
            EndProject
            Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "A", "src\A\A.csproj", "{22222222-2222-2222-2222-222222222222}"
            EndProject
            Project("{9A19103F-16F7-4668-BE54-9A1E7A4F7556}") = "A again", "src/A/A.csproj", "{33333333-3333-3333-3333-333333333333}"
            EndProject
            Project("{F2A71F9B-5D33-465A-A702-920D77279786}") = "F", "src\F\F.fsproj", "{44444444-4444-4444-4444-444444444444}"
            EndProject
            Project("{9A19103F-16F7-4668-BE54-9A1E7A4F7556}") = "Gone", "src\Gone\Gone.csproj", "{55555555-5555-5555-5555-555555555555}"
            EndProject
            """);

        var resolved = Resolve("Repo.sln");

        Assert.Equal([a], resolved.ProjectPaths);
        Assert.Equal(_root, resolved.BaseDirectory);
        Assert.Equal(sln, resolved.SolutionPath);
        Assert.Contains(_diagnostics.Messages, m => m.Severity == PipetteSeverity.Warning && m.Text.Contains("Gone.csproj"));
        Assert.Contains(_diagnostics.Messages, m => m.Severity == PipetteSeverity.Verbose && m.Text.Contains("F.fsproj"));
    }

    [Fact]
    public void Slnx_ListsNestedProjects_RelativeToSolutionFolder()
    {
        var a = CreateFile("repo/src/A/A.csproj");
        var t = CreateFile("repo/tests/T/T.csproj");
        CreateFile("repo/Repo.slnx", """
            <Solution>
              <Folder Name="/src/">
                <Project Path="src/A/A.csproj" />
              </Folder>
              <Folder Name="/tests/">
                <Project Path="tests\T\T.csproj" />
              </Folder>
              <Project Path="src/A/A.csproj" />
              <Folder Name="/Solution Items/">
                <File Path="global.json" />
              </Folder>
            </Solution>
            """);

        var resolved = Resolve("repo/Repo.slnx");

        Assert.Equal([a, t], resolved.ProjectPaths);
        Assert.Equal(Path.Combine(_root, "repo"), resolved.BaseDirectory);
    }

    [Theory]
    [InlineData("Missing.csproj")]
    [InlineData("Missing.sln")]
    [InlineData("Missing.slnx")]
    public void MissingInput_Throws(string input)
    {
        var ex = Assert.Throws<InputResolutionException>(() => Resolve(input));
        Assert.Contains("does not exist", ex.Message);
    }

    [Theory]
    [InlineData("Lib.vbproj")]
    [InlineData("global.json")]
    [InlineData("folder")]
    public void UnknownExtension_Throws(string input)
    {
        CreateFile(input);

        var ex = Assert.Throws<InputResolutionException>(() => Resolve(input));
        Assert.Contains("Unsupported input", ex.Message);
    }

    [Fact]
    public void SolutionCombinedWithOtherInputs_Throws()
    {
        CreateFile("A.csproj");
        CreateFile("One.slnx", "<Solution />");
        CreateFile("Two.sln", "");

        Assert.Throws<InputResolutionException>(() => Resolve("One.slnx", "A.csproj"));
        Assert.Throws<InputResolutionException>(() => Resolve("One.slnx", "Two.sln"));
    }

    [Fact]
    public void SameSolutionTwice_IsAccepted()
    {
        CreateFile("One.slnx", "<Solution />");

        var resolved = Resolve("One.slnx", "./One.slnx");

        Assert.Empty(resolved.ProjectPaths);
        Assert.Contains(_diagnostics.Messages, m => m.Severity == PipetteSeverity.Warning && m.Text.Contains("no C# projects"));
    }

    [Fact]
    public void NoInputs_Throws() =>
        Assert.Throws<InputResolutionException>(() => Resolve());

    [Fact]
    public void MalformedSlnx_Throws()
    {
        CreateFile("Bad.slnx", "<Solution>");

        Assert.Throws<InputResolutionException>(() => Resolve("Bad.slnx"));
    }
}
