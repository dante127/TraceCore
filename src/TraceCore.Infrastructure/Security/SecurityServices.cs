using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TraceCore.Application.Common.Interfaces;
using TraceCore.Domain.Entities.Cases;
using TraceCore.Domain.Enums;

namespace TraceCore.Infrastructure.Security;

public class JwtOptions
{
    public const string SectionName = "Jwt";
    public string SecretKey { get; set; } = string.Empty;
    public string Issuer { get; set; } = "TraceCore.Api";
    public string Audience { get; set; } = "TraceCore.Client";
    public int ExpiryMinutes { get; set; } = 120;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(SecretKey) || Encoding.UTF8.GetByteCount(SecretKey) < 32)
            throw new InvalidOperationException(
                "Jwt:SecretKey is missing or too short. Set Jwt__SecretKey env var (min 32 bytes).");
        if (string.IsNullOrWhiteSpace(Issuer))
            throw new InvalidOperationException("Jwt:Issuer is missing. Set Jwt__Issuer env var.");
        if (string.IsNullOrWhiteSpace(Audience))
            throw new InvalidOperationException("Jwt:Audience is missing. Set Jwt__Audience env var.");
        if (ExpiryMinutes <= 0 || ExpiryMinutes > 1440)
            throw new InvalidOperationException("Jwt:ExpiryMinutes must be between 1 and 1440.");
    }
}

public interface IJwtTokenService
{
    string GenerateToken(Guid userId, string email, string displayName, IEnumerable<string> roles, IEnumerable<string> permissions);
}

public class JwtTokenService : IJwtTokenService
{
    private readonly JwtOptions _options;

    public JwtTokenService(IOptions<JwtOptions> options)
    {
        _options = options.Value;
    }

    public string GenerateToken(Guid userId, string email, string displayName, IEnumerable<string> roles, IEnumerable<string> permissions)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Name, displayName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        foreach (var permission in permissions)
        {
            claims.Add(new Claim("permission", permission));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SecretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_options.ExpiryMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public interface ICaseAuthorizationService
{
    Task<bool> HasCaseAccessAsync(Guid caseId, CaseAccessLevel requiredLevel, CancellationToken cancellationToken = default);
}

public class CaseAuthorizationService : ICaseAuthorizationService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public CaseAuthorizationService(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<bool> HasCaseAccessAsync(Guid caseId, CaseAccessLevel requiredLevel, CancellationToken cancellationToken = default)
    {
        // 1. Administrators have full access
        if (_currentUser.IsInRole("Administrator"))
            return true;

        var userId = _currentUser.UserId;
        if (!userId.HasValue)
            return false;

        var @case = await _context.Cases
            .Include(c => c.AccessGrants)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == caseId, cancellationToken);

        if (@case == null)
            return false;

        // 2. Assigned investigator has ReadWrite access
        if (@case.AssignedInvestigatorId == userId.Value)
            return true;

        // 3. Explicit Access Grants
        var grant = @case.AccessGrants.FirstOrDefault(g => g.UserId == userId.Value);
        if (grant != null && grant.AccessLevel >= requiredLevel)
            return true;

        // 4. Public or Internal cases allow Read if user has general Case.Read permission
        if (requiredLevel == CaseAccessLevel.Read &&
            @case.ConfidentialityLevel is ConfidentialityLevel.Public or ConfidentialityLevel.Internal &&
            _currentUser.HasPermission("Case.Read"))
        {
            return true;
        }

        return false;
    }
}
