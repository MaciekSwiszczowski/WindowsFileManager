# Modernization & Slop Audit

A code-level review of the whole solution. The **toolchain is already modern** (`net10.0-windows`, central package management, `Nullable`/`ImplicitUsings` on, CsWin32 for all P/Invoke, analyzer suite under `Debug_Analyzers`). So this document does **not** chase framework/SDK bumps. Instead it points at concrete *slop* — dead dependencies, contradictory build config, leak-prone lifetimes, and outdated patterns — with file/line references.

Each item is tagged with a severity:

- **[Correctness]** — wrong behavior / resource-correctness bug.
- **[Leak]** — memory or handle retention risk.
- **[Pattern]** — works today, but fights the rest of the codebase / modern .NET.
- **[Hygiene]** — dead code, noise, misleading API.

> Honest framing on leaks: the app is single-window and most leak-prone objects are DI singletons that live until process exit, so several "leaks" below are **latent** (they bite under multi-window, container re-creation, or repeated view-scoped re-resolution during a session) rather than active today. They are still worth fixing because the disposal code *exists* and is simply never reached — that is the definition of a latent leak waiting for a second window.

---

## 1. NuGet / build-config modernization

### 1.1 Dead package: `Nerdbank.Streams` — [Hygiene]

`Nerdbank.Streams` is referenced but has **zero** usages in source (only the package declaration and the `<PackageReference>` match a solution-wide search).

- `Directory.Packages.props:33` — `<PackageVersion Include="Nerdbank.Streams" Version="2.13.16" />`
- `src/WinUiFileManager.Infrastructure/WinUiFileManager.Infrastructure.csproj:13` — `<PackageReference Include="Nerdbank.Streams" />`

**Fix:** remove both lines. Dead dependencies inflate restore graph and imply capabilities that don't exist.

### 1.2 `NuGetAudit` is disabled — [Correctness/security]

`Directory.Build.props:15`

```xml
<NuGetAudit>false</NuGetAudit>
```

This turns off vulnerability scanning of the dependency graph for the whole solution. There is no comment explaining why.

**Fix:** re-enable (`true`) and set `<NuGetAuditMode>all</NuGetAuditMode>`; if a specific advisory is noisy, suppress *that* advisory rather than disabling auditing globally.

### 1.3 Custom `ITimeProvider` duplicates BCL `TimeProvider` — [Pattern]

`src/WinUiFileManager.Infrastructure/Services/SystemTimeProvider.cs`

```csharp
internal sealed class SystemTimeProvider : Application.Abstractions.ITimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
```

.NET 8+ ships `System.TimeProvider`, which is the standard, test-friendly time abstraction (and supports timers, not just `UtcNow`). The hand-rolled `ITimeProvider` is a pre-.NET-8 idiom.

**Fix:** delete the custom interface and inject `TimeProvider`; register `TimeProvider.System` as a singleton. This also unlocks `FakeTimeProvider` in tests instead of a bespoke fake.

### 1.4 `System.Reactive` leaking into the Application layer — [Pattern]

`src/WinUiFileManager.Application/Abstractions/ISchedulerProvider.cs` exposes `System.Reactive.Concurrency.IScheduler`, forcing `WinUiFileManager.Application.csproj:12` to take a heavy Rx dependency just to express "give me a background scheduler". The Application (domain) layer should not be coupled to Rx's threading model.

**Fix:** define a minimal `IBackgroundScheduler` / `IMainThreadScheduler` (or use `TimeProvider` + `System.Threading.Channels`) in Application, and keep `System.Reactive` confined to Infrastructure/Presentation where it's actually used.

### 1.5 JSON persistence is reflection-based — [Pattern, trim/AOT blocker]

`JsonSettingsRepository` serializes `AppSettings` with the reflection-based `JsonSerializer`. There is no `JsonSerializerContext`. This is the single biggest blocker if trimming/AOT is ever attempted (see `docs/MEMORY_OPTIMIZATION_RECOMMENDATIONS.md` §7) and it silently allocates reflection metadata.

**Fix:** add a source-generated `[JsonSerializable(typeof(SettingsDto))] partial class SettingsJsonContext : JsonSerializerContext` and pass `SettingsJsonContext.Default` to the serializer.

