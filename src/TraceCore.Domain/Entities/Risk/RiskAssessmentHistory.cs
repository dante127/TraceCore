using TraceCore.Domain.Common;
using TraceCore.Domain.Enums;

namespace TraceCore.Domain.Entities.Risk;

public class RiskAssessmentHistory : BaseEntity
{
    public Guid CaseId { get; private set; }
    public int RiskScore { get; private set; }
    public RiskLevel RiskLevel { get; private set; }
    public DateTime EvaluatedAtUtc { get; private set; } = DateTime.UtcNow;
    public string TriggerReason { get; private set; } = string.Empty;
    public string FactorsJson { get; private set; } = "{}";

    protected RiskAssessmentHistory() : base() { }

    public RiskAssessmentHistory(
        Guid caseId,
        int riskScore,
        RiskLevel riskLevel,
        string triggerReason,
        string factorsJson) : base()
    {
        CaseId = caseId;
        RiskScore = Math.Clamp(riskScore, 0, 100);
        RiskLevel = riskLevel;
        EvaluatedAtUtc = DateTime.UtcNow;
        TriggerReason = triggerReason?.Trim() ?? string.Empty;
        FactorsJson = string.IsNullOrWhiteSpace(factorsJson) ? "{}" : factorsJson;
    }
}
