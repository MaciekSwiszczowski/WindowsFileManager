# Spec: Long-Path Support

Scope: make the app a first-class tool for working with Windows long paths (> 260 UTF-16 code units) while honestly representing the parts of Windows that cannot follow. No silent failures; every unsupported action is visibly disabled with a tooltip that names the limit.

Assumes `SPEC_TOOLING_AND_ANALYZERS.md`, `SPEC_BUG_FIXES.md`, and `SPEC_NUGET_MODERNIZATION.md` are in flight. Specifically: this spec supersedes bug B2 (which in isolation said "use `DisplayPath` for ShellExecute") — under the capability model, ShellExecute still uses `DisplayPath`, but the command is also hidden or disabled when the selection is a long path.

## 1. Goal and non-goals

**Goal.** A developer can navigate into, enumerate, select, copy, move, rename, delete, observe, and inspect files on any path up to ~32 767 UTF-16 code units on an NTFS volume. Features that fundamentally cannot work for long paths (shell open, Explorer reveal, shell Properties dialog, drag-drop to Explorer, file-association-based thumbnails) are disabled with an explanation instead of producing confusing errors.

**Non-goals.**
- Making Windows Explorer, shell COM, WinRT Storage, or third-party tools long-path-aware. Out of our control.
- Fixing `longPathAware` / registry misconfiguration on the user's machine. Detect and inform.
- Supporting UNC long paths (`\\?\UNC\server\share\…`) in v1. Flag in path validation; defer full support to a later spec.

## 2. Path-length classification

Define a single type that classifies every path the app handles.

### 2.1. `PathLength` enum

New file: `src/WinUiFileManager.Domain/ValueObjects/PathLength.cs`

```csharp
namespace WinUiFileManager.Domain.ValueObjects;

public enum PathLength
{
    /// <summary>≤ MAX_PATH (259 UTF-16 code units of path + trailing NUL).
    /// Every Windows API works for these paths.</summary>
    Standard = 0,

    /// <summary>260–32 767 UTF-16 code units. Requires manifest `longPathAware` + registry
    /// `LongPathsEnabled`. Win32 file APIs and .NET I/O work; Shell, WinRT Storage, and
    /// shell-open flows do not.</summary>
    Extended = 1,

    /// <summary>&gt; 32 767 UTF-16 code units (theoretical cap). Rejected outright.</summary>
    OutOfRange = 2,
}
```

### 2.2. `NormalizedPath` classification

Extend `src/WinUiFileManager.Domain/ValueObjects/NormalizedPath.cs` with a computed property:

```csharp
// Windows' practical limits.
public const int MaxStandardPathLength = 259;   // 259 UTF-16 + NUL = 260
public const int MaxExtendedPathLength = 32_767; // kernel32 hard cap

public PathLength Length
{
    get
    {
        var length = DisplayPath.Length;
        if (length > MaxExtendedPathLength) return PathLength.OutOfRange;
        return length > MaxStandardPathLength ? PathLength.Extended : PathLength.Standard;
    }
}

public bool IsLongPath => Length == PathLength.Extended;
```

