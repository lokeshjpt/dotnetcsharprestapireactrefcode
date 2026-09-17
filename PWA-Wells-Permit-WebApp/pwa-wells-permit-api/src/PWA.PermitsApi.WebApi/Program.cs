using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Threading.RateLimiting;
using PWA.PermitsApi.Application.Configuration;
using PWA.PermitsApi.Application.Interfaces;
using PWA.PermitsApi.Application.Interfaces.Integration;
using PWA.PermitsApi.Application.Interfaces.Repositories;
using PWA.PermitsApi.Application.Services;
using PWA.PermitsApi.Infrastructure.Services;
using PWA.PermitsApi.Persistence;
using PWA.PermitsApi.Persistence.Repositories;
using PWA.PermitsApi.WebApi.Json;
using PWA.PermitsApi.WebApi.Middleware;
using PWA.PermitsApi.WebApi.RateLimiting;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<DatabaseOptions>(builder.Configuration);
builder.Services.Configure<IntelliPayOptions>(builder.Configuration.GetSection("IntelliPay"));
builder.Services.Configure<IcapOptions>(builder.Configuration.GetSection("Icap"));
builder.Services.Configure<FtpOptions>(builder.Configuration.GetSection("Ftp"));
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection("Email"));
builder.Services.Configure<EmailRoutingOptions>(builder.Configuration.GetSection("EmailRouting"));
builder.Services.Configure<IntraOptions>(builder.Configuration.GetSection("Intra"));
builder.Services.Configure<CaptchaOptions>(builder.Configuration.GetSection("Captcha"));
// No "Auth" section ships in appsettings by design: a blank AuthOptions.TrustedClientIds is fail-open
// (any validated Entra token is a trusted client, e.g. the intra SPA calling the captcha-guarded
// /applications/search). This binding stays so an env can restrict the captcha bypass to specific
// Entra client IDs via the Auth__TrustedClientIds environment variable, with no code change.
builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection("Auth"));
builder.Services.Configure<RateLimitOptions>(builder.Configuration.GetSection("RateLimit"));

var connStr = builder.Configuration.GetConnectionString("PWAWellPermitsDBConnection")
    ?? string.Empty;

builder.Services.AddSingleton(new DapperContext(connStr));

builder.Services.AddScoped<IApplicationRepository, ApplicationRepository>();
builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();
builder.Services.AddScoped<IInspectionRepository, InspectionRepository>();
builder.Services.AddScoped<IInspectionWorkbookRepository, InspectionWorkbookRepository>();
builder.Services.AddScoped<IWorkPermitRepository, WorkPermitRepository>();
builder.Services.AddScoped<IReferenceRepository, ReferenceRepository>();
builder.Services.AddScoped<IConditionsRepository, ConditionsRepository>();
builder.Services.AddScoped<IReportRepository, ReportRepository>();
builder.Services.AddScoped<IHistoryRepository, HistoryRepository>();
builder.Services.AddScoped<IMaintenanceRepository, MaintenanceRepository>();
builder.Services.AddScoped<IAppUserRepository, AppUserRepository>();

builder.Services.AddScoped<IApplicationService, ApplicationService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<IApprovalService, ApprovalService>();
builder.Services.AddScoped<IInspectionService, InspectionService>();
builder.Services.AddScoped<IFileUploadService, FileUploadService>();
builder.Services.AddScoped<IReferenceService, ReferenceService>();
builder.Services.AddScoped<IConditionsService, ConditionsService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IHistoryService, HistoryService>();
builder.Services.AddScoped<IMaintenanceService, MaintenanceService>();

builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IPermitNotificationService, PermitNotificationService>();
builder.Services.AddScoped<IPermitDocumentService, PermitDocumentService>();
builder.Services.AddScoped<IIntelliPayGateway, IntelliPayGateway>();
builder.Services.AddScoped<IVirusScanner, IcapVirusScanner>();
builder.Services.AddScoped<IFileTransferService, FtpFileTransferService>();
builder.Services.AddScoped<ICaptchaVerifier, GoogleReCaptchaVerifier>();

