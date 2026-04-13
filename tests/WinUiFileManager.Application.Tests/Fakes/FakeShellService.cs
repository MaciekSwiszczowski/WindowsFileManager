using WinUiFileManager.Application.Abstractions;
using WinUiFileManager.Domain.ValueObjects;

namespace WinUiFileManager.Application.Tests.Fakes;

public sealed class FakeShellService : IShellService
{
    public NormalizedPath? LastOpenedPath { get; private set; }

    public Task OpenWithDefaultAppAsync(NormalizedPath path, CancellationToken ct)
    {
        LastOpenedPath = path;
        return Task.CompletedTask;
    }
}
