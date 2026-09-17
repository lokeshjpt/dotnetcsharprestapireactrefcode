using PWA.PermitsApi.Application.DTOs;

namespace PWA.PermitsApi.Application.Interfaces;

public interface IApplicationService
{
    Task<ApplicationDto?> GetByIdAsync(string appId, CancellationToken cancellationToken = default);
    Task<ApplicationDto> SubmitAsync(SubmitApplicationRequest request, CancellationToken cancellationToken = default);
    Task<ApplicationSearchResult> SearchAsync(ApplicationSearchRequest request, CancellationToken cancellationToken = default);

    /// <summary>Applies a staff edit to the "Project Information" section, then returns the refreshed application (null if not found).</summary>
    Task<ApplicationDto?> UpdateProjectInfoAsync(string appId, UpdateProjectInfoRequest request, string updatedBy, CancellationToken cancellationToken = default);

    /// <summary>Applies a staff edit to the "Applicant Information" section, then returns the refreshed application (null if not found).</summary>
    Task<ApplicationDto?> UpdateApplicantInfoAsync(string appId, UpdateApplicantInfoRequest request, string updatedBy, CancellationToken cancellationToken = default);

    /// <summary>Applies a staff edit to the "Site Hazard Information" section, then returns the refreshed application (null if not found).</summary>
    Task<ApplicationDto?> UpdateHazardAsync(string appId, UpdateHazardRequest request, string updatedBy, CancellationToken cancellationToken = default);

    /// <summary>Applies a staff edit to a "Work Requesting Permit" section, then returns the refreshed application (null if not found).</summary>
    Task<ApplicationDto?> UpdateWorkAsync(string appId, int workId, UpdateWorkRequest request, string updatedBy, CancellationToken cancellationToken = default);

    /// <summary>Adds a new work (staff "Add Work") from a work category + type, then returns the refreshed application (null if the application is not found). Throws InvalidOperationException when the application is in a terminal state or the work type is unknown.</summary>
    Task<ApplicationDto?> AddWorkAsync(string appId, AddWorkRequest request, string addedBy, CancellationToken cancellationToken = default);

    /// <summary>Cancels a single work (sets CAN on the work + its specs), then returns the refreshed application (null if not found). Throws InvalidOperationException when the work cannot be cancelled in the application's current state.</summary>
    Task<ApplicationDto?> CancelWorkAsync(string appId, int workId, string updatedBy, CancellationToken cancellationToken = default);

    /// <summary>Deletes a single not-yet-approved work (and its specs/conditions), then returns the refreshed application (null if not found). Throws InvalidOperationException when it is the last work or the application is already approved/cancelled.</summary>
    Task<ApplicationDto?> DeleteWorkAsync(string appId, int workId, string updatedBy, CancellationToken cancellationToken = default);

    /// <summary>Saves the "Enter WCR" Well Completion Report grid for a work (approved applications only), then returns the refreshed application. Throws when the application is not approved or not found.</summary>
    Task<ApplicationDto?> UpdateWcrAsync(string appId, int workId, WcrUpdateRequest request, string updatedBy, CancellationToken cancellationToken = default);

    /// <summary>Persists the Approval Wizard workflow fields (site-visit type / sitemap received date), then returns the refreshed application (null if not found).</summary>
    Task<ApplicationDto?> UpdateApprovalDetailsAsync(string appId, UpdateApprovalDetailsRequest request, string updatedBy, CancellationToken cancellationToken = default);

    /// <summary>Cancels an application that is not yet approved (sets CAN on payment/works/application + sends a cancellation email), then returns the refreshed application (null if not found). Throws InvalidOperationException when the application cannot be cancelled in its current state.</summary>
    Task<ApplicationDto?> CancelAsync(string appId, string cancelledBy, CancellationToken cancellationToken = default);

    /// <summary>Returns the permit-specific extras (approver/date + issued permit numbers) for the printable permit, or null when the application does not exist.</summary>
    Task<PermitInfoDto?> GetPermitInfoAsync(string appId, CancellationToken cancellationToken = default);

    /// <summary>Lists the uploaded documents (APP_DOCUMENT_LINKS + DOCUMENT_TYPES description) for the staff "Upload Documents" screen.</summary>
    Task<IReadOnlyList<AppDocumentDto>> GetDocumentsAsync(string appId, CancellationToken cancellationToken = default);

    /// <summary>Deletes a single uploaded document (APP_DOCUMENT_LINKS row) by SEQ_NUM. Returns false when the row does not exist.</summary>
    Task<bool> DeleteDocumentAsync(string appId, int seqNum, CancellationToken cancellationToken = default);

    /// <summary>Lists the free-text notes (INSPECTION_NOTES) for the staff "View/Add Notes" screen, newest first.</summary>
    Task<IReadOnlyList<AppNoteDto>> GetNotesAsync(string appId, CancellationToken cancellationToken = default);

    /// <summary>Adds a free-text note (INSPECTION_NOTES) recorded against the acting staff user, returning the created note.</summary>
    Task<AppNoteDto> AddNoteAsync(string appId, string notesText, string addedBy, CancellationToken cancellationToken = default);

    /// <summary>Records a post-approval permit extension (legacy proc=extendu): validates the dates, updates APPLICATION_INFO.extend_*, writes the note trail, and returns the refreshed application (null when unknown).</summary>
    Task<ApplicationDto?> UpdateExtensionAsync(string appId, UpdateExtensionRequest request, string updatedBy, CancellationToken cancellationToken = default);
}
