namespace DummyCoreProject.Domain;

public sealed class GameCollection
{
    private readonly Dictionary<Guid, Game> games = [];

    public GameCollection(Guid ownerId)
    {
        if (ownerId == Guid.Empty)
        {
            throw new ArgumentException("A collection must have an owner.", nameof(ownerId));
        }

        OwnerId = ownerId;
    }

    public Guid OwnerId { get; }

    public IReadOnlyCollection<Game> Games => games.Values.ToArray();

    public void Add(Game game)
    {
        ArgumentNullException.ThrowIfNull(game);

        if (!games.TryAdd(game.Id, game))
        {
            throw new InvalidOperationException("The game is already in the collection.");
        }
    }

    public bool Contains(Guid gameId) => games.ContainsKey(gameId);
}
