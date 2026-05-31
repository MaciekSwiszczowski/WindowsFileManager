using WinUiFileManager.Application.Abstractions;
using WinUiFileManager.Application.Validation;
using WinUiFileManager.Application.FileEntries;

namespace WinUiFileManager.Infrastructure.Services;

/// <summary>
/// Converts user-supplied path strings into the canonical <see cref="NormalizedPath"/> value object and validates
/// raw path syntax (non-empty, no invalid characters, fully qualified). Infrastructure implementation of
/// <see cref="IPathNormalizationService"/>. This is purely syntactic — it does not check existence or the NTFS
/// policy (that is <see cref="NtfsVolumePolicyService"/>'s job).
/// </summary>
internal sealed class WindowsPathNormalizationService : IPathNormalizationService
{
    /// <summary>Normalizes user/typed input to the canonical extended-length <see cref="NormalizedPath"/> form.</summary>
    public NormalizedPath Normalize(string path) => NormalizedPath.FromUserInput(path);

    /// <summary>Validates path syntax only (emptiness, illegal characters, fully-qualified form).</summary>
    /// <param name="path">The raw path string to check.</param>
    /// <returns>A <see cref="PathValidationResult"/> with a user-facing reason on failure.</returns>
    public PathValidationResult Validate(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return PathValidationResult.Invalid("Path cannot be empty.");
        }

        if (path.AsSpan().IndexOfAny(Path.GetInvalidPathChars()) >= 0)
        {
            return PathValidationResult.Invalid("Path contains invalid characters.");
        }

        // Relative paths are rejected: the app operates on absolute paths only (it has no per-pane "current dir").
        if (!Path.IsPathFullyQualified(path))
        {
            return PathValidationResult.Invalid("Path must be fully qualified.");
        }

        return PathValidationResult.Valid();
    }
}
