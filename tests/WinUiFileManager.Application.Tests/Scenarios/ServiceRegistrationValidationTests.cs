using Microsoft.Extensions.DependencyInjection;
using TUnit.Core;
using WinUiFileManager.Diagnostics;
using WinUiFileManager.Application.Navigation;
using WinUiFileManager.Application.Settings;
using WinUiFileManager.Infrastructure;
using WinUiFileManager.Presentation.Services;
using WinUiFileManager.Presentation.ViewModels;
using WinUiFileManager.Presentation.ViewModels.Inspector;
using WinUiFileManager.Presentation.ViewModels.Inspector.Buttons;
using WinUiFileManager.Presentation.ViewModels.Inspector.Fields;
using WinUiFileManager.Presentation.ViewModels.Inspector.Search;

namespace WinUiFileManager.Application.Tests.Scenarios;

public sealed class ServiceRegistrationValidationTests
{
    [Test]
    public async Task Test_ServiceGraph_BuildsWithValidation()
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddInfrastructureServices();
        services.AddDiagnosticsServices();

        services.AddSingleton<IClipboardService, Fakes.FakeClipboardService>();
        services.AddSingleton<IShellService, Fakes.FakeShellService>();
        services.AddSingleton<DialogService>();
        services.AddSingleton<ISettingsRepository, Fakes.FakeSettingsRepository>();

        services.AddSingleton<PanelNavigationService>();

        services.AddSingleton<SetParallelExecutionCommandHandler>();
        services.AddSingleton<PersistPaneStateCommandHandler>();

        services.AddTransient<AppInitializationViewModel>();
        services.AddTransient<PanelsViewModel>();
        services.AddTransient<CommandButtonsViewModel>();
        services.AddTransient<InspectorInitializationViewModel>();
        services.AddTransient<InspectorRefreshButtonViewModel>();
        services.AddTransient<InspectorPropertiesButtonViewModel>();
        services.AddTransient<InspectorCopyToClipboardButtonViewModel>();
        services.AddTransient<InspectorSearchViewModel>();
        services.AddTransient<InspectorAttributeToggleViewModel>();
        services.AddTransient<InspectorViewModel>();
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
