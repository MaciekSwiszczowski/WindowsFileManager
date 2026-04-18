using WinUiFileManager.Application.Tests.Fakes;

namespace WinUiFileManager.Application.Tests.Scenarios;

public sealed class ViewModelOperationProgressTests
{
    [Test]
    public async Task Test_CopyCommand_ShowsProgressAndRoutesCancellation()
    {
        using var _ = new SynchronizationContextScope(null);
        using var fixture = new NtfsTempDirectoryFixture();
        var sourceDir = fixture.CreateDirectory("source");
        var destDir = fixture.CreateDirectory("dest");
        fixture.CreateFile("source/target.txt", sizeInBytes: 64);

        var operationService = new BlockingFileOperationService();
        var builder = new ViewModelTestBuilder
        {
            FileOperationServiceOverride = operationService
        };

        var vm = builder.Build();
        vm.LeftPane.PaneId = PaneId.Left;
        vm.RightPane.PaneId = PaneId.Right;

        await vm.LeftPane.NavigateToCommand.ExecuteAsync(sourceDir);
        await vm.RightPane.NavigateToCommand.ExecuteAsync(destDir);

        var targetEntry = vm.LeftPane.Items.First(i => i.Name == "target.txt");
        vm.LeftPane.CurrentItem = targetEntry;

        var copyTask = vm.CopyCommand.ExecuteAsync(null);
        await operationService.Started.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(vm.OperationProgress.IsVisible).IsTrue();
        await Assert.That(vm.OperationProgress.IsRunning).IsTrue();
        await Assert.That(vm.OperationProgress.OperationName).IsEqualTo("Copy");
        await Assert.That(operationService.LastPlan).IsNotNull();
        await Assert.That(operationService.LastPlan!.Type).IsEqualTo(OperationType.Copy);
        await Assert.That(operationService.LastPlan.Items.Count).IsEqualTo(1);

        vm.OperationProgress.CancelCommand.Execute(null);
        await copyTask;

        await Assert.That(operationService.LastCancellationToken.IsCancellationRequested).IsTrue();
        await Assert.That(builder.DialogService.LastOperationResult).IsNotNull();
        await Assert.That(builder.DialogService.LastOperationResult!.WasCancelled).IsTrue();
        await Assert.That(vm.OperationProgress.IsVisible).IsFalse();
    }

    [Test]
    public async Task Test_CopyCommand_IgnoresReentryWhileOperationIsRunning()
    {
        using var _ = new SynchronizationContextScope(null);
        using var fixture = new NtfsTempDirectoryFixture();
        var sourceDir = fixture.CreateDirectory("source");
        var destDir = fixture.CreateDirectory("dest");
        fixture.CreateFile("source/target.txt", sizeInBytes: 64);

        var operationService = new BlockingFileOperationService();
        var builder = new ViewModelTestBuilder
        {
            FileOperationServiceOverride = operationService
        };

        var vm = builder.Build();
        vm.LeftPane.PaneId = PaneId.Left;
        vm.RightPane.PaneId = PaneId.Right;

        await vm.LeftPane.NavigateToCommand.ExecuteAsync(sourceDir);
        await vm.RightPane.NavigateToCommand.ExecuteAsync(destDir);

        vm.LeftPane.CurrentItem = vm.LeftPane.Items.First(i => i.Name == "target.txt");

        var firstCopyTask = vm.CopyCommand.ExecuteAsync(null);
        await operationService.Started.WaitAsync(TimeSpan.FromSeconds(5));

        await vm.CopyCommand.ExecuteAsync(null);

        await Assert.That(operationService.ExecuteCallCount).IsEqualTo(1);

        vm.OperationProgress.CancelCommand.Execute(null);
        await firstCopyTask;
    }

    private sealed class SynchronizationContextScope : IDisposable
    {
        private readonly SynchronizationContext? _previous = SynchronizationContext.Current;

        public SynchronizationContextScope(SynchronizationContext? next)
        {
            SynchronizationContext.SetSynchronizationContext(next);
        }

        public void Dispose()
        {
            SynchronizationContext.SetSynchronizationContext(_previous);
        }
    }
}
