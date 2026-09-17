using DummyCoreProject.Domain;
using DummyCoreProject.Services;

namespace DummyIntegrationTests;

public class CollectionIsolationTests
{
    [Fact]
    public void UsersHaveIndependentGameCollections()
    {
        var firstUser = new User("Alex");
        var secondUser = new User("Sam");
        var game = new Game("Chess", 2, 2);
        var collectionService = new GameCollectionService();

        collectionService.AddGame(firstUser, game);

        Assert.True(collectionService.OwnsGame(firstUser, game.Id));
        Assert.False(collectionService.OwnsGame(secondUser, game.Id));
        Assert.Empty(collectionService.GetCollection(secondUser).Games);
    }
}
