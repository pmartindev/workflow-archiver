using System.Threading.Tasks;

namespace GitHub.App.Workflows.WebApi.Clients;

public interface IStorageClient
{
    public string Endpoint { get; }

    Task UploadFile(string location, string fileName, byte[] buffer);
}