### 1.6 Contradictory analyzer config — [Hygiene]

`Directory.Build.props` lists the **same** diagnostics in both `<WarningsAsErrors>` and `<NoWarn>`:

- `CA2007`, `VSTHRD100/103/110/200`, `MA0040/MA0041/MA0134`, `AF0001/AF0004`, `CS1998` appear in `WarningsAsErrors` (lines 18–22) **and** in `NoWarn` (lines 28–32).

`NoWarn` wins, so these rules are effectively **off** in the default build and only the `Debug_Analyzers` configuration enforces them. The duplicated lists read as if the rules are enforced when they are not.

**Fix:** keep each diagnostic in exactly one list. If the intent is "error only under `Debug_Analyzers`", move the `WarningsAsErrors` block into the `Debug_Analyzers` `PropertyGroup` and drop them from the global `NoWarn`.

---

## 2. Better patterns

### 2.1 `StrongReferenceMessenger` is the app-wide messenger — [Pattern → root cause of §3 leaks]

`src/WinUiFileManager.Infrastructure/InfrastructureContainerBuilderExtensions.cs:16`

```csharp
builder.RegisterInstance(StrongReferenceMessenger.Default).As<IMessenger>();
```

With `StrongReferenceMessenger`, **every** `Register` roots the recipient until an explicit `Unregister`/`UnregisterAll`. The whole solution therefore depends on disposal running perfectly — which it does not (see §3.1). `WeakReferenceMessenger` would make a missed `UnregisterAll` a non-event for GC.

**Decision needed:** either (a) switch view-scoped recipients to `WeakReferenceMessenger`, or (b) keep Strong but guarantee the disposal chain in §3.1. Mixing is fine: keep Strong for app-lifetime singletons, Weak for view/VM-scoped recipients.

### 2.2 Imperative `Register(this, OnX)` instead of `IRecipient<T>` — [Pattern]

All handlers use the closure/imperative form, e.g. every Diagnostics handler:

`src/WinUiFileManager.Diagnostics/Inspector/InspectorIdentityDiagnosticsHandler.cs` (registration block)

```csharp
_messenger.Register<InspectorIdentityDiagnosticsRequestMessage>(this,
    (_, message) => message.Reply(Task.Run(() => Load(message))));
```

The lambda captures `this` **in addition** to the `this` recipient token — two strong roots per registration. The CommunityToolkit-idiomatic form is `IRecipient<T>` with a `Receive(T)` instance method and `messenger.RegisterAll(this)`, which avoids the extra closure and is what the toolkit's weak-reference fast path is optimized for.

**Fix:** convert handlers to `IRecipient<TMessage>`.

### 2.3 Non-idempotent `Initialize()` registration — [Correctness]

Handlers register inside an `Initialize()` that has no guard, e.g.:

- `src/WinUiFileManager.Application/Navigation/PanelNavigationService.cs:27` (`Initialize` → `Register` with no `_initialized` check)
- `src/WinUiFileManager.Diagnostics/FileOperations/FileOperationRequestHandler.cs:26`
- the 7 inspector diagnostics handlers (same shape)

`StartupChain` calls all of them once (`StartupChain.Initialize()` block), but nothing prevents a second call from double-registering and handling every message twice.

**Fix:** add `if (_initialized) return; _initialized = true;`, or register in the constructor with Autofac `AutoActivate()`, or move to `IRecipient<T>` + `RegisterAll`.

### 2.4 Manual `INotifyPropertyChanged` / `ICommand` where the toolkit generators are used elsewhere — [Pattern]

The solution already depends on `CommunityToolkit.Mvvm` source generators, but several types hand-roll what `[ObservableProperty]` / `[RelayCommand]` generate:

