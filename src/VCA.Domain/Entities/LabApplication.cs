using VCA.Domain.Enums;

namespace VCA.Domain.Entities;

/// <summary>
/// Candidatura de um usuário a um projeto do VCA Labs.
/// </summary>
public class LabApplication
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid ProjectId { get; private set; }
    public LabApplicationStatus Status { get; private set; }
    public DateTime AppliedAt { get; private set; }

    public User? User { get; private set; }
    public LabProject? Project { get; private set; }

    private LabApplication() { }

    public static LabApplication Create(Guid userId, Guid projectId)
    {
        return new LabApplication
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ProjectId = projectId,
            Status = LabApplicationStatus.Pending,
            AppliedAt = DateTime.UtcNow
        };
    }

    public void Accept() => Status = LabApplicationStatus.Accepted;
    public void Reject() => Status = LabApplicationStatus.Rejected;
    public void Withdraw() => Status = LabApplicationStatus.Withdrawn;
}
