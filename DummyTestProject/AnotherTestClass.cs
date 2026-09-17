using TestingVial.NET;

namespace DummyTestProject;

[UnitTestVial("AnotherBusinessEntity", Description ="This test class represents another business entity")]
public class AnotherTestClass {

    [Test]
    public void AnotherTestMethod()
    {
        Assert.Pass();
    }

}
