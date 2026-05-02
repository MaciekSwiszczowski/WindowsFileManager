using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using CommunityToolkit.Mvvm.Messaging;
using DynamicData;
using DynamicData.Binding;
using WinUiFileManager.Domain.Enums;
using WinUiFileManager.Domain.Events;
using WinUiFileManager.Domain.ValueObjects;
using WinUiFileManager.Presentation.FileEntryTable;
using WinUiFileManager.Presentation.FileEntryTable.Messages;

namespace WinUiFileManager.TestApp;

internal sealed class FileEntryTableDataSource : IDisposable
{
    private const int EntryBatchSize = 512;
    private const string ParentEntryKey = "\0..";

    private readonly SourceCache<SpecFileEntryViewModel, string> _source =
        new(static item => item.Model?.FullPath.Value ?? ParentEntryKey);
    private readonly CompositeDisposable _disposables = new();
    private readonly SerialDisposable _folderSubscription = new();
    private readonly IMessenger _messenger;
    private readonly IScheduler _backgroundScheduler;
    private readonly IScheduler _uiScheduler;
    private WindowsFolderEntryStream? _entryStream;
    private int _loadVersion;
    private bool _disposed;

    public FileEntryTableDataSource(
        string identity,
        string initialPath,
        IScheduler uiScheduler,
        IMessenger? messenger = null,
        IScheduler? backgroundScheduler = null)
    {
        ArgumentNullException.ThrowIfNull(uiScheduler);

        Identity = identity;
        Items = new ObservableCollectionExtended<SpecFileEntryViewModel>();
        _uiScheduler = uiScheduler;
        _messenger = messenger ?? WeakReferenceMessenger.Default;
        _backgroundScheduler = backgroundScheduler ?? TaskPoolScheduler.Default;

        var sourceSubscription = _source.Connect()
            .ObserveOn(_uiScheduler)
            .Bind(Items)
            .Subscribe();

        _disposables.Add(sourceSubscription);
        _disposables.Add(_folderSubscription);
        _disposables.Add(_source);

        _messenger.Register<FileTableNavigateUpRequestedMessage>(this, OnNavigateUpRequested);
        _messenger.Register<FileTableNavigateDownRequestedMessage>(this, OnNavigateDownRequested);

        LoadDirectory(initialPath);
    }

    public string Identity { get; }

    public ObservableCollectionExtended<SpecFileEntryViewModel> Items { get; }

    public string CurrentPath { get; private set; } = string.Empty;

    public event EventHandler? CurrentPathChanged;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _messenger.UnregisterAll(this);
        _entryStream?.Dispose();
        _disposables.Dispose();
    }

    private void LoadDirectory(string path)
    {
        var normalized = NormalizedPath.FromUserInput(Path.GetFullPath(path));
        if (!Directory.Exists(normalized.DisplayPath))
        {
            return;
        }

        _entryStream?.Dispose();
        var entryStream = new WindowsFolderEntryStream(normalized, _backgroundScheduler);
        _entryStream = entryStream;
        var loadVersion = Interlocked.Increment(ref _loadVersion);

        CurrentPath = normalized.DisplayPath;
        CurrentPathChanged?.Invoke(this, EventArgs.Empty);

        var hasParent = Directory.GetParent(normalized.DisplayPath) is not null;
        _source.Edit(cache =>
        {
            cache.Clear();
            if (hasParent)
            {
                cache.AddOrUpdate(SpecFileEntryViewModel.CreateParentEntry());
            }
        });

        var scan = entryStream.Entries
            .Select(static entry => new SpecFileEntryViewModel(entry))
            .Buffer(EntryBatchSize)
            .Where(static entries => entries.Count > 0)
            .ObserveOn(_uiScheduler)
            .Do(entries => AddEntries(loadVersion, entries))
            .Select(static _ => 0)
            .IgnoreElements();

        var watch = Observable.Defer(() => entryStream.WatchChanges())
            .ObserveOn(_uiScheduler)
            .Do(change => ApplyChange(loadVersion, change))
            .Select(static _ => 0)
            .IgnoreElements();

        _folderSubscription.Disposable = scan
            .Concat(watch)
            .Subscribe(static _ => { }, OnFolderStreamError);
    }

    private void AddEntries(int loadVersion, IList<SpecFileEntryViewModel> entries)
    {
        if (loadVersion != Volatile.Read(ref _loadVersion))
        {
            return;
        }

        _source.AddOrUpdate(entries);
    }

    private void ApplyChange(int loadVersion, DirectoryChange change)
    {
        if (loadVersion != Volatile.Read(ref _loadVersion))
        {
            return;
        }

        switch (change.Kind)
        {
            case DirectoryChangeKind.Created:
            case DirectoryChangeKind.Changed:
                AddOrRemoveChangedEntry(change.Path);
                break;
            case DirectoryChangeKind.Deleted:
                _source.RemoveKey(change.Path.Value);
                break;
            case DirectoryChangeKind.Renamed:
                if (change.OldPath is { } oldPath)
                {
                    _source.RemoveKey(oldPath.Value);
                }

                AddOrRemoveChangedEntry(change.Path);
                break;
            case DirectoryChangeKind.Invalidated:
                LoadNearestExistingDirectory(CurrentPath);
                break;
        }
    }

    private void AddOrRemoveChangedEntry(NormalizedPath path)
    {
        if (WindowsFolderEntryStream.GetEntry(path) is { } model)
        {
            _source.AddOrUpdate(new SpecFileEntryViewModel(model));
            return;
        }

        _source.RemoveKey(path.Value);
    }

    private void LoadNearestExistingDirectory(string path)
    {
        var candidate = new DirectoryInfo(path);
        while (candidate is not null && !candidate.Exists)
        {
            candidate = candidate.Parent;
        }

        if (candidate is not null)
        {
            LoadDirectory(candidate.FullName);
        }
    }

    private void OnFolderStreamError(Exception error)
    {
        System.Diagnostics.Debug.WriteLine(error);
    }

    private void OnNavigateUpRequested(
        object recipient,
        FileTableNavigateUpRequestedMessage message)
    {
        if (message.Identity != Identity)
        {
            return;
        }

        var parent = Directory.GetParent(CurrentPath);
        if (parent is not null)
        {
            LoadDirectory(parent.FullName);
        }
    }

    private void OnNavigateDownRequested(
        object recipient,
        FileTableNavigateDownRequestedMessage message)
    {
        if (message.Identity != Identity
            || message.Item.Model is not { Kind: ItemKind.Directory } model)
        {
            return;
        }

        LoadDirectory(model.FullPath.DisplayPath);
    }
}
