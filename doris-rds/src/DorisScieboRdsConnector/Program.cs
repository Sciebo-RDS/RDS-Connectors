using System.Net.Http;
using System.Net.Http.Headers;
using DorisScieboRdsConnector.Services.ScieboRds;
using DorisScieboRdsConnector.Services.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WebDav;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

builder.Services.AddHttpClient<IScieboRdsService, ScieboRdsService>();

 /*var webDav = new WebDavClient(httpClient);
        this.storageService = new NextCloudStorageService(webDav, httpClient, logger, configuration);*/


builder.Services.AddTransient<IWebDavClient>(sp => 
{
    var configuration = sp.GetRequiredService<IConfiguration>();

    string authString = configuration.GetValue<string>("NextCloud:User") + ":" + configuration.GetValue<string>("NextCloud:Password");
    string basicAuth = System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(authString));

    var httpClient = new HttpClient();
    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", basicAuth);
    httpClient.DefaultRequestHeaders.Add("Host", "localhost");

    return new WebDavClient(httpClient);
});

builder.Services.AddHttpClient<IStorageService, NextCloudStorageService>((sp, httpClient) =>
{
     var configuration = sp.GetRequiredService<IConfiguration>();

    string authString = configuration.GetValue<string>("NextCloud:User") + ":" + configuration.GetValue<string>("NextCloud:Password");
    string basicAuth = System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(authString));

    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", basicAuth);
    httpClient.DefaultRequestHeaders.Add("Host", "localhost");
});

var app = builder.Build();
app.UseAuthorization();
app.MapControllers();

app.UseHttpLogging();

// Register connector with Sciebo RDS
var tokenService = app.Services.GetRequiredService<IScieboRdsService>();
await tokenService.RegisterConnector();

app.Run();
