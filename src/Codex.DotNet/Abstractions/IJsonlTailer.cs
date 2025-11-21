using Codex.DotNet.Public;

namespace Codex.DotNet.Abstractions;

/// <summary>
/// Defines an abstraction for tailing JSONL (JSON Lines) files.
/// </summary>
/// <remarks>
/// This interface provides functionality similar to the Unix 'tail -f' command,
/// continuously reading new lines appended to a JSONL file. It's designed for
/// monitoring Codex session logs that are actively being written.
/// </remarks>
public interface IJsonlTailer
{
    /// <summary>
    /// Tails a JSONL file, yielding new lines as they are written to the file.
    /// </summary>
    /// <param name="filePath">The absolute path to the JSONL file to tail.</param>
    /// <param name="options">
    /// Configuration options controlling where to start reading from the file.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to monitor for cancellation requests. When canceled, the enumeration stops.
    /// </param>
    /// <returns>
    /// An async enumerable that yields each new line from the file as a string.
    /// Lines are yielded without the trailing newline character.
    /// When <see cref="EventStreamOptions.Follow"/> is false, the enumeration completes
    /// once the existing content has been read to the end of the file.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="filePath"/> or <paramref name="options"/> is null.
    /// </exception>
    /// <exception cref="FileNotFoundException">
    /// Thrown when the specified file does not exist.
    /// </exception>
    /// <exception cref="IOException">
    /// Thrown when an I/O error occurs while reading the file.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Thrown when the operation is canceled via <paramref name="cancellationToken"/>.
    /// </exception>
    /// <remarks>
    /// This method follows the file as it grows, similar to 'tail -f'. It handles:
    /// <list type="bullet">
    /// <item><description>Starting from the beginning, a specific timestamp, or a byte offset based on <paramref name="options"/></description></item>
    /// <item><description>Polling for new content when the end of file is reached</description></item>
    /// <item><description>Handling file rotation or truncation scenarios</description></item>
    /// <item><description>Gracefully stopping when cancellation is requested</description></item>
    /// </list>
    /// The enumeration continues until the file is no longer accessible, the process writing to it exits,
    /// or the operation is canceled. Empty lines are included in the output.
    /// </remarks>
    IAsyncEnumerable<string> TailAsync(
        string filePath,
        EventStreamOptions options,
        CancellationToken cancellationToken);
}
