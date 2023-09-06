using System.Threading.Tasks;
using System.Text.Json.Nodes;

namespace DorisScieboRdsConnector.Services.Doris;

public interface IDorisService
{
    Task PostManifest(JsonObject manifest);
}
