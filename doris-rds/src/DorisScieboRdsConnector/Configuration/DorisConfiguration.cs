using System;
using System.ComponentModel.DataAnnotations;

namespace DorisScieboRdsConnector.Configuration;

public record DorisConfiguration
{
    public const string ConfigurationSection = "Doris";

    public bool DorisApiEnabled { get; init; } = true;
    [Required]
    public required string PrincipalDomain { get; init; }
    [Required]
    public required Uri ApiUrl { get; init; }
    [Required]
    public required string ApiKey { get; init; }
 }
