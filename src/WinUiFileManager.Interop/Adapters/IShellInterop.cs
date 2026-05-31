namespace WinUiFileManager.Interop.Adapters;

/// <summary>
/// Abstraction over the Win32 Shell "Properties" surface and its required STA-COM apartment management.
/// Implemented by <see cref="ShellInterop"/>. Exists so higher layers can request native property-sheet
/// behavior without referencing <c>Windows.Win32.*</c> directly.
/// </summary>
/// <remarks>Implementations are STA-affine: see <see cref="TryInitializeStaCom"/> / <see cref="UninitializeCom"/>.</remarks>
internal interface IShellInterop
{
    /// <summary>Shows the native shell property sheet for <paramref name="objectName"/>.</summary>
    /// <param name="objectName">Fully-qualified path of the target object.</param>
    /// <param name="lastError">Captured Win32 error on failure; <c>0</c> on success.</param>
    /// <returns><see langword="true"/> on success.</returns>
    bool ShowObjectProperties(string objectName, out int lastError);

    /// <summary>
    /// Initializes the calling thread's COM apartment as STA for subsequent shell calls.
    /// </summary>
    /// <returns><see langword="true"/> if the apartment is usable (COM was newly or already initialized).</returns>
    /// <remarks>See <see cref="ShellInterop.TryInitializeStaCom"/> for the S_OK vs S_FALSE uninitialize hazard.</remarks>
    bool TryInitializeStaCom();

    /// <summary>Releases the COM apartment via <c>CoUninitialize</c>. Must balance a matching S_OK initialize only.</summary>
    void UninitializeCom();

    /// <summary>Invokes the shell "properties" verb for <paramref name="objectName"/> via <c>ShellExecuteEx</c>.</summary>
    /// <param name="objectName">Fully-qualified path of the target object.</param>
    /// <returns>Result carrying success, Win32 error, and the shell <c>hInstApp</c>.</returns>
    ShellExecutePropertiesResult ExecutePropertiesVerb(string objectName);
}
