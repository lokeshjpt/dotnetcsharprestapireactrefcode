# .NET — Project Structure (Clean Architecture)

A .NET 8 Web API organized in **Clean Architecture** layers. Dependencies point **inward**:
`WebApi → Application → Domain`, with `Infrastructure` and `Persistence` implementing `Application`
interfaces and wired only in `WebApi/Program.cs` (composition root).

```
PWA.PermitsApi.sln
db/                                  # ordered, idempotent SQL migration scripts (001_*.sql, 002_*.sql)
src/
  PWA.PermitsApi.Domain/             # entities/value objects — NO dependencies
    Models/                          #   Application, ApplicationWork, Applicant, AppPayment, ...
  PWA.PermitsApi.Application/        # depends on Domain only
    DTOs/                            #   request/response records (SubmitApplicationRequest, ApplicationDto, ...)
    Interfaces/                      #   IApplicationService, ... + Interfaces/Repositories, Interfaces/Integration
    Services/                        #   business logic (ApplicationService, PaymentService, ...)
    Configuration/                   #   options POCOs (DatabaseOptions, FtpOptions, CaptchaOptions, ...)
    Common/                          #   pure helpers (PhoneNormalizer, FeeCalculator, ...)
    Notifications/                   #   email composition
    Exceptions/
  PWA.PermitsApi.Infrastructure/     # implements Application integration interfaces
    Services/                        #   EmailService, IntelliPayGateway, IcapVirusScanner,
                                     #   FtpFileTransferService, GoogleReCaptchaVerifier
  PWA.PermitsApi.Persistence/        # implements Application repository interfaces (Dapper)
    DapperContext.cs                 #   IDbConnection factory over the connection string
    Repositories/                    #   ApplicationRepository, PaymentRepository, ... + SqlOrderBy
  PWA.PermitsApi.WebApi/             # host / composition root
    Program.cs                       #   DI, auth, CORS, rate limiting, Serilog, Swagger/Scalar
    Controllers/                     #   thin controllers (ApplicationController, ...)
    Authorization/                   #   PublicAccessGuardFilter, WhitelistAuthorizationFilter, proofs
    Middleware/                      #   CorrelationIdMiddleware, ErrorStatusAuditMiddleware
    RateLimiting/                    #   PublicRateLimit endpoint scoping
    Json/                            #   LenientStringConverter
    GlobalExceptionHandler.cs
    appsettings.json + appsettings.{local,Development,TEST,UAT,Production}.json
tests/
  PWA.PermitsApi.Tests/              # xUnit unit tests for Application services + WebApi filters
  PWA.PermitsApi.E2E/                # live/UI integration suite (needs a running API + DB)
```

## Layer responsibilities & rules

| Layer | May reference | Contains | Never contains |
|-------|---------------|----------|----------------|
| **Domain** | (nothing) | Entities/value objects mapped from the legacy schema | EF/Dapper, HTTP, options, `System.Data` |
| **Application** | Domain | Services, DTO **records**, interfaces, options POCOs, pure helpers | Concrete DB/HTTP/SMTP implementations |
| **Infrastructure** | Application, Domain | External integrations (SMTP, FTP, ICAP, IntelliPay, reCAPTCHA) behind `Application` interfaces | Controllers |
| **Persistence** | Application, Domain | Dapper repositories + `DapperContext` | Business rules |
| **WebApi** | all | Controllers, DI, filters, middleware, config | Business logic (delegate to services) |

- Interfaces are **declared in Application**, implemented in Infrastructure/Persistence, and bound in
  `Program.cs`. This keeps Application testable with fakes (`tests/**/Fakes.cs`).
- Add data access = new `IXxxRepository` (Application) + `XxxRepository` (Persistence) + registration.
- Add an integration = new `IXxx` (Application/Interfaces/Integration) + `Xxx` (Infrastructure) with a
  `Xxx.UseMock` option for offline runs.

## Composition root — `Program.cs`

Single source of truth. Order: bind options → register `DapperContext` (singleton) → repositories
(scoped) → services (scoped) → integrations (scoped) → `AddHttpClient` (named clients) → controllers
(+ global filters, JSON converters) → Swagger/Scalar → ProblemDetails + `GlobalExceptionHandler` →
Entra JWT auth + default policy → CORS → rate limiter → Serilog. Middleware pipeline: forwarded
headers → correlation id → error-status audit → exception handler → swagger → https redirect →
routing → CORS → rate limiter → authentication → authorization → controllers.

```csharp
builder.Services.Configure<CaptchaOptions>(builder.Configuration.GetSection("Captcha"));
builder.Services.AddSingleton(new DapperContext(connStr));
builder.Services.AddScoped<IApplicationRepository, ApplicationRepository>();
builder.Services.AddScoped<IApplicationService, ApplicationService>();
builder.Services.AddScoped<IVirusScanner, IcapVirusScanner>();
```

## Configuration & secrets

- Per-environment `appsettings.{env}.json` are committed for non-secret keys (CORS origins, feature
  flags). Secrets (DB password, `Ftp:Password`, `Captcha:SecretKey`) come from env vars
  (double-underscore binds to colon, e.g. `Captcha__SecretKey` → `Captcha:SecretKey`) or user-secrets
  in Development. Connection string via `PWA_DB_CONNECTION_STRING` / `ConnectionStrings:...`.
- Integration `UseMock` flags default **false** (live) — set `*__UseMock=true` to run fully offline.

## Testing layout

- `tests/PWA.PermitsApi.Tests` — fast xUnit unit tests per service/filter/helper, one file per unit
  (`ApplicationServiceTests.cs`, `FeeCalculatorTests.cs`, `PublicAccessGuardFilterTests.cs`), shared
  `Fakes.cs`. Run: `dotnet test tests/PWA.PermitsApi.Tests`, single class:
  `--filter "FullyQualifiedName~ApplicationServiceTests"`.
- `tests/PWA.PermitsApi.E2E` — live API/UI tests; kept out of the normal loop.
