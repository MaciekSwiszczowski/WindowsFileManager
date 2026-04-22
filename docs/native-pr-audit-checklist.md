# Native PR Audit Checklist

Use this checklist on every PR that touches native code, COM interop, `SafeHandle`, or cancellation behavior around interop.

- [ ] Every `new SomethingStream(...)`, `Process.Start(...)`, `FileSystemWatcher`, and `CancellationTokenSource` is `using`-wrapped or owned by a disposable container that is itself disposed.
- [ ] Every `[DllImport]` lives in `NativeMethods.txt` or generated interop output. No new hand-rolled imports were added elsewhere.
- [ ] Every `ComImport` interface is released with `Marshal.FinalReleaseComObject` in `finally`, on the thread that created it.
- [ ] Every `SafeHandle` acquisition is either returned up the stack or `using`-wrapped. Any `DangerousGetHandle` call stays inside the owning handle's lifetime scope.
- [ ] Every `async` method that touches native code rethrows `OperationCanceledException` before any broader exception catch.
- [ ] Every new interop-facing API states who owns the returned handle, stream, RCW, or subscription, and where it is released.