- `src/WinUiFileManager.Application/Dialogs/RenameDialogViewModel.cs` — full manual `INotifyPropertyChanged` (setters + `OnPropertyChanged`).
- `src/WinUiFileManager.Presentation/ViewModels/MainShellViewModel.cs:42–51, 222–232` — manual `_isInspectorVisible` backing field + `OnPropertyChanged`.
- `src/WinUiFileManager.Presentation/ViewModels/Inspector/Buttons/InspectorPropertiesButtonViewModel.cs:11` and `InspectorRefreshButtonViewModel.cs:10` — `new AsyncRelayCommand(...)` / `new RelayCommand(...)` in the ctor instead of `[RelayCommand]` on a method.
- `src/WinUiFileManager.Presentation/ViewModels/Panels/PanelFileEntryDataSourceViewModel.cs:91–99` — manual `OnPropertyChanged(nameof(HasPathValidationError))` instead of `[NotifyPropertyChangedFor]`.

**Fix:** convert to `[ObservableProperty]` / `[RelayCommand]` / `[NotifyPropertyChangedFor]` for consistency and less drift.

### 2.5 Implicit `string ⇄ Identity` conversions — [Pattern, foot-gun]

`src/WinUiFileManager.Application/Messaging/Identity.cs:14–16`

```csharp
public static implicit operator string(Identity identity) => identity.Value;
public static implicit operator Identity(string value) => new(value);
```

Combined with raw string literals at send sites (`StartupChain` sends `new FileTableNavigateToPathRequestedMessage("Left", ...)` / `"Right"`), a typo like `"left"` compiles fine and fails silently at runtime.

**Fix:** remove the implicit operators; expose `PanelIdentity.Left` / `PanelIdentity.Right` constants (or a small enum-backed struct) and force explicit construction.

### 2.6 Settings load-modify-write is a TOCTOU race — [Correctness]

`ISettingsRepository` only offers `LoadAsync` + `SaveAsync`, so every caller does read-modify-write:

- `src/WinUiFileManager.Application/Settings/PersistPaneStateCommandHandler.cs:19–38`
- `src/WinUiFileManager.Application/Settings/SetParallelExecutionCommandHandler.cs:19–29`

Two concurrent updates (e.g. pane-state persist on close racing the parallel-execution toggle) can lose a write — the repository's `SemaphoreSlim` serializes each op, not the whole load→save.

Also, the write itself is non-atomic — `JsonSettingsRepository` does `File.Create` + `SerializeAsync`, so a crash mid-write corrupts `settings.json`.

**Fix:** add `ISettingsRepository.UpdateAsync(Func<AppSettings, AppSettings> mutate, CancellationToken)` that holds the lock across the full read-modify-write; write to a temp file then move into place atomically.

### 2.7 Layering: the Diagnostics layer drives UI dialogs — [Pattern]

`src/WinUiFileManager.Diagnostics/FileOperations/FileOperationRequestHandler.cs:64–75` builds a `MessageDialogViewModel` and sends `ShowDialogMessage`. A diagnostics/file-operations handler should report a failure result; **Presentation** should decide to show a dialog.

**Fix:** publish an `AttributeChangeFailedMessage` (or reply with a result) and let a Presentation-layer recipient render the dialog.

### 2.8 God classes — [Pattern, violates the repo's SRP rule]

`AGENTS.md` calls out SRP as "very important". Two classes clearly violate it:

- `src/WinUiFileManager.App/Startup/StartupChain.cs` — ~15 injected dependencies; knows about panels, every diagnostics handler, navigation, rename, settings, volumes, messaging. Split into per-assembly `IStartupInitializer` implementations that `StartupChain` just iterates.
- `src/WinUiFileManager.Diagnostics/Inspector/InspectorCloudDiagnosticsHandler.cs` — ~380 lines doing cloud status, placeholder bitmasks, sync-root walking, storage properties, and label formatting. Extract `CloudStorageItemLoader`, `CloudStatusLabelBuilder`, `SyncStateFormatter`.

### 2.9 Dead / no-op code — [Hygiene]

- `src/WinUiFileManager.Diagnostics/FileOperations/FileOperationRequestHandler.cs:100`

  ```csharp
  _ = request is { HasReceivedResponse: true, Response: true };
  ```

  The pattern result is computed and discarded — either branch on it or delete the `CreateSelectionSnapshot` send entirely.

