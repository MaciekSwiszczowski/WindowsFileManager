using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using CommunityToolkit.Mvvm.Messaging;
using DynamicData;
using DynamicData.Binding;
using WinUiFileManager.Application.Abstractions;
using WinUiFileManager.Application.Messages;
using WinUiFileManager.Domain.Enums;
using WinUiFileManager.Domain.Events;
using WinUiFileManager.Domain.ValueObjects;
using WinUiFileManager.Presentation.FileEntryTable;

namespace WinUiFileManager.Presentation.FileEntryTable.Data;

internal sealed class FileEntryTableDataSource : IDisposable
{
    private static readonly TimeSpan ScanBatchInterval = TimeSpan.FromMilliseconds(200);

    private const string ParentEntryKey = "\0..";

    private readonly CompositeDisposable _disposables = new();
    private readonly SerialDisposable _activeDirectoryLoad = new();
    private readonly SerialDisposable _folderWatchingService = new();
    private readonly BehaviorSubject<FileEntryTableDataState> _states;
    private readonly IFileSystemService _fileSystemService;
    private readonly IMessenger _messenger;
    private readonly IScheduler _backgroundScheduler;
    private readonly IScheduler _uiScheduler;
    private int _loadVersion;
    private bool _disposed;

    public FileEntryTableDataSource(
        string identity,
        string initialPath,
        IScheduler uiScheduler,
        IFileSystemService fileSystemService,
        IMessenger? messenger = null,
        IScheduler? backgroundScheduler = null)
    {
        ArgumentNullException.ThrowIfNull(uiScheduler);
        ArgumentNullException.ThrowIfNull(fileSystemService);

        Identity = identity;
        Items = new ObservableCollectionExtended<SpecFileEntryViewModel>();
        _uiScheduler = uiScheduler;
        _fileSystemService = fileSystemService;
        _messenger = messenger ?? WeakReferenceMessenger.Default;
        _backgroundScheduler = backgroundScheduler ?? TaskPoolScheduler.Default;
        _states = new BehaviorSubject<FileEntryTableDataState>(
            new FileEntryTableDataState(string.Empty, Items));

        _disposables.Add(_activeDirectoryLoad);
        _disposables.Add(_folderWatchingService);
        _messenger.Register<FileTableNavigateUpRequestedMessage>(this, OnNavigateUpRequested);
        _messenger.Register<FileTableNavigateDownRequestedMessage>(this, OnNavigateDownRequested);

        LoadDirectory(initialPath);
    }

    public string Identity { get; }

    public ObservableCollectionExtended<SpecFileEntryViewModel> Items { get; private set; }

    public string CurrentPath { get; private set; } = string.Empty;

