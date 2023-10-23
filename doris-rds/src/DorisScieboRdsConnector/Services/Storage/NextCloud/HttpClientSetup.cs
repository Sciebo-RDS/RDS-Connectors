using DorisScieboRdsConnector.Configuration;
using System;
using System.Net.Http;
using System.Net.Http.Headers;

namespace DorisScieboRdsConnector.Services.Storage.NextCloud;

internal static class HttpClientSetup
{
    public static void SetupForNextCloud(this HttpClient httpClient, NextCloudConfiguration configuration)
    {
        string authString = configuration.User + ":" + configuration.Password;
        string basicAuth = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(authString));
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", basicAuth);
        httpClient.DefaultRequestHeaders.Add("Host", "localhost");
    }
}
