using WinUiFileManager.Domain.Events;

namespace WinUiFileManager.Application.Abstractions;

public interface IOperationProgressDialog
{
    void ReportProgress(OperationProgressEvent progressEvent);

    Task CloseAsync(CancellationToken ct);
}