- `src/WinUiFileManager.Presentation/ViewModels/MainShellViewModel.cs:122–141` — `CopyAsync`, `RefreshActivePaneAsync`, etc. are `[RelayCommand]` methods returning `Task.CompletedTask` (stubs wired to UI).
- `src/WinUiFileManager.Presentation/ViewModels/StatusBarViewModel.cs` — registered in DI (`PresentationContainerBuilderExtensions.cs:32`) but never referenced anywhere in `src/`. Orphan type.
- `src/WinUiFileManager.Application/GlobalUsings.cs:1–5` — global usings for `Diagnostics`/`Navigation`/`Settings` namespaces hide real per-file dependencies; prefer explicit `using`s.

### 2.10 Allocation on domain hot paths — [Pattern, see AGENTS.md row-leanness rule]

- `src/WinUiFileManager.Application/FileEntries/NormalizedPath.cs:14–17` — `DisplayPath` substrings on **every** access. After `FromUserInput` the value always carries the `\\?\` prefix, so every cell binding, every `SourceCache` key, every inspector field, every log line re-allocates. Compute once in the constructor and store alongside `Value`.
- `src/WinUiFileManager.Application/FileEntries/FileSystemEntryModel.cs:27` — `FullPath => new(Path.Combine(DirectoryPath.Value, Name))` allocates a fresh string + struct per access on the per-row model.

(Both are already tracked in `docs/SPEC_LOW_HANGING_IMPROVEMENTS.md` §M-1; cited here for completeness.)

---

## 3. Code safety — memory & resource leaks

### 3.1 The disposal chain is wired but never invoked — [Leak, latent]

Many objects implement `IDisposable` and correctly call `UnregisterAll(this)` / dispose subscriptions — but **nothing ever calls their `Dispose()`**:

- `src/WinUiFileManager.App/App.xaml.cs` — the `AutofacServiceProvider` / container is never disposed on shutdown, so no singleton `IDisposable` (`PanelNavigationService`, all diagnostics handlers, `RenameService`, `ActivePanelsService`) is ever released.
- `src/WinUiFileManager.App/Windows/MainShellWindow.xaml.cs:64–85` — `OnAppWindowClosing` persists state and `Close()`s, but never calls `_viewModel.Dispose()` or tears down `WindowManager`.
- `src/WinUiFileManager.Presentation/ViewModels/MainShellViewModel.cs:235–238` — `Dispose()` only `UnregisterAll(this)`; it does **not** cascade to `Inspector`, `Commands`, or the panel VMs (which each own messenger registrations + Rx `CompositeDisposable`s).
- Panel chain: `PanelsView.OnUnloaded` disposes the **views** but `PanelViewModel.Dispose()` / `PanelFileEntryDataSourceViewModel.Dispose()` (which disposes `FileEntryTableDataSource`'s Rx + directory-watcher subscriptions) is never called.

Because the messenger is `StrongReferenceMessenger` (§2.1), every never-disposed recipient is rooted for the process lifetime. Single-window today = no visible growth; a second window or any session-scoped re-resolution = real accumulation.

**Fix:** dispose the container on app exit; in `MainShellWindow` closing, call `_viewModel.Dispose()`; make `MainShellViewModel.Dispose()` cascade to `Inspector`, `Commands`, and both panels; in `SinglePanelView.Dispose()` call `ViewModel?.Dispose()`.

### 3.2 `WindowManager` subscribes to a long-lived VM and never unsubscribes — [Leak]

`src/WinUiFileManager.App/Windows/WindowManager.cs:32–34`

```csharp
viewModel.PropertyChanged += OnViewModelPropertyChanged;
_window.SizeChanged += OnWindowSizeChanged;
_appWindow.Changed += OnAppWindowChanged;
```

There is **no** `-=` anywhere in the file and no `Detach()` method. `MainShellViewModel` outlives the window, so these delegates keep `WindowManager`, `Window`, and `AppWindow` alive after close. (Contrast `MainShellView.xaml.cs:OnUnloaded`, which *does* unsubscribe its `PropertyChanged` handlers — the right pattern.)

**Fix:** add `WindowManager.Detach()` that removes all three handlers; call it from `MainShellWindow` closing.

### 3.3 `AddHandler` without `RemoveHandler` in views — [Leak]

Code-behind hooks routed events with `AddHandler(..., handledEventsToo: true)` but never `RemoveHandler`:

- `src/WinUiFileManager.Presentation/Views/MainShellView.xaml.cs:65–75` — splitter + three global pointer handlers.
- `src/WinUiFileManager.Presentation/Views/PanelsView.xaml.cs:9` — splitter pointer handler.
- `src/WinUiFileManager.Presentation/FileEntryTable/SpecFileEntryTableView.xaml.cs:23–24` — `Loaded +=` and `DoubleTapped` `AddHandler`.
- `src/WinUiFileManager.Presentation/Views/CommandButtonsView.xaml.cs:11,52` — `Loaded +=` and a dynamically-injected debug button `Click +=` closure that captures `OpenMessageLogWindow`.

**Fix:** store the handler delegates in fields and `RemoveHandler` / `-=` in `OnUnloaded`.

### 3.4 COM ref-count bug: `S_FALSE` treated as "we initialized" — [Correctness]

`src/WinUiFileManager.Interop/Adapters/ShellInterop.cs:34–35` and `42–43`

```csharp
private static bool TryInitializeStaComCore(Func<int> initializeStaCom)
    => initializeStaCom() is 0 or 1;          // 1 == S_FALSE == "already initialized"
