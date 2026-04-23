using System.Diagnostics;
using Microsoft.Win32.SafeHandles;
using TUnit.Core;
using WinUiFileManager.Infrastructure.FileSystem;
using WinUiFileManager.Infrastructure.Tests.Fixtures;
using WinUiFileManager.Interop.Adapters;

namespace WinUiFileManager.Infrastructure.Tests.Scenarios;

public sealed class NtfsFileIdentityServiceTests
{
    [Test]
    public async Task Test_GetStreamDiagnosticsAsync_DoesNotLeakHandlesAcrossConcurrentCalls()
    {
        using var fixture = new NtfsTempDirectoryFixture();
        var filePath = fixture.CreateFile("streamed.txt", sizeInBytes: 1024);
        await File.WriteAllTextAsync($"{filePath}:meta", "one");
        await File.WriteAllTextAsync($"{filePath}:info", "two");

        var sut = new NtfsFileIdentityService(
            new FileIdentityInterop(new RestartManagerInterop(), new ShellInterop()),
            new CloudFilesInterop());

        using var process = Process.GetCurrentProcess();
        process.Refresh();
        var baselineHandleCount = process.HandleCount;

        for (var i = 0; i < 10; i++)
        {
            await Task.WhenAll(
                sut.GetStreamDiagnosticsAsync(filePath, CancellationToken.None),
                sut.GetStreamDiagnosticsAsync(filePath, CancellationToken.None));
        }

        process.Refresh();
        await Assert.That(process.HandleCount).IsLessThanOrEqualTo(baselineHandleCount + 32);
    }
}
