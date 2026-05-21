using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData;
using DynamicData.Binding;
using WinUiFileManager.Application.Messages.RequestMessages.Navigation;
using WinUiFileManager.Presentation.FileEntryTable;

namespace WinUiFileManager.Presentation.FileEntryTableData;

internal sealed class FileEntryTableDataSource : IDisposable
{
    private const string ParentEntryKey = "\0..";

    private readonly CompositeDisposable _disposables = new();
    private readonly SourceList<SpecFileEntryViewModel> _rows = new();
    private readonly IFileEntryDataReader _fileEntryDataReader;
    private readonly IDirectoryChangeStream _directoryChangeStream;
    private readonly IMessenger _messenger;
    private readonly IScheduler _backgroundScheduler;
    private bool _disposed;

    public FileEntryTableDataSource(
        string identity,
        NormalizedPath folderPath,
        IScheduler uiScheduler,
        IFileEntryDataReader fileEntryDataReader,
        IDirectoryChangeStream directoryChangeStream,
        IMessenger messenger,
        IScheduler? backgroundScheduler = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identity);
        ArgumentNullException.ThrowIfNull(uiScheduler);
        ArgumentNullException.ThrowIfNull(fileEntryDataReader);
        ArgumentNullException.ThrowIfNull(directoryChangeStream);
        ArgumentNullException.ThrowIfNull(messenger);

        Identity = identity;
        FolderPath = folderPath;
        CurrentPath = folderPath.DisplayPath;
        Items = new ObservableCollectionExtended<SpecFileEntryViewModel>();
        _fileEntryDataReader = fileEntryDataReader;
        _directoryChangeStream = directoryChangeStream;
        _messenger = messenger;
        _backgroundScheduler = backgroundScheduler ?? TaskPoolScheduler.Default;

