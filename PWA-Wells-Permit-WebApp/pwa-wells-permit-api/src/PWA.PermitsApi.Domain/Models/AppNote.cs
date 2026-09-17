namespace PWA.PermitsApi.Domain.Models;

public sealed class AppNote
{
    public string AppId { get; set; } = string.Empty;
    public int NoteId { get; set; }
    public string AddBy { get; set; } = string.Empty;
    public DateTime AddTs { get; set; }
    public string? NotesText { get; set; }
}
