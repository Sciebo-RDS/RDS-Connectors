namespace DorisScieboRdsConnector.Services.Doris;

using Microsoft.Extensions.Configuration;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Text.Json.Nodes;

public class DorisService : IDorisService
{
    private readonly HttpClient httpClient;
    private readonly IConfiguration configuration;

    public DorisService(HttpClient httpClient, IConfiguration configuration)
    {
        this.httpClient = httpClient;
        this.configuration = configuration;
    }

    public async Task PostManifest(JsonObject manifest)
    {
        string? url = configuration["ManifestIndex:Url"];
        string? apiKey = configuration["ManifestIndex:ApiKey"];

        if (string.IsNullOrEmpty(url) ||
            string.IsNullOrEmpty(apiKey))
        {
            return;
        }
        httpClient.DefaultRequestHeaders.Add("X-API-Key", apiKey);
        var response = await httpClient.PostAsJsonAsync(url, manifest);

        response.EnsureSuccessStatusCode();
    }
}
