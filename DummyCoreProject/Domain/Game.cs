namespace DummyCoreProject.Domain;

public sealed class Game
{
    public Game(string title, int minimumPlayers, int maximumPlayers)
        : this(Guid.NewGuid(), title, minimumPlayers, maximumPlayers)
    {
    }

    public Game(Guid id, string title, int minimumPlayers, int maximumPlayers)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("A game must have a non-empty identifier.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("A game must have a title.", nameof(title));
        }

        if (minimumPlayers < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumPlayers), "A game must support at least one player.");
        }

        if (maximumPlayers < minimumPlayers)
        {
            throw new ArgumentException("The maximum number of players cannot be lower than the minimum.", nameof(maximumPlayers));
        }

        Id = id;
        Title = title.Trim();
        MinimumPlayers = minimumPlayers;
        MaximumPlayers = maximumPlayers;
    }

    public Guid Id { get; }

    public string Title { get; }

    public int MinimumPlayers { get; }

    public int MaximumPlayers { get; }
}
