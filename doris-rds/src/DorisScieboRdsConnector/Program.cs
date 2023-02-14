using DorisScieboRdsConnector.Services.ScieboRds;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

builder.Services.AddHttpClient<IScieboRdsService, ScieboRdsService>();

builder.Services.AddHttpLogging(logging =>
{
    logging.LoggingFields = HttpLoggingFields.All;
});

var app = builder.Build();
app.UseAuthorization();
app.MapControllers();

app.UseHttpLogging();

// Register connector with Sciebo RDS
var tokenService = app.Services.GetRequiredService<IScieboRdsService>();
await tokenService.RegisterConnector();

app.Run();
