using DummyCoreProject.Domain;

namespace DummyTestProject;

public class UserTests
{
    [Test]
    public void User_WhenNameIsBlank_Throws()
    {
        Assert.That(() => new User(" "), Throws.ArgumentException);
    }

    [Test]
    public void User_WhenIdIsEmpty_Throws()
    {
        Assert.That(() => new User(Guid.Empty, "Alex"), Throws.ArgumentException);
    }
}
