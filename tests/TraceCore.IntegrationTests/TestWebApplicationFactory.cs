using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using TraceCore.Infrastructure.Security;

namespace TraceCore.IntegrationTests;

public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // NOTE: ConfigureAppConfiguration values do not reach WebApplicationBuilder.Configuration
        // in this setup, so test settings are applied via UseSetting (host settings flow into config).
        builder.UseSetting("UseInMemoryDatabase", "true");
        builder.UseSetting("ConnectionStrings:Redis", "");
        builder.UseSetting("Jwt:SecretKey", "IntegrationTestSecretKey-Min32Bytes-0123456789!");
        builder.UseSetting("Jwt:Issuer", "TraceCore.Api");
        builder.UseSetting("Jwt:Audience", "TraceCore.Client");
        builder.UseSetting("Jwt:ExpiryMinutes", "60");

        builder.UseEnvironment("Development");
    }

    public HttpClient CreateAdminClient()
    {
        var client = CreateClient();
        var jwtService = Services.GetRequiredService<IJwtTokenService>();
        var token = jwtService.GenerateToken(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "admin@tracecore.gov",
            "Chief Admin",
            ["Administrator"],
            TraceCore.Api.Common.Permissions.All);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public HttpClient CreateInvestigatorClient()
    {
        var client = CreateClient();
        var jwtService = Services.GetRequiredService<IJwtTokenService>();
        var token = jwtService.GenerateToken(
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            "investigator@tracecore.gov",
            "Senior Investigator",
            ["Investigator"],
            TraceCore.Api.Common.Permissions.InvestigatorPermissions);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
