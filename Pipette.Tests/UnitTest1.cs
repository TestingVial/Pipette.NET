namespace Pipette.Tests;

public class PipetteToolTests
{
    [Test]
    public void Run_WhenNoArguments_ReturnsError()
    {
        var exitCode = PipetteTool.Run([], new StringWriter(), new StringWriter());

        Assert.That(exitCode, Is.EqualTo(1));
    }

    [Test]
    public void Run_WhenUnknownArgumentProvided_ReturnsError()
    {
        var exitCode = PipetteTool.Run(["--nope"], new StringWriter(), new StringWriter());

        Assert.That(exitCode, Is.EqualTo(1));
    }

    [Test]
    public void Run_WhenProjectProvided_GeneratesExpectedVialFile()
    {
        var repositoryRoot = FindRepositoryRoot();
        var projectPath = Path.Combine(repositoryRoot, "DummyTestProject", "DummyTestProject.csproj");
        var outputPath = Path.ChangeExtension(projectPath, ".vial");

        if (File.Exists(outputPath))
        {
            File.Delete(outputPath);
        }

        try
        {
            var exitCode = PipetteTool.Run(["--project", projectPath, "--vial-version", "2.0"], new StringWriter(), new StringWriter());

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(0));
                Assert.That(File.Exists(outputPath), Is.True);
            });

            var content = File.ReadAllText(outputPath);
            Assert.That(content, Does.Contain("vial-version: 2.0"));
            Assert.That(content, Does.Contain("project: DummyTestProject.csproj"));
            Assert.That(content, Does.Contain("- UnitTestVial: 1").Or.Contain("- UnitTestVialAttribute: 1"));
            Assert.That(content, Does.Not.Contain("- IVial:"));
        }
        finally
        {
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);

        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Pipette.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Could not find repository root.");
    }
}
