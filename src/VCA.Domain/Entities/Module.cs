namespace VCA.Domain.Entities;

/// <summary>
/// Módulo de uma trilha — agrupa aulas sequenciais.
/// </summary>
public class Module
{
    public Guid Id { get; private set; }
    public Guid TrailId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public int Order { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public Trail? Trail { get; private set; }
    public ICollection<Lesson> Lessons { get; private set; } = [];

    private Module() { }

    public static Module Create(Guid trailId, string title, int order)
    {
        return new Module
        {
            Id = Guid.NewGuid(),
            TrailId = trailId,
            Title = title,
            Order = order,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(string title, int order)
    {
        Title = title;
        Order = order;
    }
}
