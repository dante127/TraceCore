using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using TraceCore.Application.Cases;
using TraceCore.Application.Cases.Commands;
using TraceCore.Domain.Enums;
using Xunit;

namespace TraceCore.IntegrationTests;

public class ApiAuthorizationTests : IClassFixture<TestWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly TestWebApplicationFactory _factory;

    public ApiAuthorizationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Investigator_WithoutAuditPermission_ShouldBeForbidden()
    {
        var client = _factory.CreateInvestigatorClient();

        var response = await client.GetAsync("/api/v1/audit?PageNumber=1&PageSize=5");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Investigator_WithoutCaseGrant_ShouldBeForbidden()
    {
        var admin = _factory.CreateAdminClient();
        var investigator = _factory.CreateInvestigatorClient();

        var createCmd = new CreateCaseCommand(
            "Restricted Authorization Probe",
            "Confidential case without investigator grant.",
            CaseType.FinancialCrime,
            CasePriority.High,
            ConfidentialityLevel.Confidential,
            72,
            DateTime.UtcNow.AddDays(7),
            true);
        var createRes = await admin.PostAsJsonAsync("/api/v1/cases", createCmd, JsonOptions);
        createRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createRes.Content.ReadFromJsonAsync<CreateCaseResult>(JsonOptions);

        var response = await investigator.GetAsync($"/api/v1/cases/{created!.CaseId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Investigator_CaseList_ShouldExcludeUngrantedConfidentialCase()
    {
        var admin = _factory.CreateAdminClient();
        var investigator = _factory.CreateInvestigatorClient();

        var createCmd = new CreateCaseCommand(
            "List Filter Probe",
            "Confidential case invisible to ungranted investigator.",
            CaseType.FinancialCrime,
            CasePriority.High,
            ConfidentialityLevel.Confidential,
            72,
            DateTime.UtcNow.AddDays(7),
            true);
        var createRes = await admin.PostAsJsonAsync("/api/v1/cases", createCmd, JsonOptions);
        var created = await createRes.Content.ReadFromJsonAsync<CreateCaseResult>(JsonOptions);

        var invList = await investigator.GetFromJsonAsync<TraceCore.Application.Common.Models.PagedList<TraceCore.Application.Cases.CaseDto>>(
            "/api/v1/cases?PageNumber=1&PageSize=100", JsonOptions);
        invList!.Items.Should().NotContain(c => c.Id == created!.CaseId);

        var adminList = await admin.GetFromJsonAsync<TraceCore.Application.Common.Models.PagedList<TraceCore.Application.Cases.CaseDto>>(
            "/api/v1/cases?PageNumber=1&PageSize=100", JsonOptions);
        adminList!.Items.Should().Contain(c => c.Id == created!.CaseId);
    }

    [Fact]
    public async Task Auditor_WithoutTaskWrite_ShouldBeForbidden()
    {
        var auditor = _factory.CreateAuditorClient();

        var response = await auditor.GetAsync("/api/v1/tasks/overdue");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task MarkNotificationAsRead_ForUnknownId_ShouldBeNotFound()
    {
        var investigator = _factory.CreateInvestigatorClient();

        var response = await investigator.PostAsync(
            $"/api/v1/notifications/{Guid.NewGuid()}/read", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateCase_WithMalformedRowVersion_ShouldBeBadRequest()
    {
        var admin = _factory.CreateAdminClient();

        var createCmd = new CreateCaseCommand(
            "RowVersion Validation Probe",
            "Case for malformed token test.",
            CaseType.Fraud,
            CasePriority.Medium,
            ConfidentialityLevel.Internal,
            168,
            null,
            true);
        var createRes = await admin.PostAsJsonAsync("/api/v1/cases", createCmd, JsonOptions);
        var created = await createRes.Content.ReadFromJsonAsync<CreateCaseResult>(JsonOptions);

        var updateCmd = new UpdateCaseCommand(
            created!.CaseId,
            "New title",
            "New description",
            CaseType.Fraud,
            ConfidentialityLevel.Internal,
            "!!!not-base64!!!");

        var response = await admin.PutAsJsonAsync($"/api/v1/cases/{created.CaseId}", updateCmd, JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
