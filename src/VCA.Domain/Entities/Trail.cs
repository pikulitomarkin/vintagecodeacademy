using VCA.Domain.Enums;

namespace VCA.Domain.Entities;

/// <summary>
/// Trilha de aprendizado — agrupa módulos em torno de uma stack ou tema.
/// </summary>
public class Trail
{
    public Guid Id { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string Stack { get; private set; } = string.Empty;
    public TrailLevel Level { get; private set; }
    public int Order { get; private set; }
    public bool IsPublished { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public ICollection<Module> Modules { get; private set; } = [];

    private Trail() { }

    public static Trail Create(string title, string description, string stack, TrailLevel level, int order)
    {
        return new Trail
        {
            Id = Guid.NewGuid(),
            Title = title,
            Description = description,
            Stack = stack,
            Level = level,
            Order = order,
            IsPublished = false,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Publish() => IsPublished = true;
    public void Unpublish() => IsPublished = false;

    public void Update(string title, string description, string stack, TrailLevel level, int order)
    {
        Title = title;
        Description = description;
        Stack = stack;
        Level = level;
        Order = order;
    }
}
