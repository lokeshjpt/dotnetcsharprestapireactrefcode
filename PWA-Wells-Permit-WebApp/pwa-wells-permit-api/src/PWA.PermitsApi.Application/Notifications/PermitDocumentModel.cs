using PWA.PermitsApi.Application.DTOs;
using DomainApplication = PWA.PermitsApi.Domain.Models.Application;
using DomainPayment = PWA.PermitsApi.Domain.Models.AppPayment;

namespace PWA.PermitsApi.Application.Notifications;

/// <summary>
/// Everything the server-side permit PDF generator needs to render a Water Resources well permit
/// that mirrors the intra React <c>PermitPage</c> and the legacy <c>DisplayPdf</c> permit: the full
/// application graph (parties, works, specs), the issued permit rows, the payment, and the resolved
/// list of specific work-permit conditions. <see cref="Approved"/> switches between the approved
/// permit and the "PREVIEW" (not yet approved) rendering.
/// </summary>
public sealed record PermitDocumentModel(
    DomainApplication Application,
    PermitInfoDto? PermitInfo,
    DomainPayment? Payment,
    IReadOnlyList<string> Conditions,
    bool Approved);
