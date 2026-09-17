using DummyCoreProject.Domain;
using DummyCoreProject.Services;

namespace DummyIntegrationTests;

public class IntegrationTest1
{
    [Fact]
    public void UserCanCollectAndPlayAGameWithOtherUsers()
    {
        var owner = new User("Alex");
        var friend = new User("Sam");
        var game = new Game("Chess", 2, 2);
        var collectionService = new GameCollectionService();
        var playService = new PlayService();

        collectionService.AddGame(owner, game);
        var play = playService.RecordPlay(
            game,
            [owner, friend],
            new DateTimeOffset(2025, 1, 1, 12, 0, 0, TimeSpan.Zero));

        Assert.True(collectionService.OwnsGame(owner, game.Id));
        Assert.Single(collectionService.GetCollection(owner).Games);
        Assert.Same(game, play.Game);
        Assert.Equal(2, play.Participants.Count);
        Assert.Single(playService.Plays);
        Assert.Same(play, Assert.Single(playService.Plays));
    }
}
