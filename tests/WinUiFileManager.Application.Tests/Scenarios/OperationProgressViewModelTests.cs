using Microsoft.UI.Xaml;
using WinUiFileManager.Presentation.ViewModels;

namespace WinUiFileManager.Application.Tests.Scenarios;

public sealed class OperationProgressViewModelTests
{
    [Test]
    public async Task Test_Start_Report_Cancel_And_Reset_UpdatesState()
    {
        var sut = new OperationProgressViewModel();
        var path = NormalizedPath.FromUserInput(@"C:\temp\source.txt");

        sut.Start(OperationType.Copy);
        sut.ReportProgress(new OperationProgressEvent(
            OperationType.Copy,
            4,
            2,
            100,
            50,
            path,
            "Processing item"));

        await Assert.That(sut.OperationName).IsEqualTo("Copy");
        await Assert.That(sut.TotalItems).IsEqualTo(4);
        await Assert.That(sut.CompletedItems).IsEqualTo(2);
        await Assert.That(sut.CurrentItemPath).IsEqualTo(path.DisplayPath);
        await Assert.That(sut.ProgressPercentage).IsEqualTo(50d);
        await Assert.That(sut.PanelVisibility).IsEqualTo(Visibility.Visible);
        await Assert.That(sut.CanCancel).IsTrue();

        sut.CancelCommand.Execute(null);

        await Assert.That(sut.CancellationToken.IsCancellationRequested).IsTrue();
        await Assert.That(sut.CanCancel).IsFalse();

        sut.Reset();

        await Assert.That(sut.IsVisible).IsFalse();
        await Assert.That(sut.IsRunning).IsFalse();
        await Assert.That(sut.PanelVisibility).IsEqualTo(Visibility.Collapsed);
        await Assert.That(sut.TotalItems).IsEqualTo(0);
        await Assert.That(sut.CurrentItemPath).IsNull();
    }

    [Test]
    public async Task Test_Start_WithDelete_SetsFriendlyOperationName()
    {
        var sut = new OperationProgressViewModel();

        sut.Start(OperationType.Delete);

        await Assert.That(sut.OperationName).IsEqualTo("Delete");
        await Assert.That(sut.StatusMessage).IsEqualTo("Delete in progress");
        await Assert.That(sut.IsRunning).IsTrue();
    }
}
