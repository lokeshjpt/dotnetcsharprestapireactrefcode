---
name: dotnet-standards
description: >-
    .NET 8 REST API house-style standards for the Alameda County PWA Wells Permit backend — Clean
    Architecture layering (Domain / Application / Infrastructure / Persistence / WebApi), Dapper
    repositories with parameterized SQL, sealed service classes, immutable DTO records, the options
    pattern, integration gateways behind interfaces with mock modes, the dual-client security model
    (Entra + allowlist for staff, captcha/token guard for public writes, rate limiting for open reads),
    Serilog + correlation ids + ProblemDetails, and Swagger/Scalar. Use when building, extending, or
    scaffolding a .NET REST API that must match this architecture and conventions.
user-invocable: true
---

# .NET Standards (PWA Wells Permit house style)

Reproduce the .NET 8 Web API's architecture and conventions in a new or existing API. This skill
indexes granular reference docs; read the one you need, follow the patterns, adapt names.

## When to use

- "Build/scaffold a .NET 8 REST API in the PWA / ACGov house style."
- "Add a controller / service / Dapper repository / integration to this API."
- "Apply the dual-client auth model (Entra staff + captcha public) / rate limiting / Serilog / ProblemDetails."

## Reference docs (in this references library)

| Topic | File |
|-------|------|
| Clean Architecture layers, projects, DI wiring | `dotnet-project-structure.md` |
| Repository / service / options / DTO / mapping patterns | `dotnet-design-patterns.md` |
| Controllers, auth model, guards, rate limiting, logging, errors | `dotnet-api-conventions.md` |
| Sequence diagrams: Create / Search / Details (mermaid) | `dotnet-sequence-diagrams.md` |

## Non-negotiables (the "house style" checklist)

1. **Clean Architecture, dependencies inward**: `WebApi → Application → Domain`; Infrastructure &
   Persistence implement Application interfaces; `Program.cs` is the only composition root.
2. **Interfaces in Application**, implementations in Infrastructure/Persistence, bound in `Program.cs`
   — keeps services unit-testable with fakes.
3. **Dapper, hand-written parameterized SQL** (no ORM). Dynamic sort/paging via a whitelist. Never
   concatenate user input.
4. **Services own business rules** (fees, status transitions), compute trusted values server-side, map
   Domain⇄DTO by hand, and return the refreshed DTO after mutations.
5. **DTOs are `sealed record`s** with `init` props; normalize with `with { ... }` (e.g. phone digits).
6. **Options pattern** for config; integration gateways behind interfaces with a `UseMock` flag and a
   **named** `HttpClient`.
7. **Async + `CancellationToken`** on every I/O method, threaded controller → service → repo.
8. **Dual-client security**: choose per endpoint — `[Authorize]` (staff, Entra + allowlist),
   `PublicAccessGuardFilter` (public write, captcha/token), or open read (rate-limited). Acting user
   from the Entra identity, never the body.
9. **Cross-cutting via middleware/filters** — correlation id, error-status audit, ProblemDetails +
   `GlobalExceptionHandler`, Serilog daily rolling file. Never put these in services.
10. **Secrets from env/user-secrets** (`Xxx__Key` binds to `Xxx:Key`); per-env `appsettings.{env}.json`
    for non-secret keys.

## Scaffold procedure

1. Create the 5 projects + 2 test projects and the `.sln` (see `dotnet-project-structure.md`).
2. Add `Program.cs` with the standard bind → repos → services → integrations → controllers → auth →
   CORS → rate limiter → Serilog order and the middleware pipeline.
3. For each aggregate: Domain model → `IXxxRepository` + Dapper `XxxRepository` → `IXxxService` +
   `XxxService` → DTO records → thin `XxxController` → xUnit tests.
4. Pick the protection for each endpoint using the checklist in `dotnet-api-conventions.md`.
