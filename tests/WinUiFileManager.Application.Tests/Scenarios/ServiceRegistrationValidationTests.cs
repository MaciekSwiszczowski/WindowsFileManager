using Microsoft.Extensions.DependencyInjection;
using TUnit.Core;
using WinUiFileManager.Application.Favourites;
using WinUiFileManager.Application.Navigation;
using WinUiFileManager.Application.Settings;
using WinUiFileManager.Infrastructure;
using WinUiFileManager.Presentation.FileEntryTable.Data;
using WinUiFileManager.Presentation.Services;
using WinUiFileManager.Presentation.ViewModels;

namespace WinUiFileManager.Application.Tests.Scenarios;

public sealed class ServiceRegistrationValidationTests
{
    [Test]
    public async Task Test_ServiceGraph_BuildsWithValidation()
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddInfrastructureServices();

        services.AddSingleton<IClipboardService, Fakes.FakeClipboardService>();
        services.AddSingleton<IShellService, Fakes.FakeShellService>();
        services.AddSingleton<DialogService>();
        services.AddSingleton<FileEntryTableDataSourceFactory>();
        services.AddSingleton<IFavouritesRepository, Fakes.FakeFavouritesRepository>();
        services.AddSingleton<ISettingsRepository, Fakes.FakeSettingsRepository>();

        services.AddSingleton<GoToPathCommandHandler>();
        services.AddSingleton<PanelNavigationService>();

        services.AddSingleton<AddFavouriteCommandHandler>();
        services.AddSingleton<RemoveFavouriteCommandHandler>();
        services.AddSingleton<OpenFavouriteCommandHandler>();

        services.AddSingleton<SetParallelExecutionCommandHandler>();
        services.AddSingleton<PersistPaneStateCommandHandler>();

        services.AddTransient<AppInitializationViewModel>();
        services.AddTransient<PanelsViewModel>();
        services.AddTransient<CommandButtonsViewModel>();
        services.AddTransient<FileInspectorViewModel>();
        services.AddTransient<MainShellViewModel>();
        services.AddTransient<StatusBarViewModel>();

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });

        var shell = provider.GetRequiredService<MainShellViewModel>();

        await Assert.That(shell).IsNotNull();
        await Assert.That(shell.Inspector).IsNotNull();
    }
}
