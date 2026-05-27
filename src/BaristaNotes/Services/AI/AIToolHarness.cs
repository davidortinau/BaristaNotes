#if DEBUG
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace BaristaNotes.Services.AI;

/// <summary>
/// DEBUG-only static harness that drives the source-generated AI tool surface
/// (VoiceTools.Default.Tools) end-to-end through the live DI graph. Lets us
/// validate [ExportAIFunction] generator output, AIFunctionArguments.Services
/// resolution, real VoiceCommandService + NavigationTools instances, and JSON
/// argument deserialization — all without a live mic or LLM round-trip.
///
/// Read results with: maui devflow logs --limit 300 | grep AI-HARNESS
/// </summary>
internal static class AIToolHarness
{
    public static async Task RunAsync(IServiceProvider rootProvider)
    {
        // Give the rest of startup a moment to settle (DB migrations, Shell init).
        await Task.Delay(TimeSpan.FromSeconds(8));

        ILogger logger;
        try
        {
            logger = rootProvider.GetRequiredService<ILoggerFactory>()
                .CreateLogger("BaristaNotes.AIToolHarness");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"AI-HARNESS: failed to resolve logger: {ex}");
            return;
        }

        try
        {
            logger.LogInformation("AI-HARNESS: starting");

            using var scope = rootProvider.CreateScope();
            var sp = scope.ServiceProvider;

            // Resolve the host services first so any wiring problem surfaces clearly.
            _ = sp.GetRequiredService<VoiceCommandService>();
            _ = sp.GetRequiredService<NavigationTools>();

            var tools = VoiceTools.Default.Tools;
            logger.LogInformation("AI-HARNESS: VoiceTools.Default.Tools count = {Count}", tools.Count);

            // Read-only probes only. Every method here uses optional parameters with
            // safe defaults so empty arg dictionaries are valid.
            var probes = new (string Name, Dictionary<string, object?> Args)[]
            {
                ("get_available_pages", new()),
                ("get_shot_count", new()),
                ("get_bean_count", new()),
                ("get_bag_count", new()),
                ("get_equipment_count", new()),
                ("get_profile_count", new()),
                ("get_last_shot", new()),
                ("find_shots", new() { ["limit"] = 3 }),
                ("find_beans", new() { ["limit"] = 3 }),
                ("navigate_to", new() { ["pageName"] = "history" }),
            };

            var passed = 0;
            var failed = 0;
            foreach (var (name, args) in probes)
            {
                var tool = tools.OfType<AIFunction>().FirstOrDefault(t => t.Name == name);
                if (tool is null)
                {
                    logger.LogWarning("AI-HARNESS FAIL {Tool}: not present in generated context", name);
                    failed++;
                    continue;
                }

                try
                {
                    var fnArgs = new AIFunctionArguments(args) { Services = sp };
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                    var result = await tool.InvokeAsync(fnArgs, cts.Token);
                    var asText = result switch
                    {
                        null => "<null>",
                        string s => s,
                        JsonElement je => je.ToString(),
                        _ => JsonSerializer.Serialize(result)
                    };
                    if (asText.Length > 200) asText = asText[..200] + "…";
                    logger.LogInformation("AI-HARNESS PASS {Tool}: {Result}", name, asText);
                    passed++;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "AI-HARNESS FAIL {Tool}", name);
                    failed++;
                }
            }

            logger.LogInformation("AI-HARNESS: summary passed={Passed} failed={Failed} total={Total}",
                passed, failed, probes.Length);

