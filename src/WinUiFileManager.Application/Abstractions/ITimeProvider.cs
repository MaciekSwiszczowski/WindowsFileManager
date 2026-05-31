namespace WinUiFileManager.Application.Abstractions;

/// <summary>
/// Abstracts the system clock so time-dependent logic stays testable (tests inject a fake).
/// Implemented in Infrastructure over the real clock.
/// </summary>
public interface ITimeProvider
{
    /// <summary>The current UTC time.</summary>
    DateTime UtcNow { get; }
}