`Length` is measured against `DisplayPath.Length`, not `Value.Length`. The 4-char `\\?\` prefix is an internal routing detail and does not count toward the user-observed limit.

`NormalizedPath.FromUserInput` must reject `PathLength.OutOfRange` with a dedicated exception (not `ArgumentException`) so validators at the edge can produce a clean error message.

## 3. Capability matrix (authoritative reference)

This table is the source of truth. It tells engineering which APIs are allowed for which `PathLength`. Every service method listed below must honor these rules.

| Operation | `Standard` | `Extended` | Notes |
|---|---|---|---|
| `CreateFileW` via CsWin32 with `\\?\` form | ✓ | ✓ | Pass `NormalizedPath.Value`. |
| `FindFirstFileExW` / `FindNextFileW` (directory enumeration) | ✓ | ✓ | Pass `Value`; .NET `FileSystemEnumerable` also works on long paths with manifest+registry. |
| `CopyFile2`, `MoveFileExW`, `DeleteFileW`, `RemoveDirectoryW` | ✓ | ✓ | Pass `Value`. |
| `CreateDirectoryW` | ✓ | ✓ | Pass `Value`. Intermediate directories must already exist — planner already handles this. |
| `FindFirstStreamW` / `FindNextStreamW` (ADS) | ✓ | ✓ | Pass `Value`. |
| `GetFileInformationByHandle[Ex]`, `DeviceIoControl` | ✓ | ✓ | Handle-based — path length irrelevant once opened. |
| `GetFinalPathNameByHandleW` | ✓ | ✓ | Handle-based. |
| `GetVolumeInformationW` | ✓ | ✓ | Accepts `\\?\C:\` root. |
| `System.IO.File.*`, `Directory.*`, `FileInfo`, `DirectoryInfo` on .NET 10 | ✓ | ✓ | Requires `longPathAware` manifest + `LongPathsEnabled` registry value. Pass `Value`. |
| `FileSystemWatcher` | ✓ | ✓ | Construct with `Value`. Events report `FullPath` without the prefix — re-normalize before comparing. |
| `ACL` (`FileSystemSecurity.GetAccessControl()`) | ✓ | ✓ | .NET supports long paths. |
| `Restart Manager` (`RmRegisterResources`) | ✓ | ✗ | Documented MAX_PATH-only per resource. Skip for long paths. |
| `SHObjectProperties` ("Properties" dialog) | ✓ | ✗ | Shell dialog — no long-path support. Hide/disable. |
| `ShellExecuteEx` / `Process.Start(UseShellExecute=true)` (open with default app) | ✓ | ✗ | Shell doesn't resolve associations for `\\?\`. Hide/disable. |
| `SHCreateItemFromParsingName` (used for `IFileIsInUse`) | ✓ | ✗ | Many shell handlers reject long paths; treat as unsupported for `Extended`. |
| `StorageFile.GetFileFromPathAsync` / `StorageFolder.GetFolderFromPathAsync` | ✓ | ✗ | WinRT Storage broker — inconsistent long-path support; treat as unsupported. |
| `StorageFile.GetThumbnailAsync` | ✓ | ✗ | Same as above. |
| `StorageProviderSyncRootManager.GetSyncRootInformationForFolder` | ✓ | ✗ | Same. |
| `explorer.exe /select,"…"` ("Reveal in Explorer") | ✓ | ✗ | Explorer rejects `\\?\`. Hide/disable. |
| `wt.exe -d "…"` ("Open Terminal here") | ✓ | partial | Newer Terminal handles long paths; fall back to disabled if probe fails. |
| `DataPackage.SetStorageItems` (drag-out to Explorer / clipboard file drop) | ✓ | ✗ | Requires shell `IShellItem`. Disable for long selections. |
| `SetClipboardText` for the **path string itself** (Ctrl+Shift+C) | ✓ | ✓ | Pure text — always works. |
| `ContentDialog` (app's own modal UI) | ✓ | ✓ | Our own UI — no shell involvement. |

When in doubt, test on a known long path with `\\?\C:\longpath_test\...\` fixture (see §9); if any step produces `ERROR_PATH_NOT_FOUND`, `ERROR_FILENAME_EXCED_RANGE`, `E_INVALIDARG`, or a silent no-op, mark the API `✗` for `Extended` and add it to §6.

## 4. Capability service

One policy object centralizes the matrix so services, VMs, and bindings all agree.

### 4.1. `IPathCapabilityPolicy`

New file: `src/WinUiFileManager.Application/Abstractions/IPathCapabilityPolicy.cs`

```csharp
namespace WinUiFileManager.Application.Abstractions;

public interface IPathCapabilityPolicy
{
    bool Supports(PathCapability capability, PathLength length);

    bool SupportsAll(PathCapability capability, IEnumerable<NormalizedPath> paths);
}
```

New file: `src/WinUiFileManager.Domain/ValueObjects/PathCapability.cs`

```csharp
namespace WinUiFileManager.Domain.ValueObjects;

public enum PathCapability
{
    // File-system operations (all supported for Extended).
    EnumerateContents,
    CreateEntry,
    DeleteEntry,
    CopyEntry,
    MoveEntry,
    RenameEntry,
    ReadNtfsMetadata,
    WriteNtfsAttributes,
    ObserveChanges,
    ReadAlternateStreams,
    ReadSecurity,

    // Shell / WinRT / external-process operations (disabled for Extended).
    OpenWithDefaultApp,
    ShowShellPropertiesDialog,
    RevealInExplorer,
    OpenInTerminal,
    DragDropToExplorer,
    QueryShellThumbnail,
    QueryCloudStatus,
    QueryInUseLocks,
}
```

### 4.2. `DefaultPathCapabilityPolicy`

New file: `src/WinUiFileManager.Infrastructure/Services/DefaultPathCapabilityPolicy.cs`

```csharp
namespace WinUiFileManager.Infrastructure.Services;

