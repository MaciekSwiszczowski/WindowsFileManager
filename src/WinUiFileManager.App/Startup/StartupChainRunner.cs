using Microsoft.Extensions.Logging;

namespace WinUiFileManager.App.Startup;

public sealed class StartupChainRunner
{
    private readonly StartupChain _startupChain;
    private readonly ILogger<StartupChainRunner> _logger;
    private bool _started;

    public StartupChainRunner(StartupChain startupChain, ILogger<StartupChainRunner> logger)
    {
        _startupChain = startupChain;
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
            await _startupChain.StartupChainAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Startup chain failed");
        }
    }
}
