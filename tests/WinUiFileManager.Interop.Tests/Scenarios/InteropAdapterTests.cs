using System.Runtime.InteropServices;
using Windows.Win32.Foundation;
using Windows.Win32.Storage.CloudFilters;
using WinUiFileManager.Interop.Adapters;

namespace WinUiFileManager.Interop.Tests.Scenarios;

public sealed class InteropAdapterTests
{
    [Fact]
    public void RestartManagerInterop_StartSessionCore_UsesWritableZeroedBuffer()
    {
        static unsafe (int Result, uint SessionHandle) Execute()
        {
            var result = RestartManagerInterop.StartSessionCore(
                static (sessionHandle, sessionKey) =>
                {
                    *sessionHandle = 42;
                    var firstCharWasZero = sessionKey[0] == '\0';
                    sessionKey[0] = 'A';
                    return firstCharWasZero ? 7 : 8;
                },
                out var sessionHandle);

            return (result, sessionHandle);
        }

        var (result, sessionHandle) = Execute();

        Assert.Equal(7, result);
        Assert.Equal((uint)42, sessionHandle);
    }

    [Fact]
    public void RestartManagerInterop_RegisterResourcesCore_PinsAndPassesPaths()
    {
        static unsafe (int Result, string? FirstPath, string? SecondPath) Execute()
        {
            string? firstPath = null;
            string? secondPath = null;

            var result = RestartManagerInterop.RegisterResourcesCore(
                (sessionHandle, resourceCount, resources) =>
                {
                    if (sessionHandle != 5 || resourceCount != 2)
                    {
                        return -1;
                    }

                    firstPath = Marshal.PtrToStringUni((nint)resources[0].Value);
                    secondPath = Marshal.PtrToStringUni((nint)resources[1].Value);
                    return 19;
                },
                5,
                [@"C:\temp\a.txt", @"C:\temp\b.txt"]);

            return (result, firstPath, secondPath);
        }

        var (result, firstPath, secondPath) = Execute();

        Assert.Equal(19, result);
        Assert.Equal(@"C:\temp\a.txt", firstPath);
        Assert.Equal(@"C:\temp\b.txt", secondPath);
    }

    [Fact]
    public void CloudFilesInterop_GetPlaceholderStateCore_ReturnsUnderlyingFlags()
    {
        var result = CloudFilesInterop.GetPlaceholderStateCore(
            static (_, _) => (CF_PLACEHOLDER_STATE)0x0018u,
            0x20,
            0x9000001A);

        Assert.Equal((uint)0x0018, result);
    }
}
