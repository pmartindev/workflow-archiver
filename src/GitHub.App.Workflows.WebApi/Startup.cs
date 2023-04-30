using System;
using GitHub.App.Workflows.WebApi.Clients;
using GitHub.App.Workflows.WebApi.Factories;
using GitHub.App.Workflows.WebApi.Processors;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Octokit.Webhooks;
using Octokit.Webhooks.AspNetCore;

namespace GitHub.App.Workflows.WebApi;

public class Startup
{
    public IConfiguration Configuration { get; }

    public Startup(IConfiguration configuration)
    {
        Configuration = configuration;
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseRouting();

        var clientSecret = Configuration["GitHub:ClientSecret"];

        if (string.IsNullOrWhiteSpace(clientSecret)) throw new NullReferenceException("The client secret is required.");

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
            endpoints.MapGitHubWebhooks(secret: clientSecret);
        });
    }

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddControllers();

        services.AddSingleton<GitHubClientFactory, GitHubClientFactory>();
        services.AddSingleton<ISplunkClient, SplunkClient>();
        services.AddSingleton<IStorageClient, StorageClient>();
        services.AddSingleton<WebhookEventProcessor, WorkflowCompleteEventProcessor>();
    }
}
