namespace Codex.DotNet.Public;

/// <summary>
/// Represents configuration options for reading Codex event streams.
/// </summary>
/// <remarks>
/// This record defines how an event stream should be read, including whether to start
/// from the beginning, a specific timestamp, or a byte offset within the log file.
/// Only one positioning option should be used at a time.
/// </remarks>
/// <param name="FromBeginning">
/// Indicates whether to read the event stream from the beginning.
/// Default is true. When true, the stream starts at the first available event.
/// </param>
/// <param name="AfterTimestamp">
/// Optional timestamp to start reading events after.
/// When specified, only events occurring after this timestamp are included.
/// </param>
/// <param name="FromByteOffset">
/// Optional byte offset within the log file to start reading from.
/// Must be non-negative if specified. Useful for resuming reading from a known position.
/// </param>
/// <param name="Follow">
/// Indicates whether the tailer should continue following the file for new content after
/// the current end-of-file is reached. For live sessions this should remain true; for
/// historical reads (resume/attach) set to false to complete once existing content is read.
/// </param>
public sealed record EventStreamOptions(
    bool FromBeginning = true,
    DateTimeOffset? AfterTimestamp = null,
    long? FromByteOffset = null,
    bool Follow = true)
{
    /// <summary>
    /// Gets a value indicating whether to read the event stream from the beginning.
    /// </summary>
    public bool FromBeginning { get; init; } = FromBeginning;

    /// <summary>
    /// Gets the optional timestamp to start reading events after.
    /// </summary>
    public DateTimeOffset? AfterTimestamp { get; init; } = AfterTimestamp;

    private readonly long? _fromByteOffset = FromByteOffset;

    /// <summary>
    /// Gets the optional byte offset within the log file to start reading from.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the value is negative.
    /// </exception>
    public long? FromByteOffset
    {
        get => _fromByteOffset;
        init
        {
            if (value.HasValue && value.Value < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(FromByteOffset),
                    value,
                    "Byte offset must be non-negative.");

            _fromByteOffset = value;
        }
    }

    /// <summary>
    /// Gets a value indicating whether the tailer should continue following the file for
    /// new data after reaching the current end-of-file position.
    /// </summary>
    public bool Follow { get; init; } = Follow;

    /// <summary>
    /// Creates default EventStreamOptions that reads from the beginning.
    /// </summary>
    public static EventStreamOptions Default => new();

    /// <summary>
    /// Creates EventStreamOptions that starts reading after a specific timestamp.
    /// </summary>
    /// <param name="timestamp">The timestamp after which to read events.</param>
    /// <param name="follow">
    /// Whether to continue following the log after reading existing content. Defaults to true.
    /// </param>
    /// <returns>An EventStreamOptions instance configured to read after the specified timestamp.</returns>
    public static EventStreamOptions FromTimestamp(DateTimeOffset timestamp, bool follow = true) =>
        new(FromBeginning: false, AfterTimestamp: timestamp, Follow: follow);

    /// <summary>
    /// Creates EventStreamOptions that starts reading from a specific byte offset.
    /// </summary>
    /// <param name="byteOffset">The byte offset to start reading from. Must be non-negative.</param>
    /// <param name="follow">
    /// Whether to continue following the log after reading existing content. Defaults to true.
    /// </param>
    /// <returns>An EventStreamOptions instance configured to read from the specified byte offset.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when byteOffset is negative.
    /// </exception>
    public static EventStreamOptions FromOffset(long byteOffset, bool follow = true)
    {
        if (byteOffset < 0)
            throw new ArgumentOutOfRangeException(
                nameof(byteOffset),
                byteOffset,
                "Byte offset must be non-negative.");

        return new(FromBeginning: false, FromByteOffset: byteOffset, Follow: follow);
    }
}
