using DummyCoreProject.Domain;
using DummyCoreProject.Services;

namespace DummyTestProject;

public class GameCollectionServiceTests
{
    [Test]
    public void GameCollectionService_WhenGameIsAdded_ReportsOwnership()
    {
        var service = new GameCollectionService();
        var user = new User("Alex");
        var game = new Game("Chess", 2, 2);

        service.AddGame(user, game);

        Assert.Multiple(() =>
        {
            Assert.That(service.OwnsGame(user, game.Id), Is.True);
            Assert.That(service.GetCollection(user).Games, Has.One.SameAs(game));
        });
    }

    [Test]
    public void GameCollectionService_WhenGameIsNotOwned_ReportsFalse()
    {
        var service = new GameCollectionService();
        var user = new User("Alex");

        Assert.That(service.OwnsGame(user, Guid.NewGuid()), Is.False);
    }

    [Test]
    public void GameCollectionService_CollectionsAreIndependentPerUser()
    {
        var service = new GameCollectionService();
        var firstUser = new User("Alex");
        var secondUser = new User("Sam");
        var game = new Game("Chess", 2, 2);

        service.AddGame(firstUser, game);

        Assert.Multiple(() =>
        {
            Assert.That(service.OwnsGame(firstUser, game.Id), Is.True);
            Assert.That(service.OwnsGame(secondUser, game.Id), Is.False);
            Assert.That(service.GetCollection(secondUser).Games, Is.Empty);
        });
    }

    [Test]
    public void GameCollectionService_WhenGameIsAddedTwice_LeavesOneGameInCollection()
    {
        var service = new GameCollectionService();
        var user = new User("Alex");
        var game = new Game("Chess", 2, 2);

        service.AddGame(user, game);

        Assert.That(() => service.AddGame(user, game), Throws.InvalidOperationException);
        Assert.That(service.GetCollection(user).Games, Has.Count.EqualTo(1));
    }
}
