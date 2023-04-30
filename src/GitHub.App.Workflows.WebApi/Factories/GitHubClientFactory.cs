using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Octokit;

namespace GitHub.App.Workflows.WebApi.Factories;

public class GitHubClientFactory
{
    private readonly Dictionary<string, DateTime> _accessExpireDateTimes;
    private readonly string _applicationName;
    private readonly IConfiguration _configuration;
    private readonly Dictionary<string, IGitHubClient> _gitHubClients;

    public GitHubClientFactory(IConfiguration configuration)
    {
        var applicationName = configuration.GetSection("GitHub:ApplicationName").Value;

        if (string.IsNullOrWhiteSpace(applicationName)) throw new NullReferenceException("The Application Name was not specified in the settings file.");

        _accessExpireDateTimes = new Dictionary<string, DateTime>();
        _applicationName = applicationName;
        _configuration = configuration;
        _gitHubClients = new Dictionary<string, IGitHubClient>();
    }

    private async Task<IGitHubClient> CreateGitHubClient(string organization)
    {
        var apiUrl = _configuration.GetSection($"GitHub:{organization}:ApiUrl").Value;

        if (string.IsNullOrWhiteSpace(apiUrl)) apiUrl = "https://api.github.com";

        var applicationId = _configuration.GetSection($"GitHub:{organization}:ApplicationId").Value;

        if (string.IsNullOrWhiteSpace(applicationId))
        {
            throw new NullReferenceException($"The Application ID was not specified in the settings file for '{organization}' organization.");
        }

        var pemFilePath = _configuration.GetSection($"GitHub:{organization}:PrivateKeyFilePath").Value;

        if (string.IsNullOrWhiteSpace(pemFilePath))
        {
            throw new NullReferenceException("The path to the PEM file was not specified in the settings file for '{organization}' organization.");
        }

        var jwtToken = await GetJwtToken(applicationId, pemFilePath);

        var appGitHubClient = new GitHubClient(new ProductHeaderValue(_applicationName), new Uri(apiUrl))
        {
            Credentials = new Credentials(jwtToken, AuthenticationType.Bearer)
        };

        var installation = await appGitHubClient.GitHubApps.GetOrganizationInstallationForCurrent(organization);

        var accessToken = await appGitHubClient.GitHubApps.CreateInstallationToken(installation.Id);

        if (_accessExpireDateTimes.ContainsKey(organization))
        {
            _accessExpireDateTimes[organization] = accessToken.ExpiresAt.UtcDateTime;
        }
        else
        {
            _accessExpireDateTimes.Add(organization, accessToken.ExpiresAt.UtcDateTime);
        }

        return new GitHubClient(new ProductHeaderValue(_applicationName), new Uri(apiUrl))
        {
            Credentials = new Credentials(accessToken.Token, AuthenticationType.Bearer)
        };
    }

    public async Task<IGitHubClient> GetClient(string organization)
    {
        if (!_gitHubClients.ContainsKey(organization)) _gitHubClients.Add(organization, await CreateGitHubClient(organization));

        var tokenExpiresAt = _accessExpireDateTimes[organization];

        // Refresh token within 1 minute of expiring
        if (tokenExpiresAt.AddMinutes(1) < DateTime.UtcNow) await RefreshGitHubToken(organization);

        return _gitHubClients[organization];
    }

    private static async Task<string> GetJwtToken(string applicationId, string pemFilePath)
    {
        var pem = await File.ReadAllTextAsync(pemFilePath);

        pem = pem.Replace("-----BEGIN RSA PRIVATE KEY-----", string.Empty);
        pem = pem.Replace("-----END RSA PRIVATE KEY-----", string.Empty);
        pem = pem.Trim();

        var privateKeyRaw = Convert.FromBase64String(pem);

        using var rsa = RSA.Create();

        rsa.ImportRSAPrivateKey(new ReadOnlySpan<byte>(privateKeyRaw), out _);

        var utcDateTime = DateTime.UtcNow.AddSeconds(-60);

        var unixUtcDateTime = new DateTimeOffset(utcDateTime).ToUnixTimeSeconds().ToString();

        var rsaKey = new RsaSecurityKey(rsa);

        var jwtSecurityToken = new JwtSecurityToken(
            default,
            default,
            new List<Claim>
            {
                new(JwtRegisteredClaimNames.Iss, applicationId),
                new(JwtRegisteredClaimNames.Iat, unixUtcDateTime, "http://www.w3.org/2001/XMLSchema#integer")
            },
            utcDateTime,
            utcDateTime.AddMinutes(10), // GitHub App JWT tokens are only valid for 10 minutes
            new SigningCredentials(rsaKey, SecurityAlgorithms.RsaSha256)
            {
                CryptoProviderFactory = new CryptoProviderFactory
                {
                    CacheSignatureProviders = false
                }
            }
        );

        var jwtSecurityTokenHandler = new JwtSecurityTokenHandler();

        return jwtSecurityTokenHandler.WriteToken(jwtSecurityToken);
    }

    private async Task RefreshGitHubToken(string organization)
    {
        _gitHubClients[organization] = await CreateGitHubClient(organization);
    }
}
