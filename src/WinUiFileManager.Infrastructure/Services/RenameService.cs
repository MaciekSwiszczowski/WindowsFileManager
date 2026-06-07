using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using WinUiFileManager.Application.Abstractions;
using WinUiFileManager.Application.Dialogs;
using WinUiFileManager.Application.Messages;
using WinUiFileManager.Application.Messages.RequestMessages;
using WinUiFileManager.Application.FileEntries;

namespace WinUiFileManager.Infrastructure.Services;

/// <summary>
/// Coordinates the interactive rename flow: on a rename key press it resolves the single selected entry in the
/// active pane, shows the rename dialog, and (if confirmed) performs the file/directory move, reporting failures
/// via a message dialog. Lives in Infrastructure because it performs real file-system I/O.
/// </summary>
/// <remarks>
/// MESSENGER LIFETIME (AGENTS.md §4/§5): registers <see cref="RenameKeyPressedMessage"/> in <see cref="Initialize"/>
/// against the shared messenger, rooting this instance until <see cref="Dispose"/> calls <c>UnregisterAll</c>; as a
/// DI singleton that disposal only runs on container shutdown. <see cref="Dispose"/> is idempotent.
/// THREADING/UI: the rename flow shows dialogs (UI work), so the message handler kicks off an async flow that uses
/// the messenger's request/response dialogs; the actual <c>File.Move</c>/<c>Directory.Move</c> runs inline within
/// that async flow. Failures are caught and logged rather than thrown, and surfaced to the user as a dialog.
/// </remarks>
public sealed class RenameService : IDisposable
{
    private readonly IActivePanelsService _activePanels;
    private readonly ILogger<RenameService> _logger;
    private readonly IMessenger _messenger;
    private readonly Func<FileSystemEntryModel, RenameDialogViewModel> _renameDialogFactory;
    private readonly Func<string, MessageDialogViewModel> _messageDialogFactory;
    private bool _disposed;

    public RenameService(
        IActivePanelsService activePanels,
        ILogger<RenameService> logger,
        IMessenger messenger,
        Func<FileSystemEntryModel, RenameDialogViewModel> renameDialogFactory,
        Func<string, MessageDialogViewModel> messageDialogFactory)
    {
        ArgumentNullException.ThrowIfNull(messenger);
        _activePanels = activePanels;
        _logger = logger;
        _messenger = messenger;
        _renameDialogFactory = renameDialogFactory;
        _messageDialogFactory = messageDialogFactory;
    }

    /// <summary>Subscribes to the rename-key message. Call exactly once (not guarded against double-registration; §4).</summary>
    public void Initialize()
    {
        _messenger.Register<RenameKeyPressedMessage>(this, OnRenameKeyPressed);
    }

    /// <summary>Unregisters from the messenger. Idempotent; required to release the rooted recipient (§4).</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _messenger.UnregisterAll(this);
    }

    // Message handlers must be synchronous, so the async rename flow is intentionally fire-and-forget. The discard
    // is safe because HandleRenameAsync has its own top-level try/catch and never throws back into the messenger.
    private void OnRenameKeyPressed(object recipient, RenameKeyPressedMessage message)
    {
        _ = HandleRenameAsync();
    }

    // Orchestrates the full interactive rename. Top-level try/catch is mandatory because this runs detached from
    // the synchronous message handler (see OnRenameKeyPressed) — an unhandled exception here would otherwise be lost.
    private async Task HandleRenameAsync()
    {
        try
        {
            var activePanelIdentity = _activePanels.ActivePanelIdentity;
            if (string.IsNullOrWhiteSpace(activePanelIdentity))
            {
                return;
            }

            // Rename targets exactly one entry; bail if the active pane has zero or multiple selected.
            var request = _messenger.Send(
                new FileTableSelectedEntriesRequestMessage(activePanelIdentity));
            if (!request.HasReceivedResponse || request.Response.Count != 1)
            {
                return;
            }

            var item = request.Response[0];
            var viewModel = _renameDialogFactory(item);
            var dialogRequest = _messenger.Send(
                new ShowDialogMessage(
                    viewModel,
                    [
                        new DialogButtonConfiguration(DialogButtonRole.Primary, "Rename", IsDefault: true),
                        new DialogButtonConfiguration(DialogButtonRole.Close, "Cancel"),
                    ],
                    title: "Rename",
                    contentTemplateKey: DialogTemplateKeys.Rename));

            // Only proceed on the primary ("Rename") button; Cancel/Close abort silently.
            // ConfigureAwait(true): this flow shows dialogs via _messenger.Send(ShowDialogMessage…), which must run
            // on the UI thread (AGENTS.md §6), so every await here must resume on the captured UI context.
            var result = await dialogRequest.Response.ConfigureAwait(true);
            if (result.ButtonRole is not DialogButtonRole.Primary)
            {
                return;
            }

            await RenameAsync(item, viewModel.NewName.Trim()).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle rename request.");
        }
    }

    // Performs the actual move after validating the new name. Validation failures are raised as exceptions and
    // then caught locally so both "bad name" and "move failed" paths report through the same error dialog.
    private async Task RenameAsync(FileSystemEntryModel item, string newName)
    {
        // No-op rename (unchanged name) is a silent success — avoids a pointless move and potential self-conflict.
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

            // Rename stays within the same parent folder: combine the new leaf name onto the existing directory.
            var sourcePath = item.FullPath.DisplayPath;
            var parentPath = Path.GetDirectoryName(sourcePath);
            if (string.IsNullOrWhiteSpace(parentPath))
            {
                throw new InvalidOperationException("Cannot determine the parent folder.");
            }

            var destinationPath = Path.Combine(parentPath, newName);
            // Directory.Move and File.Move are distinct APIs; pick by the entry kind.
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
            // ConfigureAwait(true): keep the UI context so the error dialog can be sent on the UI thread.
            await ShowRenameErrorAsync(ex.Message).ConfigureAwait(true);
        }
    }

    // Surfaces a rename failure to the user via a modal message dialog; the response is awaited but discarded.
    private async Task ShowRenameErrorAsync(string message)
    {
        var dialogRequest = _messenger.Send(
            new ShowDialogMessage(
                _messageDialogFactory(message),
                [
                    new DialogButtonConfiguration(DialogButtonRole.Close, "OK", IsDefault: true),
                ],
                title: "Rename failed",
                contentTemplateKey: DialogTemplateKeys.Message));

        _ = await dialogRequest.Response.ConfigureAwait(true);
    }
}
