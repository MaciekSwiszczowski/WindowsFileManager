namespace WinUiFileManager.App.Startup;

using Diagnostics.FileOperations;
using Infrastructure.Services;

public sealed class StartupChain
{
    private readonly ActivePanelsService _activePanelsService;
    private readonly FileOperationRequestHandler _fileOperationRequestHandler;
    private readonly RenameService _renameService;

    public StartupChain(
        ActivePanelsService activePanelsService,
        FileOperationRequestHandler fileOperationRequestHandler,
        RenameService renameService)
    {
        _activePanelsService = activePanelsService;
        _fileOperationRequestHandler = fileOperationRequestHandler;
        _renameService = renameService;
    }

    public Task StartupChainAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _activePanelsService.Initialize();
        _fileOperationRequestHandler.Initialize();
        _renameService.Initialize();

        return Task.CompletedTask;
    }
}
