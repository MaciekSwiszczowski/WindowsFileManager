using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using WinUiFileManager.Application.Dialogs;
using WinUiFileManager.Application.Messages;
using WinUiFileManager.Domain.Enums;
using WinUiFileManager.Domain.ValueObjects;

namespace WinUiFileManager.Infrastructure.Services;

public sealed class RenameService : IDisposable
{
    private readonly ActivePanelsService _activePanels;
    private readonly ILogger<RenameService> _logger;
    private bool _disposed;

    public RenameService(
        ActivePanelsService activePanels,
        ILogger<RenameService> logger)
    {
        _activePanels = activePanels;
        _logger = logger;

        WeakReferenceMessenger.Default.Register<RenameKeyPressedMessage>(this, OnRenameKeyPressed);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        WeakReferenceMessenger.Default.UnregisterAll(this);
    }

    private void OnRenameKeyPressed(object recipient, RenameKeyPressedMessage message)
    {
        _ = HandleRenameAsync();
    }

    private async Task HandleRenameAsync()
    {
        try
        {
            var activePanelIdentity = _activePanels.ActivePanelIdentity;
            if (string.IsNullOrWhiteSpace(activePanelIdentity))
            {
                return;
            }

            var request = WeakReferenceMessenger.Default.Send(
                new FileTableSelectedEntriesRequestMessage(activePanelIdentity));
            if (!request.HasReceivedResponse || request.Response.Count != 1)
            {
                return;
            }

            var item = request.Response[0];
            var viewModel = new RenameDialogViewModel(item);
            var dialogRequest = WeakReferenceMessenger.Default.Send(
                new ShowDialogMessage(
                    viewModel,
                    [
                        new DialogButtonConfiguration(DialogButtonRole.Primary, "Rename", IsDefault: true),
                        new DialogButtonConfiguration(DialogButtonRole.Close, "Cancel"),
                    ],
                    title: "Rename",
                    contentTemplateKey: DialogTemplateKeys.Rename));

            var result = await dialogRequest.Response;
            if (result.ButtonRole is not DialogButtonRole.Primary)
            {
                return;
            }

            await RenameAsync(item, viewModel.NewName.Trim());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle rename request.");
        }
    }

    private async Task RenameAsync(FileSystemEntryModel item, string newName)
    {
        if (string.Equals(newName, item.Name, StringComparison.Ordinal))
        {
            return;
        }

        try
        {
            if (string.IsNullOrWhiteSpace(newName))
            {
                throw new ArgumentException("New name cannot be empty.", nameof(newName));
            }

            if (newName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                throw new ArgumentException("New name contains invalid characters.", nameof(newName));
            }

            var sourcePath = item.FullPath.DisplayPath;
            var parentPath = Path.GetDirectoryName(sourcePath);
            if (string.IsNullOrWhiteSpace(parentPath))
            {
                throw new InvalidOperationException("Cannot determine the parent folder.");
            }

            var destinationPath = Path.Combine(parentPath, newName);
            if (item.Kind is ItemKind.Directory)
            {
                Directory.Move(sourcePath, destinationPath);
            }
            else
            {
                File.Move(sourcePath, destinationPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to rename {Path} to {NewName}", item.FullPath.DisplayPath, newName);
            await ShowRenameErrorAsync(ex.Message);
        }
    }

    private static async Task ShowRenameErrorAsync(string message)
    {
        var dialogRequest = WeakReferenceMessenger.Default.Send(
            new ShowDialogMessage(
                new MessageDialogViewModel(message),
                [
                    new DialogButtonConfiguration(DialogButtonRole.Close, "OK", IsDefault: true),
                ],
                title: "Rename failed",
                contentTemplateKey: DialogTemplateKeys.Message));

        _ = await dialogRequest.Response;
    }
}
