#pragma warning disable RS0030 // Legacy DllImport declarations stay quarantined here until the CsWin32 migration batch replaces them.
using System.Diagnostics;
using System.Runtime.InteropServices;
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

    public Task ShowPropertiesAsync(NormalizedPath path, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        try
        {
            var displayPath = path.DisplayPath;
            const uint shopFilePath = 0x00000002;
            if (SHObjectProperties(
                IntPtr.Zero,
                shopFilePath,
                displayPath,
                null))
            {
                return Task.CompletedTask;
            }

            var lastError = Marshal.GetLastWin32Error();
            _logger.LogWarning(
                "SHObjectProperties failed for {Path}. Win32Error={Win32Error}. Falling back to ShellExecuteEx.",
                path.DisplayPath,
                lastError);

            var shouldUninitializeCom = TryInitializeStaCom();

            try
            {
                var executeInfo = new ShellExecuteInfo
                {
                    cbSize = Marshal.SizeOf<ShellExecuteInfo>(),
                    fMask = 0x0000000C,
                    hwnd = IntPtr.Zero,
                    lpVerb = "properties",
                    lpFile = displayPath,
                    nShow = 5
                };

                if (!ShellExecuteExW(ref executeInfo))
                {
                    _logger.LogWarning(
                        "ShellExecuteEx(properties) failed for {Path}. Win32Error={Win32Error}. HInstApp={HInstApp}",
                        displayPath,
                        Marshal.GetLastWin32Error(),
                        executeInfo.hInstApp);
                }
            }
            finally
            {
                if (shouldUninitializeCom)
                {
                    CoUninitialize();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to show shell properties for {Path}", path.DisplayPath);
        }

        return Task.CompletedTask;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SHObjectProperties(
        IntPtr hwndOwner,
        uint shopObjectType,
        string pszObjectName,
        string? pszPropertyPage);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShellExecuteExW(ref ShellExecuteInfo lpExecInfo);

    [DllImport("ole32.dll")]
    private static extern int CoInitializeEx(IntPtr pvReserved, uint dwCoInit);

    [DllImport("ole32.dll")]
    private static extern void CoUninitialize();

    private static bool TryInitializeStaCom()
    {
        const uint coInitApartmentThreaded = 0x2;
        const int rpcEChangedMode = unchecked((int)0x80010106);

        var hr = CoInitializeEx(IntPtr.Zero, coInitApartmentThreaded);
        return hr switch
        {
            0 => true,
            1 => true,
            rpcEChangedMode => false,
            _ => false
        };
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShellExecuteInfo
    {
        public int cbSize;
        public uint fMask;
        public IntPtr hwnd;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? lpVerb;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? lpFile;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? lpParameters;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? lpDirectory;
        public int nShow;
        public IntPtr hInstApp;
        public IntPtr lpIDList;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? lpClass;
        public IntPtr hkeyClass;
        public uint dwHotKey;
        public IntPtr hIconOrMonitor;
        public IntPtr hProcess;
    }
}