// Guards the anonymous public write endpoints: allowed when ANY registered proof is satisfied
// (trusted Entra bearer OR browser captcha token). Order = cheapest-first; the guard short-circuits
// on the first success. Registered so it can be applied via [ServiceFilter(typeof(PublicAccessGuardFilter))].
builder.Services.AddScoped<PWA.PermitsApi.WebApi.Authorization.IPublicAccessProof, PWA.PermitsApi.WebApi.Authorization.TrustedClientProof>();
builder.Services.AddScoped<PWA.PermitsApi.WebApi.Authorization.IPublicAccessProof, PWA.PermitsApi.WebApi.Authorization.CaptchaProof>();
builder.Services.AddScoped<PWA.PermitsApi.WebApi.Authorization.PublicAccessGuardFilter>();

// Database-backed staff allowlist (EEAOWN.app_users) used by the WhitelistAuthorizationFilter,
// cached briefly in memory so the table is not queried on every intra request.
builder.Services.AddMemoryCache();
builder.Services.AddScoped<PWA.PermitsApi.WebApi.Authorization.IWhitelistProvider, PWA.PermitsApi.WebApi.Authorization.WhitelistProvider>();

builder.Services.AddHttpClient();
builder.Services.AddHttpClient(IntelliPayGateway.HttpClientName);
builder.Services.AddHttpClient(IcapVirusScanner.HttpClientName);
builder.Services.AddHttpClient(GoogleReCaptchaVerifier.HttpClientName);

builder.Services.AddControllers(options =>
    {
        // Enforces the application allowlist on every Entra-protected intra endpoint (authenticated
        // but non-whitelisted users get 403); anonymous ecomm endpoints are skipped.
        options.Filters.Add<PWA.PermitsApi.WebApi.Authorization.WhitelistAuthorizationFilter>();
    })
    .AddJsonOptions(options =>
    {
        // Tolerate identifiers/amounts sent with loose JSON types (e.g. a numeric appId) so the
        // API binds them instead of returning 400. Writing/serialization is unaffected.
        options.JsonSerializerOptions.Converters.Add(new LenientStringConverter());
        options.JsonSerializerOptions.NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString;
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    // Azure AD bearer support in Swagger UI (parity with maps-tracker) so protected
    // intra endpoints can be exercised with a pasted token.
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter 'Bearer' [space] and then your valid token."
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<PWA.PermitsApi.WebApi.GlobalExceptionHandler>();

// Azure AD (Entra ID) SSO authentication — mirrors the maps-tracker API. The intra React
// app signs in via MSAL and sends a v1 bearer token; ecomm (public portal) endpoints stay
// anonymous (no [Authorize]). Intra-only endpoints are protected with [Authorize].
var azureTenantId = builder.Configuration["AzureAd:TenantId"];
var azureClientId = builder.Configuration["AzureAd:ClientId"];

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer("AzureAD", options =>
    {
        options.Authority = $"https://login.microsoftonline.com/{azureTenantId}";
        options.Audience = $"api://{azureClientId}";
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = $"https://sts.windows.net/{azureTenantId}/",
            ValidateAudience = true,
            ValidAudience = $"api://{azureClientId}",
            ValidateLifetime = true
        };
    });

builder.Services.AddAuthorization(options =>
{
    // [Authorize] on intra controllers resolves to this policy, requiring an AzureAD token.
    options.DefaultPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .AddAuthenticationSchemes("AzureAD")
        .Build();
});

// Allowed CORS origins for the two SPA clients. Each key may hold a comma-separated list so a
// single environment (e.g. Development) can permit both the local dev hosts (localhost:3000/3003)
// and the deployed dev hostnames at once. Trailing slashes are trimmed because CORS origin
// matching is exact (scheme+host+port, no path/slash). Same-origin reverse-proxy deployments never
// hit CORS; listing the origins keeps direct cross-origin calls working too.
var corsOrigins = new[] { "Cors:EcommOrigin", "Cors:IntraOrigin" }
    .Select(key => builder.Configuration[key])
    .Where(value => !string.IsNullOrWhiteSpace(value))
    .SelectMany(value => value!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    .Select(origin => origin.TrimEnd('/'))
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();
if (corsOrigins.Length == 0)
{
    corsOrigins = new[] { "http://localhost:3000", "http://localhost:3003" };
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("PortalClients", policy =>
        policy.WithOrigins(corsOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod());
});

// Per-client-IP rate limiting (sliding window) to throttle scripted/bot floods against the *open*
// public endpoints only — anonymous routes that are NOT already behind the captcha / public-API-token
// guard (see PublicRateLimit.AppliesTo). Captcha-guarded public writes and Entra-authorized staff
// routes carry their own protection and are exempt. Partitions on the forwarded client IP; disabled
// (no limiter) when RateLimit:Enabled is false so it never throttles an environment that opts out.
// Rejections return 429.
var rateLimitOptions = builder.Configuration.GetSection("RateLimit").Get<RateLimitOptions>() ?? new RateLimitOptions();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    {
        if (!rateLimitOptions.Enabled || !PublicRateLimit.AppliesTo(httpContext.GetEndpoint()))
        {
            return RateLimitPartition.GetNoLimiter("__nolimit");
        }

        var clientKey = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetSlidingWindowLimiter(clientKey, _ => new SlidingWindowRateLimiterOptions
        {
            PermitLimit = rateLimitOptions.PermitLimit,
            Window = TimeSpan.FromSeconds(rateLimitOptions.WindowSeconds),
            SegmentsPerWindow = Math.Max(1, rateLimitOptions.SegmentsPerWindow),
            QueueLimit = rateLimitOptions.QueueLimit,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        });
    });
    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.ContentType = "application/problem+json";
        await context.HttpContext.Response.WriteAsync(
            "{\"title\":\"Too many requests\",\"status\":429,\"detail\":\"Rate limit exceeded. Please retry later.\"}",
            token);
    };
});