public sealed class DefaultPathCapabilityPolicy : IPathCapabilityPolicy
{
    private static readonly HashSet<PathCapability> ExtendedSupported =
    [
        PathCapability.EnumerateContents,
        PathCapability.CreateEntry,
        PathCapability.DeleteEntry,
        PathCapability.CopyEntry,
        PathCapability.MoveEntry,
        PathCapability.RenameEntry,
        PathCapability.ReadNtfsMetadata,
        PathCapability.WriteNtfsAttributes,
        PathCapability.ObserveChanges,
        PathCapability.ReadAlternateStreams,
        PathCapability.ReadSecurity,
    ];

    public bool Supports(PathCapability capability, PathLength length)
    {
        return length switch
        {
            PathLength.Standard => true,
            PathLength.Extended => ExtendedSupported.Contains(capability),
            _ => false,
        };
    }

    public bool SupportsAll(PathCapability capability, IEnumerable<NormalizedPath> paths)
    {
        foreach (var path in paths)
        {
            if (!Supports(capability, path.Length))
            {
                return false;
            }
        }
        return true;
    }
}
```

Registered as a singleton in `ServiceCollectionExtensions.AddInfrastructureServices`.

Rationale for a data-driven policy (rather than a fan-out of `IXxxService.Supports` methods): a single file lists every API's tolerance. When Windows widens `StorageFile` long-path support (or narrows it), we update one set. Services consult the policy; they do not duplicate the matrix.

## 5. Service-layer changes

### 5.1. `IShellService` gains capability-aware guards

```csharp
// src/WinUiFileManager.Application/Abstractions/IShellService.cs
Task OpenWithDefaultAppAsync(NormalizedPath path, CancellationToken ct);
Task ShowPropertiesAsync(NormalizedPath path, CancellationToken ct);
Task RevealInExplorerAsync(NormalizedPath path, CancellationToken ct);
Task OpenTerminalAsync(NormalizedPath folder, CancellationToken ct);
```

Each implementation in `WindowsShellService` starts with a capability guard:

```csharp
public Task OpenWithDefaultAppAsync(NormalizedPath path, CancellationToken ct)
{
    if (!_capabilities.Supports(PathCapability.OpenWithDefaultApp, path.Length))
    {
        throw new PathLengthNotSupportedException(path, PathCapability.OpenWithDefaultApp);
    }
    // existing body, fixed to use path.DisplayPath (per bug B2)
}
```

New exception type: `src/WinUiFileManager.Domain/Errors/PathLengthNotSupportedException.cs`. Carries the path, the capability, and a localized explanation.

VMs do not rely on the exception — they pre-check the capability and disable the command. The exception is a defense-in-depth guard for tests and mistakes.

### 5.2. `NtfsFileIdentityService` skips unsupported batches

`GetCloudDiagnosticsAsync`, `GetThumbnailDiagnosticsAsync`, and `GetLockDiagnosticsAsync` short-circuit on long paths:

```csharp
public Task<FileCloudDiagnosticsDetails> GetCloudDiagnosticsAsync(string path, CancellationToken ct)
{
    var normalized = NormalizedPath.FromUserInput(path);
    if (!_capabilities.Supports(PathCapability.QueryCloudStatus, normalized.Length))
    {
        return Task.FromResult(FileCloudDiagnosticsDetails.Unsupported);
    }
    // existing body
}
```

`FileCloudDiagnosticsDetails.Unsupported` (and the equivalent sentinels for thumbnail/lock) renders in the Inspector as "Unavailable for extended-length paths". Sentinels are distinct from `.None` (which means "file exists and is not cloud-managed") so the Inspector can pick the right message.

### 5.3. `WindowsFileSystemService`, `WindowsFileOperationService` — no-op

These already work for long paths (they use `NormalizedPath.Value`, i.e., the `\\?\` form). No change required beyond:
- Add an assertion at each entry point: `Debug.Assert(path.Length != PathLength.OutOfRange)`.
- Ensure `WindowsDirectoryChangeStream.CreateAndStart` constructs the watcher with the **display** path when `LongPathsEnabled` is set, and the `\\?\` path otherwise. (FileSystemWatcher on .NET 10 accepts both; test to confirm. If both work, standardize on `DisplayPath` for readability.)

## 6. UI policy

### 6.1. Command-level disable

Every `[RelayCommand]` in `MainShellViewModel` that maps to a shell/WinRT API must expose a `CanExecute` predicate derived from the capability policy. CommunityToolkit MVVM supports this:

```csharp
[RelayCommand(CanExecute = nameof(CanOpenWithDefaultApp))]
private async Task OpenSelectedAsync() { ... }

