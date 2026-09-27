using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TraceCore.Api.Data;
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
    private readonly IApplicationDbContext _context;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IJwtTokenService jwtService,
        ICurrentUserService currentUser,
        IApplicationDbContext context,
        ILogger<AuthController> logger)
    {
        _jwtService = jwtService;
        _currentUser = currentUser;
        _context = context;
        _logger = logger;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return Unauthorized(InvalidCredentials());

        var email = request.Email.Trim().ToLowerInvariant();

        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == email, ct);

        if (user is null || !user.IsActive)
        {
            _logger.LogWarning("Failed login attempt for {Email}", email);
            return Unauthorized(InvalidCredentials());
        }

        bool verified;
        try
        {
            verified = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Password verification failed for {Email}", email);
            return Unauthorized(InvalidCredentials());
        }

        if (!verified)
        {
            _logger.LogWarning("Failed login attempt for {Email}", email);
            return Unauthorized(InvalidCredentials());
        }

        var roles = user.GetRoles();
        var perms = DataSeeder.PermissionsForRoles(roles);

        var token = _jwtService.GenerateToken(user.Id, user.Email, user.DisplayName, roles, perms);

        return Ok(new LoginResponse(token, user.Email, user.DisplayName, roles, perms));
    }

    private static ProblemDetails InvalidCredentials() => new()
    {
        Title = "Invalid Credentials",
        Detail = "The email or password is incorrect.",
        Status = StatusCodes.Status401Unauthorized
    };

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
