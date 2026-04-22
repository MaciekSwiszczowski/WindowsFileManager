using TUnit.Core;
using WinUiFileManager.Interop.SafeHandles;

namespace WinUiFileManager.Interop.Tests.Scenarios;

public sealed class SafeFindFilesHandleTests
{
    [Test]
    public async Task Test_Dispose_CallsFindCloseExactlyOnce()
    {
        var releaseCount = 0;
#pragma warning disable IDISP017 // This test intentionally calls Dispose multiple times to verify SafeHandle release idempotency.
        var handle = new SafeFindFilesHandle(
            new IntPtr(123),
            ownsHandle: true,
            _ =>
            {
                releaseCount++;
                return true;
            });

        try
        {
            handle.Dispose();
            handle.Dispose();
        }
        finally
        {
            handle.Dispose();
        }
#pragma warning restore IDISP017

        await Assert.That(releaseCount).IsEqualTo(1);
    }
}
