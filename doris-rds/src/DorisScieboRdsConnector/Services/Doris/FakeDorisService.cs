using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace DorisScieboRdsConnector.Services.Doris;

public class FakeDorisService : IDorisService
{
    public Task PostRoCrate(JsonObject manifest)
    {
        return Task.CompletedTask;
    }
}
