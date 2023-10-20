using DorisScieboRdsConnector.Configuration;
using DorisScieboRdsConnector.Services.ScieboRds;
using DorisScieboRdsConnector.Services.Storage;
using DorisScieboRdsConnector.Services.Storage.OcsApi;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

builder.Services.AddHttpClient<IScieboRdsService, ScieboRdsService>();
builder.Services.AddHttpClient<IStorageService, NextCloudStorageService>();
builder.Services.AddHttpClient<OcsApiClient>();

builder.Services.AddOptions<NextCloudSettings>()
    .Bind(builder.Configuration.GetSection(NextCloudSettings.ConfigurationSection))
    .ValidateDataAnnotations()
    .ValidateOnStart();


var app = builder.Build();
app.MapControllers();

// Register connector with Sciebo RDS
var tokenService = app.Services.GetRequiredService<IScieboRdsService>();
await tokenService.RegisterConnector();

app.Run();
