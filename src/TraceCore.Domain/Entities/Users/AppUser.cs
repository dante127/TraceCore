using TraceCore.Domain.Common;
using TraceCore.Domain.Common.Exceptions;

namespace TraceCore.Domain.Entities.Users;

public class AppUser : BaseEntity, IAggregateRoot
{
    public string Email { get; private set; } = default!;
    public string DisplayName { get; private set; } = default!;
    public string PasswordHash { get; private set; } = default!;
    public bool IsActive { get; private set; } = true;
    public string RolesCsv { get; private set; } = string.Empty;

    protected AppUser() : base() { }

    public AppUser(string email, string displayName, string passwordHash, IEnumerable<string> roles)
        : base()
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new DomainException("Email is required.");
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new DomainException("Password hash is required.");

        Email = email.Trim().ToLowerInvariant();
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? Email : displayName.Trim();
        PasswordHash = passwordHash;
        SetRoles(roles);
    }

    public AppUser(Guid id, string email, string displayName, string passwordHash, IEnumerable<string> roles)
        : base(id)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new DomainException("Email is required.");
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new DomainException("Password hash is required.");

        Email = email.Trim().ToLowerInvariant();
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? Email : displayName.Trim();
        PasswordHash = passwordHash;
        SetRoles(roles);
    }

    public IReadOnlyList<string> GetRoles() =>
        RolesCsv.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public void SetRoles(IEnumerable<string> roles)
    {
        var cleaned = (roles ?? []).Select(r => r.Trim()).Where(r => !string.IsNullOrWhiteSpace(r)).Distinct().ToArray();
        RolesCsv = string.Join(';', cleaned);
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void SetPasswordHash(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new DomainException("Password hash is required.");
        PasswordHash = passwordHash;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
