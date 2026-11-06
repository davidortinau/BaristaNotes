using BaristaNotes.Api.Security;
using Microsoft.AspNetCore.Http;

namespace BaristaNotes.Api.Tests;

public class ClientHeaderTests
{
    static HeaderDictionary Headers(params (string Name, string Value)[] values)
    {
        var headers = new HeaderDictionary();
        foreach (var (name, value) in values) headers[name] = value;
        return headers;
    }

    [Theory]
    [InlineData("POST", "/demo/reset")]
    [InlineData("POST", "/runs")]
    [InlineData("PUT", "/agent/settings")]
    [InlineData("DELETE", "/memory/1")]
    public void State_changes_need_the_header(string method, string path)
    {
        Assert.False(ClientHeader.IsAllowed(method, path, Headers()));
        Assert.True(ClientHeader.IsAllowed(method, path, Headers((ClientHeader.Name, "app"))));
    }

    [Fact]
    public void Reads_do_not_need_it()
    {
        Assert.True(ClientHeader.IsAllowed("GET", "/runs/latest", Headers()));
    }

    [Fact]
    public void The_signed_webhook_does_not_need_it()
    {
        Assert.True(ClientHeader.IsAllowed("POST", "/webhooks/orders", Headers()));
    }

    [Fact]
    public void The_forwarder_needs_an_api_key_header()
    {
        Assert.False(ClientHeader.IsAllowed("POST", "/openai/deployments/x/chat/completions", Headers()));
        Assert.True(ClientHeader.IsAllowed("POST", "/openai/deployments/x/chat/completions", Headers(("api-key", "placeholder"))));
    }
}
