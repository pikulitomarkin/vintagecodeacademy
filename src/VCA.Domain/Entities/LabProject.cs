namespace VCA.Domain.Entities;

/// <summary>
/// Projeto open-source do VCA Labs disponível para candidaturas de alunos.
/// </summary>
public class LabProject
{
    public Guid Id { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string Stack { get; private set; } = string.Empty;
    public string Status { get; private set; } = "open";
    public int SlotsAvailable { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public ICollection<LabApplication> Applications { get; private set; } = [];

    private LabProject() { }

    public static LabProject Create(string title, string description, string stack, int slotsAvailable)
    {
        return new LabProject
        {
            Id = Guid.NewGuid(),
            Title = title,
            Description = description,
            Stack = stack,
            SlotsAvailable = slotsAvailable,
            Status = "open",
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Close() => Status = "closed";
    public void Open() => Status = "open";
    public void DecrementSlot()
    {
        if (SlotsAvailable > 0)
            SlotsAvailable--;
    }
}
