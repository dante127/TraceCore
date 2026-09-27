using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using TraceCore.Api.Controllers;
using TraceCore.Application.Cases;
using TraceCore.Application.Cases.Commands;
using TraceCore.Application.Common.Interfaces;
using TraceCore.Application.Common.Models;
using TraceCore.Application.Evidence;
using TraceCore.Application.Risk;
using TraceCore.Application.Search;
using TraceCore.Application.Tasks;
using TraceCore.Domain.Enums;
using Xunit;
using TaskStatus = TraceCore.Domain.Enums.TaskStatus;

namespace TraceCore.IntegrationTests;

public class ApiWorkflowTests : IClassFixture<TestWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly HttpClient _adminClient;

    public ApiWorkflowTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _adminClient = factory.CreateAdminClient();
    }

    [Fact]
    public async Task HealthCheck_ShouldReturnOk()
    {
        // Act
        var response = await _client.GetAsync("/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Auth_Login_WithValidCredentials_ShouldReturnJwtToken()
    {
        // Arrange
        var request = new LoginRequest("admin@tracecore.gov", "Password123!");

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions);
        content.Should().NotBeNull();
        content!.AccessToken.Should().NotBeNullOrWhiteSpace();
        content.Roles.Should().Contain("Administrator");
        content.Permissions.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Auth_Login_WithInvalidCredentials_ShouldReturnUnauthorized()
    {
        // Arrange
        var request = new LoginRequest("admin@tracecore.gov", "WrongPassword!");

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task FullInvestigativeWorkflow_ShouldExecuteEndToEnd()
    {
        // 1. Create a new Case
        var createCaseCmd = new CreateCaseCommand(
            "Project Cerberus: Insider Trading Probe",
            "Detailed inquiry into anomalous equity purchases prior to merger announcement.",
            CaseType.FinancialCrime,
            CasePriority.High,
            ConfidentialityLevel.Confidential,
            72,
            DateTime.UtcNow.AddDays(14),
            true);

        var createCaseRes = await _adminClient.PostAsJsonAsync("/api/v1/cases", createCaseCmd);
        createCaseRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdCase = await createCaseRes.Content.ReadFromJsonAsync<CreateCaseResult>(JsonOptions);
        createdCase.Should().NotBeNull();
        var caseId = createdCase!.CaseId;
        createdCase.CaseNumber.Should().StartWith("CAS-");

        // 2. Fetch Case by ID
        var getCaseRes = await _adminClient.GetAsync($"/api/v1/cases/{caseId}");
        getCaseRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var caseDetail = await getCaseRes.Content.ReadFromJsonAsync<CaseDetailDto>(JsonOptions);
        caseDetail.Should().NotBeNull();
        caseDetail!.Status.Should().Be(CaseStatus.Open);
        caseDetail.Priority.Should().Be(CasePriority.High);

        // 3. Assign Investigator
        var investigatorId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var assignCmd = new AssignCaseCommand(caseId, investigatorId);
        var assignRes = await _adminClient.PostAsJsonAsync($"/api/v1/cases/{caseId}/assign", assignCmd);
        assignRes.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // 4. Transition Status to UnderInvestigation
        var statusCmd = new ChangeCaseStatusCommand(caseId, CaseStatus.UnderInvestigation, "Investigator assigned and warrant requested.");
        var statusRes = await _adminClient.PostAsJsonAsync($"/api/v1/cases/{caseId}/status", statusCmd);
        statusRes.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // 5. Register Evidence with SHA-256 hash
        string evidenceHash = "7F83B1657FF1FC53B92DC18148A1D65DFC2D4B1FA3D677284ADDD200126D9069";
        var createEvidenceCmd = new CreateEvidenceCommand(
            caseId,
            EvidenceType.Digital,
            "Encrypted Signal Chat Export",
            "Confiscated iPhone 15",
            ConfidentialityLevel.Confidential,
            "Evidence Vault Alpha",
            evidenceHash,
            true,
            "Initial intake from evidence locker");

        var createEvidenceRes = await _adminClient.PostAsJsonAsync("/api/v1/evidence", createEvidenceCmd);
        createEvidenceRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var evidenceId = await createEvidenceRes.Content.ReadFromJsonAsync<Guid>(JsonOptions);
        evidenceId.Should().NotBeEmpty();

        // 6. Transfer Evidence Custody (with hash chaining)
        var transferCmd = new TransferEvidenceCustodyCommand(
            evidenceId,
            investigatorId,
            CustodyAction.CheckedOutForAnalysis,
            "Forensic Lab Station #2",
            "Volatile memory acquisition.",
            evidenceHash);

        var transferRes = await _adminClient.PostAsJsonAsync($"/api/v1/evidence/{evidenceId}/transfer", transferCmd);
        transferRes.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // 7. Verify Evidence Chain of Custody
        var verifyRes = await _adminClient.GetAsync($"/api/v1/evidence/{evidenceId}/verify");
        verifyRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var verification = await verifyRes.Content.ReadFromJsonAsync<EvidenceVerificationDto>(JsonOptions);
        verification.Should().NotBeNull();
        verification!.IsChainValid.Should().BeTrue(verification.Message);
        verification.TotalEventsVerified.Should().Be(2); // Genesis + Transfer

        // 8. Create Task under Case
        var taskCmd = new CreateTaskCommand(
            caseId,
            "Subpoena Trade Confirmation Records",
            "Request broker execution logs",
            TaskPriority.High,
            investigatorId,
            DateTime.UtcNow.AddDays(5));

        var taskRes = await _adminClient.PostAsJsonAsync("/api/v1/tasks", taskCmd);
        taskRes.StatusCode.Should().Be(HttpStatusCode.Created);

        // 9. Assess Case Risk (Deterministic Rule Engine)
        var assessRes = await _adminClient.PostAsync($"/api/v1/risk/cases/{caseId}/assess?triggerReason=AutomatedWorkflow", null);
        assessRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var riskResult = await assessRes.Content.ReadFromJsonAsync<RiskAssessmentResult>(JsonOptions);
        riskResult.Should().NotBeNull();
        riskResult!.TotalScore.Should().BeGreaterThan(0);
        riskResult.FactorScores.Should().ContainKey("Priority");
        riskResult.FactorScores.Should().ContainKey("CriticalEvidence");

        // 10. Multi-Entity Search
        var searchRes = await _adminClient.GetAsync("/api/v1/search?query=Cerberus");
        searchRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var searchResult = await searchRes.Content.ReadFromJsonAsync<SearchSummaryDto>(JsonOptions);
        searchResult.Should().NotBeNull();
        searchResult!.Items.Should().Contain(i => i.Title.Contains("Cerberus"));

        // 11. Concurrency Conflict Test (Stale RowVersion triggers 409 Conflict ProblemDetails)
        string staleRowVersion = Convert.ToBase64String(new byte[] { 0, 0, 0, 0, 0, 0, 0, 99 });
        var staleUpdateCmd = new UpdateCaseCommand(
            caseId,
            "Conflicting Title Update",
            "New description",
            CaseType.FinancialCrime,
            ConfidentialityLevel.Confidential,
            staleRowVersion);

        var conflictRes = await _adminClient.PutAsJsonAsync($"/api/v1/cases/{caseId}", staleUpdateCmd);
        conflictRes.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
