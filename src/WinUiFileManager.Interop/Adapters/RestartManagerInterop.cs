using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.RestartManager;

namespace WinUiFileManager.Interop.Adapters;

internal sealed class RestartManagerInterop : IRestartManagerInterop
{
    private const int CchRmSessionKey = 33;

    public unsafe int StartSession(out uint sessionHandle)
    {
        return StartSessionCore(StartSessionNative, out sessionHandle);
    }

    public unsafe int RegisterResources(uint sessionHandle, string[] resources)
    {
        return RegisterResourcesCore(RegisterResourcesNative, sessionHandle, resources);
    }

    public unsafe int GetList(
        uint sessionHandle,
        out uint processInfoNeeded,
        ref uint processInfo,
        RestartManagerProcessInfo[]? processInfos,
        out uint rebootReasons)
    {
        if (processInfos is null || processInfos.Length == 0)
        {
            return GetListWithoutProcessesCore(
                GetListNative,
                sessionHandle,
                out processInfoNeeded,
                ref processInfo,
                out rebootReasons);
        }

        Span<RM_PROCESS_INFO> nativeInfos = processInfos.Length <= 16
            ? stackalloc RM_PROCESS_INFO[processInfos.Length]
            : new RM_PROCESS_INFO[processInfos.Length];

        var result = GetListWithProcessesCore(
            GetListNative,
            sessionHandle,
            out processInfoNeeded,
            ref processInfo,
            nativeInfos,
            out rebootReasons);

        if (result == 0)
        {
            MapProcessInfosCore(nativeInfos, processInfos, processInfo);
        }

        return result;
    }

    public int EndSession(uint sessionHandle)
    {
        return EndSessionCore(static handle => (int)PInvoke.RmEndSession(handle), sessionHandle);
    }

    private static unsafe int StartSessionNative(uint* sessionHandle, char* sessionKey)
    {
        return (int)PInvoke.RmStartSession(sessionHandle, 0, sessionKey);
    }

    private static unsafe int RegisterResourcesNative(uint sessionHandle, uint resourceCount, PCWSTR* resources)
    {
        return (int)PInvoke.RmRegisterResources(sessionHandle, resourceCount, resources, 0, null, 0, null);
    }

    private static unsafe int GetListNative(
        uint sessionHandle,
        uint* processInfoNeeded,
        uint* processInfo,
        RM_PROCESS_INFO* processInfos,
        uint* rebootReasons)
    {
        return (int)PInvoke.RmGetList(sessionHandle, processInfoNeeded, processInfo, processInfos, rebootReasons);
    }

    internal static unsafe int StartSessionCore(
        StartSessionDelegate startSession,
        out uint sessionHandle)
    {
        sessionHandle = 0;
        Span<char> sessionKeyBuffer = stackalloc char[CchRmSessionKey];
        sessionKeyBuffer.Clear();

        fixed (uint* sessionHandlePointer = &sessionHandle)
        fixed (char* sessionKeyPointer = sessionKeyBuffer)
        {
            return startSession(sessionHandlePointer, sessionKeyPointer);
        }
    }

    internal static unsafe int RegisterResourcesCore(
        RegisterResourcesDelegate registerResources,
        uint sessionHandle,
        string[] resources)
    {
        if (resources.Length == 0)
        {
            return registerResources(sessionHandle, 0, null);
        }

        var pins = new GCHandle[resources.Length];
        Span<PCWSTR> resourcePointers = resources.Length <= 16
            ? stackalloc PCWSTR[resources.Length]
            : new PCWSTR[resources.Length];

        try
        {
            for (var i = 0; i < resources.Length; i++)
            {
                pins[i] = GCHandle.Alloc(resources[i], GCHandleType.Pinned);
                resourcePointers[i] = new PCWSTR((char*)pins[i].AddrOfPinnedObject());
            }

            fixed (PCWSTR* resourcePointersPointer = resourcePointers)
            {
                return registerResources(sessionHandle, (uint)resourcePointers.Length, resourcePointersPointer);
            }
        }
        finally
        {
            foreach (var pin in pins)
            {
                if (pin.IsAllocated)
                {
                    pin.Free();
                }
            }
        }
    }

    internal static unsafe int GetListWithoutProcessesCore(
        GetListDelegate getList,
        uint sessionHandle,
        out uint processInfoNeeded,
        ref uint processInfo,
        out uint rebootReasons)
    {
        processInfoNeeded = 0;
        rebootReasons = 0;

        fixed (uint* processInfoNeededPointer = &processInfoNeeded)
        fixed (uint* processInfoPointer = &processInfo)
        fixed (uint* rebootReasonsPointer = &rebootReasons)
        {
            return getList(sessionHandle, processInfoNeededPointer, processInfoPointer, null, rebootReasonsPointer);
        }
    }

    internal static unsafe int GetListWithProcessesCore(
        GetListDelegate getList,
        uint sessionHandle,
        out uint processInfoNeeded,
        ref uint processInfo,
        Span<RM_PROCESS_INFO> processInfos,
        out uint rebootReasons)
    {
        processInfoNeeded = 0;
        rebootReasons = 0;

        fixed (uint* processInfoNeededPointer = &processInfoNeeded)
        fixed (uint* processInfoPointer = &processInfo)
        fixed (uint* rebootReasonsPointer = &rebootReasons)
        fixed (RM_PROCESS_INFO* processInfosPointer = processInfos)
        {
            return getList(sessionHandle, processInfoNeededPointer, processInfoPointer, processInfosPointer, rebootReasonsPointer);
        }
    }

    internal static void MapProcessInfosCore(
        ReadOnlySpan<RM_PROCESS_INFO> source,
        RestartManagerProcessInfo[] destination,
        uint processInfoCount)
    {
        var count = (int)Math.Min(processInfoCount, (uint)Math.Min(source.Length, destination.Length));
        for (var i = 0; i < count; i++)
        {
            destination[i] = new RestartManagerProcessInfo(
                source[i].Process.dwProcessId <= int.MaxValue ? (int)source[i].Process.dwProcessId : 0,
                source[i].strAppName.ToString(),
                source[i].strServiceShortName.ToString());
        }
    }

    internal static int EndSessionCore(Func<uint, int> endSession, uint sessionHandle)
    {
        return endSession(sessionHandle);
    }

    internal unsafe delegate int StartSessionDelegate(uint* sessionHandle, char* sessionKey);

    internal unsafe delegate int RegisterResourcesDelegate(
        uint sessionHandle,
        uint resourceCount,
        PCWSTR* resources);

    internal unsafe delegate int GetListDelegate(
        uint sessionHandle,
        uint* processInfoNeeded,
        uint* processInfo,
        RM_PROCESS_INFO* processInfos,
        uint* rebootReasons);
}
