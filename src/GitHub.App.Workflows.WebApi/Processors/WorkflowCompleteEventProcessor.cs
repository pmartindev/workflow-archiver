using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using GitHub.App.Workflows.WebApi.Clients;
using GitHub.App.Workflows.WebApi.Factories;
using GitHub.App.Workflows.WebApi.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Octokit.Webhooks;
using Octokit.Webhooks.Events;
using Octokit.Webhooks.Events.WorkflowRun;

namespace GitHub.App.Workflows.WebApi.Processors;

public class WorkflowCompleteEventProcessor : WebhookEventProcessor
{
    private readonly IConfiguration _configuration;
    private readonly GitHubClientFactory _gitHubClientFactory;
    private readonly ILogger<WorkflowCompleteEventProcessor> _logger;
    private readonly ISplunkClient _splunkClient;
    private readonly IStorageClient _storageClient;

    public WorkflowCompleteEventProcessor(
        GitHubClientFactory gitHubClientFactory,
        IConfiguration configuration,
        ILogger<WorkflowCompleteEventProcessor> logger,
        ISplunkClient splunkClient,
        IStorageClient storageClient)
    {
        _configuration = configuration;
        _gitHubClientFactory = gitHubClientFactory;
        _logger = logger;
        _splunkClient = splunkClient;
        _storageClient = storageClient;
    }

    private async Task<byte[]> AddWorkflowRunEventToArchive(WorkflowRunEvent workflowRunEvent, byte[] rawLogsArchive)
    {
        _logger.LogInformation("Adding workflow run event to archive");

        _logger.LogDebug("Serializing workflow run event to JSON");
        var workflowRunJson = JsonConvert.SerializeObject(workflowRunEvent.WorkflowRun, Formatting.Indented);

        _logger.LogDebug("Getting bytes for JSON");
        var workflowRunJsonBuffer = Encoding.UTF8.GetBytes(workflowRunJson);

        _logger.LogDebug("Writing archive to memory stream");
        using var zipFileMemoryStream = new MemoryStream();
        await zipFileMemoryStream.WriteAsync(rawLogsArchive);

        using (var zipArchive = new ZipArchive(zipFileMemoryStream, ZipArchiveMode.Update))
        {
            _logger.LogDebug("Creating entry in archive for `workflow-run-event.json` file");
            var workflowEventZipArchiveEntry = zipArchive.CreateEntry("workflow-run-event.json");

            _logger.LogDebug("Writing JSON to `workflow-run-event.json` file in the archive");
            await using var zipArchiveEntryStream = workflowEventZipArchiveEntry.Open();
            await zipArchiveEntryStream.WriteAsync(workflowRunJsonBuffer);
        }

        return zipFileMemoryStream.ToArray();
    }

    private string CreateFileName(string organization, string repository, string workflowName, DateTimeOffset starDateTime)
    {
        _logger.LogInformation("Generating file name for archive");
        var fileName = _configuration.GetSection($"S3Bucket:{organization}:{repository}:{workflowName}:FileName").Value;

        return fileName ?? $"{starDateTime.ToUnixTimeSeconds()}-{organization}-{repository}-{workflowName}.zip";
    }

    private string GetBucketName(string organization, string repository, string workflowName)
    {
        _logger.LogInformation("Getting S3 Bucket name");
        var bucketName = _configuration.GetSection($"S3Bucket:{organization}:{repository}:{workflowName}:BucketName").Value;

        return bucketName ?? $"{organization}-{repository}-{workflowName}".ToLower();
    }

