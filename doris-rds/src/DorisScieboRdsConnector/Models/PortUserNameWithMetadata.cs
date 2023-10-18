namespace DorisScieboRdsConnector.Models;

using System.Text.Json;

public record PortUserNameWithMetadata(string UserId, JsonElement Metadata) : PortUserName(UserId);
