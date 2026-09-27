using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TraceCore.Application.Common.Interfaces;
using TraceCore.Domain.Entities.Cases;
using TraceCore.Domain.Enums;
using TaskStatus = TraceCore.Domain.Enums.TaskStatus;

namespace TraceCore.Infrastructure.BackgroundWorkers;

// 1. Transactional Outbox Processor
public class OutboxProcessorWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OutboxProcessorWorker> _logger;

    public OutboxProcessorWorker(IServiceProvider serviceProvider, ILogger<OutboxProcessorWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TraceCore Outbox Processor Worker is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
                var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();

                var pendingMessages = await context.OutboxMessages
                    .Where(m => m.ProcessedOnUtc == null && m.RetryCount < 5)
                    .OrderBy(m => m.OccurredOnUtc)
                    .Take(25)
                    .ToListAsync(stoppingToken);

                foreach (var message in pendingMessages)
                {
                    try
                    {
                        var eventType = Type.GetType(message.Type);
                        if (eventType == null)
                        {
                            message.MarkFailed($"Unknown event type '{message.Type}'.");
                            continue;
                        }

                        var domainEvent = JsonSerializer.Deserialize(message.ContentJson, eventType);
                        if (domainEvent is not INotification notification)
                        {
                            message.MarkFailed($"Message content is not a dispatchable notification for type '{message.Type}'.");
                            continue;
                        }

                        await publisher.Publish(notification, stoppingToken);
                        message.MarkProcessed();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to dispatch outbox message #{Id} of type {Type}.", message.Id, message.Type);
                        message.MarkFailed(ex.Message);
                    }
                }

                if (pendingMessages.Count > 0)
                {
                    await context.SaveChangesAsync(stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during Outbox processing cycle.");
            }

            await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
        }
    }
}

// 2. SLA Monitoring Worker
public class SlaMonitoringWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SlaMonitoringWorker> _logger;

    public SlaMonitoringWorker(IServiceProvider serviceProvider, ILogger<SlaMonitoringWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TraceCore SLA Monitoring Worker is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
                var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
                var now = DateTime.UtcNow;

                var breachedCases = await context.Cases
                    .Where(c => !c.IsSlaBreached &&
                                c.SlaDeadlineUtc.HasValue &&
                                c.SlaDeadlineUtc.Value < now &&
                                (c.Status == CaseStatus.Open || c.Status == CaseStatus.UnderInvestigation))
                    .Take(50)
                    .ToListAsync(stoppingToken);

                foreach (var @case in breachedCases)
                {
                    @case.MarkSlaBreached(now);
                    _logger.LogWarning("SLA breached for Case {CaseNumber} (Id: {CaseId}). Deadline was {Deadline}.",
                        @case.CaseNumber, @case.Id, @case.SlaDeadlineUtc);
                }

                if (breachedCases.Count > 0)
                {
                    // Persist breach state before notifying so alerts never precede committed state.
                    await context.SaveChangesAsync(stoppingToken);

                    foreach (var @case in breachedCases)
                    {
                        if (@case.AssignedInvestigatorId.HasValue)
                        {
                            await notificationService.SendAsync(
                                @case.AssignedInvestigatorId.Value,
                                $"SLA Breached: {@case.CaseNumber}",
                                $"The SLA deadline for case '{@case.Title}' has expired.",
                                NotificationType.SlaBreached,
                                "Case",
                                @case.Id,
                                stoppingToken);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during SLA Monitoring cycle.");
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}

// 3. Task Overdue Monitoring Worker
public class TaskOverdueWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TaskOverdueWorker> _logger;

    public TaskOverdueWorker(IServiceProvider serviceProvider, ILogger<TaskOverdueWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TraceCore Task Overdue Worker is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
                var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
                var now = DateTime.UtcNow;

                var overdueTasks = await context.Tasks
                    .Where(t => t.DueDateUtc.HasValue &&
                                t.DueDateUtc.Value < now &&
                                t.Status != TaskStatus.Completed &&
                                t.Status != TaskStatus.Cancelled &&
                                t.AssignedToUserId.HasValue)
                    .Take(50)
                    .ToListAsync(stoppingToken);

                // Skip tasks already notified to avoid re-alerting every cycle.
                var overdueIds = overdueTasks.Select(t => t.Id).ToList();
                var alreadyNotified = await context.Notifications
                    .AsNoTracking()
                    .Where(n => n.Type == NotificationType.TaskOverdue &&
                                n.ReferenceType == "Task" &&
                                n.ReferenceId.HasValue &&
                                overdueIds.Contains(n.ReferenceId.Value))
                    .Select(n => n.ReferenceId!.Value)
                    .Distinct()
                    .ToListAsync(stoppingToken);
                var notified = new HashSet<Guid>(alreadyNotified);

                foreach (var task in overdueTasks)
                {
                    task.MarkOverdue();
                }

                if (overdueTasks.Count > 0)
                {
                    // Persist overdue state before notifying so alerts never precede committed state.
                    await context.SaveChangesAsync(stoppingToken);

                    foreach (var task in overdueTasks)
                    {
                        if (!task.AssignedToUserId.HasValue || notified.Contains(task.Id))
                            continue;

                        await notificationService.SendAsync(
                            task.AssignedToUserId.Value,
                            $"Task Overdue: {task.Title}",
                            $"Task due date was {task.DueDateUtc:u}.",
                            NotificationType.TaskOverdue,
                            "Task",
                            task.Id,
                            stoppingToken);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during Task Overdue cycle.");
            }

            await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);
        }
    }
}
