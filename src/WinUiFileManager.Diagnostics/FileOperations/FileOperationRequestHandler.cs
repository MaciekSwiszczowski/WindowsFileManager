using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using WinUiFileManager.Application.Dialogs;
using WinUiFileManager.Application.FileEntries;
using WinUiFileManager.Application.Messages.RequestMessages.FileOperations;

namespace WinUiFileManager.Diagnostics.FileOperations;

/// <summary>
/// Diagnostics-layer handler for the <see cref="SetFileAttributeFlagRequestedMessage"/>: toggles a single
/// NTFS file-attribute flag on a path and reports failures via a dialog. Answers the message rather than
/// returning a value (it is a fire-and-forget command, not a request/reply).
/// </summary>
/// <remarks>
/// Lifetime: registered as a DI singleton; <see cref="Initialize"/> hooks the messenger and
/// <see cref="Dispose"/> performs <c>UnregisterAll</c>. However, the container is never disposed
/// (AGENTS.md §5), so <see cref="Dispose"/> is effectively never reached — this object is process-lifetime
/// and its single registration lives for the whole run.
/// <para>
/// Threading: the actual attribute change runs on the thread pool via <see cref="Task.Run(System.Action)"/>.
/// <b>UI-thread hazard (documented, not fixed — AGENTS.md §6):</b> on failure,
/// <see cref="ShowAttributeChangeFailureAsync"/> sends a <see cref="ShowDialogMessage"/> while still on a
/// thread-pool thread (we got there after <c>ConfigureAwait(false)</c>), instead of first marshalling
/// back to the UI thread. Dialog/<c>XamlRoot</c> work is UI-affine, so this is a latent threading bug;
/// it is described here intentionally and must not be silently "fixed" as part of doc work.
/// </para>
/// </remarks>
public sealed class FileOperationRequestHandler : IDisposable
{
    private readonly ILogger<FileOperationRequestHandler> _logger;
    private readonly IMessenger _messenger;
    private readonly Func<string, MessageDialogViewModel> _messageDialogFactory;
    private bool _disposed;

    public FileOperationRequestHandler(
        IMessenger messenger,
        ILogger<FileOperationRequestHandler> logger,
        Func<string, MessageDialogViewModel> messageDialogFactory)
    {
        _messenger = messenger;
        _logger = logger;
        _messageDialogFactory = messageDialogFactory;
    }

    /// <summary>
    /// Registers this handler with the messenger. Call once (the startup chain does so).
    /// </summary>
    /// <remarks>Not idempotent — a second call would double-register and double-handle (AGENTS.md §4).</remarks>
    public void Initialize()
    {
        _messenger.Register<SetFileAttributeFlagRequestedMessage>(this, OnSetFileAttributeFlagRequested);
    }

    /// <summary>
    /// Unregisters from the messenger. Idempotent via the <see cref="_disposed"/> guard.
    /// </summary>
    /// <remarks>See type remarks: in practice this is never invoked because the container is not disposed.</remarks>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _messenger.UnregisterAll(this);
    }

    // Messenger callback is synchronous; kick off the async work and intentionally drop the task
    // (fire-and-forget). Exceptions are handled inside SetAttributeFlagAsync, not here.
    private void OnSetFileAttributeFlagRequested(object recipient, SetFileAttributeFlagRequestedMessage message)
        => _ = SetAttributeFlagAsync(message);

    private async Task SetAttributeFlagAsync(SetFileAttributeFlagRequestedMessage message)
    {
        try
        {
            CreateSelectionSnapshot(message);
            // Run the blocking attribute change off the calling thread.
            await Task.Run(() => SetAttributeFlag(message)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to set file attribute {Flag} to {Enabled} for {Path}",
                message.Flag,
                message.Enabled,
                message.Path.DisplayPath);
            await ShowAttributeChangeFailureAsync(message, ex).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Shows a modal error dialog describing a failed attribute change and awaits its dismissal.
    /// </summary>
    /// <param name="message">The originating request (for path/flag/state context).</param>
    /// <param name="exception">The failure to surface to the user.</param>
    /// <remarks>
    /// UI-thread hazard: this <see cref="ShowDialogMessage"/> is sent from a thread-pool thread (the
    /// caller reached here after <c>ConfigureAwait(false)</c>) without marshalling to the UI thread first
    /// — see the type-level remarks. Described, not fixed.
    /// </remarks>
    private async Task ShowAttributeChangeFailureAsync(SetFileAttributeFlagRequestedMessage message, Exception exception)
    {
        var dialogRequest = _messenger.Send(
            new ShowDialogMessage(
                _messageDialogFactory(CreateAttributeChangeFailureMessage(message, exception)),
                [
                    new DialogButtonConfiguration(DialogButtonRole.Close, "OK", IsDefault: true),
                ],
                title: "Attribute change failed",
                contentTemplateKey: DialogTemplateKeys.Message));

        _ = await dialogRequest.Response.ConfigureAwait(false);
    }

    private static string CreateAttributeChangeFailureMessage(SetFileAttributeFlagRequestedMessage message, Exception exception) =>
        string.Join(
            Environment.NewLine,
            $"Path: {message.Path.DisplayPath}",
            $"Attribute: {message.Flag}",
            $"Requested state: {(message.Enabled ? "Enabled" : "Disabled")}",
            $"Error: {exception.Message}");

    /// <summary>
    /// Asks the file table to record the current selection around the target path before the attribute
    /// change, so the row can be re-selected after the table refreshes.
    /// </summary>
    /// <param name="message">The originating request whose path identifies the row and its directory.</param>
    /// <remarks>
    /// Best-effort: the response is deliberately ignored (the discard simply documents that a reply may
    /// or may not have arrived) and a missing directory short-circuits silently.
    /// </remarks>
    private void CreateSelectionSnapshot(SetFileAttributeFlagRequestedMessage message)
    {
        var directoryPath = Path.GetDirectoryName(message.Path.DisplayPath);
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            return;
        }

        var request = _messenger.Send(
            new FileTableSelectionSnapshotRequestMessage(
                NormalizedPath.FromUserInput(directoryPath),
                message.Path,
                message.Path));

        // Response is informational only; the snapshot is best-effort.
        _ = request is { HasReceivedResponse: true, Response: true };
    }

    /// <summary>
    /// Applies the requested add/remove of a single <see cref="FileAttributes"/> flag, skipping the write
    /// when the attribute set is already in the desired state. Runs on a thread-pool thread.
    /// </summary>
    private static void SetAttributeFlag(SetFileAttributeFlagRequestedMessage message)
    {
        var currentAttributes = File.GetAttributes(message.Path.DisplayPath);
        var updatedAttributes = message.Enabled
            ? currentAttributes | message.Flag
            : currentAttributes & ~message.Flag;

        if (updatedAttributes != currentAttributes)
        {
            File.SetAttributes(message.Path.DisplayPath, updatedAttributes);
        }
    }
}