    public IObservable<FileEntryTableDataState> States => _states.AsObservable();

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _messenger.UnregisterAll(this);
        _disposables.Dispose();
        _states.Dispose();
    }

    private void LoadDirectory(string path)
    {
        if (!TryCreateLoadContext(path, out var context))
        {
            return;
        }

        var entries = _fileSystemService.ObserveDirectoryEntries(
            context.Path,
            _backgroundScheduler,
            CancellationToken.None);
        var directoryLoad = BuildDirectoryLoadQuery(context, entries);

        _activeDirectoryLoad.Disposable = directoryLoad;
    }

    private bool TryCreateLoadContext(string path, out DirectoryLoadContext context)
    {
        var normalized = NormalizedPath.FromUserInput(Path.GetFullPath(path));
        if (!Directory.Exists(normalized.DisplayPath))
        {
            context = default!;
            return false;
        }

        var loadVersion = Interlocked.Increment(ref _loadVersion);
        var hasParent = Directory.GetParent(normalized.DisplayPath) is not null;

        context = CreateNewItemsCollection(normalized, loadVersion, hasParent);
        return true;
    }

    private DirectoryLoadContext CreateNewItemsCollection(
        NormalizedPath path,
        int loadVersion,
        bool hasParent)
    {
        var oldItems = Items;

        DisposeFolderWatchingService();
        DisposeActiveDirectoryLoad();

        Items = new ObservableCollectionExtended<SpecFileEntryViewModel>();
        CurrentPath = path.DisplayPath;
        _states.OnNext(new FileEntryTableDataState(CurrentPath, Items));

        return new DirectoryLoadContext(path, loadVersion, hasParent, Items, oldItems);
    }

    private IDisposable BuildDirectoryLoadQuery(
        DirectoryLoadContext context,
        IObservable<FileSystemEntryModel> entries)
    {
        var changes = ObservableChangeSet.Create<SpecFileEntryViewModel, string>(
            cache => SubscribeToDirectoryCache(context, cache, entries),
            GetKey);

        var itemsBinding = changes
            .ObserveOn(_uiScheduler)
            .Bind(context.Items)
            .Subscribe(static _ => { }, OnFolderStreamError);

        return itemsBinding;
    }

    private IDisposable SubscribeToDirectoryCache(
        DirectoryLoadContext context,
        ISourceCache<SpecFileEntryViewModel, string> cache,
        IObservable<FileSystemEntryModel> entries)
    {
        AddParentEntryIfNeeded(context, cache);

        var watchLifetime = new SerialDisposable();
        var scanChanges = BuildBatchedScanRows(entries)
            .ToObservableChangeSet(GetKey)
            .Publish();

        var scanPopulation = scanChanges.PopulateInto(cache);
        var scanCompletion = scanChanges
            .Select(static _ => 0)
            .IgnoreElements()
            .Subscribe(
                static _ => { },
                OnFolderStreamError,
                () => OnFolderEnumerationCompleted(context, cache, watchLifetime));
        var scanConnection = scanChanges.Connect();

        return new CompositeDisposable(scanPopulation, scanCompletion, scanConnection, watchLifetime);
    }

    private void AddParentEntryIfNeeded(
        DirectoryLoadContext context,
        ISourceCache<SpecFileEntryViewModel, string> cache)
    {
        if (!context.HasParent)
        {
            return;
        }

        cache.AddOrUpdate(SpecFileEntryViewModel.CreateParentEntry());
    }

    private IObservable<IEnumerable<SpecFileEntryViewModel>> BuildBatchedScanRows(
        IObservable<FileSystemEntryModel> entries)
    {
        var rows = entries.Select(static entry => new SpecFileEntryViewModel(entry));

        return rows
            .Select(static (item, index) => (Item: item, Index: index))
            .Publish(shared => shared
                .Where(static row => row.Index == 0)
                .Select(static row => (IEnumerable<SpecFileEntryViewModel>)new[] { row.Item })
                .Merge(shared
                    .Where(static row => row.Index > 0)
                    .Select(static row => row.Item)
                    .Buffer(ScanBatchInterval, _uiScheduler)
                    .Where(static batch => batch.Count > 0)
                    .Select(static batch => (IEnumerable<SpecFileEntryViewModel>)batch)));
    }

    private void OnFolderEnumerationCompleted(
        DirectoryLoadContext context,
        ISourceCache<SpecFileEntryViewModel, string> cache,
        SerialDisposable watchLifetime)
    {
        watchLifetime.Disposable = CreateAndStartFolderWatchingService(context, cache);
        DisposeOldItemsOnBackground(context.OldItems);
    }

    private IDisposable CreateAndStartFolderWatchingService(
        DirectoryLoadContext context,
        ISourceCache<SpecFileEntryViewModel, string> cache)
    {
        var folderWatchService = new FolderWatchService(context.Path);
        var subscription = folderWatchService.Changes
            .ObserveOn(_uiScheduler)
            .Subscribe(
                change => ApplyChange(cache, context.LoadVersion, change),
                OnFolderStreamError);

        var lifetime = new CompositeDisposable(folderWatchService, subscription);
        _folderWatchingService.Disposable = lifetime;
        folderWatchService.Start();

        return lifetime;
    }

    private void DisposeFolderWatchingService()
    {
        _folderWatchingService.Disposable = Disposable.Empty;
    }

    private void DisposeActiveDirectoryLoad()
    {
        _activeDirectoryLoad.Disposable = Disposable.Empty;
    }

    private void DisposeOldItemsOnBackground(
        ObservableCollectionExtended<SpecFileEntryViewModel> oldItems)
    {
        _backgroundScheduler.Schedule(oldItems, static (_, items) =>
        {
            items.Clear();
            return Disposable.Empty;
        });
    }

    private void ApplyChange(
        ISourceCache<SpecFileEntryViewModel, string> cache,
        int loadVersion,
        DirectoryChange change)
    {
        if (loadVersion != Volatile.Read(ref _loadVersion))
        {
            return;
        }

        switch (change.Kind)
        {
            case DirectoryChangeKind.Created:
            case DirectoryChangeKind.Changed:
                _ = AddOrRemoveChangedEntryAsync(cache, loadVersion, change.Path);
                break;
            case DirectoryChangeKind.Deleted:
                cache.RemoveKey(change.Path.Value);
                break;
            case DirectoryChangeKind.Renamed:
                if (change.OldPath is { } oldPath)
                {
                    cache.RemoveKey(oldPath.Value);
                }

                _ = AddOrRemoveChangedEntryAsync(cache, loadVersion, change.Path);
                break;
            case DirectoryChangeKind.Invalidated:
                LoadNearestExistingDirectory(CurrentPath);
                break;
        }
    }

    private async Task AddOrRemoveChangedEntryAsync(
        ISourceCache<SpecFileEntryViewModel, string> cache,
        int loadVersion,
        NormalizedPath path)
    {
        FileSystemEntryModel? model;
        try
        {
            model = await _fileSystemService.GetEntryAsync(path, CancellationToken.None);
        }
        catch (IOException)
        {
            model = null;
        }
        catch (UnauthorizedAccessException)
        {
            model = null;
        }
        catch (Exception ex)
        {
            OnFolderStreamError(ex);
            model = null;
        }

        _ = _uiScheduler.Schedule(() =>
        {
            if (loadVersion != Volatile.Read(ref _loadVersion))
            {
                return;
            }

            if (model is { })
            {
                cache.AddOrUpdate(new SpecFileEntryViewModel(model));
                return;
            }

            cache.RemoveKey(path.Value);
        });
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

    private void OnNavigateUpRequested(object recipient, FileTableNavigateUpRequestedMessage message)
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

    private void OnNavigateDownRequested(object recipient, FileTableNavigateDownRequestedMessage message)
    {
        if (message.Identity != Identity
            || message.Item.Kind != ItemKind.Directory)
        {
            return;
        }

        LoadDirectory(message.Item.FullPath.DisplayPath);
    }

    private static string GetKey(SpecFileEntryViewModel item)
    {
        return item.Model?.FullPath.Value ?? ParentEntryKey;
    }

    private sealed record DirectoryLoadContext(
        NormalizedPath Path,
        int LoadVersion,
        bool HasParent,
        ObservableCollectionExtended<SpecFileEntryViewModel> Items,
        ObservableCollectionExtended<SpecFileEntryViewModel> OldItems);
}
