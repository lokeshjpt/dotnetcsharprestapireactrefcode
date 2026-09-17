namespace PWA.PermitsApi.Application.DTOs;

/// <summary>
/// Permit-specific extras the printable permit page needs on top of the application + payment +
/// conditions it already loads: the approver/approval date and the issued permit numbers.
/// Mirrors the data the legacy DisplayPdf permit pulls from APPLICATION_INFO and APP_WORK_PERMITS.
/// </summary>
public sealed record PermitInfoDto(
    string AppId,
    string? ApprovedBy,
    DateTime? ApprovedDate,
    IReadOnlyList<PermitLineDto> Permits);

/// <summary>A single issued permit row (APP_WORK_PERMITS) tied to a work / work-spec.</summary>
public sealed record PermitLineDto(
    string PermitNumber,
    int WorkId,
    int? WorkSpecsId,
    DateTime? IssuedDate,
    DateTime? ExpireDate,
    string StatusCode);
