namespace DorisScieboRdsConnector.Services.Storage.OcsApi;

using DorisScieboRdsConnector.Services.Storage.OcsApi.Requests;
using DorisScieboRdsConnector.Services.Storage.OcsApi.Responses;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

public class OcsApiClient
{
    private readonly HttpClient httpClient;

    private const string sharesUri = "/ocs/v2.php/apps/files_sharing/api/v1/shares";

    public OcsApiClient(HttpClient httpClient, IConfiguration configuration)
    {
        this.httpClient = httpClient;

        string authString = configuration.GetValue<string>("NextCloud:User") + ":" + configuration.GetValue<string>("NextCloud:Password");
        string basicAuth = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(authString));

        this.httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", basicAuth);
        this.httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        this.httpClient.DefaultRequestHeaders.Add("OCS-APIRequest", "true");
        this.httpClient.DefaultRequestHeaders.Add("Host", "localhost");
        this.httpClient.BaseAddress = new Uri(configuration.GetValue<string>("NextCloud:BaseUrl")!);
    }

    public async Task<OcsResponse<IEnumerable<OcsShare>>> GetShares(OcsGetSharesRequest request)
    {
        var uri = AddQueryParameters(sharesUri, request);
        var result = await httpClient.GetFromJsonAsync<OcsResponse<IEnumerable<OcsShare>>>(uri).ConfigureAwait(false);

        return result!;
    }

    public async Task<OcsResponse<OcsShare>> CreateShare(OcsCreateShareRequest request)
    {
        var uri = AddQueryParameters(sharesUri, request);
        using var result = await httpClient.PostAsync(uri, null).ConfigureAwait(false);

        return (await result.Content.ReadFromJsonAsync<OcsResponse<OcsShare>>())!;
    }

    private static string AddQueryParameters(string uri, object parameters)
    {
        var jsonDocument = JsonSerializer.SerializeToDocument(parameters);
        var queryValues = jsonDocument.RootElement.EnumerateObject()
            .Where(v =>
                v.Value.ValueKind != JsonValueKind.Null &&
                v.Value.ValueKind != JsonValueKind.Undefined)
            .Select(v =>
                Uri.EscapeDataString(v.Name) + "=" +
                Uri.EscapeDataString(v.Value.ToString()));

        return uri + "?" + string.Join("&", queryValues);
    }
}