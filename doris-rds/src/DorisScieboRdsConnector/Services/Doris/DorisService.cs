namespace DorisScieboRdsConnector.Services.Doris;

using DorisScieboRdsConnector.Configuration;
using Microsoft.Extensions.Options;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

public class DorisService : IDorisService
{
    private readonly HttpClient httpClient;
    private readonly DorisConfiguration configuration;

    public DorisService(HttpClient httpClient, IOptions<DorisConfiguration> configuration)
    {
        this.httpClient = httpClient;
        this.configuration = configuration.Value;

        this.httpClient.DefaultRequestHeaders.Add("X-API-Key", this.configuration.ApiKey);
    }

    public async Task PostRoCrate(JsonObject roCrate)
    {
        var response = await httpClient.PostAsJsonAsync(configuration.ApiUrl, roCrate);

        response.EnsureSuccessStatusCode();
    }
}
