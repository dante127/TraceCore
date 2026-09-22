using FluentAssertions;
using TraceCore.Domain.Common.Exceptions;
using TraceCore.Domain.Entities.Investigations;
using TraceCore.Domain.Entities.Tasks;
using TraceCore.Domain.Enums;
using TraceCore.Domain.Events;
using Xunit;
using TaskStatus = TraceCore.Domain.Enums.TaskStatus;

namespace TraceCore.UnitTests.Domain;

public class TaskAndInvestigationTests
{
    private readonly Guid _caseId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    [Fact]
    public void Task_Create_ShouldInitializeInTodoStatus()
    {
        // Act
        var task = CaseTask.Create(
            _caseId,
            "Interview CFO",
            "Collect financial disclosures",
            TaskPriority.High,
            _userId,
            DateTime.UtcNow.AddDays(3));

        // Assert
        task.Status.Should().Be(TaskStatus.Todo);
        task.Priority.Should().Be(TaskPriority.High);
        task.DomainEvents.Should().ContainSingle(e => e is TaskCreatedDomainEvent);
    }

    [Fact]
    public void Task_UpdateStatus_ToCompleted_ShouldSetCompletedDateAndEmitEvent()
    {
        // Arrange
        var task = CaseTask.Create(
            _caseId,
            "Forensics Analysis",
            "Details",
            TaskPriority.Urgent,
            _userId,
            DateTime.UtcNow.AddDays(1));

        task.UpdateStatus(TaskStatus.InProgress, _userId);

        // Act
        task.UpdateStatus(TaskStatus.Completed, _userId);

        // Assert
        task.Status.Should().Be(TaskStatus.Completed);
        task.CompletedAtUtc.Should().NotBeNull();
        task.DomainEvents.Should().Contain(e => e is TaskCompletedDomainEvent);
    }

    [Fact]
    public void Task_IsOverdue_WhenPastDueDateAndNotCompleted_ShouldReturnTrue()
    {
        // Arrange
        var pastDue = DateTime.UtcNow.AddDays(-2);
        var task = CaseTask.Create(
            _caseId,
            "Overdue Subpoena",
            "Details",
            TaskPriority.High,
            _userId,
            pastDue);

        // Act & Assert
        task.IsOverdue().Should().BeTrue();
    }

    [Fact]
    public void Investigation_AddActivity_ShouldRecordActivity()
    {
        // Arrange
        var investigation = Investigation.Create(
            _caseId,
            "Preliminary Accounting Audit",
            _userId,
            "Audit all ledger records from Q3.");

        // Act
        var activity = investigation.AddActivity(
            ActivityType.Interview,
            _userId,
            DateTime.UtcNow,
            "Interview of Lead Accountant",
            "Accountant confirmed unverified entries.",
            "Meeting Room 1",
            "Notes");

        // Assert
        investigation.Activities.Should().ContainSingle();
        activity.ActivityType.Should().Be(ActivityType.Interview);
        activity.Description.Should().Be("Interview of Lead Accountant");
    }

    [Fact]
    public void Investigation_Complete_ShouldUpdateStatusAndEndDate()
    {
        // Arrange
        var investigation = Investigation.Create(
            _caseId,
            "Full Investigation",
            _userId,
            "Objectives");

        // Act
        investigation.Complete("Final conclusions: fraud substantiated with documentary proof.");

        // Assert
        investigation.Status.Should().Be(InvestigationStatus.Completed);
        investigation.Findings.Should().Contain("fraud substantiated");
        investigation.EndDateUtc.Should().NotBeNull();
        investigation.DomainEvents.Should().Contain(e => e is InvestigationCompletedDomainEvent);
    }
}