private bool CanOpenWithDefaultApp()
{
    var path = ActivePane.CurrentItem?.Model.FullPath;
    return path is { } p && _capabilities.Supports(PathCapability.OpenWithDefaultApp, p.Length);
}
```

The toolbar and flyout buttons bind to the generated `*Command`s — WinUI disables the button automatically when `CanExecute` returns `false`.

Re-raise `CanExecuteChanged` whenever:
- `ActivePane.CurrentItem` changes.
- `ActivePane.SelectedCount` changes.
- `ActivePane.CurrentNormalizedPath` changes.

A small helper on `MainShellViewModel`:

```csharp
private void NotifyPathCapabilityCommandsChanged()
{
    OpenSelectedCommand.NotifyCanExecuteChanged();
    ShowPropertiesCommand.NotifyCanExecuteChanged();
    RevealInExplorerCommand.NotifyCanExecuteChanged();
    OpenTerminalCommand.NotifyCanExecuteChanged();
    // …
}
```

Wire to the three observable triggers above in the existing Rx pipeline that drives the inspector signature.

### 6.2. Tooltip on disabled commands

Plain "button is greyed out" is not enough — the user must learn *why*. Extend the `ToolTipService.ToolTip` on each affected button to change based on capability:

```xml
<AppBarButton
    Icon="OpenFile"
    Command="{x:Bind ViewModel.OpenSelectedCommand}"
    ToolTipService.ToolTip="{x:Bind ViewModel.OpenSelectedTooltip, Mode=OneWay}" />
```

VM-side:

```csharp
public string OpenSelectedTooltip => CanOpenWithDefaultApp()
    ? "Open (Enter)"
    : "Not available for extended-length paths (> 260 chars). Open via a different app or shorten the path.";
```

### 6.3. Long-path indicator in the pane header

When `ActivePane.CurrentNormalizedPath.Length == PathLength.Extended`, show a small "LONG" chip in the pane's path row:

- `src/WinUiFileManager.Presentation/Panels/FilePaneView.xaml` — add a `Border` with text "LONG" next to the path/breadcrumb; bound `Visibility` to `CurrentIsLongPath`.
- Color: accent-on-black; tooltip explains "Path exceeds 260 characters. Shell and thumbnail features disabled in this folder."

Also apply the same chip per-entry in the file list — override the `TableView` name column template to append a subtle glyph next to the name when `entry.Model.FullPath.IsLongPath`. Keeps it visible even when the user hasn't navigated *into* a long folder but is standing on a long entry.

### 6.4. Path-input box validation

`PathBox_KeyDown` on `VirtualKey.Enter` calls `NavigateToCommand`. The command pipeline already calls `IPathNormalizationService.Validate`. Extend `WindowsPathNormalizationService.Validate` to check length:

```csharp
if (path.Length > NormalizedPath.MaxExtendedPathLength + "\\\\?\\".Length)
{
    return PathValidationResult.Invalid(
        $"Path exceeds the maximum of {NormalizedPath.MaxExtendedPathLength} characters.");
}
```

Paste of a path > MAX_PATH but ≤ 32 767 is allowed and navigation proceeds; the pane then shows the LONG indicator.

## 7. Inspector policy

### 7.1. Batch-level gating

Each `InspectorBatchDefinition` gains a `PathCapability`:

```csharp
private sealed record InspectorBatchDefinition(
    string Category,
    bool IsFinalBatch,
    PathCapability RequiredCapability,
    Func<FileInspectorSelection, CancellationToken, Task<InspectorBatchLoadResult>> LoadAsync);
