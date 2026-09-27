using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TraceCore.Application.Common.Interfaces;
using TraceCore.Infrastructure.BackgroundWorkers;
using TraceCore.Infrastructure.Caching;
using TraceCore.Infrastructure.Persistence;
using TraceCore.Infrastructure.Security;
using TraceCore.Infrastructure.Services;
using TraceCore.Infrastructure.Storage;

namespace TraceCore.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // 1. Interceptors
        services.AddScoped<AuditInterceptor>();

        // 2. Database Context
        bool useInMemory = configuration.GetValue<bool>("UseInMemoryDatabase");
        if (useInMemory)
        {
            services.AddDbContext<TraceCoreDbContext>((sp, options) =>
            {
                options.UseInMemoryDatabase("TraceCoreIntegrationDb");
                var auditInterceptor = sp.GetRequiredService<AuditInterceptor>();
                options.AddInterceptors(auditInterceptor);
            });
        }
        else
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? "Server=(localdb)\\mssqllocaldb;Database=TraceCoreDb;Trusted_Connection=True;MultipleActiveResultSets=true";

            services.AddDbContext<TraceCoreDbContext>((sp, options) =>
            {
                var auditInterceptor = sp.GetRequiredService<AuditInterceptor>();
                options.UseSqlServer(connectionString, sqlOptions =>
                {
                    sqlOptions.MigrationsAssembly(typeof(TraceCoreDbContext).Assembly.FullName);
                    sqlOptions.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorNumbersToAdd: null);
                });
                options.AddInterceptors(auditInterceptor);
            });
        }

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<TraceCoreDbContext>());

        // 3. Distributed Caching (Redis with MemoryCache fallback)
        var redisConnection = configuration.GetConnectionString("Redis");
        if (!string.IsNullOrWhiteSpace(redisConnection))
        {
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnection;
                options.InstanceName = "TraceCore:";
            });
        }
        else
        {
            services.AddDistributedMemoryCache();
        }

        services.AddSingleton<ICacheService, RedisCacheService>();

        // 4. File Storage
        services.AddSingleton<IFileStorage, LocalFileStorage>();

        // 5. Core Services
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
        services.AddScoped<INotificationService, NotificationService>();

        // 6. Security
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddScoped<ICaseAuthorizationService, CaseAuthorizationService>();

        // 7. Hosted Services / Background Workers
        services.AddHostedService<OutboxProcessorWorker>();
        services.AddHostedService<SlaMonitoringWorker>();
        services.AddHostedService<TaskOverdueWorker>();

        return services;
    }
}
