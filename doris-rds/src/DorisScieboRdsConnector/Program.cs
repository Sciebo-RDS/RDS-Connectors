using DorisScieboRdsConnector.Configuration;
using DorisScieboRdsConnector.Services.Doris;
using DorisScieboRdsConnector.Services.ScieboRds;
using DorisScieboRdsConnector.Services.Storage;
using DorisScieboRdsConnector.Services.Storage.NextCloud;
using DorisScieboRdsConnector.Services.Storage.NextCloud.OcsApi;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Net.Http;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddHttpClient<IDorisService, DorisService>();
builder.Services.AddHttpClient<IScieboRdsService, ScieboRdsService>().ConfigureHttpMessageHandlerBuilder((c) =>
     new HttpClientHandler()
     {
         ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
     }
   );

builder.Services.AddHttpClient<IStorageService, NextCloudStorageService>().ConfigureHttpMessageHandlerBuilder((c) =>
     new HttpClientHandler()
     {
         ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
     }
   );

builder.Services.AddHttpClient<OcsApiClient>();

builder.Services.AddOptions<DorisConfiguration>()
    .Bind(builder.Configuration.GetSection(DorisConfiguration.ConfigurationSection))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<NextCloudConfiguration>()
    .Bind(builder.Configuration.GetSection(NextCloudConfiguration.ConfigurationSection))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<ScieboRdsConfiguration>()
    .Bind(builder.Configuration.GetSection(ScieboRdsConfiguration.ConfigurationSection))
    .ValidateDataAnnotations()
    .ValidateOnStart();

var app = builder.Build();
app.MapControllers();

// Register connector with Sciebo RDS
var scieboRdsConfiguration = app.Services.GetRequiredService<IOptions<ScieboRdsConfiguration>>();
if (scieboRdsConfiguration.Value.RegisterConnectorOnStartup)
{
    var tokenService = app.Services.GetRequiredService<IScieboRdsService>();
    await tokenService.RegisterConnector();
}

app.Run();
