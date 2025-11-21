using System;
using System.IO;
using NCodexSDK.Public.Models;

namespace NCodexSDK.Public;

/// <summary>
/// Represents configuration options for starting a new Codex session.
/// </summary>
/// <remarks>
/// This class encapsulates all the settings required to start a new Codex CLI session,
/// including the initial prompt, working directory, model selection, and reasoning effort level.
/// Required fields must be set before starting a session.
/// </remarks>
public class CodexSessionOptions
{
    private string? _workingDirectory;
    private string? _prompt;
    private CodexModel _model = CodexModel.Default;
    private CodexReasoningEffort _reasoningEffort = CodexReasoningEffort.Medium;
    private IReadOnlyList<string> _additionalOptions = Array.Empty<string>();

    /// <summary>
    /// Gets or sets the working directory where the Codex session will run.
    /// </summary>
    /// <remarks>
    /// This is the directory context in which the Codex CLI will execute.
    /// It affects file operations and relative path resolution during the session.
    /// This property is required and must be set to a valid directory path.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// Thrown when attempting to set a null value.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when attempting to set an empty or whitespace value.
    /// </exception>
    public string WorkingDirectory
    {
        get => _workingDirectory ?? throw new InvalidOperationException(
            "WorkingDirectory is required but has not been set.");
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException(
                    "Working directory cannot be empty or whitespace.",
                    nameof(WorkingDirectory));

            _workingDirectory = value;
        }
    }

    /// <summary>
    /// Gets or sets the initial prompt to send to the Codex session.
    /// </summary>
    /// <remarks>
    /// This is the user's instruction or query that Codex will process.
    /// This property is required and must be set to a non-empty value.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// Thrown when attempting to set a null value.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when attempting to set an empty or whitespace value.
    /// </exception>
    public string Prompt
    {
        get => _prompt ?? throw new InvalidOperationException(
            "Prompt is required but has not been set.");
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException(
                    "Prompt cannot be empty or whitespace.",
                    nameof(Prompt));

            _prompt = value;
        }
    }

    /// <summary>
    /// Gets or sets the Codex model to use for this session.
    /// </summary>
    /// <remarks>
    /// Default is CodexModel.Default. This determines which model version
    /// will process the session requests.
    /// </remarks>
    public CodexModel Model
    {
        get => _model;
        set => _model = value;
    }

    /// <summary>
    /// Gets or sets the reasoning effort level for this session.
    /// </summary>
    /// <remarks>
    /// Default is CodexReasoningEffort.Medium. This controls the depth and
    /// thoroughness of the model's reasoning process. Higher effort levels
    /// may provide more comprehensive responses but take longer to process.
    /// </remarks>
    public CodexReasoningEffort ReasoningEffort
    {
        get => _reasoningEffort;
        set => _reasoningEffort = value;
    }

    /// <summary>
    /// Gets or sets additional command-line options to pass to the Codex CLI.
    /// </summary>
    /// <remarks>
    /// Default is an empty list. These options are passed directly to the Codex CLI
    /// process and can be used to enable experimental features or provide additional
    /// configuration not covered by the standard properties.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// Thrown when attempting to set a null value.
    /// </exception>
    public IReadOnlyList<string> AdditionalOptions
    {
        get => _additionalOptions;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _additionalOptions = value;
        }
    }

    /// <summary>
    /// Gets or sets the path to the Codex CLI executable for this specific session.
    /// </summary>
    /// <remarks>
    /// When null, the executable path from CodexClientOptions or system PATH will be used.
    /// This property allows overriding the executable path on a per-session basis,
    /// which can be useful for testing or using different Codex versions.
    /// </remarks>
    public string? CodexBinaryPath { get; set; }

    /// <summary>
    /// Creates a new instance of CodexSessionOptions.
    /// </summary>
    /// <remarks>
    /// The WorkingDirectory and Prompt properties must be set before using this instance
    /// to start a session.
    /// </remarks>
    public CodexSessionOptions()
    {
    }

    /// <summary>
    /// Creates a new instance of CodexSessionOptions with required fields.
    /// </summary>
    /// <param name="workingDirectory">The working directory for the session.</param>
    /// <param name="prompt">The initial prompt for the session.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when workingDirectory or prompt is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when workingDirectory or prompt is empty or whitespace.
    /// </exception>
    public CodexSessionOptions(string workingDirectory, string prompt)
    {
        WorkingDirectory = workingDirectory;
        Prompt = prompt;
    }

    /// <summary>
    /// Validates that all required options are set and valid.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when required fields are not set or validation fails.
    /// </exception>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(_workingDirectory))
            throw new InvalidOperationException("WorkingDirectory is required and cannot be empty.");

        if (!Directory.Exists(_workingDirectory))
            throw new InvalidOperationException($"WorkingDirectory '{_workingDirectory}' does not exist.");

        if (string.IsNullOrWhiteSpace(_prompt))
            throw new InvalidOperationException("Prompt is required and cannot be empty.");

        if (string.IsNullOrWhiteSpace(Model.Value))
            throw new InvalidOperationException("Model is required and cannot be empty.");

        if (string.IsNullOrWhiteSpace(ReasoningEffort.Value))
            throw new InvalidOperationException("ReasoningEffort is required and cannot be empty.");

        if (string.Equals(ReasoningEffort.Value, CodexReasoningEffort.XHigh.Value, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(Model.Value, CodexModel.Gpt51CodexMax.Value, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "ReasoningEffort 'xhigh' is only supported with model 'gpt-5.1-codex-max'.");
        }
    }

    /// <summary>
    /// Creates a copy of the current options.
    /// </summary>
    /// <returns>A new CodexSessionOptions instance with the same values.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when required fields are not set.
    /// </exception>
    public CodexSessionOptions Clone()
    {
        Validate();

        return new CodexSessionOptions(WorkingDirectory, Prompt)
        {
            Model = Model,
            ReasoningEffort = ReasoningEffort,
            AdditionalOptions = new List<string>(AdditionalOptions),
            CodexBinaryPath = CodexBinaryPath
        };
    }
}
