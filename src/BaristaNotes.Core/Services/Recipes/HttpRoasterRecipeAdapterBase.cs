using System.Net.Http;
using BaristaNotes.Core.Models;
using Microsoft.Extensions.Logging;

namespace BaristaNotes.Core.Services.Recipes;

/// <summary>
/// Shared base for HTTP-based roaster adapters. Provides a safe fetch helper
/// that swallows transport errors, respects cancellation, and logs
/// diagnostically so concrete adapters can focus on URL construction and
/// HTML parsing.
/// </summary>
public abstract class HttpRoasterRecipeAdapterBase : IRoasterRecipeAdapter
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

    protected readonly HttpClient HttpClient;
    protected readonly ILogger Logger;

    protected HttpRoasterRecipeAdapterBase(HttpClient httpClient, ILogger logger)
    {
        HttpClient = httpClient;
        Logger = logger;
    }

    public abstract string Id { get; }
    public abstract string RoasterName { get; }

    public virtual bool CanHandle(Bean bean)
    {
        var r = bean.Roaster;
        if (string.IsNullOrWhiteSpace(r)) return false;
        return string.Equals(r.Trim(), RoasterName, StringComparison.OrdinalIgnoreCase)
            || r.Trim().Replace(" ", "", StringComparison.Ordinal)
                .Equals(RoasterName.Replace(" ", "", StringComparison.Ordinal),
                    StringComparison.OrdinalIgnoreCase);
    }

    public async Task<IReadOnlyList<ScrapedRecipe>> FetchAsync(Bean bean, CancellationToken ct)
    {
        try
        {
            return await FetchCoreAsync(bean, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            Logger.LogDebug("Adapter {AdapterId} cancelled BeanId={BeanId}", Id, bean.Id);
            return Array.Empty<ScrapedRecipe>();
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex,
                "Adapter {AdapterId} failed BeanId={BeanId} Roaster={Roaster}",
                Id, bean.Id, bean.Roaster);
            return Array.Empty<ScrapedRecipe>();
        }
    }

    /// <summary>
    /// Concrete adapters implement URL construction + HTML parsing here.
    /// May throw — <see cref="FetchAsync"/> converts exceptions to empty list.
    /// </summary>
    protected abstract Task<IReadOnlyList<ScrapedRecipe>> FetchCoreAsync(
        Bean bean, CancellationToken ct);

    /// <summary>
    /// Politely fetches an HTML document. Uses a polite User-Agent and a
    /// 10-second timeout. Returns null on non-success status or transport failure.
    /// </summary>
    protected async Task<string?> TryGetHtmlAsync(string url, CancellationToken ct)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(DefaultTimeout);

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.TryAddWithoutValidation("User-Agent",
                "BaristaNotesApp/1.0 (+https://github.com/davidortinau/BaristaNotes)");
            request.Headers.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml");

            using var response = await HttpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseContentRead,
                cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                Logger.LogDebug(
                    "Adapter {AdapterId} HTTP {StatusCode} for {Url}",
                    Id, (int)response.StatusCode, url);
                return null;
            }

            return await response.Content.ReadAsStringAsync(cts.Token);
        }
        catch (HttpRequestException ex)
        {
            Logger.LogDebug(ex, "Adapter {AdapterId} transport error for {Url}", Id, url);
            return null;
        }
    }
}
