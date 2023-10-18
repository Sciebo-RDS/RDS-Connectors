namespace DorisScieboRdsConnector.Services.Doris;

using System.Threading.Tasks;
using System.Text.Json.Nodes;

public interface IDorisService
{
    Task PostManifest(JsonObject manifest);
}
