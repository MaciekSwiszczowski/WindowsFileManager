using CommunityToolkit.Mvvm.Messaging.Messages;

namespace WinUiFileManager.Presentation.Dialogs;

public sealed class ShowDialogMessage : AsyncRequestMessage<DialogResult>
{
    public ShowDialogMessage(
        object viewModel,
        IReadOnlyList<DialogButtonConfiguration> buttons,
        string? title = null,
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
        CancellationToken = cancellationToken;
    }

    public string DialogId { get; }

    public object ViewModel { get; }

    public IReadOnlyList<DialogButtonConfiguration> Buttons { get; }

    public string? Title { get; }

    public CancellationToken CancellationToken { get; }
}
