using DummyCoreProject.Domain;

namespace DummyTestProject;

public class GameCollectionTests
{
    [Test]
    public void GameCollection_WhenGameIsAddedTwice_Throws()
    {
        var collection = new GameCollection(Guid.NewGuid());
        var game = new Game("Chess", 2, 2);

        collection.Add(game);

        Assert.That(() => collection.Add(game), Throws.InvalidOperationException);
    }
}
