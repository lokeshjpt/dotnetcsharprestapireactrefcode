# .NET — Backend Sequence Diagrams

Mermaid sequence diagrams for the three core flows. Rendered by GitHub, VS Code (Mermaid preview),
and most Markdown viewers. Grounded in `ApplicationController` + `ApplicationService` +
`ApplicationRepository`.

Common request pipeline (applies to all): `ForwardedHeaders → CorrelationIdMiddleware →
ErrorStatusAuditMiddleware → ExceptionHandler → Routing → CORS → RateLimiter → Authentication →
Authorization → Controller`.

---

## 1. Create Application — `POST /api/applications`

Anonymous public write, guarded by captcha/token. Status codes mirror the legacy `ProcessAppServlet`:
application → `PENDS`, works → `PENDC`, specs → `PEND`. The CC card is **vaulted for $0** at submit;
the real fee is charged later by staff.

```mermaid
sequenceDiagram
    autonumber
    actor User as Public applicant (ecomm SPA)
    participant Guard as PublicAccessGuardFilter
    participant Ctrl as ApplicationController
    participant Svc as ApplicationService
    participant Repo as ApplicationRepository (Dapper)
    participant Pay as PaymentRepository
    participant IPay as IntelliPayGateway
    participant Insp as InspectionRepository
    participant Notify as PermitNotificationService / EmailService
    participant DB as SQL Server

    User->>Guard: POST /api/applications (X-Captcha-Token, body)
    Note over Guard: Any proof passes (trusted bearer OR reCAPTCHA), else 401
    Guard->>Ctrl: authorized
    Ctrl->>Svc: SubmitAsync(request, ct)

    Svc->>Svc: appId = UtcNow unix-ms
    Svc->>Repo: GetSiteExtraRateAsync(ct)
    Repo->>DB: SELECT site extra rate
    DB-->>Repo: rate
    Repo-->>Svc: siteExtraRate
    Svc->>Svc: build works (PENDC) + specs (PEND), application (PENDS)
    Svc->>Repo: CreateAsync(application, ct)
    Repo->>DB: INSERT application + works + specs + hazard + docs + notes
    DB-->>Repo: created aggregate
    Repo-->>Svc: created

    Svc->>Svc: authAmount = CalculateAuthAmount(works)
    alt PaymentType == CC
        Svc->>IPay: VaultZeroDollarAsync(appId, ct)
        IPay-->>Svc: { CustomerId, Approved }  (mock when UseMock)
    end
    Svc->>Svc: resolve paymentStatus (EXMPT / PENDP / PEND)
    Svc->>Pay: UpsertAsync(payment, ct)
    Pay->>DB: UPSERT APP_PAYMENT_INFO (AUTH_ID_ENCR encrypted)

    opt inspection slot chosen
        Svc->>Insp: AddAsync(inspection IRSRV, ct)
        Insp->>DB: INSERT inspection slot
    end

    alt PaymentType != CC
        Svc->>Repo: GetByIdAsync(appId) reload for descriptions
        Repo->>DB: SELECT aggregate
        DB-->>Repo: application
        Repo-->>Svc: confirmApp
        Svc->>Notify: SendApplicationConfirmationAsync(...)  (best-effort)
    else CC
        Note over Svc,Notify: Confirmation deferred to PaymentService.PreAuthorizeAsync (after lightbox)
    end

    Svc-->>Ctrl: ApplicationDto (Map(created))
    Ctrl-->>User: 201 Created (Location: /api/applications/{appId})
```

**Payment status rules:** `EXMPT` → `EXMPT` (paid 0); `CASH` and `CHECK` without a check number →
`PENDP` (Pending Payment); `CC` (and `CHECK` with a number) → `PEND` (Pending Approval).

---

## 2. Search Applications — `GET /api/applications/search`

Anonymous public read (ecomm Track) also called by the staff SPA. Guarded, paged, whitelisted sort.
Returns items + total count for server-side pagination.

```mermaid
sequenceDiagram
    autonumber
    actor Client as ecomm Track / intra staff
    participant Guard as PublicAccessGuardFilter
    participant Ctrl as ApplicationController
    participant Svc as ApplicationService
    participant Repo as ApplicationRepository (Dapper)
    participant DB as SQL Server

    Client->>Guard: GET /api/applications/search?appId=&status=&page=&pageSize=&sortBy=&sortDir=
    Note over Guard: trusted bearer OR X-Captcha-Token, else 401
    Guard->>Ctrl: authorized
    Ctrl->>Svc: SearchAsync(request, ct)

    Svc->>Repo: SearchAsync(request, ct)
    Note over Repo: parameterized WHERE from supplied filters;<br/>SortBy whitelisted (unknown -> newest-first); OFFSET/FETCH paging
    Repo->>DB: SELECT page of matching applications
    DB-->>Repo: rows
    Repo-->>Svc: items

    Svc->>Repo: CountAsync(request, ct)
    Repo->>DB: SELECT COUNT(*) with same filters
    DB-->>Repo: totalCount
    Repo-->>Svc: totalCount

    Svc->>Svc: mapped = items.Select(Map)
    Svc-->>Ctrl: ApplicationSearchResult(mapped, totalCount)
    Ctrl-->>Client: 200 OK { items, totalCount }
```

---

## 3. Application Details — `GET /api/applications/{appId}`

Loads the full aggregate for the intra Application Detail screen (the staff SPA also sends the Entra
bearer via its axios interceptor). Returns `404` when not found.

```mermaid
sequenceDiagram
    autonumber
    actor Staff as intra SPA (Entra bearer)
    participant Ctrl as ApplicationController
    participant Svc as ApplicationService
    participant Repo as ApplicationRepository (Dapper)
    participant DB as SQL Server

    Staff->>Ctrl: GET /api/applications/{appId}
    Ctrl->>Svc: GetByIdAsync(appId, ct)
    Svc->>Repo: GetByIdAsync(appId, ct)
    Repo->>DB: SELECT application + works + specs + payment + hazard + docs + notes
    DB-->>Repo: aggregate rows
    Repo-->>Svc: DomainApplication (or null)

    alt found
        Svc->>Svc: Map(application) -> ApplicationDto (codes enriched to labels)
        Svc-->>Ctrl: ApplicationDto
        Ctrl-->>Staff: 200 OK (rendered by DetailSection cards)
    else not found
        Svc-->>Ctrl: null
        Ctrl-->>Staff: 404 Not Found
    end
```

**Staff edits** from the detail screen (`PUT /api/applications/{appId}/project|applicant|hazard|…`) are
`[Authorize]`-only; each normalizes input (`with { Phone = PhoneNormalizer.DigitsOnly(...) }`), calls a
repository `Update…Async`, then **re-loads and returns** the refreshed `ApplicationDto` so the SPA
re-renders from server truth. The acting user comes from `ResolveActingUser()` (Entra identity), not
the body.
