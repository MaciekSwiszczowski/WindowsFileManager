using Microsoft.Extensions.Logging;
using WinUiFileManager.Application.Settings;

namespace WinUiFileManager.App.Startup;

/// <summary>
/// Starts <see cref="StartupChain"/> once on the thread pool and reports startup failures.
/// </summary>
/// <remarks>
/// Allowed here: fire-and-forget scheduling of the DI-owned startup chain and centralized exception
/// logging for that background work.
/// Not allowed here: awaiting startup from the WinUI launch path, passing view or view model instances
/// from code-behind, running startup inline on the caller thread, or exposing startup tasks to controls.
/// </remarks>
public sealed class StartupChainRunner
{
    private readonly Func<StartupChain> _startupChainFactory;
    private readonly ILogger<StartupChainRunner> _logger;
    private bool _started;

    public StartupChainRunner(Func<StartupChain> startupChainFactory, ILogger<StartupChainRunner> logger)
    {
        _startupChainFactory = startupChainFactory;
        _logger = logger;
    }

    /// <summary>
    /// Schedules the startup chain to run once on the thread pool. Returns immediately.
    /// </summary>
    /// <param name="settings">Settings already loaded by the launch path before the shell window is shown.</param>
    /// <remarks>
    /// Called from the UI launch path, so it must not block. The <see cref="_started"/> guard makes this
    /// idempotent — repeated calls are no-ops, which matters because <see cref="StartupChain"/>'s own
    /// <c>Initialize()</c> calls are not idempotent and must run exactly once (AGENTS.md §4).
    /// The <see cref="Task.Run(System.Action)"/> result is intentionally discarded (fire-and-forget);
    /// failures are not surfaced to the caller but are caught and logged in <see cref="RunAsync"/>.
    /// This field is only touched on the UI thread, so no synchronization is needed.
    /// </remarks>
    public void Start(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (_started)
        {
            return;
        }

        _started = true;
        _ = Task.Run(() => RunAsync(settings));
    }

    /// <summary>
    /// Resolves a fresh <see cref="StartupChain"/> and awaits it, logging any failure.
    /// </summary>
    /// <remarks>
    /// Runs on a thread-pool thread. The top-level <c>try/catch</c> is mandatory: this task is not
    /// awaited by anyone, so an unobserved exception would otherwise be swallowed silently (or crash on
    /// finalization). The chain is obtained via a factory so a new instance is built at start time
    /// rather than at runner-construction time.
    /// </remarks>
    private async Task RunAsync(AppSettings settings)
    {
        try
        {
            var startupChain = _startupChainFactory();
            await startupChain.StartupChainAsync(settings).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Startup chain failed");
        }
    }
}
