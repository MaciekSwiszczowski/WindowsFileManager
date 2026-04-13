namespace WinUiFileManager.Presentation.Services;

public interface IUiDispatcher
{
    void Invoke(Action action);

    Task InvokeAsync(Func<Task> func);
}
