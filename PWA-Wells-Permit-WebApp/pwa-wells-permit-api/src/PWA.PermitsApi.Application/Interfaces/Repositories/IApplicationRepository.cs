using PWA.PermitsApi.Application.DTOs;
using PWA.PermitsApi.Domain.Models;
using DomainApplication = PWA.PermitsApi.Domain.Models.Application;

namespace PWA.PermitsApi.Application.Interfaces.Repositories;

public interface IApplicationRepository
{
    Task<DomainApplication?> GetByIdAsync(string appId, CancellationToken cancellationToken = default);
    Task<DomainApplication> CreateAsync(DomainApplication application, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DomainApplication>> SearchAsync(ApplicationSearchRequest request, CancellationToken cancellationToken = default);

    /// <summary>Total number of applications matching the search filters, ignoring paging. Drives the server-side pagination controls (total pages and the "N applications found" count).</summary>
    Task<int> CountAsync(ApplicationSearchRequest request, CancellationToken cancellationToken = default);
    Task UpdateStatusAsync(string appId, string statusCode, string updatedBy, CancellationToken cancellationToken = default);

    /// <summary>Approves the application on APPLICATION_INFO: sets status_code plus approved_by/approved_date (and update_by/update_ts), mirroring legacy BeanApp.updateApprove so the approver and approval date are persisted.</summary>
    Task UpdateApprovalAsync(string appId, string statusCode, string approvedBy, CancellationToken cancellationToken = default);

    /// <summary>Sets the status_code on all APP_WORKS and APP_WORK_SPECS rows for the application (used on approval, matching legacy ProcessApprovalServlet which sets works/specs to APPRV).</summary>
    Task UpdateWorkStatusesAsync(string appId, string statusCode, string updatedBy, CancellationToken cancellationToken = default);

    /// <summary>Inserts an APP_DOCUMENT_LINKS row, assigning the next SEQ_NUM for the application. Returns the assigned SEQ_NUM.</summary>
    Task<int> AddDocumentLinkAsync(AppDocumentLink link, string addedBy, CancellationToken cancellationToken = default);

    /// <summary>Lists the uploaded documents (APP_DOCUMENT_LINKS) for an application, joined to DOCUMENT_TYPES for the type description, ordered like the legacy intra list. Used by the staff "Upload Documents" screen.</summary>
    Task<IReadOnlyList<AppDocumentDto>> GetDocumentsAsync(string appId, CancellationToken cancellationToken = default);

    /// <summary>Deletes a single APP_DOCUMENT_LINKS row by application + SEQ_NUM. Returns true when a row was removed.</summary>
    Task<bool> DeleteDocumentLinkAsync(string appId, int seqNum, CancellationToken cancellationToken = default);

    /// <summary>Lists the free-text notes (INSPECTION_NOTES) for an application, newest first. Used by the staff "View/Add Notes" screen (parity with legacy BeanInspectionNotes).</summary>
    Task<IReadOnlyList<AppNoteDto>> GetNotesAsync(string appId, CancellationToken cancellationToken = default);

    /// <summary>Inserts a free-text note into INSPECTION_NOTES, assigning the next SEQ_NUM for the application. Returns the created note.</summary>
    Task<AppNoteDto> AddNoteAsync(string appId, string notesText, string addedBy, CancellationToken cancellationToken = default);

    /// <summary>Records a post-approval permit extension on APPLICATION_INFO (extend_start/end/count/by) and writes the matching INSPECTION_NOTES trail. New extensions increment the count; edits keep it.</summary>
    Task UpdateExtensionAsync(string appId, DateTime startDate, DateTime endDate, bool isEdit, string updatedBy, CancellationToken cancellationToken = default);

    /// <summary>Records a received site map on APPLICATION_INFO (sitemap_filename + sitemap_received_date), matching legacy BeanApp.updateSitemap.</summary>
    Task UpdateSitemapAsync(string appId, string sitemapFilename, string updatedBy, CancellationToken cancellationToken = default);

    /// <summary>Updates the Approval Wizard workflow columns on APPLICATION_INFO: site_visit_type and sitemap_received_date.</summary>
    Task UpdateApprovalDetailsAsync(string appId, UpdateApprovalDetailsRequest request, string updatedBy, CancellationToken cancellationToken = default);

    /// <summary>Updates the project/site + owner + client columns on APPLICATION_INFO (staff edit of the "Project Information" section).</summary>
    Task UpdateProjectInfoAsync(string appId, UpdateProjectInfoRequest request, string updatedBy, CancellationToken cancellationToken = default);

    /// <summary>Updates the applicant + contact columns on APPLICATION_INFO (staff edit of the "Applicant Information" section).</summary>
    Task UpdateApplicantInfoAsync(string appId, UpdateApplicantInfoRequest request, string updatedBy, CancellationToken cancellationToken = default);

    /// <summary>Updates the consultant/safety-officer/facility columns on APP_HAZARD_INFO (staff edit of the "Site Hazard Information" section).</summary>
    Task UpdateHazardAsync(string appId, UpdateHazardRequest request, string updatedBy, CancellationToken cancellationToken = default);

    /// <summary>Updates a single APP_WORKS row and its APP_WORK_SPECS numeric fields (staff edit of a "Work Requesting Permit" section).</summary>
    Task UpdateWorkAsync(string appId, int workId, UpdateWorkRequest request, string updatedBy, CancellationToken cancellationToken = default);

    /// <summary>Adds a new APP_WORKS row (staff "Add Work"): assigns the next work_id for the application, seeds the fee columns from the WORK_TYPES lookup for the chosen category + type, and starts the row at status PENDC. Mirrors legacy UpdateAppServlet proc=wrktp. Throws when the work type is unknown.</summary>
    Task AddWorkAsync(string appId, AddWorkRequest request, string addedBy, CancellationToken cancellationToken = default);

    /// <summary>Cancels a single work: sets status_code = 'CAN' on the APP_WORKS row and its APP_WORK_SPECS rows. Mirrors legacy UpdateAppServlet proc=canwrk. Returns false when the work does not exist.</summary>
    Task<bool> CancelWorkAsync(string appId, int workId, string updatedBy, CancellationToken cancellationToken = default);

    /// <summary>Hard-deletes a single work and its APP_WORK_SPECS rows. Used by the staff "Delete Work" action for a not-yet-approved work. Returns false when the work does not exist.</summary>
    Task<bool> DeleteWorkAsync(string appId, int workId, CancellationToken cancellationToken = default);

    /// <summary>Saves the Well Completion Report (WCR / legacy "DWR") data-entry grid for a work: State Well #, WCR #, Construction Permit #/WCR #, and the shared-WCR flag on each APP_WORK_SPECS row. Mirrors legacy BeanAppWrkSpecs.updDWR.</summary>
    Task UpdateWcrAsync(string appId, int workId, WcrUpdateRequest request, string updatedBy, CancellationToken cancellationToken = default);

    /// <summary>Records an uploaded file name against a single APP_WORK_SPECS row in the given column (geolog_file or dwr_image). Mirrors legacy BeanAppWrkSpecs.updateGeologFile / updateDwrImage.</summary>
    Task UpdateSpecFileAsync(string appId, int workId, int workSpecsId, string column, string fileName, string updatedBy, CancellationToken cancellationToken = default);

    /// <summary>Cancels the whole application in one transaction: sets status_code = 'CAN' on APP_PAYMENT_INFO, APP_WORKS, APP_WORK_SPECS and APPLICATION_INFO. Mirrors legacy ApplicationBean.cancelApplication.</summary>
    Task CancelApplicationAsync(string appId, string cancelledBy, CancellationToken cancellationToken = default);

    /// <summary>Returns the approver/approval date plus the issued permit rows (APP_WORK_PERMITS) for the printable permit. Null when the application does not exist.</summary>
    Task<PermitInfoDto?> GetPermitInfoAsync(string appId, CancellationToken cancellationToken = default);

    /// <summary>Global per-well site-extra rate (WORK_TYPES row work_category='system', work_type='siteExtra').
    /// Charged for each well drilled beyond a tiered "site" work type's site_max. Returns 0 when the row is absent.</summary>
    Task<decimal> GetSiteExtraRateAsync(CancellationToken cancellationToken = default);
}
