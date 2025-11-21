using Codex.DotNet.Abstractions;

namespace Codex.DotNet.Infrastructure;

/// <summary>
/// Real file system implementation using actual file I/O operations.
/// </summary>
/// <remarks>
/// This implementation provides access to the actual file system.
/// For testing, use a mock implementation of <see cref="IFileSystem"/>.
/// </remarks>
public sealed class RealFileSystem : IFileSystem
{
    /// <inheritdoc />
    public bool FileExists(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        try
        {
            return File.Exists(path);
        }
        catch (Exception)
        {
            // Handle potential security exceptions, path too long, etc.
            return false;
        }
    }

    /// <inheritdoc />
    public bool DirectoryExists(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        try
        {
            return Directory.Exists(path);
        }
        catch (Exception)
        {
            // Handle potential security exceptions, path too long, etc.
            return false;
        }
    }

    /// <inheritdoc />
    public IEnumerable<string> GetFiles(string directory, string searchPattern)
    {
        if (string.IsNullOrWhiteSpace(directory))
            throw new ArgumentNullException(nameof(directory), "Directory path cannot be null or whitespace.");

        if (string.IsNullOrWhiteSpace(searchPattern))
            throw new ArgumentNullException(nameof(searchPattern), "Search pattern cannot be null or whitespace.");

        try
        {
            return Directory.GetFiles(directory, searchPattern, SearchOption.AllDirectories);
        }
        catch (DirectoryNotFoundException ex)
        {
            throw new DirectoryNotFoundException($"The directory '{directory}' does not exist.", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new UnauthorizedAccessException($"Access to the directory '{directory}' is denied.", ex);
        }
        catch (Exception ex)
        {
            throw new IOException($"An error occurred while accessing directory '{directory}'.", ex);
        }
    }

    /// <inheritdoc />
    public Stream OpenRead(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path cannot be null or whitespace.", nameof(path));

        try
        {
            // Allow concurrent readers while Codex is writing to the same log file.
            return File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        }
        catch (FileNotFoundException ex)
        {
            throw new FileNotFoundException($"The file '{path}' does not exist.", path, ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new UnauthorizedAccessException($"Access to the file '{path}' is denied.", ex);
        }
        catch (Exception ex)
        {
            throw new IOException($"An error occurred while opening file '{path}'.", ex);
        }
    }

    /// <inheritdoc />
    public DateTime GetFileCreationTimeUtc(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentNullException(nameof(path), "Path cannot be null or whitespace.");

        try
        {
            return File.GetCreationTimeUtc(path);
        }
        catch (FileNotFoundException ex)
        {
            throw new FileNotFoundException($"The file '{path}' does not exist.", path, ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new UnauthorizedAccessException($"Access to the file '{path}' is denied.", ex);
        }
        catch (Exception ex)
        {
            throw new IOException($"An error occurred while getting creation time for '{path}'.", ex);
        }
    }

    /// <inheritdoc />
    public long GetFileSize(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentNullException(nameof(path), "Path cannot be null or whitespace.");

        try
        {
            var fileInfo = new FileInfo(path);
            return fileInfo.Length;
        }
        catch (FileNotFoundException ex)
        {
            throw new FileNotFoundException($"The file '{path}' does not exist.", path, ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new UnauthorizedAccessException($"Access to the file '{path}' is denied.", ex);
        }
        catch (Exception ex)
        {
            throw new IOException($"An error occurred while getting file size for '{path}'.", ex);
        }
    }
}
