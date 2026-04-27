namespace WinUiFileManager.Presentation.FileEntryTable;

internal sealed class ActiveFileEntryRowState
{
    private readonly Func<object?> _resolveParentRowContainer;
    private readonly Func<SpecFileEntryViewModel, object?> _resolveBodyItemContainer;
    private readonly Action<object?, bool> _setActiveRowIndicator;

    private bool _isParentRowActive;
    private SpecFileEntryViewModel? _activeBodyItem;

    public ActiveFileEntryRowState(
        Func<object?> resolveParentRowContainer,
        Func<SpecFileEntryViewModel, object?> resolveBodyItemContainer,
        Action<object?, bool> setActiveRowIndicator)
    {
        ArgumentNullException.ThrowIfNull(resolveParentRowContainer);
        ArgumentNullException.ThrowIfNull(resolveBodyItemContainer);
        ArgumentNullException.ThrowIfNull(setActiveRowIndicator);

        _resolveParentRowContainer = resolveParentRowContainer;
        _resolveBodyItemContainer = resolveBodyItemContainer;
        _setActiveRowIndicator = setActiveRowIndicator;
    }

    public void ActivateParentRow()
    {
        ClearActiveRowIndicator();
        _isParentRowActive = true;
        _activeBodyItem = null;
        _setActiveRowIndicator(_resolveParentRowContainer(), true);
    }

    public void ActivateBodyRow(SpecFileEntryViewModel item)
    {
        ArgumentNullException.ThrowIfNull(item);

        ClearActiveRowIndicator();
        _isParentRowActive = false;
        _activeBodyItem = item;
        _setActiveRowIndicator(_resolveBodyItemContainer(item), true);
    }

    private void ClearActiveRowIndicator()
    {
        if (_isParentRowActive)
        {
            _setActiveRowIndicator(_resolveParentRowContainer(), false);
        }
        else if (_activeBodyItem is { } item)
        {
            _setActiveRowIndicator(_resolveBodyItemContainer(item), false);
        }
    }
}
