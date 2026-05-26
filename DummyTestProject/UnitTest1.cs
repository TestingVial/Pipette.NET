using TestingVial.NET;

namespace DummyTestProject;

public class Tests
{
    [SetUp]
    public void Setup()
    {
    }

    [Test]
    public void Test1()
    {
        var referencedTestingVialTypes = new[] { typeof(UnitTestVial), typeof(IVial) };
        Assert.That(referencedTestingVialTypes, Has.Length.EqualTo(2));
        Assert.Pass();
    }
}
