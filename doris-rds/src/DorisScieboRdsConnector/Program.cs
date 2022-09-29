using DorisScieboRdsConnector.Services.ScieboRdsTokenStorage;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

builder.Services.AddHttpClient<IScieboRdsService, ScieboRdsService>();


var app = builder.Build();
app.UseAuthorization();
app.MapControllers();

// Register connector with Sciebo RDS
var tokenService = app.Services.GetRequiredService<IScieboRdsService>();
await tokenService.RegisterConnector();

app.Run();
