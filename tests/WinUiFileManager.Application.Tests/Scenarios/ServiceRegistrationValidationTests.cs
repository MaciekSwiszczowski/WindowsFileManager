using Autofac;
using Autofac.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using TUnit.Core;
using WinUiFileManager.Application.Dialogs;
using WinUiFileManager.Diagnostics;
using WinUiFileManager.Diagnostics.FileOperations;
using WinUiFileManager.Application.Navigation;
using WinUiFileManager.Application.Settings;
using WinUiFileManager.Infrastructure;
using WinUiFileManager.Presentation.FileEntryTableData;
using WinUiFileManager.Presentation.Services;
using WinUiFileManager.Presentation.ViewModels;
using WinUiFileManager.Presentation.ViewModels.Inspector.Fields;
using WinUiFileManager.Presentation.ViewModels.Panels;

namespace WinUiFileManager.Application.Tests.Scenarios;

public sealed class ServiceRegistrationValidationTests
{
    [Test]
    public async Task Test_ServiceGraph_BuildsWithValidation()
    {
        var builder = new ContainerBuilder();

        var services = new ServiceCollection();
        services.AddLogging();
        builder.Populate(services);

        builder.AddInfrastructureServices();
        builder.AddDiagnosticsServices();
        builder.AddFileEntryTableDataServices();
        builder.AddPresentationViewModels();

        builder.RegisterType<Fakes.FakeClipboardService>().As<IClipboardService>().SingleInstance();
        builder.RegisterType<Fakes.FakeShellService>().As<IShellService>().SingleInstance();
        builder.RegisterType<DialogService>().SingleInstance();
        builder.RegisterType<Fakes.FakeSettingsRepository>().As<ISettingsRepository>().SingleInstance();

        builder.RegisterType<PanelNavigationService>().SingleInstance();

        builder.RegisterType<SetParallelExecutionCommandHandler>().SingleInstance();
        builder.RegisterType<PersistPaneStateCommandHandler>().SingleInstance();

        builder.RegisterType<MessageDialogViewModel>().InstancePerDependency();
        builder.RegisterType<RenameDialogViewModel>().InstancePerDependency();

        using var container = builder.Build();
        var shell = container.Resolve<MainShellViewModel>();
        var rowFactory = container.Resolve<SpecFileEntryViewModel.Factory>();
        var panelFactory = container.Resolve<PanelViewModel.Factory>();
        var textFieldFactory = container.Resolve<InspectorFieldViewModel.Factory>();
        var legacyFileInspector = container.Resolve<FileInspectorViewModel>();
        var renameService = container.Resolve<RenameService>();
        var fileOperationHandler = container.Resolve<FileOperationRequestHandler>();

        await Assert.That(shell).IsNotNull();
        await Assert.That(shell.Inspector).IsNotNull();
        await Assert.That(rowFactory(CreateEntry())).IsNotNull();
        await Assert.That(panelFactory("Test")).IsNotNull();
        await Assert.That(textFieldFactory(FileInspectorCategory.Basic, "Name", "Tooltip", string.Empty)).IsNotNull();
        await Assert.That(legacyFileInspector).IsNotNull();
        await Assert.That(renameService).IsNotNull();
        await Assert.That(fileOperationHandler).IsNotNull();
    }

    private static FileSystemEntryModel CreateEntry() =>
        new(
            NormalizedPath.FromUserInput("C:\\Temp"),
            "file.txt",
            ".txt",
            ItemKind.File,
            1,
            DateTime.Now,
            DateTime.Now,
            FileAttributes.Archive);
}
