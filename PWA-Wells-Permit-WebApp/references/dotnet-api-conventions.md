# .NET — API Conventions (Controllers, Auth, Cross-cutting)

How the WebApi layer is wired. All grounded in `Program.cs` and `ApplicationController`.

## Controllers are thin

`[ApiController]`, attribute-routed at `api/<resource>`, `sealed`, constructor-inject services, return
`ActionResult<TDto>`. No business logic — delegate to services. Thread the `CancellationToken` from the
action parameter. Map results to status codes (`Ok`, `NotFound`, `CreatedAtAction`).

```csharp
[ApiController]
[Route("api/applications")]
public sealed class ApplicationController : ControllerBase
{
    private readonly IApplicationService _applicationService;
    public ApplicationController(IApplicationService applicationService) => _applicationService = applicationService;

    [HttpGet("{appId}")]
    public async Task<ActionResult<ApplicationDto>> GetById(string appId, CancellationToken ct)
    {
        var app = await _applicationService.GetByIdAsync(appId, ct);
        return app is null ? NotFound() : Ok(app);
    }

    [HttpPost]
    [ServiceFilter(typeof(PublicAccessGuardFilter))]                 // public write → captcha/token guard
    public async Task<ActionResult<ApplicationDto>> Submit([FromBody] SubmitApplicationRequest req, CancellationToken ct)
    {
        var created = await _applicationService.SubmitAsync(req, ct);
        return CreatedAtAction(nameof(GetById), new { appId = created.AppId }, created);
    }

    [HttpPut("{appId}/project")]
    [Authorize]                                                     // staff-only edit
    public async Task<ActionResult<ApplicationDto>> UpdateProject(string appId, [FromBody] UpdateProjectInfoRequest req, CancellationToken ct)
    {
        var updated = await _applicationService.UpdateProjectInfoAsync(appId, req, ResolveActingUser(), ct);
        return updated is null ? NotFound() : Ok(updated);
    }
}
```

## The dual-client security model (the key decision)

One API serves an **anonymous public SPA** and an **authenticated staff SPA**. For every endpoint,
choose one of three protections:

| Kind | Attribute | Enforced by | Failure |
|------|-----------|-------------|---------|
| **Staff** | `[Authorize]` | Entra JWT (`AzureAD` scheme) **+** global `WhitelistAuthorizationFilter` (DB allowlist `EEAOWN.app_users`, memory-cached) | 401 (no token) / 403 (not allow-listed) |
| **Public write** | `[ServiceFilter(typeof(PublicAccessGuardFilter))]` | Any registered `IPublicAccessProof` (cheapest-first): trusted Entra bearer **OR** Google reCAPTCHA `X-Captcha-Token` | 401 `verification_required` |
| **Open read** | (none) | Per-IP sliding-window rate limiter (scoped by `PublicRateLimit.AppliesTo`) | 429 |

- The acting user for audit columns comes from the **Entra identity** (`ResolveActingUser()` reads
  `ClaimTypes.Name`), never from the request body.
- **Fail-open captcha**: blank `Captcha:SecretKey` disables the guard (dev). **Fail-open allowlist**:
  blank `Auth:TrustedClientIds` means any validated Entra token is a trusted client.
- Guarded/authorized routes are **exempt** from the rate limiter; it only throttles open public reads.

## Authentication setup (Entra / Azure AD)

```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
  .AddJwtBearer("AzureAD", o => {
      o.Authority = $"https://login.microsoftonline.com/{tenantId}";
      o.Audience  = $"api://{clientId}";
      o.TokenValidationParameters = new() {
          ValidateIssuer = true, ValidIssuer = $"https://sts.windows.net/{tenantId}/",
          ValidateAudience = true, ValidAudience = $"api://{clientId}", ValidateLifetime = true };
  });
builder.Services.AddAuthorization(o =>
  o.DefaultPolicy = new AuthorizationPolicyBuilder()
      .RequireAuthenticatedUser().AddAuthenticationSchemes("AzureAD").Build());
```

## CORS

`Cors:EcommOrigin` / `Cors:IntraOrigin` each hold a **comma-separated** origin list (scheme+host+port,
no trailing slash — exact match). Split, trim, dedupe into one `PortalClients` policy. Same-origin
reverse-proxy deployments never hit CORS; the lists keep direct cross-origin calls working.

## Rate limiting

Partitioned sliding-window limiter keyed on client IP, applied only to open public endpoints
(`PublicRateLimit.AppliesTo(endpoint)`); disabled when `RateLimit:Enabled` is false. Rejections return
`429` as `application/problem+json`. Config: `PermitLimit`, `WindowSeconds`, `SegmentsPerWindow`,
`QueueLimit`.

## Error handling & ProblemDetails

- `AddProblemDetails()` + `AddExceptionHandler<GlobalExceptionHandler>()`; `app.UseExceptionHandler()`
  turns unhandled exceptions into RFC-7807 `ProblemDetails`.
- `ErrorStatusAuditMiddleware` observes final `401/403/429` (which auth middleware returns without
  throwing) and audit-emails them (method/path, status, resolved user or `(anonymous)`, caller IP,
  correlation id).

## Logging & correlation

- **Serilog** to a daily rolling file (`Logs/log-<date>.txt`, keep 7 days), `Enrich.FromLogContext()`.
- `CorrelationIdMiddleware` assigns/propagates `X-Correlation-ID` and pushes it into the log context so
  `{CorrelationId}` appears on every line. Log meaningful domain events at Information
  (`"Submitting application {AppId}"`), warnings for best-effort failures.

## JSON leniency

`LenientStringConverter` + `NumberHandling = AllowReadingFromString` so loosely-typed client JSON
(e.g. a numeric `appId`) binds instead of 400-ing. Serialization output is unaffected.

## API docs

Swagger UI + Scalar (`/scalar`) render the same Swashbuckle OpenAPI doc; both default **on** for
`local`/`Development` only, overridable via `Swagger:Enabled` / `Scalar:Enabled`. Swagger has a Bearer
security definition so protected endpoints can be exercised with a pasted token.

## Endpoint checklist (new endpoint)

1. Pick protection: `[Authorize]` (staff) / `PublicAccessGuardFilter` (public write) / none + rate-limit
   (open read).
2. Route `api/<resource>`; `ActionResult<TDto>`; thread `CancellationToken`.
3. Delegate to a service; read the acting user from the identity for audit, not the body.
4. Return `Ok`/`NotFound`/`CreatedAtAction`; let the global handler shape errors.
5. Add a unit test (service + filter behavior).
