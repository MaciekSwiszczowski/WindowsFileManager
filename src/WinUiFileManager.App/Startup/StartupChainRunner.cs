using Microsoft.Extensions.Logging;

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

    public void Start()
    {
        if (_started)
        {
            return;
        }

        _started = true;
        _ = Task.Run(RunAsync);
    }

    private async Task RunAsync()
    {
        try
        {
            var startupChain = _startupChainFactory();
            await startupChain.StartupChainAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Startup chain failed");
        }
    }
}
