using System;
using System.ComponentModel.DataAnnotations;

namespace DorisScieboRdsConnector.Configuration;

public record NextCloudConfiguration
{
    public const string ConfigurationSection = "NextCloud";

    [Required]
    public required Uri BaseUrl { get; init; }
    [Required]
    public required string User { get; init; }
    [Required]
    public required string Password { get; init; }
}
