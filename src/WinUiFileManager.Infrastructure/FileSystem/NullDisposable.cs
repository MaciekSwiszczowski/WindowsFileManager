namespace WinUiFileManager.Infrastructure.FileSystem;

internal sealed class NullDisposable : IDisposable
{
    public static NullDisposable Instance { get; } = new();

    public void Dispose()
    {
    }
}
