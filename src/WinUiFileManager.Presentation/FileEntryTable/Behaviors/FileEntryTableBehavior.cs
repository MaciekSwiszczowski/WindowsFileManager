namespace WinUiFileManager.Presentation.FileEntryTable.Behaviors;

public abstract class FileEntryTableBehavior : Behavior<SpecFileEntryTableView>
{
    protected FileEntryTableNavigationState? NavigationState { get; private set; }

    protected override void OnAttached()
    {
        base.OnAttached();
        NavigationState = AssociatedObject.NavigationState;
    }

    protected override void OnDetaching()
    {
        NavigationState = null;
        base.OnDetaching();
    }
}
