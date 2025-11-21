using Codex.DotNet.Abstractions;
using Codex.DotNet.Infrastructure;
using Codex.DotNet.Public;
using Codex.DotNet.Public.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.IO;
using Xunit;

namespace Codex.DotNet.Tests.Unit;

public class ProcessStartInfoBuilderTests
{
    [Fact]
    public void CreateProcessStartInfo_BuildsExpectedArguments_WithAdditionalOptions()
    {
        var workingDirectory = CreateTempDirectory();
        try
        {
            var options = new CodexSessionOptions(workingDirectory, "prompt")
            {
                Model = CodexModel.Gpt51CodexMini,
                ReasoningEffort = CodexReasoningEffort.High,
                AdditionalOptions = new[] { "--cheap-model", "--flag" }
            };
            var clientOptions = new CodexClientOptions();
            var pathProvider = new RecordingPathProvider("codex-default");
            var launcher = new CodexProcessLauncher(pathProvider, NullLogger<CodexProcessLauncher>.Instance);

            var startInfo = launcher.CreateProcessStartInfo(options, clientOptions);

            startInfo.FileName.Should().Be("codex-default");
            startInfo.WorkingDirectory.Should().Be(workingDirectory);
            startInfo.ArgumentList.Should().Equal(
                "exec",
                "--cd",
                workingDirectory,
                "--model",
                "gpt-5.1-codex-mini",
                "--config",
                "model_reasoning_effort=high",
                "--cheap-model",
                "--flag",
                "-");
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public void CreateResumeStartInfo_BuildsExpectedArguments()
    {
        var workingDirectory = CreateTempDirectory();
        try
        {
            var options = new CodexSessionOptions(workingDirectory, "follow-up")
            {
                Model = CodexModel.Gpt51Codex,
                ReasoningEffort = CodexReasoningEffort.Medium
            };
            var sessionId = SessionId.Parse("session-abc");
            var clientOptions = new CodexClientOptions();
            var pathProvider = new RecordingPathProvider("codex-default");
            var launcher = new CodexProcessLauncher(pathProvider, NullLogger<CodexProcessLauncher>.Instance);

            var startInfo = launcher.CreateResumeStartInfo(sessionId, options, clientOptions);

            startInfo.FileName.Should().Be("codex-default");
            startInfo.WorkingDirectory.Should().Be(workingDirectory);
            startInfo.ArgumentList.Should().Equal(
                "exec",
                "--cd",
                workingDirectory,
                "--model",
                "gpt-5.1-codex",
                "--config",
                "model_reasoning_effort=medium",
                "resume",
                "session-abc",
                "-");
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public void CreateProcessStartInfo_PrefersSessionBinaryOverride_OverClientOption()
    {
        var workingDirectory = CreateTempDirectory();
        var sessionBinary = Path.Combine(workingDirectory, "codex-session.exe");
        var clientBinary = Path.Combine(workingDirectory, "codex-client.exe");
        File.WriteAllText(sessionBinary, string.Empty);
        File.WriteAllText(clientBinary, string.Empty);

        try
        {
            var options = new CodexSessionOptions(workingDirectory, "prompt")
            {
                CodexBinaryPath = sessionBinary
            };

            var clientOptions = new CodexClientOptions
            {
                CodexExecutablePath = clientBinary
            };

            var pathProvider = new RecordingPathProvider("codex-default");
            var launcher = new CodexProcessLauncher(pathProvider, NullLogger<CodexProcessLauncher>.Instance);

            var startInfo = launcher.CreateProcessStartInfo(options, clientOptions);

            startInfo.FileName.Should().Be(sessionBinary);
            pathProvider.LastOverride.Should().Be(sessionBinary);
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public void CreateProcessStartInfo_UsesPlatformDefaultExecutable_WhenNoOverrides()
    {
        var workingDirectory = CreateTempDirectory();
        try
        {
            var options = new CodexSessionOptions(workingDirectory, "prompt");
            var clientOptions = new CodexClientOptions();
            var expectedDefault = OperatingSystem.IsWindows() ? "codex.cmd" : "codex";
            var pathProvider = new RecordingPathProvider(expectedDefault);
            var launcher = new CodexProcessLauncher(pathProvider, NullLogger<CodexProcessLauncher>.Instance);

            var startInfo = launcher.CreateProcessStartInfo(options, clientOptions);

            startInfo.FileName.Should().Be(expectedDefault);
            pathProvider.LastOverride.Should().BeNull();
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"codex-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class RecordingPathProvider : ICodexPathProvider
    {
        private readonly string _defaultPath;

        public RecordingPathProvider(string defaultPath)
        {
            _defaultPath = defaultPath;
        }

        public string? LastOverride { get; private set; }

        public string GetCodexExecutablePath(string? overridePath)
        {
            LastOverride = overridePath;
            return overridePath ?? _defaultPath;
        }

        public string GetSessionsRootDirectory(string? overrideDirectory) =>
            throw new NotImplementedException();

        public string ResolveSessionLogPath(SessionId sessionId, string? sessionsRoot) =>
            throw new NotImplementedException();
    }
}
