using System.Diagnostics;
using Microsoft.Extensions.Logging;
using WinUiFileManager.Application.Abstractions;
using WinUiFileManager.Domain.ValueObjects;
using WinUiFileManager.Interop.Adapters;

namespace WinUiFileManager.Infrastructure.Services;

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

    public Task OpenWithDefaultAppAsync(NormalizedPath path, CancellationToken ct)
    {
        return Task.Run(() =>
        {
            try
            {
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

    public Task ShowPropertiesAsync(NormalizedPath path, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        try
        {
            var displayPath = path.DisplayPath;
            if (_shellInterop.ShowObjectProperties(displayPath, out var lastError))
            {
                return Task.CompletedTask;
            }

            _logger.LogWarning(
                "SHObjectProperties failed for {Path}. Win32Error={Win32Error}. Falling back to ShellExecuteEx.",
                path.DisplayPath,
                lastError);

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
