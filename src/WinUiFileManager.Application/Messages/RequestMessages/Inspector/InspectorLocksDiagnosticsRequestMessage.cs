using CommunityToolkit.Mvvm.Messaging.Messages;
using WinUiFileManager.Application.Messaging;

namespace WinUiFileManager.Application.Messages.RequestMessages.Inspector;

public sealed class InspectorLocksDiagnosticsRequestMessage : AsyncRequestMessage<FileLockDiagnostics>, IFileManagerMessengerMessage
{
    public InspectorLocksDiagnosticsRequestMessage(NormalizedPath path, CancellationToken cancellationToken)
    {
        Path = path;
        CancellationToken = cancellationToken;
    }

    public NormalizedPath Path { get; }

    public CancellationToken CancellationToken { get; }
}
