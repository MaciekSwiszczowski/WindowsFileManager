using System.Reflection;

namespace WinUiFileManager.Presentation;

/// <summary>
/// Runtime build-configuration probe. Used to show or hide Debug-only developer affordances (e.g. the inspector
/// profiling controls) without sprinkling <c>#if DEBUG</c> through view models or XAML.
/// </summary>
/// <remarks>
/// Reads the assembly's <see cref="AssemblyConfigurationAttribute"/> (emitted by the SDK as the build
/// configuration name) once. Any configuration whose name starts with "Debug" — including <c>Debug_Analyzers</c> —
/// counts as a debug build, matching where the <c>DEBUG</c> compilation symbol is defined for this repo.
/// </remarks>
public static class BuildEnvironment
{
    /// <summary>True when this assembly was built in a Debug (or Debug_Analyzers) configuration.</summary>
    public static bool IsDebug { get; } =
        typeof(BuildEnvironment).Assembly
            .GetCustomAttribute<AssemblyConfigurationAttribute>()?
            .Configuration
            .StartsWith("Debug", StringComparison.OrdinalIgnoreCase) ?? false;
}
