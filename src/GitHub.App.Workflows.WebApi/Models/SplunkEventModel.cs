using Microsoft.Extensions.Primitives;

namespace GitHub.App.Workflows.WebApi.Models;

public class SplunkEventModel<T>
{
    public T Event { get; set; }

    public string? Host { get; set; }

    public string? Index { get; set; }

    public string? Source { get; set; }

    public string? SourceType { get; set; }

    public long? Time { get; set; }

    public SplunkEventModel(T eventModel)
    {
        Event = eventModel;
    }
}
