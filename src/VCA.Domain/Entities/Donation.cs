using VCA.Domain.Enums;

namespace VCA.Domain.Entities;

/// <summary>
/// Registro de doação feita por um usuário via Stripe ou Mercado Pago (Pix).
/// </summary>
public class Donation
{
    public Guid Id { get; private set; }
    public Guid? UserId { get; private set; }
    public decimal Amount { get; private set; }
    public DonationProvider Provider { get; private set; }
    public DonationStatus Status { get; private set; }
    public string? ExternalReference { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public User? User { get; private set; }

    private Donation() { }

    public static Donation Create(Guid? userId, decimal amount, DonationProvider provider, string? externalReference = null)
    {
        return new Donation
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Amount = amount,
            Provider = provider,
            ExternalReference = externalReference,
            Status = DonationStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Complete() => Status = DonationStatus.Completed;
    public void Fail() => Status = DonationStatus.Failed;
    public void Refund() => Status = DonationStatus.Refunded;
}
