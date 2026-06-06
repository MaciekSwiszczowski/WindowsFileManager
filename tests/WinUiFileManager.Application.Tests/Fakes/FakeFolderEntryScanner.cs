namespace WinUiFileManager.Application.Tests.Fakes;

public sealed class FakeFolderEntryScanner : IFolderListingScanner
{
    public IReadOnlyList<FileListingRow> Scan(NormalizedPath path, CancellationToken cancellationToken) => [];
}
