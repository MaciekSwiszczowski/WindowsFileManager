namespace WinUiFileManager.Application.Tests.Fakes;

public sealed class FakeFileListingRowReader : IFileListingRowReader
{
    public FileListingRow? TryRead(NormalizedPath path, CancellationToken cancellationToken) => null;
}
