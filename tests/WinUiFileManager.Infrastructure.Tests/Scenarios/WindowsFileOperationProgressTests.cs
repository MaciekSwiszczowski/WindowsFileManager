using Microsoft.Extensions.Logging.Abstractions;
using WinUiFileManager.Domain.Enums;
using WinUiFileManager.Domain.Events;
using WinUiFileManager.Domain.Operations;
using WinUiFileManager.Domain.ValueObjects;
using WinUiFileManager.Infrastructure.Execution;
using WinUiFileManager.Infrastructure.Tests.Fakes;
using WinUiFileManager.Infrastructure.Tests.Fixtures;

namespace WinUiFileManager.Infrastructure.Tests.Scenarios;

public sealed class WindowsFileOperationProgressTests
{
    [Test]
    public async Task Test_CopyOperation_ReportsCurrentItemBeforeCompletion_AndReturnsCancelledSummary()
    {
        using var fixture = new NtfsTempDirectoryFixture();
        var sourcePath = fixture.CreateFile("source.txt", sizeInBytes: 128);
        var destDir = fixture.CreateDirectory("dest");
        var destinationPath = Path.Combine(destDir, "source.txt");
        var interop = new BlockingFileOperationInterop();
        var sut = new WindowsFileOperationService(
            interop,
            NullLogger<WindowsFileOperationService>.Instance);

        OperationProgressEvent? startedEvent = null;
        var progress = new Progress<OperationProgressEvent>(e =>
        {
            if (e.CompletedItems == 0 && e.CurrentItemPath is not null)
            {
                startedEvent ??= e;
            }
        });

        var plan = new OperationPlan(
            OperationType.Copy,
            [
                new OperationItemPlan(
                    NormalizedPath.FromUserInput(sourcePath),
                    NormalizedPath.FromUserInput(destinationPath),
                    ItemKind.File,
                    new FileInfo(sourcePath).Length)
            ],
            NormalizedPath.FromUserInput(destDir),
            CollisionPolicy.Ask,
            new ParallelExecutionOptions());

        using var cancellation = new CancellationTokenSource();
        var executeTask = sut.ExecuteAsync(plan, progress, cancellation.Token);

        await interop.Started.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(startedEvent).IsNotNull();
        await Assert.That(startedEvent!.CurrentItemPath!.Value.DisplayPath).IsEqualTo(sourcePath);
        await Assert.That(startedEvent.TotalItems).IsEqualTo(1);
        await Assert.That(executeTask.IsCompleted).IsFalse();

        cancellation.Cancel();
        interop.Release();

        var summary = await executeTask;
        await Assert.That(summary.Status).IsEqualTo(OperationStatus.Cancelled);
        await Assert.That(summary.WasCancelled).IsTrue();
    }
}
