namespace PWA.PermitsApi.Application.Interfaces.Integration;

/// <summary>
/// Result of the $0 IntelliPay vault/pre-authorization. The returned <see cref="CustomerId"/>
/// (custid) is stored encrypted in AUTH_ID_ENCR and later used by the intra app to charge the
/// real permit fee. The card is never charged during the vault (amount = 0).
/// </summary>
public sealed record IntelliPayVaultResult(string? CustomerId, bool Approved, string? DeclineReason = null);
