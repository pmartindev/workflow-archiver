using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using GitHub.App.Workflows.WebApi.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using RestSharp;
using RestSharp.Serializers.Json;

namespace GitHub.App.Workflows.WebApi.Clients;

public class SplunkClient : ISplunkClient
{
    private readonly string? _host;
    private readonly string? _index;
    private readonly ILogger _logger;
    private readonly RestClient _restClient;
    private readonly bool _sendEvents;
    private readonly string? _source;
    private readonly string? _sourceType;

    public SplunkClient(IConfiguration configuration, ILogger<SplunkClient> logger)
    {
        _logger = logger;

        var splunkEndpoint = configuration.GetSection("Splunk:Endpoint").Value;

        if (splunkEndpoint == null) throw new NullReferenceException("The Splunk endpoint could not be located in the configuration");

        var splunkToken = configuration.GetSection("Splunk:Token").Value;

        if (splunkToken == null) throw new NullReferenceException("The Splunk token could not be located in the configuration");

        _host = configuration.GetSection("Splunk:Host").Value ?? Environment.MachineName;
        _index = configuration.GetSection("Splunk:Index").Value;
        _sendEvents = string.Equals(configuration.GetSection("Splunk:SendEvents").Value, true.ToString(), StringComparison.InvariantCultureIgnoreCase);
        _source = configuration.GetSection("Splunk:Source").Value;
        _sourceType = configuration.GetSection("Splunk:SourceType").Value;

        _restClient = new RestClient(splunkEndpoint);
        _restClient.AddDefaultHeader("Authorization", $"Splunk {splunkToken}");

        _restClient.UseSystemTextJson(new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });
    }

    public async Task LogEvent<T>(SplunkEventModel<T> splunkEventModel)
    {
        if (!_sendEvents)
        {
            _logger.LogInformation("Send to Splunk feature turned off");
            return;
        }

        try
        {
            splunkEventModel.Host = _host;
            splunkEventModel.Index = _index;
            splunkEventModel.Source = _source;
            splunkEventModel.SourceType = _sourceType;

            _logger.LogInformation("Serializing data to JSON");

            var json = JsonConvert.SerializeObject(splunkEventModel, Formatting.Indented, new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                NullValueHandling = NullValueHandling.Ignore
            });

            _logger.LogDebug("SplunkEventModel: {json}", json);

            _logger.LogInformation("Creating RestRequest");

            var request = new RestRequest("/services/collector", Method.Post);

            request.AddParameter("application/json", json, ParameterType.RequestBody);

            _logger.LogInformation("Executing request");
            var response = await _restClient.ExecuteAsync(request);

            if (!response.IsSuccessful)
            {
                _logger.LogError("Failed to send data to Splunk");
            }
            else
            {
                _logger.LogInformation("Successful");
            }
        }
        catch (Exception exception)
        {
            _logger.LogError("Error sending data to Splunk:\r\n{error}", exception.ToString());
        }
    }
}
