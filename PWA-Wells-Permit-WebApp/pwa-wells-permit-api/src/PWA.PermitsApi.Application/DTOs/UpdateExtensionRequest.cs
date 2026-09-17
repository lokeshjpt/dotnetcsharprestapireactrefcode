namespace PWA.PermitsApi.Application.DTOs;

/// <summary>
/// Staff "New/Edit Post Approval Extension" (legacy UpdateAppServlet proc=extendu). Records a new
/// permit extension window on APPLICATION_INFO. On a new extension the extend_count is incremented;
/// on an edit the existing count is kept. A note is written to INSPECTION_NOTES mirroring the legacy
/// "Ext. Count: X  From: ...  To: ..." entry.
/// </summary>
public sealed record UpdateExtensionRequest
{
    /// <summary>New extension start date (required).</summary>
    public DateTime? ExtensionStartDate { get; init; }

    /// <summary>New extension end date (required, must be on/after the start date).</summary>
    public DateTime? ExtensionEndDate { get; init; }

    /// <summary>True to edit the current extension in place (keep the count); false (default) to add a new extension and increment the count.</summary>
    public bool IsEdit { get; init; }
}
