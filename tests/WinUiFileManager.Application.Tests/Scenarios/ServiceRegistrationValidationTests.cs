using Microsoft.Extensions.DependencyInjection;
using TUnit.Core;
using WinUiFileManager.Application.Favourites;
using WinUiFileManager.Application.FileOperations;
using WinUiFileManager.Application.Navigation;
using WinUiFileManager.Application.Settings;
using WinUiFileManager.Infrastructure;
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

        services.AddSingleton<IDialogService, Fakes.FakeDialogService>();
        services.AddSingleton<IClipboardService, Fakes.FakeClipboardService>();
        services.AddSingleton<IShellService, Fakes.FakeShellService>();
        services.AddSingleton<FileTableFocusService>();
        services.AddSingleton<IFavouritesRepository, Fakes.FakeFavouritesRepository>();
        services.AddSingleton<ISettingsRepository, Fakes.FakeSettingsRepository>();

        services.AddSingleton<OpenEntryCommandHandler>();
        services.AddSingleton<NavigateUpCommandHandler>();
        services.AddSingleton<GoToPathCommandHandler>();
        services.AddSingleton<RefreshPaneCommandHandler>();

        services.AddSingleton<CopySelectionCommandHandler>();
        services.AddSingleton<MoveSelectionCommandHandler>();
        services.AddSingleton<DeleteSelectionCommandHandler>();
        services.AddSingleton<CreateFolderCommandHandler>();
        services.AddSingleton<RenameEntryCommandHandler>();
        services.AddSingleton<CopyFullPathCommandHandler>();

        services.AddSingleton<AddFavouriteCommandHandler>();
        services.AddSingleton<RemoveFavouriteCommandHandler>();
        services.AddSingleton<OpenFavouriteCommandHandler>();

        services.AddSingleton<SetParallelExecutionCommandHandler>();
        services.AddSingleton<PersistPaneStateCommandHandler>();

        services.AddTransient<FilePaneViewModel>();
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
        await Assert.That(shell.LeftPane).IsNotNull();
        await Assert.That(shell.RightPane).IsNotNull();
    }
}
