using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TraceCore.Application.Common.Exceptions;
using TraceCore.Application.Common.Interfaces;
using TraceCore.Application.Common.Models;
using TraceCore.Domain.Entities.Cases;
using TraceCore.Domain.Entities.People;
using TraceCore.Domain.Enums;

namespace TraceCore.Application.People;

public sealed record PersonDto(
    Guid Id,
    string FirstName,
    string LastName,
    string DisplayName,
    string? Email,
    string? Phone,
    string? ExternalReference,
    DateTime? DateOfBirth,
    string Notes,
    DateTime CreatedAtUtc);

// 1. Create Person
public sealed record CreatePersonCommand(
    string FirstName,
    string LastName,
    string? Email = null,
    string? Phone = null,
    string? ExternalReference = null,
    DateTime? DateOfBirth = null,
    string? Notes = null) : IRequest<Guid>;

public class CreatePersonCommandValidator : AbstractValidator<CreatePersonCommand>
{
    public CreatePersonCommandValidator()
    {
        RuleFor(v => v.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(v => v.LastName).NotEmpty().MaximumLength(100);
        RuleFor(v => v.Email).EmailAddress().When(v => !string.IsNullOrEmpty(v.Email));
    }
}

public class CreatePersonCommandHandler : IRequestHandler<CreatePersonCommand, Guid>
{
    private readonly IApplicationDbContext _context;

    public CreatePersonCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> Handle(CreatePersonCommand request, CancellationToken cancellationToken)
    {
        var person = new Person(
            request.FirstName,
            request.LastName,
            request.Email,
            request.Phone,
            request.ExternalReference,
            request.DateOfBirth,
            request.Notes);

        _context.Add(person);
        await _context.SaveChangesAsync(cancellationToken);

        return person.Id;
    }
}

// 2. Update Person
public sealed record UpdatePersonCommand(
    Guid Id,
    string FirstName,
    string LastName,
    string? Email,
    string? Phone,
    string? ExternalReference,
    DateTime? DateOfBirth,
    string? Notes) : IRequest<Unit>;

public class UpdatePersonCommandHandler : IRequestHandler<UpdatePersonCommand, Unit>
{
    private readonly IApplicationDbContext _context;

    public UpdatePersonCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Unit> Handle(UpdatePersonCommand request, CancellationToken cancellationToken)
    {
        var person = await _context.People
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Person), request.Id);

        person.Update(
            request.FirstName,
            request.LastName,
            request.Email,
            request.Phone,
            request.ExternalReference,
            request.DateOfBirth,
            request.Notes);

        await _context.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

// 3. Link Person to Case
public sealed record LinkPersonToCaseCommand(
    Guid CaseId,
    Guid PersonId,
    ParticipantRole Role,
    string Notes) : IRequest<Unit>;

public class LinkPersonToCaseCommandHandler : IRequestHandler<LinkPersonToCaseCommand, Unit>
{
    private readonly IApplicationDbContext _context;

    public LinkPersonToCaseCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Unit> Handle(LinkPersonToCaseCommand request, CancellationToken cancellationToken)
    {
        var @case = await _context.Cases
            .Include(c => c.Persons)
            .FirstOrDefaultAsync(c => c.Id == request.CaseId, cancellationToken)
            ?? throw new NotFoundException(nameof(Case), request.CaseId);

        var personExists = await _context.People.AnyAsync(p => p.Id == request.PersonId, cancellationToken);
        if (!personExists)
            throw new NotFoundException(nameof(Person), request.PersonId);

        @case.AddPerson(request.PersonId, request.Role, request.Notes);
        await _context.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

// 4. Remove Person from Case
public sealed record RemovePersonFromCaseCommand(
    Guid CaseId,
    Guid PersonId,
    ParticipantRole Role) : IRequest<Unit>;

public class RemovePersonFromCaseCommandHandler : IRequestHandler<RemovePersonFromCaseCommand, Unit>
{
    private readonly IApplicationDbContext _context;

    public RemovePersonFromCaseCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Unit> Handle(RemovePersonFromCaseCommand request, CancellationToken cancellationToken)
    {
        var @case = await _context.Cases
            .Include(c => c.Persons)
            .FirstOrDefaultAsync(c => c.Id == request.CaseId, cancellationToken)
            ?? throw new NotFoundException(nameof(Case), request.CaseId);

        @case.RemovePerson(request.PersonId, request.Role);
        await _context.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

// 5. Get People Paged
public sealed class GetPeoplePagedQuery : PagedRequest, IRequest<PagedList<PersonDto>>
{
    public string? SearchTerm { get; set; }
}

public class GetPeoplePagedQueryHandler : IRequestHandler<GetPeoplePagedQuery, PagedList<PersonDto>>
{
    private readonly IApplicationDbContext _context;

    public GetPeoplePagedQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PagedList<PersonDto>> Handle(GetPeoplePagedQuery request, CancellationToken cancellationToken)
    {
        var query = _context.People.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim().ToLower();
            query = query.Where(p => p.DisplayName.ToLower().Contains(term) ||
                                     (p.Email != null && p.Email.ToLower().Contains(term)) ||
                                     (p.Phone != null && p.Phone.Contains(term)) ||
                                     (p.ExternalReference != null && p.ExternalReference.ToLower().Contains(term)));
        }

        int totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(p => p.LastName)
            .ThenBy(p => p.FirstName)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(p => new PersonDto(
                p.Id,
                p.FirstName,
                p.LastName,
                p.DisplayName,
                p.Email,
                p.Phone,
                p.ExternalReference,
                p.DateOfBirth,
                p.Notes,
                p.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return new PagedList<PersonDto>(items, totalCount, request.PageNumber, request.PageSize);
    }
}

// 6. Get Person By Id
public sealed record GetPersonByIdQuery(Guid Id) : IRequest<PersonDto>;

public class GetPersonByIdQueryHandler : IRequestHandler<GetPersonByIdQuery, PersonDto>
{
    private readonly IApplicationDbContext _context;

    public GetPersonByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PersonDto> Handle(GetPersonByIdQuery request, CancellationToken cancellationToken)
    {
        var person = await _context.People
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Person), request.Id);

        return new PersonDto(
            person.Id,
            person.FirstName,
            person.LastName,
            person.DisplayName,
            person.Email,
            person.Phone,
            person.ExternalReference,
            person.DateOfBirth,
            person.Notes,
            person.CreatedAtUtc);
    }
}
