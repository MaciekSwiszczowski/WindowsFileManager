namespace WinUiFileManager.Presentation.FileEntryTable.Behaviors;

public abstract class FileEntryTableBehaviorBase : Behavior<SpecFileEntryTableView>
{
    private FileEntryTableContext? _context;

    protected FileEntryTableContext Context =>
        _context ?? throw new InvalidOperationException($"{GetType().Name} is not loaded.");

    protected override void OnAttached()
    {
        base.OnAttached();
        AssociatedObject.Loaded += OnLoaded;
    }

    protected override void OnDetaching()
    {
        AssociatedObject.Loaded -= OnLoaded;

        if (_context is not null)
        {
            OnUnloaded(_context);
            _context.Messenger.UnregisterAll(this);
            _context = null;
        }

        base.OnDetaching();
    }

    protected virtual void OnLoaded(FileEntryTableContext context)
    {
    }

    protected virtual void OnUnloaded(FileEntryTableContext context)
    {
    }

    protected bool IsLoaded => _context is not null;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_context is not null)
        {
            return;
        }

        _context = FileEntryTableContext.Create(AssociatedObject);
        OnLoaded(_context);
    }
}
