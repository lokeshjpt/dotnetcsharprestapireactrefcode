using Microsoft.AspNetCore.Http;

namespace PWA.PermitsApi.WebApi.Authorization;

/// <summary>
/// A single, self-contained way a request can prove it is allowed through the public-endpoint guard
/// (<see cref="PublicAccessGuardFilter"/>). Each implementation represents one accepted credential — a
/// browser captcha, a fixed server-to-server token, a trusted Entra client, etc. The guard treats the
/// registered proofs as an <b>OR</b>: the request is allowed as soon as any one proof is satisfied.
/// </summary>
/// <remarks>
/// Keeping each credential in its own proof lets a scheme be unit-tested in isolation and lets the guard
/// stay a thin orchestrator, rather than mixing unrelated verification logic in a single class. A proof
/// must return <c>false</c> (never throw) when its credential is simply absent, so the guard can move on
/// to the next proof.
/// </remarks>
public interface IPublicAccessProof
{
    /// <summary>
    /// Returns <c>true</c> when this proof's credential is present on the request and valid, allowing the
    /// request through. Returns <c>false</c> when the credential is absent or invalid.
    /// </summary>
    Task<bool> IsSatisfiedAsync(HttpContext context);
}
