using WinUiFileManager.Application.Abstractions;

namespace WinUiFileManager.Infrastructure.Services;

internal sealed class SystemTimeProvider : Application.Abstractions.ITimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
