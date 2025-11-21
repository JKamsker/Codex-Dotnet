using System.Text.Json;
using Codex.DotNet.Public;
using Codex.DotNet.Public.Models;

namespace Codex.DotNet.Demo;

internal static class Program
{
    private sealed record CliOptions(
        string Prompt,
        string WorkingDirectory,
        string SessionsRoot,
        string? CodexExecutablePath,
        CodexModel Model,
        CodexReasoningEffort Reasoning,
        bool FollowStream);

    private static async Task<int> Main(string[] args)
    {
        var options = ParseArgs(args);
        EnsureSessionsRoot(options.SessionsRoot);

        PrintBanner();
        PrintConfig(options);

        using var shutdownCts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true; // allow graceful cleanup
            shutdownCts.Cancel();
        };

        var clientOptions = new CodexClientOptions
        {
            SessionsRootDirectory = options.SessionsRoot,
            CodexExecutablePath = options.CodexExecutablePath
        };

        await using var client = new CodexClient(clientOptions);

        try
        {
            // Show latest cached rate limits (if any)
            await ShowRateLimitsAsync(client, noCache: false, shutdownCts.Token);

            // Refresh rate limits by sending a tiny "hi" in a throwaway session
            await RefreshRateLimitsAsync(client, options, shutdownCts.Token);
            await ShowRateLimitsAsync(client, noCache: true, shutdownCts.Token);

            var sessionOptions = new CodexSessionOptions(options.WorkingDirectory, options.Prompt)
            {
                Model = options.Model,
                ReasoningEffort = options.Reasoning
            };

            Console.WriteLine("Starting Codex session...");
            await using var session = await client.StartSessionAsync(sessionOptions, shutdownCts.Token);

            PrintSessionInfo(session.Info);
            Console.WriteLine("\nStreaming events (press Ctrl+C to stop):\n");

            var streamOptions = EventStreamOptions.Default with { Follow = options.FollowStream };
            await foreach (var evt in session.GetEventsAsync(streamOptions, shutdownCts.Token))
            {
                RenderEvent(evt);
            }

            // If follow is false the stream ends naturally; keep process alive briefly
            if (!options.FollowStream && session.IsLive)
            {
                Console.WriteLine("\nSession is still live; waiting for graceful exit...");
                await session.WaitForExitAsync(shutdownCts.Token);
            }

            // Demonstrate resume-launch with a follow-up prompt
            Console.WriteLine("\nResuming the session with follow-up: \"how are you\" ...\n");
            var followUpOptions = sessionOptions.Clone();
            followUpOptions.Prompt = "how are you";
            await using var resumed = await client.ResumeSessionAsync(session.Info.Id, followUpOptions, shutdownCts.Token);

            await foreach (var evt in resumed.GetEventsAsync(EventStreamOptions.Default with { Follow = true }, shutdownCts.Token))
            {
                RenderEvent(evt);
            }

            return 0;
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("\nCancelled by user.");
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("\nDemo failed:");
            Console.Error.WriteLine(ex.Message);
            await PrintDebugInfo(options);
            return 1;
        }
    }

    private static async Task ShowRateLimitsAsync(CodexClient client, bool noCache, CancellationToken ct)
    {
        var limits = await client.GetRateLimitsAsync(noCache, ct);
        if (limits is null)
        {
            Console.WriteLine(noCache
                ? "Rate limits: none found after refresh."
                : "Rate limits: none cached/found yet.");
            return;
        }

        static string Scope(RateLimitScope? s) =>
            s is null
                ? "n/a"
                : $"used={s.UsedPercent?.ToString("0.#") ?? "?"}% window={s.WindowMinutes?.ToString() ?? "?"}m resets={s.ResetsAt?.ToLocalTime():G}";

        var primary = Scope(limits.Primary);
        var secondary = Scope(limits.Secondary);
        var credits = limits.Credits is null
            ? "n/a"
            : $"has={limits.Credits.HasCredits?.ToString() ?? "?"}, unlimited={limits.Credits.Unlimited?.ToString() ?? "?"}, balance={limits.Credits.Balance ?? "?"}";

        Console.WriteLine($"Rate limits (noCache={noCache}): primary [{primary}], secondary [{secondary}], credits [{credits}]");
    }

    private static async Task RefreshRateLimitsAsync(CodexClient client, CliOptions options, CancellationToken ct)
    {
        var refreshOpts = new CodexSessionOptions(options.WorkingDirectory, "hi")
        {
            Model = options.Model,
            ReasoningEffort = options.Reasoning,
            AdditionalOptions = Array.Empty<string>()
        };

        await using var session = await client.StartSessionAsync(refreshOpts, ct);
        await foreach (var _ in session.GetEventsAsync(EventStreamOptions.Default with { Follow = false }, ct))
        {
            // drain events; rate limits will be updated in logs
        }
    }

    private static CliOptions ParseArgs(string[] args)
    {
        var prompt = new List<string>();
        string workingDirectory = Directory.GetCurrentDirectory();
        string sessionsRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".codex",
            "sessions");
        string? codexExecutablePath = null;
        var model = CodexModel.Default;
        var reasoning = CodexReasoningEffort.Medium;
        var follow = true;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-p" or "--prompt":
                    prompt.Add(RequireValue(args, ++i, "prompt"));
                    break;
                case "-w" or "--workdir":
                    workingDirectory = RequireValue(args, ++i, "workdir");
                    break;
                case "-s" or "--sessions":
                    sessionsRoot = RequireValue(args, ++i, "sessions");
                    break;
                case "--codex-path":
                    codexExecutablePath = RequireValue(args, ++i, "codex-path");
                    break;
                case "-m" or "--model":
                    var modelValue = RequireValue(args, ++i, "model");
                    model = CodexModel.Parse(modelValue);
                    break;
                case "-r" or "--reasoning":
                    var reasoningValue = RequireValue(args, ++i, "reasoning");
                    reasoning = CodexReasoningEffort.Parse(reasoningValue);
                    break;
                case "--no-follow":
                    follow = false;
                    break;
                default:
                    prompt.Add(args[i]);
                    break;
            }
        }

        var resolvedPrompt = prompt.Count > 0
            ? string.Join(" ", prompt)
            : "Summarize this repository in three concise bullet points.";

        return new CliOptions(
            resolvedPrompt,
            workingDirectory,
            sessionsRoot,
            codexExecutablePath,
            model,
            reasoning,
            follow);
    }

    private static string RequireValue(string[] args, int index, string name)
    {
        if (index >= args.Length)
        {
            throw new ArgumentException($"Missing value for --{name}");
        }

        return args[index];
    }

    private static void EnsureSessionsRoot(string sessionsRoot)
    {
        Directory.CreateDirectory(sessionsRoot);
    }

    private static void PrintBanner()
    {
        Console.WriteLine("================================");
        Console.WriteLine("         Codex .NET Demo        ");
        Console.WriteLine("================================");
    }

    private static void PrintConfig(CliOptions options)
    {
        Console.WriteLine($"Working directory : {options.WorkingDirectory}");
        Console.WriteLine($"Prompt            : {options.Prompt}");
        Console.WriteLine($"Sessions root     : {options.SessionsRoot}");
        Console.WriteLine($"Model             : {options.Model.Value}");
        Console.WriteLine($"Reasoning effort  : {options.Reasoning.Value}");
        Console.WriteLine($"Follow stream     : {options.FollowStream}");
        Console.WriteLine("Ctrl+C to cancel at any time.\n");
    }

    private static void PrintSessionInfo(CodexSessionInfo info)
    {
        Console.WriteLine("Session started:");
        Console.WriteLine($"  Session ID : {info.Id.Value}");
        Console.WriteLine($"  Log file   : {info.LogPath}");
        Console.WriteLine($"  Created at : {info.CreatedAt:O}");
    }

    private static void RenderEvent(CodexEvent evt)
    {
        var timestamp = evt.Timestamp.ToLocalTime().ToString("HH:mm:ss");

        switch (evt)
        {
            case UserMessageEvent user:
                Console.WriteLine($"[{timestamp}] user   > {user.Text}");
                break;
            case AgentMessageEvent agent:
                Console.WriteLine($"[{timestamp}] codex  > {agent.Text}");
                break;
            case AgentReasoningEvent reasoning:
                Console.WriteLine($"[{timestamp}] chain  > {reasoning.Text}");
                break;
            case TurnContextEvent context:
                Console.WriteLine($"[{timestamp}] ctx    > approval={context.ApprovalPolicy ?? "n/a"}, sandbox={context.SandboxPolicyType ?? "n/a"}");
                break;
            case ResponseItemEvent responseItem:
                RenderResponseItem(timestamp, responseItem);
                break;
            case TokenCountEvent tokens:
                var input = tokens.InputTokens?.ToString() ?? "n/a";
                var output = tokens.OutputTokens?.ToString() ?? "n/a";
                var reasoningTokens = tokens.ReasoningTokens?.ToString() ?? "n/a";
                Console.WriteLine($"[{timestamp}] tokens > in={input}, out={output}, reasoning={reasoningTokens}");
                break;
            
            default:
                Console.WriteLine($"[{timestamp}] {evt.Type,-6}");// > {SafeRaw(evt)}");
                break;
        }
    }

    private static void RenderResponseItem(string timestamp, ResponseItemEvent evt)
    {
        var payload = evt.Payload;
        if (payload.SummaryTexts is { Count: > 0 })
        {
            Console.WriteLine($"[{timestamp}] resp   > {string.Join(" | ", payload.SummaryTexts)}");
            return;
        }

        if (payload.MessageTextParts is { Count: > 0 })
        {
            Console.WriteLine($"[{timestamp}] resp-msg ({payload.MessageRole ?? "n/a"}) > {string.Join(" ", payload.MessageTextParts)}");
            return;
        }

        if (payload.FunctionCall is { } fn)
        {
            Console.WriteLine($"[{timestamp}] resp-fn  > {fn.Name ?? "?"}({fn.ArgumentsJson ?? ""}) callId={fn.CallId ?? "n/a"}");
            return;
        }

        Console.WriteLine($"[{timestamp}] resp[{payload.PayloadType}] > {payload.Raw.GetRawText()}");
    }

    private static string SafeRaw(CodexEvent evt)
    {
        try
        {
            return evt.RawPayload.GetRawText();
        }
        catch (Exception ex)
        {
            return $"<failed to render raw payload: {ex.Message}>";
        }
    }

    private static async Task PrintDebugInfo(CliOptions options)
    {
        Console.Error.WriteLine("\nDebug info:");
        Console.Error.WriteLine($"  Sessions root: {options.SessionsRoot}");
        if (Directory.Exists(options.SessionsRoot))
        {
            var files = Directory.GetFiles(options.SessionsRoot, "*.jsonl");
            Console.Error.WriteLine(files.Length == 0
                ? "  (no session files present)"
                : $"  Found {files.Length} session file(s). Latest:");

            foreach (var file in files.OrderByDescending(File.GetCreationTimeUtc).Take(3))
            {
                Console.Error.WriteLine($"    - {file}");
            }
        }
        else
        {
            Console.Error.WriteLine("  Sessions root directory does not exist.");
        }

        await Task.CompletedTask;
    }
}
