using System.Threading.Tasks;
using GitHub.App.Workflows.WebApi.Models;

namespace GitHub.App.Workflows.WebApi.Clients;

public interface ISplunkClient
{
    Task LogEvent<T>(SplunkEventModel<T> splunkEventModel);
}