```

At construction time in `FileInspectorViewModel`, tag each batch:

| Batch | `RequiredCapability` |
|---|---|
| NTFS (dates, attributes) | `ReadNtfsMetadata` |
| IDs (file ID, volume, hardlinks, final path) | `ReadNtfsMetadata` |
| Locks (Restart Manager, IFileIsInUse) | `QueryInUseLocks` |
| Links (reparse, shell shortcut detection) | `ReadNtfsMetadata` (shortcut path via FileInfo works) |
| Streams (ADS) | `ReadAlternateStreams` |
| Security (ACL) | `ReadSecurity` |
| Thumbnails | `QueryShellThumbnail` |
| Cloud | `QueryCloudStatus` |

In `LoadDeferredBatchesAsync`:

```csharp
foreach (var batch in _deferredBatches)
{
    if (!_capabilities.Supports(batch.RequiredCapability, selection.PathLength))
    {
        yield return FileInspectorDeferredBatchResult.Unsupported(
            selection.RefreshVersion, batch.Category, batch.IsFinalBatch);
        continue;
    }
    var loadResult = await batch.LoadAsync(selection, cancellationToken);
    yield return new FileInspectorDeferredBatchResult(...);
}
```

`FileInspectorDeferredBatchResult.Unsupported` clears the relevant fields and sets their value to `"Unavailable (extended-length path)"`. The row stays visible so the user sees the limitation explicitly.

### 7.2. Properties button

The Inspector has a "Properties" button that calls `ShowPropertiesAsync`. Under the capability model it becomes disabled for long paths; replace the tooltip with the standard explanation. No behavioral surprise.

### 7.3. Extend `FileInspectorSelection`

Add `PathLength PathLength { get; init; }`. Populate from `entry.Model.FullPath.Length`. Consumers (batches, capability checks) read it directly instead of re-normalizing the path string.

## 8. Long-paths environment and toolbar toggle

The app exposes the machine-level `LongPathsEnabled` registry setting as a first-class toolbar control: a toggle button that reflects the current state, flips it on click (via a standard UAC elevation prompt), and explains the full long-path story in its tooltip. Always visible, always actionable, no dialogs, no keyboard shortcut.

### 8.1. Environment service with reactive state

New file: `src/WinUiFileManager.Application/Abstractions/ILongPathsEnvironment.cs`

```csharp
namespace WinUiFileManager.Application.Abstractions;

public interface ILongPathsEnvironment
{
    /// <summary>Current registry value of HKLM\…\FileSystem\LongPathsEnabled.</summary>
    bool LongPathsEnabled { get; }

    /// <summary>True — the app manifest always declares longPathAware. Reported for the tooltip.</summary>
    bool ManifestEnabled { get; }

    /// <summary>Fires whenever <see cref="LongPathsEnabled"/> changes, including external edits
    /// (regedit, GPO, fsutil).</summary>
    event EventHandler? Changed;

    /// <summary>
    /// Requests the user flip the registry value. Launches `reg.exe` via ShellExecuteEx with
    /// the `runas` verb so Windows shows the standard UAC consent prompt. Returns true if
    /// the elevated process exited with success. Does not wait for <see cref="Changed"/> to
    /// fire — the caller can await it separately if ordering matters.
    /// </summary>
    Task<bool> SetEnabledAsync(bool enabled, CancellationToken cancellationToken);
}
```

New file: `src/WinUiFileManager.Infrastructure/Services/LongPathsEnvironment.cs`

Key behaviors:

- Reads `HKLM\SYSTEM\CurrentControlSet\Control\FileSystem\LongPathsEnabled` (DWORD) on construction.
- Subscribes to `RegNotifyChangeKeyValue` on the `…\FileSystem` subkey (via CsWin32; add `RegNotifyChangeKeyValue` and flag constants to `NativeMethods.txt`). Runs a lightweight wait on a background thread that calls `WaitForSingleObject` on the notify event; on signal, re-reads the value and raises `Changed`.
- `SetEnabledAsync` builds `reg.exe add "HKLM\SYSTEM\CurrentControlSet\Control\FileSystem" /v LongPathsEnabled /t REG_DWORD /d {0|1} /f` and launches via `ShellExecuteEx` with `lpVerb = "runas"`, `fMask = SEE_MASK_NOCLOSEPROCESS | SEE_MASK_NOASYNC`, then `WaitForSingleObject` on the returned process handle. Returns `GetExitCodeProcess == 0`.
- Correctly handles UAC cancel: `ShellExecuteEx` returns `ERROR_CANCELLED` → the task resolves `false`, no exception.
- Never touches HKLM on a non-elevated path. There is no "try without UAC and fall back" — UAC is the only mechanism.

Registered as a singleton in `AddInfrastructureServices`.

### 8.2. Toolbar toggle button

Add to the `CommandBar` in `src/WinUiFileManager.Presentation/Views/MainShellView.xaml`, placed between the existing inspector toggle and the theme toggle:

```xml
<AppBarToggleButton
    x:Name="LongPathsToggle"
    IsChecked="{x:Bind ViewModel.LongPathsEnabled, Mode=OneWay}"
    Click="OnLongPathsToggleClick"
    Label="Long paths">
    <AppBarToggleButton.Icon>
        <FontIcon Glyph="&#xE8E4;" />   <!-- Segoe Fluent: "FullScreen" — connotation of expanded range -->
    </AppBarToggleButton.Icon>
    <ToolTipService.ToolTip>
        <ToolTip Content="{x:Bind ViewModel.LongPathsTooltip, Mode=OneWay}" />
    </ToolTipService.ToolTip>
