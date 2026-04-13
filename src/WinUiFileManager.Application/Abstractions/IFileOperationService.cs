using WinUiFileManager.Domain.Events;
using WinUiFileManager.Domain.Operations;
using WinUiFileManager.Domain.Results;

namespace WinUiFileManager.Application.Abstractions;

public interface IFileOperationService
{
    Task<OperationSummary> ExecuteAsync(
        OperationPlan plan,
        IProgress<OperationProgressEvent>? progress,
        CancellationToken cancellationToken);
}
