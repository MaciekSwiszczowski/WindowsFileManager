using Microsoft.Extensions.Logging;

namespace WinUiFileManager.App.Startup;

public sealed class StartupChainRunner
{
    private readonly Func<StartupChain> _startupChainFactory;
    private readonly ILogger<StartupChainRunner> _logger;
    private bool _started;

    public StartupChainRunner(
        Func<StartupChain> startupChainFactory,
        ILogger<StartupChainRunner> logger)
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
