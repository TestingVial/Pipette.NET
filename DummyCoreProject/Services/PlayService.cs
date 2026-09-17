using DummyCoreProject.Domain;

namespace DummyCoreProject.Services;

public sealed class PlayService
{
    private readonly List<Play> plays = [];

    public IReadOnlyCollection<Play> Plays => plays.ToArray();

    public Play RecordPlay(Game game, IEnumerable<User> participants, DateTimeOffset? playedAt = null)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(participants);

        var participantList = participants.ToArray();
        if (participantList.Length < game.MinimumPlayers || participantList.Length > game.MaximumPlayers)
        {
            throw new ArgumentOutOfRangeException(
                nameof(participants),
                $"{game.Title} requires between {game.MinimumPlayers} and {game.MaximumPlayers} participants.");
        }

        var play = new Play(game, participantList, playedAt ?? DateTimeOffset.UtcNow);
        plays.Add(play);
        return play;
    }
}
