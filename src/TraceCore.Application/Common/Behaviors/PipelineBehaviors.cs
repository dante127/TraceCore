using System.Diagnostics;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using TraceCore.Application.Common.Exceptions;
using TraceCore.Application.Common.Interfaces;

namespace TraceCore.Application.Common.Behaviors;

public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (_validators.Any())
        {
            var context = new ValidationContext<TRequest>(request);
            var validationResults = await Task.WhenAll(
                _validators.Select(v => v.ValidateAsync(context, cancellationToken)));

            var failures = validationResults
                .Where(r => r.Errors.Count != 0)
                .SelectMany(r => r.Errors)
                .ToList();

            if (failures.Count != 0)
                throw new Exceptions.ValidationException(failures);
        }

        return await next();
    }
}

public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<TRequest> _logger;
    private readonly ICurrentUserService _currentUserService;

    public LoggingBehavior(ILogger<TRequest> logger, ICurrentUserService currentUserService)
    {
        _logger = logger;
        _currentUserService = currentUserService;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var userId = _currentUserService.UserId?.ToString() ?? "Anonymous";

        // Payloads are intentionally not logged: requests carry PII case data.
        _logger.LogInformation("TraceCore Request: {Name} {@UserId}", requestName, userId);

        try
        {
            var response = await next();

            _logger.LogInformation("TraceCore Response: {Name} handled successfully.", requestName);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "TraceCore Request: {Name} failed for {@UserId}.", requestName, userId);
            throw;
        }
    }
}

public class PerformanceBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<TRequest> _logger;
    private readonly ICurrentUserService _currentUserService;

    public PerformanceBehavior(ILogger<TRequest> logger, ICurrentUserService currentUserService)
    {
        _logger = logger;
        _currentUserService = currentUserService;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var timer = Stopwatch.StartNew();

        try
        {
            return await next();
        }
        finally
        {
            timer.Stop();

            if (timer.ElapsedMilliseconds > 500)
            {
                var requestName = typeof(TRequest).Name;
                var userId = _currentUserService.UserId?.ToString() ?? "Anonymous";

                _logger.LogWarning("TraceCore Long Running Request: {Name} ({ElapsedMilliseconds} ms) {@UserId}",
                    requestName, timer.ElapsedMilliseconds, userId);
            }
        }
    }
}