// Serilog structured logging to a daily rolling file, matching the sibling ESPOS / MTA-Tracker APIs
// (Logs/log-<date>.txt, keep 7 days, shared). Enrich.FromLogContext() surfaces the request's
// CorrelationId (pushed by CorrelationIdMiddleware) into every log line — the sibling template
// referenced {CorrelationId} but never populated it.
Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.File(
        path: "Logs/log-.txt",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7,
        shared: true,
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] (CorrelationId={CorrelationId}) {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

builder.Logging.ClearProviders();
builder.Logging.AddSerilog(Log.Logger);

var app = builder.Build();

// Honor reverse-proxy forwarded headers from a loopback front-end (e.g. the local IIS/ARR
// site that terminates TLS and proxies /api over HTTP to Kestrel). This makes scheme-sensitive
// middleware — notably UseHttpsRedirection — see the original https request, so proxied calls
// are not 307-redirected. Only loopback proxies are trusted, and direct https callers
// (intra dev at https://localhost:7242) send no such header and are unaffected. In-process IIS
// hosting sets the scheme via ANCM instead, so this is a no-op there.
var forwardedOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};
forwardedOptions.KnownProxies.Add(System.Net.IPAddress.Loopback);
forwardedOptions.KnownProxies.Add(System.Net.IPAddress.IPv6Loopback);
app.UseForwardedHeaders(forwardedOptions);

// Assigns/propagates X-Correlation-ID and pushes it into the Serilog LogContext for every request,
// so {CorrelationId} is populated on all log lines (including anything the exception handler logs).
app.UseMiddleware<CorrelationIdMiddleware>();

// Observes the final status code and audit-emails access-denied (401/403) responses, which the auth
// middleware returns without throwing (so they never reach the global exception handler below).
app.UseMiddleware<ErrorStatusAuditMiddleware>();

app.UseExceptionHandler();

// Swagger / OpenAPI UI is exposed only where explicitly enabled. The "Swagger:Enabled" flag makes
// this configurable per environment; when the flag is absent it defaults to on for the local and
// Development environments and off everywhere else (TEST/UAT/Production).
var swaggerEnabled = app.Configuration.GetValue<bool?>("Swagger:Enabled")
    ?? (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("local"));

// Scalar API reference UI follows the same per-environment convention as Swagger: the
// "Scalar:Enabled" flag overrides, and when absent it defaults to on for local/Development only.
var scalarEnabled = app.Configuration.GetValue<bool?>("Scalar:Enabled")
    ?? (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("local"));

// Both the Swagger UI and the Scalar reference render the same Swashbuckle-generated OpenAPI
// document, so expose the JSON endpoint whenever either UI is enabled.
if (swaggerEnabled || scalarEnabled)
{
    app.UseSwagger();
}
if (swaggerEnabled)
{
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Resolve the matched endpoint before CORS / rate limiting / auth so their middleware can read the
// endpoint's metadata — the rate limiter uses it to scope throttling to the open public routes.
app.UseRouting();

app.UseCors("PortalClients");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Scalar API reference UI at /scalar, rendering the OpenAPI document served by UseSwagger above.
if (scalarEnabled)
{
    app.MapScalarApiReference(options =>
    {
        options
            .WithTitle("PWA Permits API")
            .WithOpenApiRoutePattern("/swagger/v1/swagger.json");
    });
}

app.Run();

