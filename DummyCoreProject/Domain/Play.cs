namespace DummyCoreProject.Domain;

public sealed class Play
{
    public Play(Game game, IEnumerable<User> participants, DateTimeOffset playedAt)
        : this(Guid.NewGuid(), game, participants, playedAt)
    {
    }

    public Play(Guid id, Game game, IEnumerable<User> participants, DateTimeOffset playedAt)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("A play must have a non-empty identifier.", nameof(id));
        }

        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(participants);

        var participantList = participants.ToArray();
        if (participantList.Length == 0)
        {
            throw new ArgumentException("A play must have at least one participant.", nameof(participants));
        }

        if (participantList.Any(participant => participant is null))
        {
            throw new ArgumentException("A play cannot contain a null participant.", nameof(participants));
        }

        if (participantList.Select(participant => participant.Id).Distinct().Count() != participantList.Length)
        {
            throw new ArgumentException("A play cannot contain the same participant more than once.", nameof(participants));
        }

        Id = id;
        Game = game;
        Participants = participantList;
        PlayedAt = playedAt;
    }

    public Guid Id { get; }

    public Game Game { get; }

    public DateTimeOffset PlayedAt { get; }

    public IReadOnlyCollection<User> Participants { get; }
}
