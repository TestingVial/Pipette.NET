using TestingVial.NET;

namespace DummyTestProject;

public class TestingVialTests
{
    [Test]
    [UnitTestVial<SampleVial>]
    public void Test1()
    {
        var referencedTestingVialTypes = new[] { typeof(UnitTestVial<>), typeof(IVial), typeof(SampleVial) };
        Assert.That(referencedTestingVialTypes, Has.Length.EqualTo(3));
        Assert.Pass();
    }
}

public sealed class SampleVial : IVial
{
    public string Name => nameof(SampleVial);
}
