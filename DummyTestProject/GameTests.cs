using DummyCoreProject.Domain;

namespace DummyTestProject;

public class GameTests
{
    [Test]
    public void Game_WhenMaximumPlayersIsLowerThanMinimum_Throws()
    {
        Assert.That(
            () => new Game("Chess", 2, 1),
            Throws.ArgumentException);
    }

    [Test]
    public void Game_WhenTitleIsBlank_Throws()
    {
        Assert.That(() => new Game(" ", 2, 4), Throws.ArgumentException);
    }

    [Test]
    public void Game_WhenMinimumPlayersIsZero_Throws()
    {
        Assert.That(() => new Game("Chess", 0, 4), Throws.TypeOf<ArgumentOutOfRangeException>());
    }
}
