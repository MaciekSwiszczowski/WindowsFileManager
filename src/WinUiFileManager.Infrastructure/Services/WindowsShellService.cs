using System.Diagnostics;
using WinUiFileManager.Application.Abstractions;
using WinUiFileManager.Domain.ValueObjects;

namespace WinUiFileManager.Infrastructure.Services;

public sealed class WindowsShellService : IShellService
{
    public Task OpenWithDefaultAppAsync(NormalizedPath path, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = path.Value,
            UseShellExecute = true
        };
        Process.Start(psi);
        return Task.CompletedTask;
    }
}
