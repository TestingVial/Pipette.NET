using DummyCoreProject.Domain;

namespace DummyTestProject;

public class PlayTests
{
    [Test]
    public void Play_WhenParticipantsAreEmpty_Throws()
    {
        var game = new Game("Chess", 2, 4);

        Assert.That(
            () => new Play(game, [], DateTimeOffset.UtcNow),
            Throws.ArgumentException);
    }
}
