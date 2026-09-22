using TraceCore.Domain.Common;
using TraceCore.Domain.Common.Exceptions;

namespace TraceCore.Domain.Entities.Organizations;

public class Organization : BaseEntity, IAggregateRoot
{
    public string Name { get; private set; } = default!;
    public string? RegistrationNumber { get; private set; }
    public string? Industry { get; private set; }
    public string? Email { get; private set; }
    public string? Phone { get; private set; }
    public string? Address { get; private set; }
    public string Notes { get; private set; } = string.Empty;

    protected Organization() : base() { }

    public Organization(
        string name,
        string? registrationNumber = null,
        string? industry = null,
        string? email = null,
        string? phone = null,
        string? address = null,
        string? notes = null) : base()
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Organization name is required.");

        Name = name.Trim();
        RegistrationNumber = registrationNumber?.Trim();
        Industry = industry?.Trim();
        Email = email?.Trim().ToLowerInvariant();
        Phone = phone?.Trim();
        Address = address?.Trim();
        Notes = notes?.Trim() ?? string.Empty;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public void Update(
        string name,
        string? registrationNumber,
        string? industry,
        string? email,
        string? phone,
        string? address,
        string? notes)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Organization name is required.");

        Name = name.Trim();
        RegistrationNumber = registrationNumber?.Trim();
        Industry = industry?.Trim();
        Email = email?.Trim().ToLowerInvariant();
        Phone = phone?.Trim();
        Address = address?.Trim();
        Notes = notes?.Trim() ?? string.Empty;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
