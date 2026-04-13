namespace WinUiFileManager.Application.Abstractions;

public interface ITimeProvider
{
    DateTime UtcNow { get; }
}
