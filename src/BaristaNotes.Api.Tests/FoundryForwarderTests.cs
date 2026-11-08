using System.Net;
using Azure.Core;
using BaristaNotes.Api.Forwarding;

namespace BaristaNotes.Api.Tests;

public class FoundryForwarderTests
{
    [Fact]
    public void ApplyKey_replaces_any_key_the_caller_sent()
    {
        using var request = new HttpRequestMessage();
        request.Headers.TryAddWithoutValidation(FoundryForwarder.KeyHeader, "placeholder-from-app");

        FoundryForwarder.ApplyKey(request.Headers, "real-key");

        Assert.Equal(["real-key"], request.Headers.GetValues(FoundryForwarder.KeyHeader));
    }

    [Fact]
    public void ApplyKey_removes_caller_authorization()
    {
        using var request = new HttpRequestMessage();
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer something");

        FoundryForwarder.ApplyKey(request.Headers, "real-key");

        Assert.False(request.Headers.Contains("Authorization"));
    }

    [Fact]
    public void Parse_reads_endpoint_and_key()
    {
        var connection = FoundryConnection.Parse("Endpoint=https://example.cognitiveservices.azure.com/;Key=abc123");

        Assert.Equal(new Uri("https://example.cognitiveservices.azure.com/"), connection.Endpoint);
        Assert.Equal("abc123", connection.Key);
    }

    [Theory]
    [InlineData("Key=abc123")]
    [InlineData("Endpoint=;Key=abc123")]
    [InlineData("Endpoint=https://example.cognitiveservices.azure.com/;Key=' '")]
    public void Parse_rejects_incomplete_connection_strings(string value)
    {
        Assert.Throws<InvalidOperationException>(() => FoundryConnection.Parse(value));
    }

    [Fact]
    public void Parse_accepts_provisioned_Foundry_without_a_key()
    {
        var connection = FoundryConnection.Parse(
            "Endpoint=https://example.cognitiveservices.azure.com/;EndpointAIInference=https://example.services.ai.azure.com/models");

        Assert.Equal(new Uri("https://example.cognitiveservices.azure.com/"), connection.Endpoint);
        Assert.Null(connection.Key);
    }

    [Fact]
    public async Task Entra_authentication_replaces_caller_credentials()
    {
        using var request = new HttpRequestMessage();
        request.Headers.TryAddWithoutValidation("api-key", "placeholder");
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer caller-token");
        var credential = new TestCredential();
        using var cancellation = new CancellationTokenSource();

        await FoundryForwarder.ApplyAuthenticationAsync(request.Headers,
            new FoundryConnection(new Uri("https://example.cognitiveservices.azure.com/"), null),
            credential, cancellation.Token);

        Assert.False(request.Headers.Contains("api-key"));
        Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
        Assert.Equal("server-token", request.Headers.Authorization?.Parameter);
        Assert.NotNull(credential.Context);
        Assert.Equal(["https://cognitiveservices.azure.com/.default"], credential.Context.Value.Scopes);
        Assert.Equal(cancellation.Token, credential.CancellationToken);
    }

    [Fact]
    public async Task Key_authentication_does_not_request_an_Entra_token()
    {
        using var request = new HttpRequestMessage();
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer caller-token");
        var credential = new TestCredential();

        await FoundryForwarder.ApplyAuthenticationAsync(request.Headers,
            new FoundryConnection(new Uri("https://example.cognitiveservices.azure.com/"), "server-key"),
            credential, CancellationToken.None);

        Assert.Equal(["server-key"], request.Headers.GetValues("api-key"));
        Assert.False(request.Headers.Contains("Authorization"));
        Assert.Null(credential.Context);
    }

    [Fact]
    public void ToString_never_contains_the_key()
    {
        var connection = FoundryConnection.Parse("Endpoint=https://example.cognitiveservices.azure.com/;Key=secret-value");

        Assert.DoesNotContain("secret-value", connection.ToString());
    }

    [Theory]
    [InlineData("127.0.0.1", true)]
    [InlineData("::1", true)]
    [InlineData("192.168.1.20", false)]
    public void IsLoopback_accepts_only_local_callers(string address, bool expected)
    {
        Assert.Equal(expected, FoundryForwarder.IsLoopback(IPAddress.Parse(address)));
    }

    [Fact]
    public void IsLoopback_rejects_unknown_caller()
    {
        Assert.False(FoundryForwarder.IsLoopback(null));
    }

    sealed class TestCredential : TokenCredential
    {
        public TokenRequestContext? Context { get; private set; }
        public CancellationToken CancellationToken { get; private set; }

        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            Context = requestContext;
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(new AccessToken("server-token", DateTimeOffset.UtcNow.AddHours(1)));
        }
    }
}
