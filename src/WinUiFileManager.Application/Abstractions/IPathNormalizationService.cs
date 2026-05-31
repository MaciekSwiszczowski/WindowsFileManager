using WinUiFileManager.Application.Validation;
using WinUiFileManager.Application.FileEntries;

namespace WinUiFileManager.Application.Abstractions;

/// <summary>
/// Converts raw path strings into <see cref="NormalizedPath"/> and validates them. Implemented in
/// Infrastructure; centralizes path parsing so views/handlers don't normalize ad hoc.
/// </summary>
public interface IPathNormalizationService
{
    /// <summary>Normalizes <paramref name="path"/> into the canonical <see cref="NormalizedPath"/> form.</summary>
    NormalizedPath Normalize(string path);

    /// <summary>Validates <paramref name="path"/>, returning a valid result or one with a user-facing error message.</summary>
    PathValidationResult Validate(string path);
}
