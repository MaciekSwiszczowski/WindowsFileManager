using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using WinUiFileManager.Application.Dialogs;
using WinUiFileManager.Application.Messages.RequestMessages.FileOperations;

namespace WinUiFileManager.Diagnostics.FileOperations;

public sealed class FileOperationRequestHandler : IDisposable
{
    private readonly ILogger<FileOperationRequestHandler> _logger;
    private readonly IMessenger _messenger;
    private bool _disposed;

    public FileOperationRequestHandler(
        IMessenger messenger,
        ILogger<FileOperationRequestHandler> logger)
    {
        _messenger = messenger;
        _logger = logger;
        _messenger.Register<SetFileAttributeFlagRequestedMessage>(this, OnSetFileAttributeFlagRequested);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _messenger.UnregisterAll(this);
    }

    private void OnSetFileAttributeFlagRequested(object recipient, SetFileAttributeFlagRequestedMessage message)
        => _ = SetAttributeFlagAsync(message);

    private async Task SetAttributeFlagAsync(SetFileAttributeFlagRequestedMessage message)
    {
        try
        {
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

    private async Task ShowAttributeChangeFailureAsync(SetFileAttributeFlagRequestedMessage message, Exception exception)
    {
        var dialogRequest = _messenger.Send(
            new ShowDialogMessage(
                new MessageDialogViewModel(CreateAttributeChangeFailureMessage(message, exception)),
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
