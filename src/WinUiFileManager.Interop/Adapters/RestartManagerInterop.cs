using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.RestartManager;

namespace WinUiFileManager.Interop.Adapters;

/// <summary>
/// CsWin32-backed adapter over the Windows Restart Manager (<c>RmStartSession</c>/<c>RmRegisterResources</c>/
/// <c>RmGetList</c>/<c>RmEndSession</c>), used to discover which processes currently hold a lock on a file so the
/// "file in use" diagnostics can name the offending applications. Implements <see cref="IRestartManagerInterop"/>.
/// </summary>
/// <remarks>
/// Lifetime: a session opened by <see cref="StartSession"/> MUST be closed by <see cref="EndSession"/> (the OS
/// keeps the session alive otherwise). All methods return the native Win32 result code (<c>0</c> = <c>ERROR_SUCCESS</c>),
/// not a managed exception. The static <c>*Core</c> helpers isolate the pointer-pinning and span marshalling from
/// the actual P/Invoke (passed as a delegate) so the marshalling can be unit-tested without the real Restart Manager.
/// </remarks>
internal sealed class RestartManagerInterop : IRestartManagerInterop
{
    // CCH_RM_SESSION_KEY + 1: required buffer length (in chars) for the session key string RmStartSession fills in.
    private const int CchRmSessionKey = 33;

    /// <summary>Opens a Restart Manager session.</summary>
    /// <param name="sessionHandle">Receives the session handle to pass to subsequent calls.</param>
    /// <returns>Win32 result code (<c>0</c> on success). The session must later be closed via <see cref="EndSession"/>.</returns>
    public unsafe int StartSession(out uint sessionHandle)
    {
        return StartSessionCore(StartSessionNative, out sessionHandle);
    }

    /// <summary>Registers file paths whose locking processes should be enumerated.</summary>
    /// <param name="sessionHandle">An open session handle from <see cref="StartSession"/>.</param>
    /// <param name="resources">Full file paths to register (may be empty).</param>
    /// <returns>Win32 result code (<c>0</c> on success).</returns>
    public unsafe int RegisterResources(uint sessionHandle, string[] resources)
    {
        return RegisterResourcesCore(RegisterResourcesNative, sessionHandle, resources);
    }

    /// <summary>
    /// Retrieves the processes affected by (i.e. locking) the registered resources. Two-phase per the Win32
    /// contract: call with a <see langword="null"/>/empty <paramref name="processInfos"/> to learn the required
    /// count via <paramref name="processInfoNeeded"/>, then call again with a buffer of that size.
    /// </summary>
    /// <param name="sessionHandle">An open session handle.</param>
    /// <param name="processInfoNeeded">Receives the number of process-info entries the OS wants to return.</param>
    /// <param name="processInfo">In: capacity of <paramref name="processInfos"/>. Out: number actually written.</param>
    /// <param name="processInfos">Destination buffer, or <see langword="null"/>/empty for the count-probe phase.</param>
    /// <param name="rebootReasons">Receives the <c>RM_REBOOT_REASON</c> flags.</param>
    /// <returns>Win32 result code (<c>0</c> on success; <c>ERROR_MORE_DATA</c> when the buffer was too small).</returns>
    public unsafe int GetList(
        uint sessionHandle,
        out uint processInfoNeeded,
        ref uint processInfo,
        RestartManagerProcessInfo[]? processInfos,
        out uint rebootReasons)
    {
        if (processInfos is null || processInfos.Length == 0)
        {
            // Count-probe phase: ask the OS how many entries exist without providing a buffer.
            return GetListWithoutProcessesCore(
                GetListNative,
                sessionHandle,
                out processInfoNeeded,
                ref processInfo,
                out rebootReasons);
        }

        // Marshal into native RM_PROCESS_INFO first, then project to the managed record. Stack-allocate for small
        // counts (<=16) to avoid a heap allocation on the common case.
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

        // Only project entries the OS actually populated (processInfo holds the written count) and only on success.
        if (result == 0)
        {
            MapProcessInfosCore(nativeInfos, processInfos, processInfo);
        }

        return result;
    }

