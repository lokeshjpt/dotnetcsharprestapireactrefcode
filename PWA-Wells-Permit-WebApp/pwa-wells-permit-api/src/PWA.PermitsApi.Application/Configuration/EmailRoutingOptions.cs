namespace PWA.PermitsApi.Application.Configuration;

/// <summary>
/// Models the legacy emailoutput.properties file: a set of per-project email
/// routing definitions indexed 0..numberOfProjects-1.
/// </summary>
public sealed class EmailRoutingOptions
{
    public int NumberOfProjects { get; set; }
    public string? LogLocation { get; set; }
    public string? LogRotation { get; set; }
    public bool GlobalTraceEnabled { get; set; }

    public List<EmailProjectOptions> Projects { get; set; } = new();

    public EmailProjectOptions? FindProject(string projectId) =>
        Projects.FirstOrDefault(p => string.Equals(p.ProjectId, projectId, StringComparison.OrdinalIgnoreCase));
}

public sealed class EmailProjectOptions
{
    public string ProjectId { get; set; } = string.Empty;
    public string? RecipientName { get; set; }
    public string? EmailServer { get; set; }
    public string? DefaultFrom { get; set; }
    public string? DefaultTo { get; set; }
    public string? DefaultCc { get; set; }
    public string? DefaultBcc { get; set; }
    public string? DefaultSubject { get; set; }
    public string? BackupCopyLocation { get; set; }
    public bool DebugMail { get; set; }
}
