using CommunityToolkit.Mvvm.Messaging.Messages;
using WinUiFileManager.Application.Messaging;

namespace WinUiFileManager.Application.Dialogs;

/// <summary>
/// Request to display a modal dialog. Sent by Application/Presentation code that needs user input or
/// confirmation; handled on the UI thread by the Presentation dialog host, which replies with a
/// <see cref="DialogResult"/>. Being an <see cref="AsyncRequestMessage{T}"/>, the sender awaits the reply.
/// </summary>
/// <remarks>
/// Because the handler touches <c>XamlRoot</c>/<c>ContentDialog</c>, it must run on the UI/STA thread;
/// senders on a pool thread must marshal back before sending (see AGENTS.md §6).
/// </remarks>
public sealed class ShowDialogMessage : AsyncRequestMessage<DialogResult>, IFileManagerMessengerMessage
{
    /// <param name="viewModel">The dialog's data context. Must not be null.</param>
    /// <param name="buttons">Buttons to render, in order. Must not be null; copied defensively.</param>
    /// <param name="title">Optional dialog title.</param>
    /// <param name="contentTemplateKey">Optional <see cref="DialogTemplateKeys"/> value selecting the content template.</param>
    /// <param name="dialogId">Optional stable id; a random one is generated when null/blank (used to de-dupe/track the dialog).</param>
    /// <param name="cancellationToken">Token that can dismiss the dialog programmatically.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="viewModel"/> or <paramref name="buttons"/> is null.</exception>
    public ShowDialogMessage(
        object viewModel,
        IReadOnlyList<DialogButtonConfiguration> buttons,
        string? title = null,
        string? contentTemplateKey = null,
        string? dialogId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(buttons);

        DialogId = string.IsNullOrWhiteSpace(dialogId)
            ? Guid.NewGuid().ToString("N")
            : dialogId;
        ViewModel = viewModel;
        Buttons = buttons.ToArray();
        Title = title;
        ContentTemplateKey = contentTemplateKey;
        CancellationToken = cancellationToken;
    }

    /// <summary>Stable identifier for this dialog instance (generated when not supplied).</summary>
    public string DialogId { get; }

    /// <summary>The dialog's data context, bound by the selected content template.</summary>
    public object ViewModel { get; }

    /// <summary>The buttons to render (defensive copy of the constructor argument).</summary>
    public IReadOnlyList<DialogButtonConfiguration> Buttons { get; }

    /// <summary>Optional dialog title.</summary>
    public string? Title { get; }

    /// <summary>Optional content-template key (see <see cref="DialogTemplateKeys"/>).</summary>
    public string? ContentTemplateKey { get; }

    /// <summary>Token allowing the caller to dismiss the dialog programmatically.</summary>
    public CancellationToken CancellationToken { get; }
}
