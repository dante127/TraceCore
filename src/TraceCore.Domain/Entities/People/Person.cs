using TraceCore.Domain.Common;
using TraceCore.Domain.Common.Exceptions;

namespace TraceCore.Domain.Entities.People;

public class Person : BaseEntity, IAggregateRoot
{
    public string FirstName { get; private set; } = default!;
    public string LastName { get; private set; } = default!;
    public string DisplayName { get; private set; } = default!;
    public string? Email { get; private set; }
    public string? Phone { get; private set; }
    public string? ExternalReference { get; private set; }
    public DateTime? DateOfBirth { get; private set; }
    public string Notes { get; private set; } = string.Empty;

    protected Person() : base() { }

    public Person(
        string firstName,
        string lastName,
        string? email = null,
        string? phone = null,
        string? externalReference = null,
        DateTime? dateOfBirth = null,
        string? notes = null) : base()
    {
        if (string.IsNullOrWhiteSpace(firstName))
            throw new DomainException("First name is required.");
        if (string.IsNullOrWhiteSpace(lastName))
            throw new DomainException("Last name is required.");

        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        DisplayName = $"{FirstName} {LastName}";
        Email = email?.Trim().ToLowerInvariant();
        Phone = phone?.Trim();
        ExternalReference = externalReference?.Trim();
        DateOfBirth = dateOfBirth;
        Notes = notes?.Trim() ?? string.Empty;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public void Update(
        string firstName,
        string lastName,
        string? email,
        string? phone,
        string? externalReference,
        DateTime? dateOfBirth,
        string? notes)
    {
        if (string.IsNullOrWhiteSpace(firstName))
            throw new DomainException("First name is required.");
        if (string.IsNullOrWhiteSpace(lastName))
            throw new DomainException("Last name is required.");

        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        DisplayName = $"{FirstName} {LastName}";
        Email = email?.Trim().ToLowerInvariant();
        Phone = phone?.Trim();
        ExternalReference = externalReference?.Trim();
        DateOfBirth = dateOfBirth;
        Notes = notes?.Trim() ?? string.Empty;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
