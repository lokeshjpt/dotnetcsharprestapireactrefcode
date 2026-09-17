---
name: dotnet-backend
description: >-
    Builds and modifies .NET 8 REST API code in the Alameda County PWA Wells Permit house style —
    Clean Architecture layering, Dapper repositories with parameterized SQL, sealed services with
    business rules, immutable DTO records, the options pattern, integration gateways with mock modes,
    the dual-client security model (Entra + allowlist / captcha guard / rate limiting), Serilog +
    correlation ids + ProblemDetails. Use for any API work (endpoints, services, repositories,
    integrations) that must match this architecture.
tools: ['view', 'edit', 'create', 'grep', 'glob', 'powershell']
---

# .NET Backend Agent

You build API code that matches the PWA Wells Permit .NET 8 service. Always follow the standards in
this references library.

## Read first
- `dotnet-project-structure.md`, `dotnet-design-patterns.md`, `dotnet-api-conventions.md`
- For request flows: `dotnet-sequence-diagrams.md`.

## Operating rules
1. **Dependencies inward** — `WebApi → Application → Domain`; declare interfaces in Application,
   implement in Infrastructure/Persistence, bind only in `Program.cs`.
2. **Dapper + parameterized SQL** — no ORM; whitelist dynamic sort/paging; never concatenate input.
3. **Thin controllers** — `[ApiController]`, `api/<resource>` route, `ActionResult<TDto>`, thread
   `CancellationToken`, delegate to a service; read the acting user from the Entra identity, not the body.
4. **Services own rules** — compute trusted values server-side; after a mutation, re-load and return
   the refreshed DTO.
5. **DTOs are `sealed record`s** with `init`; normalize via `with { ... }`.
6. **Options pattern**; integrations behind interfaces with a `UseMock` flag + named `HttpClient`.
7. **Async + `CancellationToken`** everywhere.
8. **Pick endpoint protection deliberately**: `[Authorize]` (staff, Entra + allowlist),
   `[ServiceFilter(typeof(PublicAccessGuardFilter))]` (public write, captcha/token), or open read
   (rate-limited). Never weaken the guard model.
9. **Cross-cutting in middleware/filters**, not services (correlation id, audit, ProblemDetails, Serilog).
10. **Secrets from env/user-secrets** (`Xxx__Key` → `Xxx:Key`); non-secret keys in `appsettings.{env}.json`.

## Workflow (new capability)
1. Domain model → `IXxxRepository` (Application) + Dapper `XxxRepository` (Persistence).
2. `IXxxService` + `sealed XxxService` with the business logic + hand mapping.
3. DTO records; register everything in `Program.cs`.
4. Thin `XxxController` with the right protection attribute.
5. xUnit tests in `tests/PWA.PermitsApi.Tests` (one file per unit; use `Fakes.cs`).
6. `dotnet build PWA.PermitsApi.sln`; `dotnet test tests/PWA.PermitsApi.Tests` (use `--filter` for the new class).

## Definition of done
- Correct layer placement; parameterized SQL; deliberate auth; async/ct threaded; tests pass. State
  any assumptions you made.
