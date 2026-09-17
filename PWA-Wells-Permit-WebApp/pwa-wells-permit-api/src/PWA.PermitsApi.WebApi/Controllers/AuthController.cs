using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace PWA.PermitsApi.WebApi.Controllers;

/// <summary>
/// Lightweight identity/authorization probe for the intra React app. The app calls
/// <c>GET /api/auth/me</c> right after Microsoft Entra sign-in to decide whether to render the staff
/// UI: a user on the allowlist gets <c>200</c> with their identity, while an authenticated user who is
/// NOT on the allowlist is rejected with <c>403</c> by the global allowlist filter (before this action
/// runs), which the app turns into the "not authorized" banner.
/// </summary>
[ApiController]
[Route("api/auth")]
[Authorize]
public sealed class AuthController : ControllerBase
{
    [HttpGet("me")]
    public ActionResult<object> Me()
    {
        var name = User.FindFirst("name")?.Value ?? User.Identity?.Name;
        var email = User.FindFirst("preferred_username")?.Value
            ?? User.FindFirst("upn")?.Value
            ?? User.FindFirst(ClaimTypes.Email)?.Value
            ?? User.FindFirst("unique_name")?.Value
            ?? User.Identity?.Name;

        return Ok(new { name, email, authorized = true });
    }
}
