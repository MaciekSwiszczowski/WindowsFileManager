using WinUiFileManager.Application.Abstractions;

namespace WinUiFileManager.Infrastructure.Services;

public sealed class SystemTimeProvider : Application.Abstractions.ITimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
