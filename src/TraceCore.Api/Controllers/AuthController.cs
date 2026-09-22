using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TraceCore.Api.Common;
using TraceCore.Application.Common.Interfaces;
using TraceCore.Infrastructure.Security;

namespace TraceCore.Api.Controllers;

public sealed record LoginRequest(string Email, string Password);
public sealed record LoginResponse(string AccessToken, string Email, string DisplayName, IReadOnlyList<string> Roles, IReadOnlyList<string> Permissions);
public sealed record UserProfileResponse(Guid? UserId, string? Email, string? DisplayName, IReadOnlyList<string> Roles, IReadOnlyList<string> Permissions);

public class AuthController : ApiControllerBase
{
    private readonly IJwtTokenService _jwtService;
    private readonly ICurrentUserService _currentUser;

    public AuthController(IJwtTokenService jwtService, ICurrentUserService currentUser)
    {
        _jwtService = jwtService;
        _currentUser = currentUser;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public ActionResult<LoginResponse> Login([FromBody] LoginRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        // Built-in seed accounts for operational and portfolio demonstration
        (Guid Id, string Name, string[] Roles, IReadOnlyList<string> Perms)? account = email switch
        {
            "admin@tracecore.gov" => (
                Guid.Parse("11111111-1111-1111-1111-111111111111"),
                "Chief Admin",
                ["Administrator"],
                Permissions.All),

            "investigator@tracecore.gov" => (
                Guid.Parse("22222222-2222-2222-2222-222222222222"),
                "Senior Investigator Sarah Connor",
                ["Investigator"],
                Permissions.InvestigatorPermissions),

            "manager@tracecore.gov" => (
                Guid.Parse("33333333-3333-3333-3333-333333333333"),
                "Case Manager David Miller",
                ["CaseManager"],
                Permissions.CaseManagerPermissions),

            "auditor@tracecore.gov" => (
                Guid.Parse("44444444-4444-4444-4444-444444444444"),
                "Compliance Auditor Rachel Green",
                ["Auditor"],
                Permissions.AuditorPermissions),

            _ => null
        };

        if (account == null || request.Password != "Password123!")
        {
            return Unauthorized(new ProblemDetails
            {
                Title = "Invalid Credentials",
                Detail = "Valid accounts: admin@tracecore.gov, investigator@tracecore.gov, manager@tracecore.gov, auditor@tracecore.gov with password 'Password123!'",
                Status = StatusCodes.Status401Unauthorized
            });
        }

        var token = _jwtService.GenerateToken(
            account.Value.Id,
            email,
            account.Value.Name,
            account.Value.Roles,
            account.Value.Perms);

        return Ok(new LoginResponse(token, email, account.Value.Name, account.Value.Roles, account.Value.Perms));
    }

    [HttpGet("me")]
    [Authorize]
    public ActionResult<UserProfileResponse> GetProfile()
    {
        return Ok(new UserProfileResponse(
            _currentUser.UserId,
            _currentUser.Email,
            _currentUser.DisplayName,
            _currentUser.Roles,
            _currentUser.Permissions));
    }
}
