using System.Net;
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
    [InlineData("Endpoint=https://example.cognitiveservices.azure.com/")]
    [InlineData("Key=abc123")]
    [InlineData("Endpoint=;Key=abc123")]
    public void Parse_rejects_incomplete_connection_strings(string value)
    {
        Assert.Throws<InvalidOperationException>(() => FoundryConnection.Parse(value));
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
}
