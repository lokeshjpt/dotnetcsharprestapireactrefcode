using PWA.PermitsApi.Application.Notifications;

namespace PWA.PermitsApi.Application.Interfaces.Integration;

/// <summary>
/// Renders the printable Water Resources well permit to a PDF byte stream so it can be attached to
/// the approval notification email (legacy intra ProcessApprovalServlet attached the permit PDF +
/// site map). The rendering matches the intra React permit page.
/// </summary>
public interface IPermitDocumentService
{
    /// <summary>Builds a PDF for the supplied permit model and returns the raw bytes.</summary>
    byte[] GeneratePermitPdf(PermitDocumentModel model);
}
