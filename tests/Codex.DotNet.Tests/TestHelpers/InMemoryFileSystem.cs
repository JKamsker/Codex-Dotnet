using System.IO;
using System.Text;
using Codex.DotNet.Abstractions;

namespace Codex.DotNet.Tests.TestHelpers;

/// <summary>
/// In-memory implementation of IFileSystem for testing purposes.
/// </summary>
/// <remarks>
/// This implementation stores all files in memory using dictionaries,
/// with no actual disk I/O. Useful for unit testing components that depend on IFileSystem.
/// </remarks>
public class InMemoryFileSystem : IFileSystem
{
    private readonly Dictionary<string, byte[]> _files = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTime> _fileCreationTimes = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _directories = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the underlying file storage dictionary for advanced test scenarios.
    /// </summary>
    public IReadOnlyDictionary<string, byte[]> Files => _files;

    /// <summary>
    /// Gets the underlying directory storage for advanced test scenarios.
    /// </summary>
    public IReadOnlySet<string> Directories => _directories;

    /// <summary>
    /// Creates a new instance of InMemoryFileSystem.
    /// </summary>
    public InMemoryFileSystem()
    {
    }

    /// <summary>
    /// Adds a file to the in-memory file system.
    /// </summary>
    /// <param name="path">The absolute path of the file.</param>
    /// <param name="content">The file content as a string.</param>
    /// <param name="creationTimeUtc">Optional creation time (defaults to current UTC time).</param>
    public void AddFile(string path, string content, DateTime? creationTimeUtc = null)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(content);

        var bytes = Encoding.UTF8.GetBytes(content);
        AddFile(path, bytes, creationTimeUtc);
    }

    /// <summary>
    /// Adds a file to the in-memory file system.
    /// </summary>
    /// <param name="path">The absolute path of the file.</param>
    /// <param name="content">The file content as a byte array.</param>
    /// <param name="creationTimeUtc">Optional creation time (defaults to current UTC time).</param>
    public void AddFile(string path, byte[] content, DateTime? creationTimeUtc = null)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(content);

        _files[path] = content;
        _fileCreationTimes[path] = creationTimeUtc ?? DateTime.UtcNow;

        // Automatically create parent directories
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            AddDirectory(directory);
        }
    }

    /// <summary>
    /// Adds a directory to the in-memory file system.
    /// </summary>
    /// <param name="path">The absolute path of the directory.</param>
    public void AddDirectory(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        _directories.Add(path);

        // Recursively add parent directories
        var parent = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(parent) && !_directories.Contains(parent))
        {
            AddDirectory(parent);
        }
    }

    /// <summary>
    /// Removes a file from the in-memory file system.
    /// </summary>
    /// <param name="path">The absolute path of the file to remove.</param>
    /// <returns>True if the file was removed; false if it didn't exist.</returns>
    public bool RemoveFile(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        _fileCreationTimes.Remove(path);
        return _files.Remove(path);
    }

    /// <summary>
    /// Removes a directory from the in-memory file system.
    /// </summary>
    /// <param name="path">The absolute path of the directory to remove.</param>
    /// <returns>True if the directory was removed; false if it didn't exist.</returns>
    public bool RemoveDirectory(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        return _directories.Remove(path);
    }

    /// <summary>
    /// Clears all files and directories from the in-memory file system.
    /// </summary>
    public void Clear()
    {
        _files.Clear();
        _fileCreationTimes.Clear();
        _directories.Clear();
    }

    #region IFileSystem Implementation

    /// <inheritdoc />
    public bool FileExists(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        return _files.ContainsKey(path);
    }

    /// <inheritdoc />
    public bool DirectoryExists(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        return _directories.Contains(path);
    }

    /// <inheritdoc />
    public IEnumerable<string> GetFiles(string directory, string searchPattern)
    {
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(searchPattern);

        if (!DirectoryExists(directory))
            throw new DirectoryNotFoundException($"Directory not found: {directory}");

        // Convert search pattern to regex pattern
        var regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(searchPattern)
            .Replace("\\*", ".*")
            .Replace("\\?", ".") + "$";
        var regex = new System.Text.RegularExpressions.Regex(regexPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        // Find all files in the directory
        var normalizedDirectory = NormalizePath(directory);
        return _files.Keys
            .Where(filePath =>
            {
                var fileDirectory = Path.GetDirectoryName(filePath);
                if (string.IsNullOrEmpty(fileDirectory))
                    return false;

                var normalizedFileDirectory = NormalizePath(fileDirectory);
                if (!string.Equals(normalizedFileDirectory, normalizedDirectory, StringComparison.OrdinalIgnoreCase))
                    return false;

                var fileName = Path.GetFileName(filePath);
                return regex.IsMatch(fileName);
            })
            .ToList();
    }

    /// <inheritdoc />
    public Stream OpenRead(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        if (!FileExists(path))
            throw new FileNotFoundException($"File not found: {path}", path);

        var bytes = _files[path];
        return new MemoryStream(bytes, writable: false);
    }

    /// <inheritdoc />
    public DateTime GetFileCreationTimeUtc(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        if (!FileExists(path))
            throw new FileNotFoundException($"File not found: {path}", path);

        return _fileCreationTimes[path];
    }

    /// <inheritdoc />
    public long GetFileSize(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        if (!FileExists(path))
            throw new FileNotFoundException($"File not found: {path}", path);

        return _files[path].Length;
    }

    #endregion

    private static string NormalizePath(string path)
    {
        return path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}
