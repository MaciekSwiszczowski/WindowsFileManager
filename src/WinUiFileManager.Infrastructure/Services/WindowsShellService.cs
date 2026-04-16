using System.Diagnostics;
using Microsoft.Extensions.Logging;
using WinUiFileManager.Application.Abstractions;
using WinUiFileManager.Domain.ValueObjects;

namespace WinUiFileManager.Infrastructure.Services;

public sealed class WindowsShellService : IShellService
{
    private readonly ILogger<WindowsShellService> _logger;

    public WindowsShellService(ILogger<WindowsShellService> logger)
    {
        _logger = logger;
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
}
