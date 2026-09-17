using TestingVial.NET;
using DummyCoreProject.Domain;
using DummyCoreProject.Services;

namespace DummyTestProject;

public class Tests
{
    [SetUp]
    public void Setup()
    {
    }

    [Test]
    [UnitTestVial<SampleVial>]
    public void Test1()
    {
        var referencedTestingVialTypes = new[] { typeof(UnitTestVial<>), typeof(IVial), typeof(SampleVial) };
        Assert.That(referencedTestingVialTypes, Has.Length.EqualTo(3));
        Assert.Pass();
    }

    [Test]
    public void Game_WhenMaximumPlayersIsLowerThanMinimum_Throws()
    {
        Assert.That(
            () => new Game("Chess", 2, 1),
            Throws.ArgumentException);
    }

    [Test]
    public void GameCollection_WhenGameIsAddedTwice_Throws()
    {
        var collection = new GameCollection(Guid.NewGuid());
        var game = new Game("Chess", 2, 2);

        collection.Add(game);

        Assert.That(() => collection.Add(game), Throws.InvalidOperationException);
    }

    [Test]
    public void PlayService_WhenParticipantCountIsOutsideGameLimits_Throws()
    {
        var service = new PlayService();
        var game = new Game("Chess", 2, 2);
        var participant = new User("Alex");

        Assert.That(
            () => service.RecordPlay(game, [participant]),
            Throws.TypeOf<ArgumentOutOfRangeException>());
    }

    [Test]
    public void PlayService_WhenParticipantIsRepeated_Throws()
    {
        var service = new PlayService();
        var game = new Game("Chess", 2, 2);
        var participant = new User("Alex");

        Assert.That(
            () => service.RecordPlay(game, [participant, participant]),
            Throws.ArgumentException);
    }

    [Test]
    public void PlayService_WhenPlayIsValid_RecordsPlay()
    {
        var service = new PlayService();
        var game = new Game("Chess", 2, 2);
        var participants = new[] { new User("Alex"), new User("Sam") };
        var playedAt = new DateTimeOffset(2025, 1, 1, 12, 0, 0, TimeSpan.Zero);

        var play = service.RecordPlay(game, participants, playedAt);

        Assert.Multiple(() =>
        {
            Assert.That(play.Game, Is.SameAs(game));
            Assert.That(play.Participants, Has.Count.EqualTo(2));
            Assert.That(play.PlayedAt, Is.EqualTo(playedAt));
            Assert.That(service.Plays, Has.Count.EqualTo(1));
        });
    }
}
public sealed class SampleVial : IVial
{
    public string Name => nameof(SampleVial);
}