...
private static unsafe int InitializeStaCom()
    => PInvoke.CoInitializeEx(null, COINIT.COINIT_APARTMENTTHREADED);
```

`CoInitializeEx` returns `S_OK` (0) when *this* call initialized COM and `S_FALSE` (1) when it was **already** initialized on the thread. `WindowsShellService` (`Services/WindowsShellService.cs:60–79`) then calls `CoUninitialize()` in its `finally` whenever this returns true — so on `S_FALSE` it decrements a ref-count it never incremented, corrupting COM lifetime for the rest of the thread.

**Fix:** return a 3-state result (`InitializedByUs` / `AlreadyInitialized` / `Failed`) and only `CoUninitialize()` for `InitializedByUs` (`hr == 0`).

### 3.5 `FileSystemWatcher` recreation race + incomplete outer dispose — [Leak/Correctness]

`src/WinUiFileManager.Infrastructure/FileSystem/WindowsDirectoryChangeStream.cs`

- Lines 146–159: on `OnError`, the watcher is nulled inside the lock but `CreateAndStart()` runs **outside** the lock. Concurrent errors can each build a `FileSystemWatcher`; only the last assigned to `_watcher.Disposable` is tracked — the others are abandoned until GC.
- Lines 36–39: the outer `Dispose()` only sets `_disposed = true`; it does not stop already-running per-subscription watchers. (Cold-observable teardown handles the common path via the subscriber's `CompositeDisposable`, so this is a semantic gap rather than a guaranteed leak — but disposing a registered singleton should still stop its watchers.)

**Fix:** recreate inside the lock (or use a generation token so a losing recreation disposes itself); track live subscriptions and dispose them in the outer `Dispose()`.

### 3.6 Restart Manager session handle has no RAII — [Leak]

`src/WinUiFileManager.Interop/Adapters/RestartManagerInterop.cs:12–14, 59–61` — `StartSession(out uint sessionHandle)` / `EndSession(uint)`. The OS session handle is a raw `uint`; any caller that misses `EndSession` on an error path leaks the session until process exit. (The GCHandle pinning in the same file *is* correct — freed in `finally`.)

**Fix:** wrap the session in an `IDisposable` (`RestartManagerSession`) whose `Dispose()` calls `RmEndSession`.

### 3.7 UI dialog sent from a thread-pool thread — [Correctness, crash risk]

`src/WinUiFileManager.Diagnostics/FileOperations/FileOperationRequestHandler.cs:45–76`

```csharp
await Task.Run(() => SetAttributeFlag(message)).ConfigureAwait(false);   // line 50
...
await ShowAttributeChangeFailureAsync(message, ex).ConfigureAwait(false); // line 60
...
var dialogRequest = _messenger.Send(new ShowDialogMessage(...));          // now on a pool thread
```

After `ConfigureAwait(false)`, the failure path sends `ShowDialogMessage` from a thread-pool thread. WinUI dialog creation requires the UI thread → intermittent `RPC_E_WRONG_THREAD`/crash.

**Fix:** marshal the dialog send back to the UI thread (`ISchedulerProvider.MainThread` / captured `DispatcherQueue`) before sending.

### 3.8 `async void` event handlers — [Correctness]

Three `async void` handlers; the first two can throw after the first `await` with no top-level catch:

- `src/WinUiFileManager.Presentation/ViewModels/MainShellViewModel.cs:54` — `OnParallelExecutionEnabledChanged` (fired from a property setter, not a UI event → prefer `IAsyncRelayCommand`).
- `src/WinUiFileManager.App/Windows/MainShellWindow.xaml.cs:64` — `OnAppWindowClosing` (a `try/finally` exists, but no `catch`; an exception in `PersistStateAsync` would be unobserved).
- `src/WinUiFileManager.Presentation/Services/DialogService.cs:155` — `OnDialogButtonClick` (acceptable WinUI pattern with a deferral, but guard against `_disposed` across the await).

### 3.9 Static collections that root windows / handlers — [Leak, debug-only]

- `src/WinUiFileManager.App/Windows/MessageLogWindow.cs:9,18` — `static readonly List<MessageLogWindow> OpenInstances`; relies on `CloseAll()` running. If the main window faults before `CloseAll`, the debug windows leak.
- `src/WinUiFileManager.Presentation/MessageLogging/MessageLogStore.cs:105–107` — registers a handler for *every* `IFileManagerMessengerMessage` type via reflection and never unregisters (pins the store + all handlers under `StrongReferenceMessenger`). Both are DEBUG-only, but should be gated behind `#if DEBUG` or use weak references.