</AppBarToggleButton>
```

No keyboard shortcut — per requirement.

View-model additions on `MainShellViewModel`:

```csharp
private readonly ILongPathsEnvironment _longPaths;

[ObservableProperty]
public partial bool LongPathsEnabled { get; set; }

public string LongPathsTooltip => BuildLongPathsTooltip();

// In constructor:
_longPaths = longPaths;
LongPathsEnabled = _longPaths.LongPathsEnabled;
_longPaths.Changed += (_, _) =>
    _schedulers.MainThread.Schedule(() =>
    {
        LongPathsEnabled = _longPaths.LongPathsEnabled;
        OnPropertyChanged(nameof(LongPathsTooltip));
    });
```

Click handler in `MainShellView.xaml.cs`:

```csharp
private async void OnLongPathsToggleClick(object sender, RoutedEventArgs e)
{
    if (ViewModel is null || sender is not AppBarToggleButton toggle)
    {
        return;
    }

    // AppBarToggleButton flips IsChecked before invoking Click. Revert it so the visual
    // state only changes after the user consents to UAC and the registry actually updates.
    var requested = toggle.IsChecked ?? false;
    toggle.IsChecked = !requested;

    try
    {
        var succeeded = await ViewModel.SetLongPathsEnabledAsync(requested, CancellationToken.None);
        // Visual state updates via the Changed event raised by ILongPathsEnvironment.
        _ = succeeded; // No UI follow-up needed; UAC cancel is silent.
    }
    catch (Exception ex)
    {
        ViewModel.Log("Failed to toggle long paths.", ex);
    }
}
```

VM method:

```csharp
public Task<bool> SetLongPathsEnabledAsync(bool enabled, CancellationToken ct) =>
    _longPaths.SetEnabledAsync(enabled, ct);
```

### 8.3. Tooltip content (normative)

The tooltip is multi-line. It opens from `LongPathsTooltip`, a computed string on the VM. Keep it under ~450 UTF-16 chars so it doesn't spill off-screen on small monitors. Two states, templated:

**When `LongPathsEnabled == true`:**

> Long paths: ENABLED (Windows machine-wide).
>
> Paths up to 32 767 characters are accepted by Win32, .NET I/O, PowerShell -LiteralPath, and most developer tools. Effective immediately for new processes; already-running shells may keep the old limit until restart.
>
> This app works for long paths regardless of this toggle — it always uses the extended-length (\\?\) form internally.
>
> Still unavailable for paths > 260 chars: Windows Shell (Open, Properties, Reveal in Explorer), WinRT Storage (thumbnails, cloud sync state), drag-drop to Explorer. Those buttons are disabled automatically when a long path is selected.
>
> Click to disable (requires admin).

**When `LongPathsEnabled == false`:**

> Long paths: DISABLED (Windows machine-wide).
>
> Paths over 260 characters are rejected by most Windows tools (cmd, PowerShell without -LiteralPath, Node, Python, older .NET, and every Shell API).
>
> This app still works for long paths — it always uses the extended-length (\\?\) form internally. Enable this setting to let the rest of your developer tools do the same.
>
> Unavailable for paths > 260 chars regardless of this toggle: Windows Shell (Open, Properties, Reveal in Explorer), WinRT Storage (thumbnails, cloud sync state), drag-drop to Explorer. Those buttons are disabled automatically when a long path is selected.
>
> Click to enable (requires admin).

Localization: both templates live in a `PathCapabilityStrings.cs` static class for now — resx-backed localization is out of scope for v1.

### 8.4. Caveats and edge cases

- **"Enabled but running process caches the old limit."** Document this in the tooltip (done above). The app itself is unaffected because it always prefixes.
- **Group Policy precedence.** If long paths are forced on or off by GPO, `reg.exe add` succeeds but the value reverts on refresh. The `Changed` event will fire twice (once for our write, once for the GPO reassertion); the button state will briefly reflect the desired state, then snap back. Acceptable for a dev tool; a future version can detect GPO ownership via `RegQueryInfoKey` flags and disable the button with an explanatory tooltip.
- **Elevation denied.** `SetEnabledAsync` returns `false`; `IsChecked` is already reverted; no visible change. The user learns via the unchanged button state.
- **Registry key missing entirely.** Some stripped Windows SKUs don't have `HKLM\…\FileSystem\LongPathsEnabled`. Treat "value not present" as `false`, and let `reg.exe add` create it on first enable.
- **Not available on Windows 8 or earlier.** The spec's minimum target (.NET 10 + Windows 10 1903+) already excludes these. `ILongPathsEnvironment.LongPathsEnabled` returns `false` on pre-1607 hosts and the toggle simply does nothing useful; document that the feature is a no-op below 1607 if anyone attempts to run the app there.

### 8.5. Startup behavior

No notice is shown at startup. The toolbar button is its own announcement — when `LongPathsEnabled == false`, the icon is unchecked, and the tooltip (on hover) explains the state. This is sufficient discoverability for a dev tool and avoids transient UI chrome.

`AppSettings.LongPathsNoticeSuppressedAt` is NOT added — we don't maintain a "don't show again" flag since there is no recurring notice.

## 9. Test fixtures

### 9.1. PowerShell helper

New file: `powershell/create-long-path-tree.ps1`

```powershell
<#
  Creates a test tree under $Root with nested folders whose full paths exceed
  MAX_PATH. Uses the \\?\ prefix so PowerShell's own path validator gets out
  of the way.
