using System.Runtime.InteropServices;
using System.Threading;
using TUnit.Core;
using Windows.Win32.Foundation;
using Windows.Win32.Storage.CloudFilters;
using WinUiFileManager.Interop.Adapters;

namespace WinUiFileManager.Interop.Tests.Scenarios;

public sealed class InteropAdapterTests
{
    [Test]
    public async Task Test_ShellInterop_TryGetFileIsInUseCore_FinalReleasesAndMapsProbeDetails()
    {
        var releasedCount = 0;

        var result = ShellInterop.TryGetFileIsInUseCore(
            static _ => (0, new FakeFileIsInUseAdapter(new object(), "notepad.exe", 1, 0x0003u)),
            _ =>
            {
                releasedCount++;
                return 1;
            },
            static () => ApartmentState.STA,
            @"C:\temp\file.txt");

        await Assert.That(result.HResult).IsEqualTo(0);
        await Assert.That(result.AppName).IsEqualTo("notepad.exe");
        await Assert.That(result.Usage).IsEqualTo("Editing");
        await Assert.That(result.CanSwitchTo).IsTrue();
        await Assert.That(result.CanClose).IsTrue();
        await Assert.That(releasedCount).IsEqualTo(1);
    }

    [Test]
    public async Task Test_RestartManagerInterop_StartSessionCore_UsesWritableZeroedBuffer()
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

        await Assert.That(result).IsEqualTo(7);
        await Assert.That(sessionHandle).IsEqualTo((uint)42);
    }

    [Test]
    public async Task Test_RestartManagerInterop_RegisterResourcesCore_PinsAndPassesPaths()
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

        await Assert.That(result).IsEqualTo(19);
        await Assert.That(firstPath).IsEqualTo(@"C:\temp\a.txt");
        await Assert.That(secondPath).IsEqualTo(@"C:\temp\b.txt");
    }

    [Test]
    public async Task Test_CloudFilesInterop_GetPlaceholderStateCore_ReturnsUnderlyingFlags()
    {
        var result = CloudFilesInterop.GetPlaceholderStateCore(
            static (_, _) => (CF_PLACEHOLDER_STATE)0x0018u,
            0x20,
            0x9000001A);

        await Assert.That(result).IsEqualTo((uint)0x0018);
    }

    private sealed class FakeFileIsInUseAdapter : ShellInterop.IFileIsInUseAdapter
    {
        private readonly string _appName;
        private readonly uint _capabilities;
        private readonly object _innerObject;
        private readonly int _usage;

        public FakeFileIsInUseAdapter(object innerObject, string appName, int usage, uint capabilities)
        {
            _innerObject = innerObject;
            _appName = appName;
            _usage = usage;
            _capabilities = capabilities;
        }

        public object InnerObject => _innerObject;

        public int GetAppName(out IntPtr appName)
        {
            appName = Marshal.StringToCoTaskMemUni(_appName);
            return 0;
        }

        public int GetUsage(out int usage)
        {
            usage = _usage;
            return 0;
        }

        public int GetCapabilities(out uint capabilities)
        {
            capabilities = _capabilities;
            return 0;
        }
    }
}
