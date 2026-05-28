using WinUiFileManager.Application.Messaging;

namespace WinUiFileManager.Application.Startup;

public sealed record AppStartupDataLoadedMessage(AppStartupData StartupData) : IFileManagerMessengerMessage;
