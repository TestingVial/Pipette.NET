using DummyCoreProject.Domain;

namespace DummyCoreProject.Services;

public sealed class GameCollectionService
{
    private readonly Dictionary<Guid, GameCollection> collections = [];

    public GameCollection GetCollection(User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (!collections.TryGetValue(user.Id, out var collection))
        {
            collection = new GameCollection(user.Id);
            collections.Add(user.Id, collection);
        }

        return collection;
    }

    public void AddGame(User user, Game game)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(game);

        GetCollection(user).Add(game);
    }

    public bool OwnsGame(User user, Guid gameId)
    {
        ArgumentNullException.ThrowIfNull(user);

        return collections.TryGetValue(user.Id, out var collection) && collection.Contains(gameId);
    }
}