#>
[CmdletBinding()]
param(
    [string]$Root = "$env:TEMP\longpath_test",
    [int]$TargetDepthChars = 400
)

$prefixed = "\\?\$Root"
if (Test-Path -LiteralPath $prefixed) {
    Remove-Item -LiteralPath $prefixed -Recurse -Force
}

New-Item -ItemType Directory -Path $prefixed | Out-Null

$segment = 'a' * 40
$current = $prefixed
while ($current.Length - $prefixed.Length -lt $TargetDepthChars) {
    $current = Join-Path -Path $current -ChildPath $segment
    [System.IO.Directory]::CreateDirectory($current) | Out-Null
}

# Sprinkle test files at three depths.
foreach ($i in 0,1,2) {
    $file = Join-Path -Path $current -ChildPath "test_$i.txt"
    [System.IO.File]::WriteAllText($file, "hello from depth $($current.Length)")
}

Write-Host "Created $($current.Length)-char test path under $Root."
```

### 9.2. TUnit fixtures

Extend `WinUiFileManager.Testing` (created in the tooling spec):

- `LongPathFolderBuilder(string root, int targetDepthChars)` — mirrors the PowerShell helper for test-time use. Uses `Directory.CreateDirectory("\\\\?\\...")` directly.
- `LongPathFixture` — `IAsyncDisposable` that builds a 400-char-deep directory with three files and cleans up on dispose.

### 9.3. Regression tests

New test project: `tests/WinUiFileManager.LongPaths.Tests/`. Covers:

- `Enumeration_LongPath_ReturnsEntries` — navigate the fixture; assert `Items.Count == 3`.
- `Watcher_LongPath_ReportsCreatedFile` — start watcher on fixture; create a file; assert the event.
- `Copy_LongPath_ToStandardDestination_Succeeds` — via the real `CopyFile2` path.
- `Capability_Disables_OpenWithDefaultApp_OnLongPath` — VM-level test, no UI.
- `Capability_AllowsRename_OnLongPath` — ensures rename is NOT accidentally disabled.
- `Inspector_LongPath_SkipsThumbnailAndCloud` — asserts those batches emit `Unsupported` sentinels.
- `Inspector_LongPath_ReturnsNtfsMetadata` — the NTFS batch must still populate `Created`, `Modified`, etc.
- `PathValidation_OverMaxExtendedLength_Rejects` — path > 32 767 chars returns `PathValidationResult.Invalid`.

All long-path tests are marked `[Category("LongPaths")]` and gated by a `LONG_PATHS_TESTS` environment variable in CI so developers without the registry flag can still run the rest of the suite.

## 10. Banned-API additions

Add to `BannedSymbols.txt` (in the tooling spec):

```
# Force all shell and WinRT Storage calls through IShellService / NtfsFileIdentityService,
# which honor IPathCapabilityPolicy — see SPEC_LONG_PATHS.md §5.
T:Windows.Storage.StorageFile;Route through IFileIdentityService / IShellService, which guard by path length.
T:Windows.Storage.StorageFolder;Route through IFileIdentityService / IShellService, which guard by path length.
T:Windows.ApplicationModel.DataTransfer.Clipboard;Route through IClipboardService.
M:System.Diagnostics.Process.Start(System.Diagnostics.ProcessStartInfo);Route through IShellService, which guards by path length.
```

Exemption surface: the concrete implementations in `WinUiFileManager.Infrastructure` and `WinUiFileManager.Presentation.Services` use a file-scoped `#pragma warning disable RS0030` with a cited justification.

