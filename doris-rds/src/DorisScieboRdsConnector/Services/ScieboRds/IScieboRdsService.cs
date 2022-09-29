using System.Threading.Tasks;

namespace DorisScieboRdsConnector.Services.ScieboRdsTokenStorage;

public interface IScieboRdsService
{
    Task RegisterConnector();
}
