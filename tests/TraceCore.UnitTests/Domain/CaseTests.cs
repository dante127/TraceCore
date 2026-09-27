using FluentAssertions;
using TraceCore.Domain.Common.Exceptions;
using TraceCore.Domain.Entities.Cases;
using TraceCore.Domain.Enums;
using TraceCore.Domain.Events;
using Xunit;

namespace TraceCore.UnitTests.Domain;

public class CaseTests
{
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _investigatorId = Guid.NewGuid();

    [Fact]
    public void CreateOpen_ShouldInitializeCase_WithOpenStatusAndDomainEvents()
    {
        // Act
        var @case = Case.CreateOpen(
            "CAS-2026-0001",
            "Test Financial Fraud",
            "Detailed Description",
            CaseType.FinancialCrime,
            CasePriority.High,
            ConfidentialityLevel.Confidential,
            _userId,
            72);

        // Assert
        @case.CaseNumber.Should().Be("CAS-2026-0001");
        @case.Status.Should().Be(CaseStatus.Open);
        @case.Priority.Should().Be(CasePriority.High);
        @case.OpenedAtUtc.Should().NotBeNull();
        @case.SlaDeadlineUtc.Should().NotBeNull();
        @case.IsSlaBreached.Should().BeFalse();
        @case.DomainEvents.Should().ContainSingle(e => e is CaseCreatedDomainEvent);
    }

    [Fact]
    public void TransitionStatus_FromOpenToUnderInvestigation_ShouldSucceed()
    {
        // Arrange
        var @case = Case.CreateOpen(
            "CAS-2026-0002",
            "Cyber Intrusion",
            "Description",
            CaseType.CyberCrime,
            CasePriority.Medium,
            ConfidentialityLevel.Internal,
            _userId,
            168);

        // Act
        @case.TransitionStatus(CaseStatus.UnderInvestigation, _investigatorId, "Investigation formally launched.");

        // Assert
        @case.Status.Should().Be(CaseStatus.UnderInvestigation);
        @case.DomainEvents.Should().Contain(e => e is CaseStatusChangedDomainEvent);
    }

    [Fact]
    public void TransitionStatus_InvalidTransition_ShouldThrowInvalidStateTransitionException()
    {
        // Arrange
        var @case = Case.CreateDraft(
            "CAS-2026-0003",
            "Draft Case",
            "Description",
            CaseType.Regulatory,
            CasePriority.Low,
            ConfidentialityLevel.Public,
            _userId,
            336);

        // Act & Assert (Draft cannot jump directly to Resolved)
        var act = () => @case.TransitionStatus(CaseStatus.Resolved, _userId, "Invalid skip");

        act.Should().Throw<InvalidStateTransitionException>()
            .WithMessage("*Draft*Resolved*");
    }

    [Fact]
    public void MarkSlaBreached_WhenCalled_ShouldSetBreachedAndRaiseEvent()
    {
        // Arrange
        var @case = Case.CreateOpen(
            "CAS-2026-0004",
            "SLA Test Case",
            "Description",
            CaseType.InternalAffairs,
            CasePriority.Low,
            ConfidentialityLevel.Confidential,
            _userId,
            24);

        var breachTime = DateTime.UtcNow;

        // Act
        @case.MarkSlaBreached(breachTime);

        // Assert
        @case.IsSlaBreached.Should().BeTrue();
        @case.SlaBreachedAtUtc.Should().Be(breachTime);
        // Priority should auto-escalate from Low to Medium
        @case.Priority.Should().Be(CasePriority.Medium);
        @case.DomainEvents.Should().Contain(e => e is CaseSlaBreachedDomainEvent);
    }

    [Fact]
    public void UpdateRiskAssessment_WhenScoreReachesCritical_ShouldAutoEscalatePriorityToHigh()
    {
        // Arrange
        var @case = Case.CreateOpen(
            "CAS-2026-0005",
            "High Risk Escalation",
            "Description",
            CaseType.Fraud,
            CasePriority.Low,
            ConfidentialityLevel.Internal,
            _userId,
            168);

        // Act (Critical risk score >= 80 derives Critical level and escalates priority)
        @case.UpdateRiskAssessment(85, "Critical evidence and SLA breach detected.");

        // Assert
        @case.CurrentRiskScore.Should().Be(85);
        @case.CurrentRiskLevel.Should().Be(RiskLevel.Critical);
        @case.Priority.Should().Be(CasePriority.High); // auto-escalated
        @case.DomainEvents.Should().Contain(e => e is CaseRiskLevelChangedDomainEvent);
        @case.DomainEvents.Should().Contain(e => e is CasePriorityChangedDomainEvent);
    }

    [Fact]
    public void AssignInvestigator_ShouldUpdateInvestigator_AndRaiseDomainEvent()
    {
        // Arrange
        var @case = Case.CreateOpen(
            "CAS-2026-0006",
            "Assignment Test",
            "Description",
            CaseType.Compliance,
            CasePriority.Medium,
            ConfidentialityLevel.Internal,
            _userId,
            168);

        // Act
        @case.AssignInvestigator(_investigatorId, _userId);

        // Assert
        @case.AssignedInvestigatorId.Should().Be(_investigatorId);
        @case.DomainEvents.Should().Contain(e => e is CaseAssignedDomainEvent);
    }
}
