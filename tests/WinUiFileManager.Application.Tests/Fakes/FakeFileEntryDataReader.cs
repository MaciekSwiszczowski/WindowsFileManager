namespace WinUiFileManager.Application.Tests.Fakes;

public sealed class FakeFileEntryDataReader : IFileEntryDataReader
{
    public IReadOnlyList<FileSystemEntryModel> Entries { get; init; } = [];

    public IReadOnlyList<FileSystemEntryModel> GetEntries(
        NormalizedPath path,
        CancellationToken cancellationToken) =>
        Entries;

    public FileSystemEntryModel? GetEntry(
        NormalizedPath path,
        CancellationToken cancellationToken) =>
        null;
}