## 11. Acceptance

This spec is complete when all of the following hold:

- `NormalizedPath` exposes `Length` and `IsLongPath`.
- `IPathCapabilityPolicy` is registered and consulted by every shell/WinRT call site.
- Commands that require `Standard` paths are visibly disabled on `Extended` paths, with explanatory tooltips.
- The Inspector shows "Unavailable (extended-length path)" in Thumbnail, Cloud, Locks, and Properties when the selection is a long path.
- A "LONG" chip is visible in the pane header when the current folder's path length is `Extended`.
- File-system operations (copy, move, rename, delete, create, rename, enumerate, watch) work end-to-end on a 400-char fixture path.
- A "Long paths" `AppBarToggleButton` is visible in the main command bar; its checked state matches the `HKLM\…\FileSystem\LongPathsEnabled` registry value at all times, including after external edits via regedit / GPO / fsutil (reactive via `RegNotifyChangeKeyValue`).
- Clicking the toggle triggers the standard Windows UAC prompt; accepting flips the registry value and updates the button within ~200 ms of the elevated `reg.exe` exiting; declining leaves the registry and button unchanged.
- The toggle's tooltip presents the two normative texts in §8.3 depending on current state, including the explicit note that the app itself works for long paths regardless of the toggle.
- No dialogs, info bars, or toasts are shown for long-path state — the toolbar is the single communication surface.
- All long-path tests pass in CI (gated by env var).
- `BannedApiAnalyzers` blocks new uses of `StorageFile`, `StorageFolder`, `Clipboard`, and `Process.Start(ProcessStartInfo)` outside the approved service layer.
- Manual smoke: navigate the fixture path in the app; copy a file from inside it to `C:\temp`; rename a file inside it; verify the file appears in the opposite pane; verify Inspector NTFS metadata shows; verify "Open with default app" is disabled with a readable tooltip.

## 12. Interactions with other specs

- **Supersedes `SPEC_BUG_FIXES.md` B2.** The prefixed-path ShellExecute bug is fixed both by switching to `DisplayPath` *and* by disabling the command for `Extended` paths. Close B2 with a reference to this spec.
- **Extends `SPEC_FEATURE_LOW_HANGING_FRUIT.md` F4 (Reveal in Explorer), F5 (breadcrumb), F8 (drag-drop), F4 (Terminal).** Each of those features gains a capability guard.
- **Builds on `SPEC_NUGET_MODERNIZATION.md` §1 (CsWin32 expansion).** The CopyFile2 migration already uses `NormalizedPath.Value`; no extra work required.
- **Depends on `SPEC_TOOLING_AND_ANALYZERS.md` §1.3** for the banned-API wiring.

## 13. Open questions

Mark these for a follow-up spec or ticket — do not block v1 long-path support on them.

- **UNC long paths (`\\?\UNC\server\share\…`)** — current validator rejects; add a domain rule and a small `IUncPath` classifier if we want to support SMB long paths later.
- **Moving a long-path file onto a non-long-path-aware destination (e.g., USB drive with FAT32)** — currently errors out from the underlying copy. Consider a pre-flight check that inspects the destination's file system and warns.
- **`Set-Location` PowerShell integration** for the "Open Terminal here" command when the path is `Extended` — `wt.exe` may accept it but then the user's shell profile may reject. Surface a one-time tip on first use.
- **File-Inspector "Link Target" resolution for `.lnk` shortcuts that point to long paths** — `.lnk` parsing is fine, but the resolved target may itself be long; that recursion case wants explicit UX.
