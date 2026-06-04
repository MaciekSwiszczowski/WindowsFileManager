namespace WinUiFileManager.Application.Messaging;

/// <summary>
/// Defines optional behaviors applied by <see cref="IFileManagerMessenger"/> registration overloads.
/// </summary>
public enum Options
{
    /// <summary>
    /// Invokes the message handler on the same thread used by the underlying messenger.
    /// </summary>
    None = 0,

    /// <summary>
    /// Requests UI-thread handler delivery when the concrete messenger implementation has a UI dispatcher.
    /// </summary>
    DispatchToUiThread = 1,
}
