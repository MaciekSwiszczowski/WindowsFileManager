namespace WinUiFileManager.Presentation.FileEntryTable.Behaviors;

public abstract class FileEntryTableBehaviorBase : Behavior<SpecFileEntryTableView>
{
    private FileEntryTableBehaviorContext? _context;

    protected FileEntryTableBehaviorContext Context =>
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

    protected virtual void OnLoaded(FileEntryTableBehaviorContext context)
    {
    }

    protected virtual void OnUnloaded(FileEntryTableBehaviorContext context)
    {
    }

    protected bool IsLoaded => _context is not null;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_context is not null)
        {
            return;
        }

        _context = FileEntryTableBehaviorContext.Create(AssociatedObject);
        OnLoaded(_context);
    }
}
