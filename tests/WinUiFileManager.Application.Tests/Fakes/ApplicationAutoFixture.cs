using AutoFixture.Kernel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using WinUiFileManager.Application.Messaging;
using WinUiFileManager.Presentation.Messaging;
using WinUiFileManager.FileListingEngine;
using WinUiFileManager.Presentation.Services;
using WinUiFileManager.Presentation.ViewModels.Inspector.Fields;

namespace WinUiFileManager.Application.Tests.Fakes;

public static class ApplicationAutoFixture
{
    public static IFixture Create()
    {
        var fixture = new Fixture
        {
            OmitAutoProperties = true,
            RepeatCount = 1,
        };

        fixture.Customizations.Add(new NullLoggerSpecimenBuilder());

        var messenger = new FileManagerMessenger(new StrongReferenceMessenger());
        fixture.Inject<IMessenger>(messenger);
        fixture.Inject<IFileManagerMessenger>(messenger);
        fixture.Inject(new SynchronizationContext());
        fixture.Inject(TimeProvider.System);
        fixture.Inject(FileEntryDisplayStringCache.Shared);
        fixture.Inject<ILogger<MainShellViewModel>>(NullLogger<MainShellViewModel>.Instance);
        fixture.Inject<ILogger<SetParallelExecutionCommandHandler>>(
            NullLogger<SetParallelExecutionCommandHandler>.Instance);
        fixture.Inject<ILogger<PersistPaneStateCommandHandler>>(
            NullLogger<PersistPaneStateCommandHandler>.Instance);

        RegisterFakes(fixture);
        RegisterInspectorLoaders(fixture);

        return fixture;
    }

    private static void RegisterFakes(IFixture fixture)
    {
        var clipboard = new FakeClipboardService();
        fixture.Inject(clipboard);
        fixture.Inject<IClipboardService>(clipboard);

        var settings = new FakeSettingsRepository();
        fixture.Inject(settings);
        fixture.Inject<ISettingsRepository>(settings);

        var shell = new FakeShellService();
        fixture.Inject(shell);
        fixture.Inject<IShellService>(shell);

        fixture.Inject<IActivePanelsService>(new FakeActivePanelsService());
        fixture.Inject<IFolderListingScanner>(new FakeFolderEntryScanner());
        fixture.Inject<IFileListingRowReader>(new FakeFileListingRowReader());
        fixture.Inject<IDirectoryChangeStream>(new FakeDirectoryChangeStream());
    }

    private static void RegisterInspectorLoaders(IFixture fixture)
    {
        var context = new SpecimenContext(fixture);
        var loaderTypes = typeof(IInspectorDeferredFieldLoader)
            .Assembly
            .GetTypes()
            .Where(static type =>
                type is { IsAbstract: false, IsInterface: false }
                && typeof(IInspectorDeferredFieldLoader).IsAssignableFrom(type))
            .ToArray();

        fixture.Register<IEnumerable<IInspectorDeferredFieldLoader>>(() =>
            loaderTypes
                .Select(type => (IInspectorDeferredFieldLoader)context.Resolve(type))
                .ToList());
    }

    private sealed class NullLoggerSpecimenBuilder : ISpecimenBuilder
    {
        public object Create(object request, ISpecimenContext context)
        {
            if (request is not Type { IsGenericType: true } type
                || type.GetGenericTypeDefinition() != typeof(ILogger<>))
            {
                return new NoSpecimen();
            }

            var loggerType = typeof(NullLogger<>).MakeGenericType(type.GenericTypeArguments[0]);
            var instanceProperty = loggerType.GetProperty(nameof(NullLogger<object>.Instance));
            var value = instanceProperty?.GetValue(null);
            return value ?? new NoSpecimen();
        }
    }
}
