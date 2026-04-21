using WinUiFileManager.Interop.Adapters;
using WinUiFileManager.Interop.Types;

namespace WinUiFileManager.Infrastructure.Tests.Fakes;

public sealed class BlockingFileOperationInterop : IFileOperationInterop
{
    private readonly ManualResetEventSlim _release = new(false);
    private readonly TaskCompletionSource _started =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task Started => _started.Task;

    public void Release()
    {
        _release.Set();
    }

    public InteropResult CopyFile(string source, string destination, bool overwrite)
    {
        _started.TrySetResult();
        _ = SpinWait.SpinUntil(() => _release.IsSet, TimeSpan.FromSeconds(5));
        return InteropResult.Ok();
    }

    public InteropResult MoveFile(string source, string destination, bool overwrite) => InteropResult.Ok();

    public InteropResult MoveDirectory(string source, string destination) => InteropResult.Ok();

    public InteropResult DeleteFile(string path) => InteropResult.Ok();

    public InteropResult RemoveDirectory(string path) => InteropResult.Ok();

    public InteropResult CreateDirectory(string path) => InteropResult.Ok();

    public InteropResult SetFileAttributes(string path, uint attributes) => InteropResult.Ok();
}
