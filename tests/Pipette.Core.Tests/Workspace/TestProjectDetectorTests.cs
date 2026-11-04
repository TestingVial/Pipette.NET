using Pipette.Core.Workspace;

namespace Pipette.Core.Tests.Workspace;

public class TestProjectDetectorTests
{
    private static bool Detect(bool? isTestProject, string[]? packages = null, string[]? assemblies = null) =>
        new TestProjectDetector().IsTestProject(new ProjectFacts(isTestProject, packages ?? [], assemblies ?? []));

    [Fact]
    public void IsTestProjectTrue_IsTestProject() => Assert.True(Detect(true));

    [Theory]
    [InlineData(null)]
    [InlineData(false)]
    public void NoFlagAndNoFrameworkReference_IsNotTestProject(bool? isTestProject) =>
        Assert.False(Detect(isTestProject, ["Newtonsoft.Json", "TestingVial.NET"], [@"C:\refs\System.Runtime.dll"]));

    [Theory]
    [InlineData("xunit")]
    [InlineData("xunit.v3")]
    [InlineData("nunit")]
    [InlineData("MSTest.TestFramework")]
    [InlineData("TUnit")]
    public void FrameworkPackageReference_IsTestProject(string package) => Assert.True(Detect(null, [package]));

    [Theory]
    [InlineData("/nuget/xunit.extensibility.core/2.9.3/lib/netstandard1.1/xunit.core.dll")]
    [InlineData("/nuget/nunit/4.0.0/lib/net8.0/nunit.framework.dll")]
    [InlineData("Microsoft.VisualStudio.TestPlatform.TestFramework.dll")]
    [InlineData("TUnit.Core.dll")]
    public void FrameworkMetadataReference_IsTestProject(string reference) => Assert.True(Detect(false, assemblies: [reference]));
}