    /// <summary>Closes a Restart Manager session opened by <see cref="StartSession"/>.</summary>
    /// <param name="sessionHandle">The session handle to close.</param>
    /// <returns>Win32 result code (<c>0</c> on success).</returns>
    public int EndSession(uint sessionHandle)
    {
        return EndSessionCore(static handle => (int)PInvoke.RmEndSession(handle), sessionHandle);
    }

    // Thin P/Invoke wrappers. These are passed as delegates into the *Core helpers so the marshalling logic can be
    // unit-tested with fakes; the dwSessionFlags / array-count "0"/null arguments below are the documented
    // "no extra options" values for these Restart Manager APIs.
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

    // Pins the out session handle and a session-key scratch buffer, then invokes the (real or fake) native call.
    internal static unsafe int StartSessionCore(
        StartSessionDelegate startSession,
        out uint sessionHandle)
    {
        sessionHandle = 0;
        Span<char> sessionKeyBuffer = stackalloc char[CchRmSessionKey];
        sessionKeyBuffer.Clear(); // RmStartSession expects a zero-initialized key buffer.

        fixed (uint* sessionHandlePointer = &sessionHandle)
        fixed (char* sessionKeyPointer = sessionKeyBuffer)
        {
            return startSession(sessionHandlePointer, sessionKeyPointer);
        }
    }

    // Builds the native array of wide-string pointers RmRegisterResources expects. Each managed string is pinned
    // individually (GCHandle) so the GC cannot move it while the native pointer array references it; the finally
    // block frees every pin even on exception — failing to free would leak pinned strings and fragment the heap.
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
            // Unpin every successfully-allocated handle (the array is fully allocated, but guard with IsAllocated
            // to be robust if a future change makes allocation partial).
            foreach (var pin in pins)
            {
                if (pin.IsAllocated)
                {
                    pin.Free();
                }
            }
        }
    }

    // Count-probe overload: passes a null process-info array so RmGetList only reports how many entries exist.
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

    // Buffer overload: pins all out params and the process-info span so RmGetList can fill them in one call.
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

    // Projects native RM_PROCESS_INFO entries to the managed record. `count` is clamped to the minimum of the
    // OS-reported count and both buffer lengths so a mismatched/oversized count can never read past either array.
    internal static void MapProcessInfosCore(
        ReadOnlySpan<RM_PROCESS_INFO> source,
        RestartManagerProcessInfo[] destination,
        uint processInfoCount)
    {
        var count = (int)Math.Min(processInfoCount, (uint)Math.Min(source.Length, destination.Length));
        for (var i = 0; i < count; i++)
        {
            destination[i] = new RestartManagerProcessInfo(
                // dwProcessId is a uint; clamp to 0 if it cannot fit in the managed int ProcessId (defensive — real PIDs fit).
                source[i].Process.dwProcessId <= int.MaxValue ? (int)source[i].Process.dwProcessId : 0,
                source[i].strAppName.ToString(),
                source[i].strServiceShortName.ToString());
        }
    }

    internal static int EndSessionCore(Func<uint, int> endSession, uint sessionHandle)
    {
        return endSession(sessionHandle);
    }

    /// <summary>Test seam delegate mirroring the native <c>RmStartSession</c> signature.</summary>
    internal unsafe delegate int StartSessionDelegate(uint* sessionHandle, char* sessionKey);

    /// <summary>Test seam delegate mirroring the native <c>RmRegisterResources</c> signature.</summary>
    internal unsafe delegate int RegisterResourcesDelegate(
        uint sessionHandle,
        uint resourceCount,
        PCWSTR* resources);

    /// <summary>Test seam delegate mirroring the native <c>RmGetList</c> signature.</summary>
    internal unsafe delegate int GetListDelegate(
        uint sessionHandle,
        uint* processInfoNeeded,
        uint* processInfo,
        RM_PROCESS_INFO* processInfos,
        uint* rebootReasons);
}