    private async Task LogToSplunk(WorkflowRunEvent workflowRunEvent, string s3BucketLocation, string fileName)
    {
        _logger.LogInformation("Sending event to Splunk");

        var workflowRunEventSplunkModel = new WorkflowRunEventSplunkModel
        {
            LogArchiveFileName = fileName,
            LogArchiveStorageBucket = s3BucketLocation,
            LogArchiveStorageEndpoint = _storageClient.Endpoint,
            Organization = workflowRunEvent.Organization?.Login,
            Repository = workflowRunEvent.Repository?.Name,
            RunAttempt = workflowRunEvent.WorkflowRun.RunAttempt,
            RunStartedAt = workflowRunEvent.WorkflowRun.RunStartedAt.UtcDateTime,
            TriggeringActorEmail = workflowRunEvent.WorkflowRun.TriggeringActor.Email,
            TriggeringActorName = workflowRunEvent.WorkflowRun.TriggeringActor.Name,
            WorkflowName = workflowRunEvent.WorkflowRun.Name,
            WorkflowResult = workflowRunEvent.WorkflowRun.Conclusion.ToString()
        };

        await _splunkClient.LogEvent(new SplunkEventModel<WorkflowRunEventSplunkModel>(workflowRunEventSplunkModel));
    }

    protected override Task ProcessPingWebhookAsync(WebhookHeaders headers, PingEvent pingEvent)
    {
        _logger.LogInformation("Ping event received");

        return Task.FromResult(0);
    }

    protected override async Task ProcessWorkflowRunWebhookAsync(WebhookHeaders headers, WorkflowRunEvent workflowRunEvent, WorkflowRunAction action)
    {
        _logger.LogInformation("Webhook event received");

        _logger.LogDebug("Webhook action is {action}", workflowRunEvent.Action);

        if (workflowRunEvent.Action == null || !workflowRunEvent.Action.Equals("completed", StringComparison.CurrentCultureIgnoreCase))
        {
            _logger.LogInformation("Action is not completed, nothing to do");
            return;
        }

        _logger.LogInformation("Webhook action is completed");

        if (workflowRunEvent.Organization == null)
        {
            throw new ArgumentException("Unable to determine the organization the event came from.", nameof(workflowRunEvent));
        }

        if (workflowRunEvent.Repository == null)
        {
            throw new ArgumentException("Unable to determine the repository the event came from.", nameof(workflowRunEvent));
        }

        _logger.LogDebug(
            "Event:\r\n\tAction: {action}\r\n\tOrganization: {org}\r\n\tRepository: {repo}\r\n\tSender: {sender}",
            workflowRunEvent.Action,
            workflowRunEvent.Organization,
            workflowRunEvent.Repository,
            workflowRunEvent.Sender);

        _logger.LogDebug("Fetching GitHub client");

        var gitHubClient = await _gitHubClientFactory.GetClient(workflowRunEvent.Organization.Login);

        _logger.LogInformation("Fetching workflow logs for `{workflow}`", workflowRunEvent.Workflow.Name);

        var rawLogs = await gitHubClient.Actions.Workflows.Runs.GetLogs(
            workflowRunEvent.Organization.Login,
            workflowRunEvent.Repository.Name,
            workflowRunEvent.WorkflowRun.Id);

        rawLogs = await AddWorkflowRunEventToArchive(workflowRunEvent, rawLogs);

        var location = GetBucketName(
            workflowRunEvent.Organization.Login,
            workflowRunEvent.Repository.Name,
            workflowRunEvent.WorkflowRun.Name);

        _logger.LogDebug("S3 Bucket: {location}", location);

        var fileName = CreateFileName(
            workflowRunEvent.Organization.Login,
            workflowRunEvent.Repository.Name,
            workflowRunEvent.WorkflowRun.Name,
            workflowRunEvent.WorkflowRun.CreatedAt);

        _logger.LogDebug("File Name: {fileName}", fileName);

        await UploadArchiveToS3Bucket(location, fileName, rawLogs);

        await LogToSplunk(workflowRunEvent, location, fileName);
    }

    private async Task UploadArchiveToS3Bucket(string location, string fileName, byte[] rawLogs)
    {
        await _storageClient.UploadFile(location, fileName, rawLogs);

        _logger.LogInformation("Successfully uploaded {location}/{fileName}", location, fileName);
    }
}
