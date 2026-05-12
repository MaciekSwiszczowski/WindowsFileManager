using WinUiFileManager.Application.Validation;
using WinUiFileManager.Application.FileEntries;

namespace WinUiFileManager.Application.Abstractions;

public interface IPathNormalizationService
{
    NormalizedPath Normalize(string path);
    PathValidationResult Validate(string path);
}
