using WinUiFileManager.Domain.ValueObjects;

namespace WinUiFileManager.Presentation.Models;

public sealed class PaneNavigationState
{
    private readonly List<NormalizedPath> _backStack = [];

    public NormalizedPath? CurrentPath { get; private set; }

    public IReadOnlyList<NormalizedPath> BackStack => _backStack.AsReadOnly();

    public int CurrentIndex { get; private set; } = -1;

    public bool CanGoBack => CurrentIndex > 0;

    public void Push(NormalizedPath path)
    {
        CurrentIndex++;

        if (CurrentIndex < _backStack.Count)
        {
            _backStack.RemoveRange(CurrentIndex, _backStack.Count - CurrentIndex);
        }

        _backStack.Add(path);
        CurrentPath = path;
    }

    public NormalizedPath? GoBack()
    {
        if (!CanGoBack)
        {
            return null;
        }

        CurrentIndex--;
        CurrentPath = _backStack[CurrentIndex];
        return CurrentPath;
    }
}
