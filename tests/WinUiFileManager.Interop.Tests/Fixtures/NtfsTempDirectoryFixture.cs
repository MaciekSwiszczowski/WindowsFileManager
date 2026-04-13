namespace WinUiFileManager.Interop.Tests.Fixtures;

public sealed class NtfsTempDirectoryFixture : IDisposable
{
    public NtfsTempDirectoryFixture()
    {
        RootPath = Path.Combine(Path.GetTempPath(), "WinUiFileManager_Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(RootPath);
    }

    public string RootPath { get; }

    public string CreateFile(string relativePath, long sizeInBytes = 0)
    {
        var fullPath = Path.Combine(RootPath, relativePath);
        var dir = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(dir);
        using var fs = File.Create(fullPath);
        if (sizeInBytes > 0)
        {
            fs.SetLength(sizeInBytes);
        }
        return fullPath;
    }

    public string CreateDirectory(string relativePath)
    {
        var fullPath = Path.Combine(RootPath, relativePath);
        Directory.CreateDirectory(fullPath);
        return fullPath;
    }

    public void Dispose()
    {
        try { Directory.Delete(RootPath, true); } catch { }
    }
}
