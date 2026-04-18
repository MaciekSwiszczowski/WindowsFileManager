using WinUiFileManager.Application.Abstractions;
using WinUiFileManager.Domain.Errors;
using WinUiFileManager.Domain.ValueObjects;

namespace WinUiFileManager.Infrastructure.Services;

public sealed class WindowsPathNormalizationService : IPathNormalizationService
{
    public NormalizedPath Normalize(string path) => NormalizedPath.FromUserInput(path);

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

        if (!Path.IsPathFullyQualified(path))
        {
            return PathValidationResult.Invalid("Path must be fully qualified.");
        }

        return PathValidationResult.Valid();
    }
}
