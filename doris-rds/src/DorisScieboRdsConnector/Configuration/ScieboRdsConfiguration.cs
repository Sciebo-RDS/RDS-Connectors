using System;
using System.ComponentModel.DataAnnotations;

namespace DorisScieboRdsConnector.Configuration;

public record ScieboRdsConfiguration
{
    public const string ConfigurationSection = "ScieboRds";

    public bool RegisterConnectorOnStartup { get; init; } = true;
    [Required]
    public required Uri TokenStorageUrl { get; init; }
    [Required]
    public required string ConnectorServiceName { get; init; }
}
