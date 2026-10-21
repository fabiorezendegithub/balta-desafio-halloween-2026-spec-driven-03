using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Encurtador.Api.Models;
using YamlDotNet.Serialization;
using Xunit;

namespace Encurtador.IntegrationTests;

// Documentação OpenAPI e interface Scalar. Interface em /api/docs (302 para /api/docs/, comportamento do Scalar).
public class ApiDocumentationTests
{
    private const string Target = "https://exemplo.com/destino";
    private const string DocumentPath = "/api/openapi/v1.json";
    private const string InterfacePath = "/api/docs/";

    [Fact]
    public async Task Document_is_served_as_json_when_enabled()
    {
        await using var factory = new LinkApiFactory(documentationEnabled: true);
        var client = factory.CreateClient();

        var response = await client.GetAsync(DocumentPath);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Interface_redirects_from_docs_to_trailing_slash_with_location_header()
    {
        await using var factory = new LinkApiFactory(documentationEnabled: true);
        var client = factory.NoRedirectClient();

        var response = await client.GetAsync("/api/docs");

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        var resolved = new Uri(new Uri("http://localhost/api/docs"), response.Headers.Location);
        Assert.Equal("/api/docs/", resolved.AbsolutePath);
    }

    [Fact]
    public async Task Interface_page_returns_html_that_references_document()
    {
        await using var factory = new LinkApiFactory(documentationEnabled: true);
        var client = factory.CreateClient();

        var response = await client.GetAsync(InterfacePath);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        // O Scalar grava a fonte relativa ("api/openapi/v1.json"), resolvida pelo cliente contra a base /api/docs/.
        Assert.Contains("api/openapi/v1.json", html);
    }

    [Fact]
    public async Task Document_contains_the_three_operations_by_operation_id()
    {
        await using var factory = new LinkApiFactory(documentationEnabled: true);
        using var document = await GetDocumentAsync(factory);

        var operations = IndexOperations(document);

        Assert.Equal(new[] { "createLink", "getLinkStats", "redirectLink" }.Order(), operations.Keys.Order());
        Assert.Equal("post", operations["createLink"].Method);
        Assert.Equal("/api/links", operations["createLink"].Path);
        Assert.Equal("get", operations["getLinkStats"].Method);
        Assert.Equal("/api/links/{code}", operations["getLinkStats"].Path);
        Assert.Equal("get", operations["redirectLink"].Method);
        Assert.Equal("/{code}", operations["redirectLink"].Path);
    }

    [Fact]
    public async Task Code_is_a_route_parameter_in_stats_and_redirect()
    {
        await using var factory = new LinkApiFactory(documentationEnabled: true);
        using var document = await GetDocumentAsync(factory);

        var operations = IndexOperations(document);

        Assert.Contains("code", operations["getLinkStats"].PathParameters);
        Assert.Contains("code", operations["redirectLink"].PathParameters);
    }

    [Fact]
    public async Task Document_matches_the_001_contract_operations_parameters_and_responses()
    {
        await using var factory = new LinkApiFactory(documentationEnabled: true);
        using var document = await GetDocumentAsync(factory);
        var generated = IndexOperations(document);
        var contract = ReadContractOperations();

        // Segurança (securitySchemes) fica fora da comparação: o documento não declara esquema (T010).
        Assert.Equal(contract.Keys.Order(), generated.Keys.Order());
        foreach (var (operationId, expected) in contract)
        {
            var actual = generated[operationId];
            Assert.Equal(expected.Method, actual.Method);
            Assert.Equal(expected.Path, actual.Path);
            Assert.Equal(expected.PathParameters.Order(), actual.PathParameters.Order());
            Assert.Equal(expected.Responses.Order(), actual.Responses.Order());
        }
    }

    [Fact]
    public async Task Management_key_is_a_header_parameter_and_document_has_no_security_schemes()
    {
        await using var factory = new LinkApiFactory(documentationEnabled: true);
        using var document = await GetDocumentAsync(factory);

        var stats = IndexOperations(document)["getLinkStats"];
        Assert.Contains("X-Management-Key", stats.HeaderParameters);
        Assert.False(document.RootElement.TryGetProperty("components", out var components)
            && components.TryGetProperty("securitySchemes", out _));
    }

    [Fact]
    public async Task Response_schemas_match_the_record_properties()
    {
        await using var factory = new LinkApiFactory(documentationEnabled: true);
        using var document = await GetDocumentAsync(factory);
        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");

        AssertSchemaMatches<CreatedLinkResponse>(schemas, "CreatedLinkResponse");
        AssertSchemaMatches<LinkStatsResponse>(schemas, "LinkStatsResponse");
    }

    [Fact]
    public async Task Documentation_does_not_expose_the_management_key()
    {
        await using var factory = new LinkApiFactory(documentationEnabled: true);
        var client = factory.CreateClient();
        var created = await TestHttp.CreateLinkAsync(client, new { targetUrl = Target, customCode = "docsemchave" });

        var json = await client.GetStringAsync(DocumentPath);
        var html = await client.GetStringAsync(InterfacePath);

        Assert.DoesNotContain(created.ManagementKey, json);
        Assert.DoesNotContain(created.ManagementKey, html);
    }

    [Fact]
    public async Task Opening_documentation_does_not_change_click_count()
    {
        await using var factory = new LinkApiFactory(documentationEnabled: true);
        var client = factory.NoRedirectClient();
        var created = await TestHttp.CreateLinkAsync(client, new { targetUrl = Target, customCode = "semcliques" });

        await client.GetAsync("/api/docs");
        await client.GetAsync(InterfacePath);
        await client.GetAsync(DocumentPath);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/links/semcliques");
        request.Headers.Add(TestHttp.ManagementKeyHeader, created.ManagementKey);
        var stats = await (await client.SendAsync(request)).Content.ReadFromJsonAsync<LinkStatsResponse>();

        Assert.NotNull(stats);
        Assert.Equal(0, stats.ClickCount);
    }

    [Fact]
    public async Task Documentation_is_on_by_default_in_development()
    {
        await using var factory = new LinkApiFactory();
        var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(DocumentPath)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(InterfacePath)).StatusCode);
    }

    [Fact]
    public async Task Documentation_routes_match_unknown_route_in_production_when_not_enabled()
    {
        await using var factory = new LinkApiFactory(environmentName: "Production");
        var client = factory.NoRedirectClient();

        var docs = await client.GetAsync("/api/docs");
        var document = await client.GetAsync(DocumentPath);
        var unknown = await client.GetAsync("/api/rota-inexistente");

        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(unknown.StatusCode, docs.StatusCode);
        Assert.Equal(unknown.StatusCode, document.StatusCode);
        Assert.Equal(await unknown.Content.ReadAsByteArrayAsync(), await docs.Content.ReadAsByteArrayAsync());
        Assert.Equal(await unknown.Content.ReadAsByteArrayAsync(), await document.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Documentation_is_served_in_production_when_explicitly_enabled()
    {
        await using var factory = new LinkApiFactory(environmentName: "Production", documentationEnabled: true);
        var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(DocumentPath)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(InterfacePath)).StatusCode);
    }

    private static async Task<JsonDocument> GetDocumentAsync(LinkApiFactory factory)
    {
        var client = factory.CreateClient();
        var response = await client.GetAsync(DocumentPath);
        response.EnsureSuccessStatusCode();

        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    private sealed record Operation(string Method, string Path, IReadOnlyList<string> PathParameters, IReadOnlyList<string> HeaderParameters, IReadOnlyList<string> Responses);

    // Indexa as operações do documento gerado pelo operationId.
    private static Dictionary<string, Operation> IndexOperations(JsonDocument document)
    {
        var result = new Dictionary<string, Operation>();
        foreach (var path in document.RootElement.GetProperty("paths").EnumerateObject())
        {
            foreach (var method in path.Value.EnumerateObject())
            {
                var op = method.Value;
                var parameters = op.TryGetProperty("parameters", out var p)
                    ? p.EnumerateArray().ToList()
                    : [];

                result[op.GetProperty("operationId").GetString()!] = new Operation(
                    method.Name,
                    path.Name,
                    parameters.Where(x => x.GetProperty("in").GetString() == "path").Select(x => x.GetProperty("name").GetString()!).ToList(),
                    parameters.Where(x => x.GetProperty("in").GetString() == "header").Select(x => x.GetProperty("name").GetString()!).ToList(),
                    op.GetProperty("responses").EnumerateObject().Select(r => r.Name).ToList());
            }
        }

        return result;
    }

    // Lê o contrato 001 e extrai as mesmas informações, resolvendo $ref de parâmetros.
    private static Dictionary<string, Operation> ReadContractOperations()
    {
        var yaml = File.ReadAllText(FindRepositoryFile("specs/001-url-shortener/contracts/openapi.yaml"));
        var root = new DeserializerBuilder().Build().Deserialize<Dictionary<object, object>>(yaml);
        var paths = (Dictionary<object, object>)root["paths"];
        var result = new Dictionary<string, Operation>();

        foreach (var (pathKey, pathValue) in paths)
        {
            foreach (var (methodKey, methodValue) in (Dictionary<object, object>)pathValue)
            {
                var op = (Dictionary<object, object>)methodValue;
                var pathParams = new List<string>();
                if (op.TryGetValue("parameters", out var ps))
                {
                    foreach (var rawParam in (List<object>)ps)
                    {
                        var param = (Dictionary<object, object>)rawParam;
                        if (param.TryGetValue("$ref", out var reference))
                        {
                            param = ResolveComponentParameter(root, (string)reference);
                        }

                        if ((string)param["in"] == "path")
                        {
                            pathParams.Add((string)param["name"]);
                        }
                    }
                }

                var responses = ((Dictionary<object, object>)op["responses"]).Keys.Select(k => k.ToString()!).ToList();
                result[(string)op["operationId"]] = new Operation(
                    ((string)methodKey).ToLowerInvariant(),
                    (string)pathKey,
                    pathParams,
                    [],
                    responses);
            }
        }

        return result;
    }

    private static Dictionary<object, object> ResolveComponentParameter(Dictionary<object, object> root, string reference)
    {
        var name = reference.Split('/').Last();
        var components = (Dictionary<object, object>)root["components"];
        var parameters = (Dictionary<object, object>)components["parameters"];
        return (Dictionary<object, object>)parameters[name];
    }

    private static string FindRepositoryFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, relativePath)))
        {
            directory = directory.Parent;
        }

        return directory is null
            ? throw new FileNotFoundException($"Arquivo não encontrado a partir de {AppContext.BaseDirectory}.", relativePath)
            : Path.Combine(directory.FullName, relativePath);
    }

    private static void AssertSchemaMatches<T>(JsonElement schemas, string schemaName)
    {
        var properties = schemas.GetProperty(schemaName).GetProperty("properties").EnumerateObject()
            .Select(p => p.Name)
            .Order()
            .ToList();

        var expected = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => JsonNamingPolicy.CamelCase.ConvertName(p.Name))
            .Order()
            .ToList();

        Assert.Equal(expected, properties);
    }
}
