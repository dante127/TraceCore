using System.Reflection;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using TraceCore.Application.Common.Behaviors;
using TraceCore.Application.Common.Interfaces;
using TraceCore.Application.Risk;
using TraceCore.Application.SLA;

namespace TraceCore.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        // 1. FluentValidation
        services.AddValidatorsFromAssembly(assembly);

        // 2. MediatR with Pipeline Behaviors
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(PerformanceBehavior<,>));
        });

        // 3. Domain Services
        services.AddScoped<IRiskAssessmentService, RiskAssessmentService>();
        services.AddSingleton<ISlaCalculationService, SlaCalculationService>();

        return services;
    }
}
