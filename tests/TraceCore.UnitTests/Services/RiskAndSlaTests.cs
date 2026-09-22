using FluentAssertions;
using TraceCore.Application.SLA;
using TraceCore.Domain.Enums;
using Xunit;

namespace TraceCore.UnitTests.Services;

public class RiskAndSlaTests
{
    private readonly SlaCalculationService _slaService = new();

    [Theory]
    [InlineData(CasePriority.Critical, CaseType.FinancialCrime, 24)]
    [InlineData(CasePriority.High, CaseType.FinancialCrime, 72)]
    [InlineData(CasePriority.High, CaseType.CyberCrime, 48)]
    [InlineData(CasePriority.High, CaseType.InternalAffairs, 48)]
    [InlineData(CasePriority.Medium, CaseType.Fraud, 168)]
    [InlineData(CasePriority.Low, CaseType.Compliance, 336)]
    public void GetTargetHours_ShouldReturnExpectedConfiguredHours(CasePriority priority, CaseType type, int expectedHours)
    {
        // Act
        int hours = _slaService.GetTargetHours(priority, type);

        // Assert
        hours.Should().Be(expectedHours);
    }

    [Fact]
    public void CalculateDeadline_ShouldAddExpectedHoursFromGivenTime()
    {
        // Arrange
        var baseTime = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

        // Act
        var deadline = _slaService.CalculateDeadline(CasePriority.Critical, CaseType.FinancialCrime, baseTime);

        // Assert
        deadline.Should().Be(baseTime.AddHours(24));
    }

    [Fact]
    public void IsBreached_WhenCurrentTimeAfterDeadline_ShouldReturnTrue()
    {
        // Arrange
        var deadline = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var currentTime = deadline.AddMinutes(5);

        // Act & Assert
        _slaService.IsBreached(deadline, currentTime).Should().BeTrue();
    }

    [Fact]
    public void IsBreached_WhenCurrentTimeBeforeDeadline_ShouldReturnFalse()
    {
        // Arrange
        var deadline = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var currentTime = deadline.AddMinutes(-30);

        // Act & Assert
        _slaService.IsBreached(deadline, currentTime).Should().BeFalse();
    }

    [Fact]
    public void CalculateRemainingHours_ShouldComputeAccurateDelta()
    {
        // Arrange
        var baseTime = DateTime.UtcNow;
        var deadline = baseTime.AddHours(12.5);

        // Act
        var remaining = _slaService.CalculateRemainingHours(deadline, baseTime);

        // Assert
        remaining.Should().Be(12.5);
    }
}
