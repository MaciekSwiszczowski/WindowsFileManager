namespace WinUiFileManager.App.Startup;

public sealed class StartupChain
{
    public Task StartupChainAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
