using Microsoft.EntityFrameworkCore;
using TraceCore.Application.Common.Interfaces;
using TraceCore.Domain.Entities.Cases;
using TraceCore.Domain.Entities.Evidence;
using TraceCore.Domain.Entities.Investigations;
using TraceCore.Domain.Entities.Organizations;
using TraceCore.Domain.Entities.People;
using TraceCore.Domain.Entities.Relationships;
using TraceCore.Domain.Entities.Tasks;
using TraceCore.Domain.Enums;
using TraceCore.Infrastructure.Persistence;

namespace TraceCore.Api.Data;

public static class DataSeeder
{
    public static async Task SeedAsync(TraceCoreDbContext context)
    {
        if (await context.Cases.AnyAsync())
            return; // Already seeded

        var adminId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var investigatorId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var managerId = Guid.Parse("33333333-3333-3333-3333-333333333333");

        // 1. People
        var suspect = new Person("Victor", "Kovac", "v.kovac@apexholdings.com", "+1-555-0199", "EXT-10492", new DateTime(1982, 5, 14), "Former VP of Treasury at Apex Holdings.");
        var witness = new Person("Elena", "Rostova", "e.rostova@fintrust.com", "+1-555-0248", "EXT-20511", new DateTime(1991, 11, 3), "Compliance Auditor at FinTrust Capital.");
        var victim = new Person("Arthur", "Pendleton", "arthur@cybsafe.io", "+1-555-0812", "EXT-99120", new DateTime(1975, 2, 28), "CEO of CybSafe Global.");

        context.People.AddRange(suspect, witness, victim);

        // 2. Organizations
        var orgApex = new Organization("Apex Holdings LLC", "REG-US-892110", "Financial Services", "contact@apexholdings.com", "+1-800-555-0100", "100 Wall Street, New York, NY", "Subject of offshore wire fraud inquiry.");
        var orgFinTrust = new Organization("FinTrust Capital", "REG-UK-392019", "Asset Management", "info@fintrust.com", "+44-20-7946-0912", "30 St Mary Axe, London, UK", "Financial intermediary.");
        var orgCybSafe = new Organization("CybSafe Global", "REG-CA-449102", "Cybersecurity", "support@cybsafe.io", "+1-416-555-0199", "250 Yonge St, Toronto, ON", "Victim of credential compromise.");

        context.Organizations.AddRange(orgApex, orgFinTrust, orgCybSafe);

        // 3. Entity Relationships
        var rel1 = new EntityRelationship(suspect.Id, EntityType.Person, orgApex.Id, EntityType.Organization, RelationshipType.EmployeeOf, "Vice President of Treasury (2018-2025)", 1.0f);
        var rel2 = new EntityRelationship(suspect.Id, EntityType.Person, witness.Id, EntityType.Person, RelationshipType.AssociateOf, "Frequent financial communication via encrypted messaging", 0.85f);
        var rel3 = new EntityRelationship(orgApex.Id, EntityType.Organization, orgFinTrust.Id, EntityType.Organization, RelationshipType.FinancialTransactionWith, "Suspicious $4.2M cross-border transfer", 0.95f);

        context.EntityRelationships.AddRange(rel1, rel2, rel3);

        // 4. Cases
        var case1 = Case.CreateOpen(
            "CAS-2026-0001",
            "Project Midas: Apex Cross-Border Wire Fraud",
            "Investigation into unauthorized $4.2M treasury wire transfers routed through offshore accounts without secondary signature authorization.",
            CaseType.FinancialCrime,
            CasePriority.Critical,
            ConfidentialityLevel.Restricted,
            adminId,
            24, // 24h SLA
            DateTime.UtcNow.AddDays(7));

        case1.AssignInvestigator(investigatorId, adminId);
        case1.AddPerson(suspect.Id, ParticipantRole.Subject, "Primary suspect behind unauthorized wire instructions.");
        case1.AddPerson(witness.Id, ParticipantRole.Witness, "Reported anomalous ledger reconciliations.");
        case1.AddOrganization(orgApex.Id, "Target", "Entity where funds were diverted from.");
        case1.AddOrganization(orgFinTrust.Id, "Intermediary", "Receiving correspondent bank.");
        case1.TransitionStatus(CaseStatus.UnderInvestigation, investigatorId, "Investigation authorized and assigned.");

        var case2 = Case.CreateOpen(
            "CAS-2026-0002",
            "Operation DarkShadow: CybSafe Infrastructure Intrusion",
            "Ransomware attempt and data exfiltration targeting customer credential datastore.",
            CaseType.CyberCrime,
            CasePriority.High,
            ConfidentialityLevel.Confidential,
            managerId,
            48,
            DateTime.UtcNow.AddDays(14));

        case2.AssignInvestigator(investigatorId, managerId);
        case2.AddPerson(victim.Id, ParticipantRole.Victim, "Executive reporting breach.");
        case2.AddOrganization(orgCybSafe.Id, "Victim", "Compromised infrastructure.");

        context.Cases.AddRange(case1, case2);

        // 5. Evidence
        var evidence1 = Evidence.Collect(
            case1.Id,
            "EVD-2026-0001",
            EvidenceType.Digital,
            "SWIFT MT103 Wire Confirmation Message Log",
            "FinTrust Gateway Router Audit Dump",
            investigatorId,
            ConfidentialityLevel.Restricted,
            "Digital Evidence Vault - Server Alpha",
            "A89F31C48D6C90833F0F4DE28D98C8229F81812E6B480CD9E57A8B78129031F2",
            true,
            "Acquired directly from correspondent gateway with forensic bit-stream clone.");

        evidence1.RecordTransfer(
            investigatorId,
            adminId,
            CustodyAction.CheckedOutForAnalysis,
            "Forensics Analysis Lab B",
            "Forensic analysis of packet headers.",
            "A89F31C48D6C90833F0F4DE28D98C8229F81812E6B480CD9E57A8B78129031F2");

        var evidence2 = Evidence.Collect(
            case1.Id,
            "EVD-2026-0002",
            EvidenceType.Physical,
            "Corporate Encrypted Laptop (ThinkPad X1)",
            "Victor Kovac Desk - Apex Headquarters",
            investigatorId,
            ConfidentialityLevel.Restricted,
            "Evidence Safe Room #4",
            "98B2C4E8A019D20F88319F4AE29D88190F8812E6B480CD9E57A8B78129034E5A",
            true,
            "Seized under search warrant #2026-W-891.");

        context.Evidence.AddRange(evidence1, evidence2);

        // 6. Investigation & Activities
        var investigation = Investigation.Create(
            case1.Id,
            "Forensic Accounting & IP Tracing",
            investigatorId,
            "1. Trace beneficial ownership of recipient accounts. 2. Correlate IP logs with suspect work schedule.",
            true);

        investigation.AddActivity(
            ActivityType.Interview,
            investigatorId,
            DateTime.UtcNow.AddDays(-1),
            "Formal interview of Elena Rostova",
            "Witness confirmed Kovac requested ledger overrides without executive countersignature.",
            "Field Office Room 302",
            "Audio recording archived under evidence registry.");

        investigation.AddActivity(
            ActivityType.EvidenceReview,
            investigatorId,
            DateTime.UtcNow.AddHours(-12),
            "Forensic review of SWIFT MT103 transaction logs",
            "Identified terminal session originating from suspect VPN IP address.",
            "Forensics Lab",
            "Correlates with suspect badge access records.");

        context.Investigations.Add(investigation);

        // 7. Tasks
        var task1 = CaseTask.Create(
            case1.Id,
            "Subpoena Offshore Correspondent Records",
            "Issue formal judicial subpoena to recipient bank for beneficiary account statements.",
            TaskPriority.Urgent,
            investigatorId,
            DateTime.UtcNow.AddDays(2),
            investigation.Id);

        var task2 = CaseTask.Create(
            case1.Id,
            "Analyze Laptop Memory Dump",
            "Perform volatile RAM analysis on seized ThinkPad to recover decrypted session tokens.",
            TaskPriority.High,
            investigatorId,
            DateTime.UtcNow.AddDays(1),
            investigation.Id);

        context.Tasks.AddRange(task1, task2);

        await context.SaveChangesAsync();
    }
}
