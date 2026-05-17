using UiDispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue;

namespace WinUiFileManager.Presentation.FileEntryTable.Messages;

internal static class FileTableSelectionChangedMessengerExtensions
{
    private static readonly DeferredEmptySelectionMessages EmptySelectionMessages = new();

    public static void SendFileTableSelectionChanged(
        this IMessenger messenger, FileTableSelectionChangedMessage message, UiDispatcherQueue dispatcherQueue)
    {
        EmptySelectionMessages.Send(messenger, message, dispatcherQueue);
    }

    private sealed class DeferredEmptySelectionMessages
    {
        private readonly Dictionary<string, int> _versionsByIdentity = [];

        public void Send(IMessenger messenger, FileTableSelectionChangedMessage message, UiDispatcherQueue dispatcherQueue)
        {
            if (message.SelectedItems.Count == 0 && !message.IsParentRowSelected)
            {
                SendEmptySelectionWhenStable(messenger, message, dispatcherQueue);
                return;
            }

            _versionsByIdentity[message.Identity] = GetNextVersion(message.Identity);
            messenger.Send(message);
        }

        private void SendEmptySelectionWhenStable(IMessenger messenger, FileTableSelectionChangedMessage message, UiDispatcherQueue dispatcherQueue)
        {
            var version = GetNextVersion(message.Identity);
            _versionsByIdentity[message.Identity] = version;
            if (!dispatcherQueue.TryEnqueue(() => SendEmptySelectionIfStable(version, messenger, message)))
            {
                SendEmptySelectionIfStable(version, messenger, message);
            }
        }

        private void SendEmptySelectionIfStable(int version, IMessenger messenger, FileTableSelectionChangedMessage message)
        {
            if (!_versionsByIdentity.TryGetValue(message.Identity, out var currentVersion)
                || version != currentVersion)
            {
                return;
            }

            messenger.Send(message);
        }

        private int GetNextVersion(string identity) =>
            _versionsByIdentity.TryGetValue(identity, out var version) ? version + 1 : 1;
    }
}
