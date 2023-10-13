using DorisScieboRdsConnector.Models;
using Microsoft.Extensions.Configuration;
using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;

namespace DorisScieboRdsConnector.Services.Storage;

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
        this.httpClient.BaseAddress = new Uri(configuration.GetValue<string>("NextCloud:BaseUrl")!);
    }

    public async Task<OcsGetResponse> GetShares(OcsGetSharesRequest request)
    {
        var uri = AddGetParams(sharesUri, request);
        var result = await httpClient.GetFromJsonAsync<OcsGetResponse>(uri).ConfigureAwait(false);

        return result!;
    }

    public async Task<OcsPostResponse> CreateShare(OcsCreateShareRequest request)
    {
        var uri = AddGetParams(sharesUri, request);
        using var result = await httpClient.PostAsync(uri, null).ConfigureAwait(false);

        return (await result.Content.ReadFromJsonAsync<OcsPostResponse>())!;
    }

    private static string AddGetParams(string uri, object? parameters = null)
    {
        if (parameters != null)
        {
            var jsonDocument = JsonSerializer.SerializeToDocument(parameters);
            var propertyValues = jsonDocument.RootElement.EnumerateObject()
                .Where(v => 
                    v.Value.ValueKind != JsonValueKind.Null && 
                    v.Value.ValueKind != JsonValueKind.Undefined)
                .Select(v => 
                    UrlEncoder.Default.Encode(v.Name) + "=" + 
                    UrlEncoder.Default.Encode(v.Value.ToString()));

            uri = uri + "?" + string.Join("&", propertyValues);
        }

        return uri;
    }
}

public class OcsGetSharesRequest
{
    public bool? include_tags { get; set; }
    public string? path { get; set; }
    public bool? reshares { get; set; }
    public bool? shared_with_me { get; set; }
    public bool? subfiles { get; set; }
}

public class OcsCreateShareRequest
{
    public string? attributes { get; set; }
    public string? expireDate { get; set; }
    public string? label { get; set; }
    public string? note { get; set; }
    public string? password { get; set; }
    public string? path { get; set; }
    public int? permissions { get; set; }
    public bool? publicUpload { get; set; }
    public bool? sendPasswordByTalk { get; set; }
    public int? shareType { get; set; }
    public string? shareWith { get; set; }
};

