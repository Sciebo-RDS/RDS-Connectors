using System.Text.Json;

namespace DorisScieboRdsConnector.Models;

public record PortUserNameWithMetadata(string UserId, JsonElement Metadata) : PortUserName(UserId);
