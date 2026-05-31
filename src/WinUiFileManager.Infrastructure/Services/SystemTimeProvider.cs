using WinUiFileManager.Application.Abstractions;

namespace WinUiFileManager.Infrastructure.Services;

/// <summary>
/// Production clock that reads the real system time. Infrastructure implementation of the Application-layer
/// <see cref="Application.Abstractions.ITimeProvider"/>, injected so time-dependent code can be unit-tested with a
/// fake clock instead of <see cref="DateTime.UtcNow"/> directly.
/// </summary>
internal sealed class SystemTimeProvider : Application.Abstractions.ITimeProvider
{
    /// <summary>The current UTC time from the system clock.</summary>
    public DateTime UtcNow => DateTime.UtcNow;
}
