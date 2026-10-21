using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;

namespace BaristaNotes.Api.Agent;

public enum ReplayMode
{
    /// <summary>Call the model and record each answer. Replay only when the model cannot be reached.</summary>
    Auto,

    /// <summary>Never call the model. Use recorded answers only.</summary>
    Only,

    /// <summary>Call the model. No recording, no replay.</summary>
    Off,
}

/// <summary>Where rehearsal answers live and which mode is on. The dashboard can switch the mode.</summary>
public sealed class ModelReplay(string directory, ReplayMode mode)
{
    public string Directory { get; } = directory;

    public ReplayMode Mode { get; set; } = mode;
}

/// <summary>
/// Conference Wi-Fi insurance. Records the model's answers during rehearsal
/// and replays the matching answer when the model cannot be reached. Every
/// replay is reported in the run's ledger, so the stage never pretends.
/// </summary>
public sealed partial class ReplayChatClient(IChatClient inner, ModelReplay replay, ILogger<ReplayChatClient> log)
    : DelegatingChatClient(inner)
{
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var list = messages.ToList();
        var key = Key(list, options);

        if (replay.Mode == ReplayMode.Only)
        {
            return await ReplayAsync(key, cause: null);
        }

        try
        {
            var response = await base.GetResponseAsync(list, options, cancellationToken);
            if (replay.Mode == ReplayMode.Auto && !string.IsNullOrEmpty(response.Text))
            {
                System.IO.Directory.CreateDirectory(replay.Directory);
                await File.WriteAllTextAsync(PathFor(key), response.Text, CancellationToken.None);
            }

            return response;
        }
        catch (Exception ex) when (replay.Mode == ReplayMode.Auto && IsUnreachable(ex, cancellationToken))
        {
            log.LogWarning(ex, "The model could not be reached. Looking for a rehearsal answer");
            return await ReplayAsync(key, ex);
        }
    }

    async Task<ChatResponse> ReplayAsync(string key, Exception? cause)
    {
        var path = PathFor(key);
        if (!File.Exists(path))
        {
            throw new InvalidOperationException(
                cause is null
                    ? "Rehearsal answers are on, and there is no recorded answer for this request."
                    : "The model could not be reached, and there is no recorded answer for this request.",
                cause);
        }

        if (RunContext.Value is { } run)
        {
            run.Flags.ReplayReason = cause is null
                ? "Rehearsal answers are switched on."
                : "The model could not be reached.";
        }

        var text = await File.ReadAllTextAsync(path);
        return new ChatResponse(new ChatMessage(ChatRole.Assistant, text)) { ModelId = "rehearsal-replay" };
    }

    static bool IsUnreachable(Exception ex, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        // A 4xx answer means the model was reached. That is a real error, not Wi-Fi.
        if (ex is System.ClientModel.ClientResultException { Status: >= 400 and < 500 })
        {
            return false;
        }

        return ex is HttpRequestException or TaskCanceledException or TimeoutException or System.ClientModel.ClientResultException
               || ex.InnerException is not null && IsUnreachable(ex.InnerException, cancellationToken);
    }

    string PathFor(string key) => Path.Combine(replay.Directory, key + ".txt");

    /// <summary>
    /// The request's identity. Dates are left out, so a demo database that was
    /// seeded on another day still matches its rehearsal.
    /// </summary>
    static string Key(IEnumerable<ChatMessage> messages, ChatOptions? options)
    {
        var text = new StringBuilder();
        text.AppendLine(options?.Instructions);
        if (options?.ResponseFormat is ChatResponseFormatJson json)
        {
            text.AppendLine(json.Schema?.ToString());
        }

        foreach (var message in messages)
        {
            text.Append(message.Role.Value).Append(": ").AppendLine(message.Text);
        }

        var normalized = Dates().Replace(text.ToString(), "DATE");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)))[..32];
    }

    [GeneratedRegex(@"\d{4}-\d{2}-\d{2}")]
    private static partial Regex Dates();
}
