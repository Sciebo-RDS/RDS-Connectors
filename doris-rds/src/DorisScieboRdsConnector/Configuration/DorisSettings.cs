using System;

namespace DorisScieboRdsConnector.Configuration;

public record DorisSettings
{
    public required string PrincipalDomain { get; init; }
    public required Uri ApiUrl { get; init; }
    public required string ApiKey { get; init; }
 }
