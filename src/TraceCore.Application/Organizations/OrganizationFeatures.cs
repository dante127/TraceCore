using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TraceCore.Application.Common.Exceptions;
using TraceCore.Application.Common.Interfaces;
using TraceCore.Application.Common.Models;
using TraceCore.Domain.Entities.Cases;
using TraceCore.Domain.Entities.Organizations;

namespace TraceCore.Application.Organizations;

public sealed record OrganizationDto(
    Guid Id,
    string Name,
    string? RegistrationNumber,
    string? Industry,
    string? Email,
    string? Phone,
    string? Address,
    string Notes,
    DateTime CreatedAtUtc);

// 1. Create Organization
public sealed record CreateOrganizationCommand(
    string Name,
    string? RegistrationNumber = null,
    string? Industry = null,
    string? Email = null,
    string? Phone = null,
    string? Address = null,
    string? Notes = null) : IRequest<Guid>;

public class CreateOrganizationCommandValidator : AbstractValidator<CreateOrganizationCommand>
{
    public CreateOrganizationCommandValidator()
    {
        RuleFor(v => v.Name).NotEmpty().MaximumLength(200);
        RuleFor(v => v.Email).EmailAddress().When(v => !string.IsNullOrEmpty(v.Email));
    }
}

public class CreateOrganizationCommandHandler : IRequestHandler<CreateOrganizationCommand, Guid>
{
    private readonly IApplicationDbContext _context;

    public CreateOrganizationCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> Handle(CreateOrganizationCommand request, CancellationToken cancellationToken)
    {
        var org = new Organization(
            request.Name,
            request.RegistrationNumber,
            request.Industry,
            request.Email,
            request.Phone,
            request.Address,
            request.Notes);

        _context.Add(org);
        await _context.SaveChangesAsync(cancellationToken);

        return org.Id;
    }
}

// 2. Update Organization
public sealed record UpdateOrganizationCommand(
    Guid Id,
    string Name,
    string? RegistrationNumber,
    string? Industry,
    string? Email,
    string? Phone,
    string? Address,
    string? Notes) : IRequest<Unit>;

public class UpdateOrganizationCommandValidator : AbstractValidator<UpdateOrganizationCommand>
{
    public UpdateOrganizationCommandValidator()
    {
        RuleFor(v => v.Id).NotEmpty();
        RuleFor(v => v.Name).NotEmpty().MaximumLength(200);
        RuleFor(v => v.Email).EmailAddress().When(v => !string.IsNullOrEmpty(v.Email));
    }
}

public class UpdateOrganizationCommandHandler : IRequestHandler<UpdateOrganizationCommand, Unit>
{
    private readonly IApplicationDbContext _context;

    public UpdateOrganizationCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Unit> Handle(UpdateOrganizationCommand request, CancellationToken cancellationToken)
    {
        var org = await _context.Organizations
            .FirstOrDefaultAsync(o => o.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Organization), request.Id);

        org.Update(
            request.Name,
            request.RegistrationNumber,
            request.Industry,
            request.Email,
            request.Phone,
            request.Address,
            request.Notes);

        await _context.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

// 3. Link Organization to Case
public sealed record LinkOrganizationToCaseCommand(
    Guid CaseId,
    Guid OrganizationId,
    string Role,
    string Notes) : IRequest<Unit>;

public class LinkOrganizationToCaseCommandValidator : AbstractValidator<LinkOrganizationToCaseCommand>
{
    public LinkOrganizationToCaseCommandValidator()
    {
        RuleFor(v => v.CaseId).NotEmpty();
        RuleFor(v => v.OrganizationId).NotEmpty();
        RuleFor(v => v.Role).NotEmpty().MaximumLength(100);
        RuleFor(v => v.Notes).MaximumLength(1000).When(v => v.Notes != null);
    }
}

public class LinkOrganizationToCaseCommandHandler : IRequestHandler<LinkOrganizationToCaseCommand, Unit>
{
    private readonly IApplicationDbContext _context;

    public LinkOrganizationToCaseCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Unit> Handle(LinkOrganizationToCaseCommand request, CancellationToken cancellationToken)
    {
        var @case = await _context.Cases
            .Include(c => c.Organizations)
            .FirstOrDefaultAsync(c => c.Id == request.CaseId, cancellationToken)
            ?? throw new NotFoundException(nameof(Case), request.CaseId);

        var orgExists = await _context.Organizations.AnyAsync(o => o.Id == request.OrganizationId, cancellationToken);
        if (!orgExists)
            throw new NotFoundException(nameof(Organization), request.OrganizationId);

        var link = @case.AddOrganization(request.OrganizationId, request.Role, request.Notes);
        if (link is not null)
            _context.Add(link);
        await _context.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

// 4. Get Organizations Paged
public sealed class GetOrganizationsPagedQuery : PagedRequest, IRequest<PagedList<OrganizationDto>>
{
    public string? SearchTerm { get; set; }
}

public class GetOrganizationsPagedQueryHandler : IRequestHandler<GetOrganizationsPagedQuery, PagedList<OrganizationDto>>
{
    private readonly IApplicationDbContext _context;

    public GetOrganizationsPagedQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PagedList<OrganizationDto>> Handle(GetOrganizationsPagedQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Organizations.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim().ToLower();
            query = query.Where(o => o.Name.ToLower().Contains(term) ||
                                     (o.RegistrationNumber != null && o.RegistrationNumber.ToLower().Contains(term)) ||
                                     (o.Industry != null && o.Industry.ToLower().Contains(term)));
        }

        int totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(o => o.Name)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(o => new OrganizationDto(
                o.Id,
                o.Name,
                o.RegistrationNumber,
                o.Industry,
                o.Email,
                o.Phone,
                o.Address,
                o.Notes,
                o.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return new PagedList<OrganizationDto>(items, totalCount, request.PageNumber, request.PageSize);
    }
}

// 5. Get Organization By Id
public sealed record GetOrganizationByIdQuery(Guid Id) : IRequest<OrganizationDto>;

public class GetOrganizationByIdQueryHandler : IRequestHandler<GetOrganizationByIdQuery, OrganizationDto>
{
    private readonly IApplicationDbContext _context;

    public GetOrganizationByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<OrganizationDto> Handle(GetOrganizationByIdQuery request, CancellationToken cancellationToken)
    {
        var org = await _context.Organizations
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Organization), request.Id);

        return new OrganizationDto(
            org.Id,
            org.Name,
            org.RegistrationNumber,
            org.Industry,
            org.Email,
            org.Phone,
            org.Address,
            org.Notes,
            org.CreatedAtUtc);
    }
}
