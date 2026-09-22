using TraceCore.Application.Common.Interfaces;
using TraceCore.Domain.Enums;

namespace TraceCore.Application.SLA;

public class SlaCalculationService : ISlaCalculationService
{
    public DateTime CalculateDeadline(CasePriority priority, CaseType type, DateTime fromUtc)
    {
        int targetHours = GetTargetHours(priority, type);
        return fromUtc.AddHours(targetHours);
    }

    public int GetTargetHours(CasePriority priority, CaseType type)
    {
        // Base hours from priority
        int hours = priority switch
        {
            CasePriority.Critical => 24,
            CasePriority.High => 72,
            CasePriority.Medium => 168, // 7 days
            _ => 336                     // 14 days
        };

        // CyberCrime and InternalAffairs require tighter turnaround
        if (type is CaseType.CyberCrime or CaseType.InternalAffairs && priority == CasePriority.High)
        {
            hours = 48; // 2 days instead of 3
        }

        return hours;
    }

    public bool IsBreached(DateTime deadlineUtc, DateTime currentUtc)
    {
        return currentUtc > deadlineUtc;
    }

    public double CalculateRemainingHours(DateTime deadlineUtc, DateTime currentUtc)
    {
        var remaining = (deadlineUtc - currentUtc).TotalHours;
        return Math.Round(remaining, 1);
    }
}
