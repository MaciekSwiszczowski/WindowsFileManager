namespace WinUiFileManager.Presentation.FileEntryTable;

internal sealed class ActiveFileEntryRowState
{
    private readonly SpecFileEntryViewModel _parentRow;
    private readonly Func<DependencyObject?> _resolveParentRowContainer;
    private readonly Func<SpecFileEntryViewModel, DependencyObject?> _resolveBodyItemContainer;
    private readonly Action<DependencyObject?, bool> _setActiveRowIndicator;

    public ActiveFileEntryRowState(
        SpecFileEntryViewModel parentRow,
        Func<DependencyObject?> resolveParentRowContainer,
        Func<SpecFileEntryViewModel, DependencyObject?> resolveBodyItemContainer,
        Action<DependencyObject?, bool> setActiveRowIndicator)
    {
        ArgumentNullException.ThrowIfNull(parentRow);
        ArgumentNullException.ThrowIfNull(resolveParentRowContainer);
        ArgumentNullException.ThrowIfNull(resolveBodyItemContainer);
        ArgumentNullException.ThrowIfNull(setActiveRowIndicator);

        _parentRow = parentRow;
        _resolveParentRowContainer = resolveParentRowContainer;
        _resolveBodyItemContainer = resolveBodyItemContainer;
        _setActiveRowIndicator = setActiveRowIndicator;
    }

    public bool IsParentRowRemembered { get; private set; }

    public SpecFileEntryViewModel? RememberedBodyItem { get; private set; }

    public bool IsIndicatorVisible { get; private set; }

    public bool IsParentRowActive => IsIndicatorVisible && IsParentRowRemembered;

    public void ActivateParentRow()
    {
        ClearRememberedActiveRowIndicator();
        IsParentRowRemembered = true;
        RememberedBodyItem = null;
        IsIndicatorVisible = true;
        ApplyRememberedActiveRowIndicator();
    }

    public void ActivateBodyRow(SpecFileEntryViewModel item)
    {
        ArgumentNullException.ThrowIfNull(item);

        ClearRememberedActiveRowIndicator();
        IsParentRowRemembered = false;
        RememberedBodyItem = item;
        IsIndicatorVisible = true;
        ApplyRememberedActiveRowIndicator();
    }

    public void HideIndicator()
    {
        ClearRememberedActiveRowIndicator();
        IsIndicatorVisible = false;
    }

    public void ShowIndicatorIfActiveRowExists(bool parentRowExists, IReadOnlyCollection<SpecFileEntryViewModel> visibleItems)
    {
        ValidateRows(parentRowExists, visibleItems);

        if (IsParentRowRemembered || RememberedBodyItem is not null)
        {
            IsIndicatorVisible = true;
        }

        ApplyRememberedActiveRowIndicator();
    }

    public void ValidateActiveRow(bool parentRowExists, IReadOnlyCollection<SpecFileEntryViewModel> visibleItems)
    {
        ClearRememberedActiveRowIndicator();
        ValidateRows(parentRowExists, visibleItems);
        ApplyRememberedActiveRowIndicator();
    }

    public bool IsBodyRowActive(SpecFileEntryViewModel item) =>
        IsIndicatorVisible && ReferenceEquals(RememberedBodyItem, item);

    public bool ShouldShowForItem(object? item) =>
        IsIndicatorVisible
        && (IsParentRowRemembered
            ? ReferenceEquals(_parentRow, item)
            : ReferenceEquals(RememberedBodyItem, item));

    private void ValidateRows(bool parentRowExists, IReadOnlyCollection<SpecFileEntryViewModel> visibleItems)
    {
        if (IsParentRowRemembered && !parentRowExists ||
            RememberedBodyItem is not null && !visibleItems.Contains(RememberedBodyItem))
        {
            Clear();
        }
    }

    private void ClearRememberedActiveRowIndicator()
    {
        if (IsParentRowRemembered)
        {
            _setActiveRowIndicator(_resolveParentRowContainer(), false);
        }
        else if (RememberedBodyItem is { } item)
        {
            _setActiveRowIndicator(_resolveBodyItemContainer(item), false);
        }
    }

    private void ApplyRememberedActiveRowIndicator()
    {
        if (!IsIndicatorVisible)
        {
            return;
        }

        if (IsParentRowRemembered)
        {
            _setActiveRowIndicator(_resolveParentRowContainer(), true);
        }
        else if (RememberedBodyItem is { } item)
        {
            _setActiveRowIndicator(_resolveBodyItemContainer(item), true);
        }
    }

    private void Clear()
    {
        IsParentRowRemembered = false;
        RememberedBodyItem = null;
        IsIndicatorVisible = false;
    }
}
