using FluentAssertions;
using TraceCore.Domain.Common.Exceptions;
using TraceCore.Domain.Entities.Cases;
using TraceCore.Domain.Entities.Documents;
using TraceCore.Domain.Entities.Investigations;
using TraceCore.Domain.Enums;
using TraceCore.Domain.Events;
using Xunit;

namespace TraceCore.UnitTests.Domain;

public class InvestigationLifecycleTests
{
    private readonly Guid _caseId = Guid.NewGuid();
    private readonly Guid _leadId = Guid.NewGuid();

    private Investigation CreateActive() =>
        Investigation.Create(_caseId, "Ledger review", _leadId, "Verify entries.", true);

    [Fact]
    public void Start_FromPlanned_ShouldActivateAndRaiseEvent()
    {
        var inv = Investigation.Create(_caseId, "Planned probe", _leadId, "Objectives.", false);

        inv.Start(_leadId);

        inv.Status.Should().Be(InvestigationStatus.Active);
        inv.DomainEvents.Should().Contain(e => e is InvestigationStartedDomainEvent);
    }

    [Fact]
    public void Complete_FromPlanned_ShouldThrow()
    {
        var inv = Investigation.Create(_caseId, "Planned probe", _leadId, "Objectives.", false);

        var act = () => inv.Complete("Findings");

        act.Should().Throw<InvalidStateTransitionException>();
    }

    [Fact]
    public void Suspend_WithoutReason_ShouldThrow()
    {
        var inv = CreateActive();

        var act = () => inv.Suspend("  ");

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Suspend_ThenResume_ShouldReturnToActive()
    {
        var inv = CreateActive();

        inv.Suspend("Awaiting subpoena.");
        inv.Status.Should().Be(InvestigationStatus.Suspended);
        inv.DomainEvents.Should().Contain(e => e is InvestigationSuspendedDomainEvent);

        inv.Start(_leadId);
        inv.Status.Should().Be(InvestigationStatus.Active);
    }

    [Fact]
    public void Complete_FromSuspended_ShouldThrow()
    {
        var inv = CreateActive();
        inv.Suspend("Awaiting subpoena.");

        var act = () => inv.Complete("Done");

        act.Should().Throw<InvalidStateTransitionException>();
    }

    [Fact]
    public void Close_ShouldTerminateAndRaiseEvent()
    {
        var inv = CreateActive();

        inv.Close();

        inv.Status.Should().Be(InvestigationStatus.Closed);
        inv.EndDateUtc.Should().NotBeNull();
        inv.DomainEvents.Should().Contain(e => e is InvestigationClosedDomainEvent);
    }

    [Theory]
    [InlineData(0, RiskLevel.Low)]
    [InlineData(29, RiskLevel.Low)]
    [InlineData(30, RiskLevel.Medium)]
    [InlineData(59, RiskLevel.Medium)]
    [InlineData(60, RiskLevel.High)]
    [InlineData(79, RiskLevel.High)]
    [InlineData(80, RiskLevel.Critical)]
    [InlineData(100, RiskLevel.Critical)]
    public void RiskLevelForScore_ShouldDeriveExpectedLevel(int score, RiskLevel expected)
    {
        Case.RiskLevelForScore(score).Should().Be(expected);
    }

    [Fact]
    public void DocumentVersion_WithEmptyHash_ShouldThrow()
    {
        var act = () => new DocumentVersion(
            Guid.NewGuid(), 1, "report.pdf", "application/pdf", 1024, "storage/abc", "  ", Guid.NewGuid(), "Initial");

        act.Should().Throw<DomainException>();
    }
}
