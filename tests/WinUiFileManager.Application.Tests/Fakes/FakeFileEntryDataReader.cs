using System.Reactive.Linq;

namespace WinUiFileManager.Application.Tests.Fakes;

public sealed class FakeFileEntryDataReader : IFileEntryDataReader
{
    public IObservable<FileSystemEntryModel> GetEntries(NormalizedPath path, CancellationToken cancellationToken) => Observable.Empty<FileSystemEntryModel>();

    public FileSystemEntryModel? GetEntry(NormalizedPath path, CancellationToken cancellationToken) => null;
}
