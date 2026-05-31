namespace WinUiFileManager.Presentation.FileEntryTable.Behaviors;

/// <summary>
/// Base class for XAML <see cref="Behavior{T}"/> implementations attached to a
/// <see cref="SpecFileEntryTableView"/>. It standardises the attach/load/unload/detach lifecycle so
/// derived behaviors only deal with a resolved <see cref="FileEntryTableContext"/> instead of poking
/// at the raw view.
/// </summary>
/// <remarks>
/// Lifecycle and leak discipline (see AGENTS.md §4/§5):
/// <list type="bullet">
/// <item><see cref="OnAttached"/> subscribes to <see cref="FrameworkElement.Loaded"/>; the matching
/// <c>-=</c> happens in <see cref="OnDetaching"/>, so the subscription is always balanced.</item>
/// <item>The <see cref="FileEntryTableContext"/> (and therefore the table, messenger, and navigation
/// state) is only resolved once the view is actually loaded, because the visual tree is not ready at
/// attach time.</item>
/// <item><see cref="OnDetaching"/> centralises messenger cleanup by calling
/// <c>Messenger.UnregisterAll(this)</c> for every derived behavior. This is the single point that
/// guarantees a behavior that registered handlers in <see cref="OnLoaded(FileEntryTableContext)"/>
/// does not leak via <see cref="CommunityToolkit.Mvvm.Messaging.StrongReferenceMessenger"/>.
/// Derived behaviors therefore do not need to unregister messenger handlers themselves; they only
/// reverse UI event subscriptions in <see cref="OnUnloaded(FileEntryTableContext)"/>.</item>
/// </list>
/// </remarks>
public abstract class FileEntryTableBehaviorBase : Behavior<SpecFileEntryTableView>
{
    private FileEntryTableContext? _context;

    /// <summary>
    /// The resolved context for the associated view. Valid only between load and unload.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when accessed before the view has loaded
    /// (i.e. before <see cref="OnLoaded(FileEntryTableContext)"/> has run).</exception>
    protected FileEntryTableContext Context =>
        _context ?? throw new InvalidOperationException($"{GetType().Name} is not loaded.");

    protected override void OnAttached()
    {
        base.OnAttached();
        // The visual tree / messenger are not available yet at attach time, so defer context
        // creation until Loaded fires. Removed again in OnDetaching to keep the +=/-= balanced.
        AssociatedObject.Loaded += OnLoaded;
    }

    protected override void OnDetaching()
    {
        AssociatedObject.Loaded -= OnLoaded;

        if (_context is not null)
        {
            // Let the derived behavior reverse its own UI event subscriptions first...
            OnUnloaded(_context);
            // ...then drop every messenger registration this behavior made. Doing it here (rather
            // than in each derived type) is what prevents StrongReferenceMessenger from rooting the
            // behavior forever. See AGENTS.md §4.
            _context.Messenger.UnregisterAll(this);
            _context = null;
        }

        base.OnDetaching();
    }

    /// <summary>
    /// Called once the associated view is loaded and a <see cref="FileEntryTableContext"/> is
    /// available. Derived behaviors register messenger handlers and subscribe to UI events here.
    /// </summary>
    protected virtual void OnLoaded(FileEntryTableContext context)
    {
    }

    /// <summary>
    /// Called during detach (when a context exists). Derived behaviors must reverse any UI event
    /// subscriptions made in <see cref="OnLoaded(FileEntryTableContext)"/> here; messenger
    /// unregistration is handled by the base class in <see cref="OnDetaching"/>.
    /// </summary>
    protected virtual void OnUnloaded(FileEntryTableContext context)
    {
    }

    /// <summary>True once the view has loaded and the context is live; used to guard work queued
    /// onto the dispatcher that might run after detach.</summary>
    protected bool IsLoaded => _context is not null;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Loaded can fire more than once (e.g. when the element is re-parented); only build the
        // context once so registrations are not duplicated.
        if (_context is not null)
        {
            return;
        }

        _context = FileEntryTableContext.Create(AssociatedObject);
        OnLoaded(_context);
    }
}
