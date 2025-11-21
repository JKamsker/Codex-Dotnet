using Codex.DotNet.Public.Models;

namespace Codex.DotNet.Public;

/// <summary>
/// Represents filter criteria for querying Codex sessions.
/// </summary>
/// <remarks>
/// This record allows filtering sessions based on various criteria including date range,
/// working directory, model type, and session ID pattern matching. All filter properties
/// are optional and can be combined for refined queries.
/// </remarks>
/// <param name="FromDate">
/// Optional start date for filtering sessions. Only sessions created on or after this date are included.
/// </param>
/// <param name="ToDate">
/// Optional end date for filtering sessions. Only sessions created on or before this date are included.
/// </param>
/// <param name="WorkingDirectory">
/// Optional working directory path to filter sessions. Matches sessions that were started in this directory.
/// </param>
/// <param name="Model">
/// Optional model identifier to filter sessions. Matches sessions that used this specific model.
/// </param>
/// <param name="SessionIdPattern">
/// Optional session ID pattern for filtering. Supports wildcard matching or regex patterns
/// depending on implementation.
/// </param>
public sealed record SessionFilter(
    DateTimeOffset? FromDate = null,
    DateTimeOffset? ToDate = null,
    string? WorkingDirectory = null,
    CodexModel? Model = null,
    string? SessionIdPattern = null)
{
    /// <summary>
    /// Gets the optional start date for filtering sessions.
    /// </summary>
    public DateTimeOffset? FromDate { get; init; } = FromDate;

    private readonly DateTimeOffset? _toDate = ToDate;

    /// <summary>
    /// Gets the optional end date for filtering sessions.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// Thrown when ToDate is earlier than FromDate.
    /// </exception>
    public DateTimeOffset? ToDate
    {
        get => _toDate;
        init
        {
            if (value.HasValue && FromDate.HasValue && value.Value < FromDate.Value)
                throw new ArgumentException(
                    $"ToDate ({value.Value:O}) must be on or after FromDate ({FromDate.Value:O}).",
                    nameof(ToDate));

            _toDate = value;
        }
    }

    /// <summary>
    /// Gets the optional working directory path to filter sessions.
    /// </summary>
    public string? WorkingDirectory { get; init; } = WorkingDirectory;

    /// <summary>
    /// Gets the optional model identifier to filter sessions.
    /// </summary>
    public CodexModel? Model { get; init; } = Model;

    /// <summary>
    /// Gets the optional session ID pattern for filtering.
    /// </summary>
    public string? SessionIdPattern { get; init; } = SessionIdPattern;

    /// <summary>
    /// Creates an empty SessionFilter with no criteria applied.
    /// </summary>
    public static SessionFilter None => new();

    /// <summary>
    /// Creates a SessionFilter for a specific date range.
    /// </summary>
    /// <param name="fromDate">The start date (inclusive).</param>
    /// <param name="toDate">The end date (inclusive).</param>
    /// <returns>A SessionFilter instance configured with the specified date range.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when toDate is earlier than fromDate.
    /// </exception>
    public static SessionFilter ForDateRange(DateTimeOffset fromDate, DateTimeOffset toDate)
    {
        if (toDate < fromDate)
            throw new ArgumentException(
                $"toDate ({toDate:O}) must be on or after fromDate ({fromDate:O}).",
                nameof(toDate));

        return new(FromDate: fromDate, ToDate: toDate);
    }

    /// <summary>
    /// Creates a SessionFilter for a specific model.
    /// </summary>
    /// <param name="model">The model to filter by.</param>
    /// <returns>A SessionFilter instance configured to filter by the specified model.</returns>
    public static SessionFilter ForModel(CodexModel model) =>
        new(Model: model);

    /// <summary>
    /// Creates a SessionFilter for a specific working directory.
    /// </summary>
    /// <param name="workingDirectory">The working directory path to filter by.</param>
    /// <returns>A SessionFilter instance configured to filter by the specified working directory.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when workingDirectory is null.
    /// </exception>
    public static SessionFilter ForWorkingDirectory(string workingDirectory)
    {
        ArgumentNullException.ThrowIfNull(workingDirectory);
        return new(WorkingDirectory: workingDirectory);
    }

    /// <summary>
    /// Creates a SessionFilter for a specific session ID pattern.
    /// </summary>
    /// <param name="sessionIdPattern">The session ID pattern to filter by.</param>
    /// <returns>A SessionFilter instance configured to filter by the specified session ID pattern.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when sessionIdPattern is null.
    /// </exception>
    public static SessionFilter ForSessionIdPattern(string sessionIdPattern)
    {
        ArgumentNullException.ThrowIfNull(sessionIdPattern);
        return new(SessionIdPattern: sessionIdPattern);
    }
}
