using WinUiFileManager.Domain.ValueObjects;

namespace WinUiFileManager.Application.Navigation;

public sealed record GoToPathResult
{
    public GoToPathResult(
        bool success,
        NormalizedPath? path,
        IReadOnlyList<FileSystemEntryModel>? entries,
        string? errorMessage)
    {
        Success = success;
        Path = path;
        Entries = entries;
        ErrorMessage = errorMessage;
    }

    public bool Success { get; init; }

    public NormalizedPath? Path { get; init; }

    public IReadOnlyList<FileSystemEntryModel>? Entries { get; init; }

    public string? ErrorMessage { get; init; }
}