### 3.10 `SemaphoreSlim` field never disposed — [Leak, minor]

`src/WinUiFileManager.Infrastructure/Persistence/JsonSettingsRepository.cs:19` — `private readonly SemaphoreSlim _semaphore = new(1, 1);`. Benign for a process-lifetime singleton, but trips `CA1001`/`IDisposableAnalyzers` and is technically an undisposed `IDisposable`.

**Fix:** implement `IDisposable`/`IAsyncDisposable` on the repository (once §3.1 disposes the container) or use a plain `lock`/`SemaphoreSlim` documented as process-lifetime.

### 3.11 Placeholder volume metadata — [Correctness]

`src/WinUiFileManager.Interop/Adapters/VolumeInterop.cs:26–28, 70–72` hardcodes `SerialNumber=0, MaxComponentLength=255, FileSystemFlags=0` for every volume, and uses banned `DriveInfo` (`#pragma warning disable RS0030` at line 1) instead of `GetVolumeInformationW` (already declared in `NativeMethods.txt` and used by `FileSystemMetadataInterop.TryGetVolumeSerialHex`). Consumers relying on those three fields get fabricated values.

**Fix:** populate from `GetVolumeInformationW` and retire the `DriveInfo` path.

---

## Priority

| # | Item | Severity | Effort |
|---|------|----------|--------|
| 3.4 | COM `S_FALSE` → erroneous `CoUninitialize` | Correctness | S |
| 3.7 | UI dialog sent from pool thread | Correctness | S |
| 3.1 | Dispose chain never invoked (container + shell VM cascade) | Leak (latent) | M |
| 3.2 | `WindowManager` event handlers never removed | Leak | S |
| 2.3 | Non-idempotent `Initialize()` registration | Correctness | S |
| 2.6 | Settings TOCTOU + non-atomic write | Correctness | M |
| 3.5 | Watcher recreation race / outer dispose | Leak/Correctness | M |
| 1.1 / 1.2 | Remove dead `Nerdbank.Streams`; re-enable `NuGetAudit` | Hygiene/Security | S |
| 2.1 / 2.2 | Weak messenger + `IRecipient<T>` migration | Pattern | L |
| 1.3 / 1.4 / 1.5 | `TimeProvider`, de-Rx the Application layer, source-gen JSON | Pattern | M |
| 2.8 | Split `StartupChain` / `InspectorCloudDiagnosticsHandler` | Pattern (SRP) | L |
| 2.10 / 1.6 | Memoize `DisplayPath`/`FullPath`; de-dupe analyzer lists | Pattern/Hygiene | S |

S = under an hour, M = half a day, L = multi-day / architectural.
