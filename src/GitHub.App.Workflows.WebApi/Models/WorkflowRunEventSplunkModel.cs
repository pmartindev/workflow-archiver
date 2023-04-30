using System;

namespace GitHub.App.Workflows.WebApi.Models;

public class WorkflowRunEventSplunkModel
{
    public string? LogArchiveFileName { get; set; }

    public string? LogArchiveStorageBucket { get; set; }

    public string? LogArchiveStorageEndpoint { get; set; }

    public string? Organization { get; set; }

    public string? Repository { get; set; }

    public long RunAttempt { get; set; }

    public DateTime RunStartedAt { get; set; }

    public string? TriggeringActorEmail { get; set; }

    public string? TriggeringActorName { get; set; }

    public string? WorkflowName { get; set; }

    public string? WorkflowResult { get; set; }
}
