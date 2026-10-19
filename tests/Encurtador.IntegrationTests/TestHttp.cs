using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Encurtador.Api.Models;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Encurtador.IntegrationTests;

public static class TestHttp
{
    public const string ManagementKeyHeader = "X-Management-Key";

    public static HttpClient NoRedirectClient(this WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    public static StringContent Json(object body) =>
        new(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

    public static async Task<CreatedLinkResponse> CreateLinkAsync(HttpClient client, object body)
    {
        var response = await client.PostAsync("/api/links", Json(body));
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<CreatedLinkResponse>())!;
    }

    public static async Task<JsonElement> AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }
}
