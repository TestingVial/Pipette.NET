using DummyCoreProject.Domain;
using DummyCoreProject.Services;

namespace DummyIntegrationTests;

public class RejectedOperationsTests
{
    [Fact]
    public void RejectedOperationsDoNotCorruptCollectionOrPlayHistory()
    {
        var owner = new User("Alex");
        var game = new Game("Chess", 2, 2);
        var collectionService = new GameCollectionService();
        var playService = new PlayService();

        collectionService.AddGame(owner, game);

        Assert.Throws<InvalidOperationException>(() => collectionService.AddGame(owner, game));
        Assert.Single(collectionService.GetCollection(owner).Games);

        var participants = new[]
        {
            owner,
            new User("Sam"),
            new User("Taylor")
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => playService.RecordPlay(game, participants));
        Assert.Empty(playService.Plays);
    }
}
