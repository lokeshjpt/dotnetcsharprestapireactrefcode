namespace PWA.PermitsApi.Application.Exceptions;

// Field-level validation failures (required / numeric / mutually-exclusive rules) — maps to HTTP 400.
public sealed class MaintenanceValidationException : Exception
{
    public IReadOnlyList<string> Errors { get; }

    public MaintenanceValidationException(IReadOnlyList<string> errors)
        : base(errors.Count > 0 ? errors[0] : "Validation failed.")
    {
        Errors = errors;
    }
}

// Duplicate key on add, or delete blocked by a foreign-key reference ("code is in use") — maps to 409.
public sealed class MaintenanceConflictException : Exception
{
    public MaintenanceConflictException(string message) : base(message) { }
}

// Update/delete against a code that does not exist — maps to 404.
public sealed class MaintenanceNotFoundException : Exception
{
    public MaintenanceNotFoundException(string message) : base(message) { }
}
