using System;
using System.Diagnostics;
using System.Linq;
using Codex.DotNet.Public;
using Codex.DotNet.Public.Models;

namespace Codex.DotNet.Infrastructure;

/// <summary>
/// Builds <see cref="ProcessStartInfo"/> instances for launching the Codex CLI.
/// </summary>
/// <remarks>
/// Encapsulates argument ordering and formatting so it can be unit tested without
/// spinning up real processes.
/// </remarks>
internal static class ProcessStartInfoBuilder
{
    /// <summary>
    /// Creates a configured <see cref="ProcessStartInfo"/> for <c>codex exec</c>.
    /// </summary>
    /// <param name="executablePath">Resolved Codex executable path.</param>
    /// <param name="options">Validated session options.</param>
    /// <returns>Populated <see cref="ProcessStartInfo"/> ready for launch.</returns>
    public static ProcessStartInfo Create(string executablePath, CodexSessionOptions options)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            throw new ArgumentException("Executable path cannot be null or whitespace.", nameof(executablePath));
        }

        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            WorkingDirectory = options.WorkingDirectory,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        startInfo.ArgumentList.Add("exec");
        startInfo.ArgumentList.Add("--cd");
        startInfo.ArgumentList.Add(options.WorkingDirectory);
        startInfo.ArgumentList.Add("--model");
        startInfo.ArgumentList.Add(options.Model.Value);
        startInfo.ArgumentList.Add("--config");
        startInfo.ArgumentList.Add($"model_reasoning_effort={options.ReasoningEffort.Value}");

        foreach (var option in options.AdditionalOptions)
        {
            startInfo.ArgumentList.Add(option);
        }

        startInfo.ArgumentList.Add("-");

        return startInfo;
    }

    /// <summary>
    /// Creates a configured <see cref="ProcessStartInfo"/> for <c>codex exec resume</c>.
    /// </summary>
    /// <param name="executablePath">Resolved Codex executable path.</param>
    /// <param name="sessionId">Session identifier to resume.</param>
    /// <param name="options">Validated session options.</param>
    /// <returns>Populated <see cref="ProcessStartInfo"/> ready for launch.</returns>
    public static ProcessStartInfo CreateResume(string executablePath, SessionId sessionId, CodexSessionOptions options)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            throw new ArgumentException("Executable path cannot be null or whitespace.", nameof(executablePath));
        }

        ArgumentNullException.ThrowIfNull(sessionId);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            WorkingDirectory = options.WorkingDirectory,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        startInfo.ArgumentList.Add("exec");
        startInfo.ArgumentList.Add("--cd");
        startInfo.ArgumentList.Add(options.WorkingDirectory);
        startInfo.ArgumentList.Add("--model");
        startInfo.ArgumentList.Add(options.Model.Value);
        startInfo.ArgumentList.Add("--config");
        startInfo.ArgumentList.Add($"model_reasoning_effort={options.ReasoningEffort.Value}");

        foreach (var option in options.AdditionalOptions)
        {
            startInfo.ArgumentList.Add(option);
        }

        startInfo.ArgumentList.Add("resume");
        startInfo.ArgumentList.Add(sessionId.Value);
        startInfo.ArgumentList.Add("-");

        return startInfo;
    }

    /// <summary>
    /// Formats the arguments for diagnostic logging.
    /// </summary>
    public static string FormatArguments(ProcessStartInfo startInfo)
    {
        if (startInfo.ArgumentList.Count == 0)
        {
            return startInfo.Arguments;
        }

        return string.Join(" ", startInfo.ArgumentList.Select(QuoteForLogging));
    }

    private static string QuoteForLogging(string value) =>
        value.Any(char.IsWhiteSpace) || value.Contains('"')
            ? $"\"{value.Replace("\"", "\\\"")}\""
            : value;
}
