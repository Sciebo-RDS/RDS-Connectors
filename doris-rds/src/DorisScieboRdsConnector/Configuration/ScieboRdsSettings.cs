using System;

namespace DorisScieboRdsConnector.Configuration;

public record ScieboRdsSettings
{
    public required Uri TokenStorageUrl { get; init; }
    public required string ConnectorServiceName { get; init; }
}
