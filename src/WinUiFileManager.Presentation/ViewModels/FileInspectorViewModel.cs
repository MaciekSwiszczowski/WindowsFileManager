using WinUiFileManager.Presentation.Services;

namespace WinUiFileManager.Presentation.ViewModels;

public sealed partial class FileInspectorViewModel : FileInspector.FileInspectorDetailsViewModelBase
{
    private readonly FileTableFocusService _fileTableFocusService;
    private bool _disposed;

    public FileInspectorViewModel(
        IFileIdentityService fileIdentityService,
        IClipboardService clipboardService,
        IShellService shellService,
        FileTableFocusService fileTableFocusService,
        ISchedulerProvider schedulers,
        ILogger<FileInspectorViewModel> logger)
        : base(
            fileIdentityService,
            clipboardService,
            shellService,
            schedulers,
            logger)
    {
        _fileTableFocusService = fileTableFocusService;
        SubscribeToTableMessages();
    }

    public override void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        WeakReferenceMessenger.Default.UnregisterAll(this);
        base.Dispose();
    }

    private void SubscribeToTableMessages()
    {
        WeakReferenceMessenger.Default.Register<FileTableSelectionChangedMessage>(this, OnFileTableSelectionChanged);
        WeakReferenceMessenger.Default.Register<FileTableFocusedMessage>(this, OnFileTableFocused);
    }

    private void OnFileTableFocused(object recipient, FileTableFocusedMessage message)
    {
        if (message.IsFocused)
        {
            ApplyTableSelectionForIdentity(message.Identity);
        }
    }

    private void OnFileTableSelectionChanged(object recipient, FileTableSelectionChangedMessage message)
    {
        if (string.Equals(message.Identity, _fileTableFocusService.SourcePanelIdentity, StringComparison.Ordinal))
        {
            ApplyTableSelection(message.SelectedItems);
        }
    }
}
