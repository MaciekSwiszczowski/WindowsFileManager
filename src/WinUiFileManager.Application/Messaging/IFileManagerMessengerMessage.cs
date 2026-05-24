namespace WinUiFileManager.Application.Messaging;

public interface IFileManagerMessengerMessage;

public interface IIdentityMessage : IFileManagerMessengerMessage
{
    public Identity Identity { get; }
}
