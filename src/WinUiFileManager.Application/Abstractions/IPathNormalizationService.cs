using WinUiFileManager.Domain.Errors;
using WinUiFileManager.Domain.ValueObjects;

namespace WinUiFileManager.Application.Abstractions;

public interface IPathNormalizationService
{
    NormalizedPath Normalize(string path);
    PathValidationResult Validate(string path);
}
