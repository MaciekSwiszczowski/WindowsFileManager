using System.Diagnostics;
using Microsoft.Extensions.Logging;
using WinUiFileManager.Application.Abstractions;
using WinUiFileManager.Application.FileEntries;
using WinUiFileManager.Interop.Adapters;

namespace WinUiFileManager.Infrastructure.Services;

/// <summary>
/// Shell integration service: opens files with their default application and shows the native Windows property
/// sheet. Infrastructure implementation of <see cref="IShellService"/>, delegating COM/Shell calls to
/// <see cref="IShellInterop"/>. Failures are logged and swallowed (these are best-effort UX actions, not data
/// operations).
/// </summary>
internal sealed class WindowsShellService : IShellService
{
    private readonly ILogger<WindowsShellService> _logger;
    private readonly IShellInterop _shellInterop;

    public WindowsShellService(
        ILogger<WindowsShellService> logger,
        IShellInterop shellInterop)
    {
        _logger = logger;
        _shellInterop = shellInterop;
    }

    /// <summary>Launches <paramref name="path"/> with the OS default handler via <c>ShellExecute</c> semantics.</summary>
    /// <param name="path">The file/folder to open.</param>
    /// <param name="ct">Used to schedule/cancel the background launch.</param>
    /// <returns>A task that completes once the launch attempt has run (failures are logged, not thrown).</returns>
    public Task OpenWithDefaultAppAsync(NormalizedPath path, CancellationToken ct)
    {
        // Offloaded to a pool thread: UseShellExecute=true can block (e.g. resolving handlers / showing UI), and we
        // must not stall the caller's thread.
        return Task.Run(() =>
        {
            try
            {
                // UseShellExecute=true makes Windows pick the registered default app rather than exec'ing a process.
                var psi = new ProcessStartInfo
                {
                    FileName = path.Value,
                    UseShellExecute = true
                };

                using var process = Process.Start(psi);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to open file with default app: {Path}", path.DisplayPath);
            }
        }, ct);
    }

    /// <summary>
    /// Shows the native property sheet for <paramref name="path"/>, preferring <c>SHObjectProperties</c> and
    /// falling back to the <c>ShellExecuteEx</c> "properties" verb if that fails.
    /// </summary>
    /// <param name="path">The file/folder whose properties to show.</param>
    /// <param name="ct">Checked before starting; the work itself is synchronous shell interop.</param>
    /// <returns>A completed task (failures are logged, not thrown).</returns>
    /// <remarks>
    /// COM HAZARD (see <see cref="ShellInterop.TryInitializeStaCom"/>): the fallback only calls
    /// <see cref="IShellInterop.UninitializeCom"/> when <see cref="IShellInterop.TryInitializeStaCom"/> returned
    /// <see langword="true"/>. Because that method reports S_OK and S_FALSE identically, an S_FALSE (COM already
    /// initialized) result here can still drive an over-release of the apartment — a known issue tracked in the
    /// Interop layer, not worked around here.
    /// </remarks>
    public Task ShowPropertiesAsync(NormalizedPath path, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        try
        {
            var displayPath = path.DisplayPath;
            // Fast path: SHObjectProperties handles most file-system objects directly.
            if (_shellInterop.ShowObjectProperties(displayPath, out var lastError))
            {
                return Task.CompletedTask;
            }

            _logger.LogWarning(
                "SHObjectProperties failed for {Path}. Win32Error={Win32Error}. Falling back to ShellExecuteEx.",
                path.DisplayPath,
                lastError);

            // The fallback verb requires an STA COM apartment. Track whether we should uninitialize afterwards.
            var shouldUninitializeCom = _shellInterop.TryInitializeStaCom();

            try
            {
                var result = _shellInterop.ExecutePropertiesVerb(displayPath);
                if (!result.Succeeded)
                {
                    _logger.LogWarning(
                        "ShellExecuteEx(properties) failed for {Path}. Win32Error={Win32Error}. HInstApp={HInstApp}",
                        displayPath,
                        result.LastError,
                        result.HInstApp);
                }
            }
            finally
            {
                // Balanced against TryInitializeStaCom — but see the COM hazard in the method remarks.
                if (shouldUninitializeCom)
                {
                    _shellInterop.UninitializeCom();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to show shell properties for {Path}", path.DisplayPath);
        }

        return Task.CompletedTask;
    }
}
