using WinUiFileManager.Application.Messaging;

namespace WinUiFileManager.Application.Startup;

/// <summary>
/// Broadcast once when startup data finishes loading. Sent by the startup/bootstrap code and handled by
/// the shell (<c>MainShellViewModel</c>) to initialize panes, volumes, and layout from
/// <see cref="AppStartupData"/>.
/// </summary>
/// <param name="StartupData">The aggregated startup payload.</param>
public sealed record AppStartupDataLoadedMessage(AppStartupData StartupData) : IFileManagerMessengerMessage;