            await RunLlmRoundTripAsync(sp, logger);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "AI-HARNESS: unhandled error");
        }

        // After validation, start the file-based prompt loop so the scenario
        // can be driven from the shell. Runs for the lifetime of the app.
        _ = Task.Run(() => RunPromptLoopAsync(rootProvider));
    }

    /// <summary>
    /// Polls /tmp/baristanotes-prompt.txt for a single prompt, runs it through
    /// VoiceCommandService.InterpretCommandAsync (the same path the mic uses),
    /// and writes the natural-language response to /tmp/baristanotes-response.txt.
    /// Lets the shell drive multi-step scenarios with screenshots between steps.
    /// </summary>
    private static async Task RunPromptLoopAsync(IServiceProvider rootProvider)
    {
        var logger = rootProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger("BaristaNotes.AIToolHarness");
        // Path.GetTempPath() resolves to the app sandbox's tmp dir on Mac Catalyst
        // (~/Library/Containers/<bundle-id>/Data/tmp/) which is reachable from the
        // host shell too.
        var promptFile = Path.Combine(Path.GetTempPath(), "baristanotes-prompt.txt");
        var responseFile = Path.Combine(Path.GetTempPath(), "baristanotes-response.txt");

        logger.LogInformation("AI-HARNESS PROMPT-LOOP: watching {Path}", promptFile);
        while (true)
        {
            try
            {
                if (File.Exists(promptFile))
                {
                    var prompt = (await File.ReadAllTextAsync(promptFile)).Trim();
                    try { File.Delete(promptFile); } catch { }
                    if (string.IsNullOrWhiteSpace(prompt))
                    {
                        await Task.Delay(500);
                        continue;
                    }
                    logger.LogInformation("AI-HARNESS PROMPT-LOOP: prompt={Prompt}", prompt);
                    using var scope = rootProvider.CreateScope();
                    var vcs = scope.ServiceProvider.GetRequiredService<VoiceCommandService>();
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
                    var req = new VoiceCommandRequestDto(prompt, Confidence: 1.0);
                    var resp = await vcs.InterpretCommandAsync(req, cts.Token);
                    var text = resp.ConfirmationMessage
                        ?? resp.ErrorMessage
                        ?? "(no message)";
                    await File.WriteAllTextAsync(responseFile, text);
                    logger.LogInformation("AI-HARNESS PROMPT-LOOP: response={Text}", text);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "AI-HARNESS PROMPT-LOOP error");
                try { await File.WriteAllTextAsync(responseFile, $"ERROR: {ex.Message}"); } catch { }
            }
            await Task.Delay(500);
        }
    }

    /// <summary>
    /// Validates the full IChatClient → UseFunctionInvocation → tool-call path.
    /// Sends real prompts through VoiceCommandService.InterpretCommandAsync (the
    /// same entry point the mic uses) and asserts the LLM picked tools from the
    /// source-generated VoiceTools surface and produced sensible answers.
    /// </summary>
    private static async Task RunLlmRoundTripAsync(IServiceProvider sp, ILogger logger)
    {
        logger.LogInformation("AI-HARNESS LLM: entering round-trip phase");
        var cfg = sp.GetRequiredService<IConfiguration>();
        var endpoint = cfg["AzureOpenAI:Endpoint"];
        var apiKey = cfg["AzureOpenAI:ApiKey"];
        logger.LogInformation("AI-HARNESS LLM: config endpoint={HasEndpoint} apiKey={HasKey}",
            !string.IsNullOrWhiteSpace(endpoint), !string.IsNullOrWhiteSpace(apiKey));
        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(apiKey))
        {
            logger.LogInformation("AI-HARNESS LLM: skipped (no AzureOpenAI config)");
            return;
        }

        // Each prompt below should compel the LLM to invoke a specific tool from
        // the generated VoiceTools surface. If the tool schema is broken or the
        // Tools collection isn't reaching the chat client, the response won't
        // contain the expected substring.
        var prompts = new (string Prompt, string[] ExpectedSubstrings)[]
        {
            ("How many shots have I pulled?",       new[] { "0", "shot" }),
        };

        var vcs = sp.GetRequiredService<VoiceCommandService>();
        var passed = 0;
        var failed = 0;

        foreach (var (prompt, expected) in prompts)
        {
            try
            {
                logger.LogInformation("AI-HARNESS LLM: sending prompt {Prompt}", prompt);
                var req = new VoiceCommandRequestDto(prompt, Confidence: 1.0);
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(25));
                var resp = await vcs.InterpretCommandAsync(req, cts.Token);
                logger.LogInformation("AI-HARNESS LLM: response received intent={Intent} hasError={HasError}",
                    resp.Intent, !string.IsNullOrEmpty(resp.ErrorMessage));

                var msg = resp.ConfirmationMessage ?? string.Empty;
                if (!string.IsNullOrEmpty(resp.ErrorMessage))
                {
                    logger.LogWarning("AI-HARNESS LLM FAIL {Prompt}: error={Error}",
                        prompt, resp.ErrorMessage);
                    failed++;
                    continue;
                }

                var lower = msg.ToLowerInvariant();
                var allMatched = expected.All(e => lower.Contains(e.ToLowerInvariant()));
                if (allMatched)
                {
                    logger.LogInformation("AI-HARNESS LLM PASS {Prompt} → {Msg}", prompt, msg);
                    passed++;
                }
                else
                {
                    logger.LogWarning(
                        "AI-HARNESS LLM FAIL {Prompt}: expected substrings [{Expected}] not all present in: {Msg}",
                        prompt, string.Join(",", expected), msg);
                    failed++;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "AI-HARNESS LLM FAIL {Prompt}", prompt);
                failed++;
            }
        }

        logger.LogInformation("AI-HARNESS LLM: summary passed={Passed} failed={Failed} total={Total}",
            passed, failed, prompts.Length);
    }
}
#endif
