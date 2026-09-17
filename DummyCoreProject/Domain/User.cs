namespace DummyCoreProject.Domain;

public sealed class User
{
    public User(string name)
        : this(Guid.NewGuid(), name)
    {
    }

    public User(Guid id, string name)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("A user must have a non-empty identifier.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A user must have a name.", nameof(name));
        }

        Id = id;
        Name = name.Trim();
    }

    public Guid Id { get; }

    public string Name { get; }
}