        _disposables.Add(_rows);
        _disposables.Add(_rows
            .Connect()
            .ObserveOn(uiScheduler)
            .Bind(Items)
            .Subscribe(static _ => { }, OnFolderStreamError));
        _disposables.Add(CreateFolderLifetime()
            .ObserveOn(uiScheduler)
            .Subscribe(ApplyChange, OnFolderStreamError));
    }

    private string Identity { get; }

    private NormalizedPath FolderPath { get; }

    public string CurrentPath { get; }

    public ObservableCollectionExtended<SpecFileEntryViewModel> Items { get; }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _disposables.Dispose();
    }

    private IObservable<FileEntryTableChange> CreateFolderLifetime()
    {
        var initialRows = Observable
            .Start(ScanCurrentFolder, _backgroundScheduler)
            .SelectMany(static rows => rows.ToObservable())
            .Select(static row => new FileEntryTableChange.AddOrUpdate(row));

        var fileNotifications = _directoryChangeStream
            .Watch(FolderPath)
            .SelectMany(CreateFileEntryChanges);

        return initialRows.Concat(fileNotifications);
    }

    private IReadOnlyList<SpecFileEntryViewModel> ScanCurrentFolder()
    {
        var rows = new List<SpecFileEntryViewModel>();

        if (Directory.GetParent(CurrentPath) is not null)
        {
            rows.Add(SpecFileEntryViewModel.CreateParentEntry());
        }

        foreach (var entry in _fileEntryDataReader.GetEntries(FolderPath, CancellationToken.None))
        {
            rows.Add(new SpecFileEntryViewModel(entry));
        }

        return rows;
    }

    private IObservable<FileEntryTableChange> CreateFileEntryChanges(DirectoryChange change) =>
        change.Kind switch
        {
            DirectoryChangeKind.Created => ReadAddOrRemoveChange(
                change.Path,
                snapshotOldPath: null,
                createSnapshotBeforeUpdate: false),
            DirectoryChangeKind.Changed => ReadAddOrRemoveChange(
                change.Path,
                snapshotOldPath: change.Path,
                createSnapshotBeforeUpdate: true),
            DirectoryChangeKind.Deleted => Observable.Return(
                new FileEntryTableChange.Remove(change.Path.Value)),
            DirectoryChangeKind.Renamed => CreateRenamedChanges(change),
            DirectoryChangeKind.Invalidated => CreateInvalidatedChange(),
            _ => Observable.Empty<FileEntryTableChange>(),
        };

    private IObservable<FileEntryTableChange> ReadAddOrRemoveChange(
        NormalizedPath path,
        NormalizedPath? snapshotOldPath,
        bool createSnapshotBeforeUpdate) =>
        Observable
            .Start(() => TryGetEntry(path), _backgroundScheduler)
            .SelectMany(model => model is null
                ? CreateMissingEntryChanges(path, snapshotOldPath)
                : CreateAddOrUpdateChanges(model, snapshotOldPath, createSnapshotBeforeUpdate));

    private IObservable<FileEntryTableChange> CreateRenamedChanges(DirectoryChange change)
    {
        if (change.OldPath is not { } oldPath)
        {
            return ReadAddOrRemoveChange(
                change.Path,
                snapshotOldPath: null,
                createSnapshotBeforeUpdate: false);
        }

        return Observable
            .Start(() => TryGetEntry(change.Path), _backgroundScheduler)
            .SelectMany(model =>
            {
                var changes = new List<FileEntryTableChange>
                {
                    new FileEntryTableChange.CreateSelectionSnapshot(),
                    new FileEntryTableChange.Remove(oldPath.Value)
                };

                if (model is not null)
                {
                    changes.Add(new FileEntryTableChange.AddOrUpdate(new SpecFileEntryViewModel(model)));
                    changes.Add(new FileEntryTableChange.ApplySelectionSnapshot(oldPath, model.FullPath));
                }
                else
                {
                    changes.Add(new FileEntryTableChange.ApplySelectionSnapshot(OldPath: null, NewPath: null));
                }

                return changes.ToObservable();
            });
    }

    private IObservable<FileEntryTableChange> CreateInvalidatedChange()
    {
        var nearestDirectory = FindNearestExistingDirectory();

        return nearestDirectory is { } path
            ? Observable.Return(new FileEntryTableChange.NavigateTo(path))
            : Observable.Empty<FileEntryTableChange>();
    }

    private IObservable<FileEntryTableChange> CreateAddOrUpdateChanges(
        FileSystemEntryModel model,
        NormalizedPath? snapshotOldPath,
        bool createSnapshotBeforeUpdate)
    {
        var item = new SpecFileEntryViewModel(model);

        if (snapshotOldPath is not { } oldPath)
        {
            return Observable.Return(new FileEntryTableChange.AddOrUpdate(item));
        }

        var changes = new List<FileEntryTableChange>();
        if (createSnapshotBeforeUpdate)
        {
            changes.Add(new FileEntryTableChange.CreateSelectionSnapshot());
        }

        changes.Add(new FileEntryTableChange.AddOrUpdate(item));
        changes.Add(new FileEntryTableChange.ApplySelectionSnapshot(oldPath, model.FullPath));

        return changes.ToObservable();
    }

    private IObservable<FileEntryTableChange> CreateMissingEntryChanges(
        NormalizedPath path,
        NormalizedPath? snapshotOldPath)
    {
        var changes = new List<FileEntryTableChange>
        {
            new FileEntryTableChange.Remove(path.Value)
        };

        if (snapshotOldPath is not null)
        {
            changes.Add(new FileEntryTableChange.ApplySelectionSnapshot(OldPath: null, NewPath: null));
        }

        return changes.ToObservable();
    }

    private FileSystemEntryModel? TryGetEntry(NormalizedPath path)
    {
        try
        {
            return _fileEntryDataReader.GetEntry(path, CancellationToken.None);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (Exception ex)
        {
            OnFolderStreamError(ex);
            return null;
        }
    }

    private NormalizedPath? FindNearestExistingDirectory()
    {
        var candidate = new DirectoryInfo(CurrentPath);
        while (candidate is not null && !candidate.Exists)
        {
            candidate = candidate.Parent;
        }

        if (candidate is null)
        {
            return null;
        }

        return NormalizedPath.FromUserInput(candidate.FullName);
    }

    private void ApplyChange(FileEntryTableChange change)
    {
        if (_disposed)
        {
            return;
        }

        switch (change)
        {
            case FileEntryTableChange.AddOrUpdate addOrUpdate:
                AddOrReplace(addOrUpdate.Item);
                break;
            case FileEntryTableChange.Remove remove:
                RemoveByKey(remove.Key);
                break;
            case FileEntryTableChange.CreateSelectionSnapshot:
                _messenger.Send(new FileTableCreateSelectionSnapshotsRequestedMessage(FolderPath));
                break;
            case FileEntryTableChange.ApplySelectionSnapshot apply:
                _messenger.Send(new FileTableApplySelectionSnapshotsRequestedMessage(
                    FolderPath,
                    apply.OldPath,
                    apply.NewPath));
                break;
            case FileEntryTableChange.NavigateTo navigateTo:
                _messenger.Send(new FileTableNavigateToPathRequestedMessage(Identity, navigateTo.Path));
                break;
        }
    }

    private void OnFolderStreamError(Exception error) => System.Diagnostics.Debug.WriteLine(error);

    private void AddOrReplace(SpecFileEntryViewModel item)
    {
        _rows.Edit(rows =>
        {
            RemoveByKey(rows, GetKey(item));
            rows.Add(item);
        });
    }

    private void RemoveByKey(string key)
    {
        _rows.Edit(rows => RemoveByKey(rows, key));
    }

    private static void RemoveByKey(IList<SpecFileEntryViewModel> rows, string key)
    {
        for (var index = rows.Count - 1; index >= 0; index--)
        {
            if (StringComparer.OrdinalIgnoreCase.Equals(GetKey(rows[index]), key))
            {
                rows.RemoveAt(index);
            }
        }
    }

    private static string GetKey(SpecFileEntryViewModel item)
    {
        return item.Model?.FullPath.Value ?? ParentEntryKey;
    }

    private abstract record FileEntryTableChange
    {
        public sealed record AddOrUpdate(SpecFileEntryViewModel Item) : FileEntryTableChange;

        public sealed record Remove(string Key) : FileEntryTableChange;

        public sealed record CreateSelectionSnapshot : FileEntryTableChange;

        public sealed record ApplySelectionSnapshot(
            NormalizedPath? OldPath,
            NormalizedPath? NewPath) : FileEntryTableChange;

        public sealed record NavigateTo(NormalizedPath Path) : FileEntryTableChange;
    }
}
