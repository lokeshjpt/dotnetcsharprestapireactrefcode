# .NET — Design Patterns & Conventions

Patterns used consistently across the API. Grounded in `ApplicationService`, `ApplicationRepository`,
and the DTO/options code.

## 1. Repository pattern (Dapper, hand-written SQL)

- Interface in **Application** (`Interfaces/Repositories/IApplicationRepository`), implementation in
  **Persistence**. No ORM/change-tracking — raw parameterized SQL via Dapper.
- `DapperContext` is a tiny factory that opens an `IDbConnection` from the connection string; injected
  as a singleton. Repositories create a connection per call.
- Repositories return **Domain models** (or projections), never leak `IDbConnection`/SQL upward.
- **Always parameterize** (`@appId`) — never string-concatenate user input. Dynamic sort/paging goes
  through a whitelist (`SqlOrderBy` / `SortBy` keys) so unknown keys fall back to a safe default.

```csharp
public sealed class ApplicationRepository : IApplicationRepository
{
    private readonly DapperContext _context;
    public ApplicationRepository(DapperContext context) => _context = context;

    public async Task<DomainApplication?> GetByIdAsync(string appId, CancellationToken ct)
    {
        using var db = _context.CreateConnection();
        // multi-table load composed into the aggregate (application + works + specs + payment ...)
        // parameterized: new { appId }
    }
}
```

## 2. Service layer (orchestration + business rules)

- One `IXxxService` per aggregate; `sealed` implementation; constructor injection of repositories +
  integrations + `ILogger<T>`.
- Services own **business rules** (fee calculation, status transitions, validation that needs data),
  compose repositories/integrations, map Domain ⇄ DTO, and log meaningful events.
- Guard/validate at the top; throw `ArgumentException`/`InvalidOperationException` for rule violations
  (surfaced as 400/404 by controllers or the global handler).

```csharp
public async Task<ApplicationDto?> UpdateExtensionAsync(string appId, UpdateExtensionRequest req, string updatedBy, CancellationToken ct)
{
    if (req.ExtensionStartDate is null) throw new ArgumentException("Extension Start Date is required.");
    if (req.ExtensionEndDate < req.ExtensionStartDate) throw new ArgumentException("End must be ≥ Start.");
    await _applicationRepository.UpdateExtensionAsync(appId, req.ExtensionStartDate.Value, req.ExtensionEndDate.Value, req.IsEdit, updatedBy, ct);
    return await GetByIdAsync(appId, ct);   // return the refreshed aggregate
}
```

**Convention:** mutations re-load and return the refreshed `ApplicationDto` (so the SPA re-renders
from server truth). Business constants (status codes `PENDS`/`PENDC`/`PEND`, fees) are computed
server-side and never trusted from the client (e.g. `siteExtraRate` is looked up, not accepted).

## 3. DTOs as immutable records; `with` for normalization

Requests/responses are `sealed record`s with `init` properties. Normalize inputs functionally before
persisting:

```csharp
public sealed record ApplicationSearchRequest {
    public string? AppId { get; init; }
    public string? StatusCode { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 25;
    public string? SortBy { get; init; }     // whitelisted server-side
    public string? SortDir { get; init; }    // "asc" | "desc"
}

// normalize with `with`:
request = request with { OwnerPhone = PhoneNormalizer.DigitsOnly(request.OwnerPhone) };
```

## 4. Options pattern for configuration

Each config section binds to a POCO via `Configure<T>(section)` and is consumed with
`IOptions<T>`/`IOptionsMonitor<T>`. Integrations carry a `UseMock` flag:

```csharp
public sealed class CaptchaOptions { public string? SecretKey { get; set; } public string VerifyUrl { get; set; } = "..."; }
builder.Services.Configure<CaptchaOptions>(builder.Configuration.GetSection("Captcha"));
```

## 5. Integration gateways behind interfaces + named HttpClient + mock mode

External systems (`IEmailService`, `IIntelliPayGateway`, `IVirusScanner`, `IFileTransferService`,
`ICaptchaVerifier`) are Application interfaces implemented in Infrastructure. HTTP integrations use a
**named** `HttpClient` (`AddHttpClient(Xxx.HttpClientName)`). Each honors its `UseMock` option to run
offline (returns a canned success), keeping local dev and tests decoupled from the network.

## 6. Mapping

Explicit hand-written mapping (`Map(domain) => dto`) inside the service — no AutoMapper. Keeps the
Domain→DTO shape obvious and lets mapping enrich values (e.g. code → label from reference data).

## 7. Async + CancellationToken everywhere

Every I/O method is `async Task<...>` and takes a `CancellationToken` threaded from the controller
action parameter down through service → repository/integration.

## 8. Cross-cutting via filters/middleware (not in services)

Auth, captcha/allowlist guards, rate limiting, correlation id, audit emailing, and error handling are
**filters/middleware** in WebApi (see `dotnet-api-conventions.md`), keeping services focused on
domain logic.

## Naming conventions

| Thing | Convention |
|-------|------------|
| Interface | `IXxxService`, `IXxxRepository`, `IXxx` (integration) |
| Impl | `sealed class Xxx...`; ctor injection; one class per file |
| DTO | `sealed record`, `init` props; `XxxRequest` / `XxxDto` / `XxxResult` |
| Options | `XxxOptions` POCO bound to config section `Xxx` |
| Async | `...Async` suffix, returns `Task`, takes `CancellationToken` |
| SQL | parameterized (`@name`); dynamic order via whitelist |
