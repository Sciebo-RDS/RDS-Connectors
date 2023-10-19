namespace DorisScieboRdsConnector.Controllers.Models;

public record PortUserName(string UserId)
{
    public string GetUserName() => UserId.Split(":")[1][2..];
}
