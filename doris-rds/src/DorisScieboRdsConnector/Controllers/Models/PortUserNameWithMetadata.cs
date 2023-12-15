namespace DorisScieboRdsConnector.Controllers.Models;

using System.Text.Json;

public record PortUserNameWithMetadata(string UserId, JsonDocument Metadata) : PortUserName(UserId);
