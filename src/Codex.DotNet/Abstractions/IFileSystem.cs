namespace Codex.DotNet.Abstractions;

/// <summary>
/// Defines an abstraction for file system operations.
/// </summary>
/// <remarks>
/// This interface provides a testable abstraction over file system operations,
/// allowing implementations to use the real file system or mock it for testing purposes.
/// All path parameters should be absolute paths unless otherwise specified.
/// </remarks>
public interface IFileSystem
{
    /// <summary>
    /// Determines whether the specified file exists.
    /// </summary>
    /// <param name="path">The absolute path to the file to check.</param>
    /// <returns>
    /// <c>true</c> if the file exists; otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    /// Returns <c>false</c> if the path is null, empty, or contains invalid characters.
    /// This method also returns <c>false</c> if the caller does not have permission to read the file.
    /// </remarks>
    bool FileExists(string path);

    /// <summary>
    /// Determines whether the specified directory exists.
    /// </summary>
    /// <param name="path">The absolute path to the directory to check.</param>
    /// <returns>
    /// <c>true</c> if the directory exists; otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    /// Returns <c>false</c> if the path is null, empty, or contains invalid characters.
    /// This method also returns <c>false</c> if the caller does not have permission to read the directory.
    /// </remarks>
    bool DirectoryExists(string path);

    /// <summary>
    /// Returns the names of files (including their paths) that match the specified search pattern
    /// in the specified directory.
    /// </summary>
    /// <param name="directory">The absolute path to the directory to search.</param>
    /// <param name="searchPattern">
    /// The search string to match against the names of files in the directory.
    /// This parameter can contain a combination of valid literal path and wildcard (* and ?) characters.
    /// </param>
    /// <returns>
    /// An enumerable collection of the full paths for the files in the directory that match
    /// the specified search pattern, or an empty enumerable if no files are found.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="directory"/> or <paramref name="searchPattern"/> is null.
    /// </exception>
    /// <exception cref="DirectoryNotFoundException">
    /// Thrown when the specified directory does not exist.
    /// </exception>
    /// <exception cref="IOException">
    /// Thrown when an I/O error occurs while accessing the directory.
    /// </exception>
    IEnumerable<string> GetFiles(string directory, string searchPattern);

    /// <summary>
    /// Opens an existing file for reading.
    /// </summary>
    /// <param name="path">The absolute path to the file to open for reading.</param>
    /// <returns>
    /// A read-only <see cref="Stream"/> on the specified path.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="path"/> is null.
    /// </exception>
    /// <exception cref="FileNotFoundException">
    /// Thrown when the specified file does not exist.
    /// </exception>
    /// <exception cref="IOException">
    /// Thrown when an I/O error occurs while opening the file.
    /// </exception>
    /// <exception cref="UnauthorizedAccessException">
    /// Thrown when the caller does not have permission to read the file.
    /// </exception>
    /// <remarks>
    /// The caller is responsible for disposing the returned stream.
    /// </remarks>
    Stream OpenRead(string path);

    /// <summary>
    /// Returns the creation date and time, in coordinated universal time (UTC), of the specified file.
    /// </summary>
    /// <param name="path">The absolute path to the file for which to obtain creation date and time information.</param>
    /// <returns>
    /// A <see cref="DateTime"/> structure set to the creation date and time for the specified file.
    /// This value is expressed in UTC time.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="path"/> is null.
    /// </exception>
    /// <exception cref="FileNotFoundException">
    /// Thrown when the specified file does not exist.
    /// </exception>
    /// <exception cref="IOException">
    /// Thrown when an I/O error occurs while accessing the file.
    /// </exception>
    /// <exception cref="UnauthorizedAccessException">
    /// Thrown when the caller does not have permission to access the file.
    /// </exception>
    DateTime GetFileCreationTimeUtc(string path);

    /// <summary>
    /// Gets the size, in bytes, of the specified file.
    /// </summary>
    /// <param name="path">The absolute path to the file.</param>
    /// <returns>
    /// The size, in bytes, of the file.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="path"/> is null.
    /// </exception>
    /// <exception cref="FileNotFoundException">
    /// Thrown when the specified file does not exist.
    /// </exception>
    /// <exception cref="IOException">
    /// Thrown when an I/O error occurs while accessing the file.
    /// </exception>
    /// <exception cref="UnauthorizedAccessException">
    /// Thrown when the caller does not have permission to access the file.
    /// </exception>
    long GetFileSize(string path);
}
