using UiDispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue;

namespace WinUiFileManager.Presentation.FileEntryTable.Messages;

/// <summary>
/// Helper for publishing <see cref="FileTableSelectionChangedMessage"/> that debounces transient
/// "selection is now empty" notifications, so consumers (e.g. the inspector) don't flicker to the
/// no-selection state during the brief window between a selection being cleared and the new selection
/// being applied.
/// </summary>
internal static class FileTableSelectionChangedMessengerExtensions
{
    // Process-lifetime debouncer keyed by table identity. A static instance is fine: the version
    // counters are small per-pane ints and the file table is created once per pane.
    private static readonly DeferredEmptySelectionMessages EmptySelectionMessages = new();

    /// <summary>Publishes a selection-changed message. Non-empty selections are sent immediately;
    /// empty selections are deferred onto <paramref name="dispatcherQueue"/> and only sent if no newer
    /// selection change for the same identity supersedes them first.</summary>
    public static void SendFileTableSelectionChanged(
        this IMessenger messenger, FileTableSelectionChangedMessage message, UiDispatcherQueue dispatcherQueue)
    {
        EmptySelectionMessages.Send(messenger, message, dispatcherQueue);
    }

    /// <summary>
    /// Per-identity version-stamping debouncer. Each send bumps the identity's version; a deferred
    /// empty-selection message only fires if its captured version is still the latest when the
    /// dispatcher drains, which cancels out an empty state immediately replaced by a real one.
    /// </summary>
    private sealed class DeferredEmptySelectionMessages
    {
        private readonly Dictionary<string, int> _versionsByIdentity = [];

        public void Send(IMessenger messenger, FileTableSelectionChangedMessage message, UiDispatcherQueue dispatcherQueue)
        {
            // Empty selection (and not the parent row) is the only case we defer; everything else is
            // an authoritative change that should reach consumers right away.
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
            // Defer to the end of the dispatcher queue; if enqueue fails (e.g. shutting down) fall back
            // to sending synchronously so the message is not silently dropped.
            if (!dispatcherQueue.TryEnqueue(() => SendEmptySelectionIfStable(version, messenger, message)))
            {
                SendEmptySelectionIfStable(version, messenger, message);
            }
        }

        private void SendEmptySelectionIfStable(int version, IMessenger messenger, FileTableSelectionChangedMessage message)
        {
            // A newer change bumped the version in the meantime: the empty state is stale, drop it.
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
