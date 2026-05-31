namespace WinUiFileManager.Interop.Adapters;

/// <summary>
/// Immutable result of <see cref="IShellInterop.ExecutePropertiesVerb"/>: whether <c>ShellExecuteEx</c> succeeded,
/// the captured Win32 error on failure, and the raw <c>hInstApp</c> the shell returned (a legacy success/error
/// indicator the caller may inspect). Internal DTO with no native ownership.
/// </summary>
/// <param name="Succeeded"><see langword="true"/> when <c>ShellExecuteEx</c> returned success.</param>
/// <param name="LastError">Win32 error code captured on failure; <c>0</c> on success.</param>
/// <param name="HInstApp">The raw <c>SHELLEXECUTEINFOW.hInstApp</c> value (informational; values &lt;= 32 denote errors).</param>
internal sealed record ShellExecutePropertiesResult(
    bool Succeeded,
    int LastError,
    nint HInstApp);
