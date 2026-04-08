namespace VCA.Domain.Enums;

/// <summary>
/// Status do pagamento de uma doação.
/// </summary>
public enum DonationStatus
{
    Pending = 0,
    Completed = 1,
    Failed = 2,
    Refunded = 3
}
