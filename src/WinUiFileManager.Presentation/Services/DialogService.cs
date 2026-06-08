using UiDispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue;

namespace WinUiFileManager.Presentation.Services;

/// <summary>
/// Presents <see cref="ContentDialog"/>s in response to <see cref="ShowDialogMessage"/>s. It is bound to
/// a window via <see cref="Attach"/> (which supplies the <see cref="XamlRoot"/> and UI dispatcher) and
/// delegates serialization of dialog requests to a <see cref="DialogMessageOrchestrator"/>; it itself is
/// responsible for building the dialog from a message and mapping its outcome to a <see cref="DialogResult"/>.
/// </summary>
/// <remarks>
/// Threading/UI affinity (AGENTS.md §6): dialogs require the UI thread and a live
/// <see cref="XamlRoot"/>. <see cref="ShowDialogOnUiThreadAsync"/> asserts thread access and returns
/// <see cref="DialogResult.Unavailable"/> if it is somehow invoked off-thread or before
/// <see cref="Attach"/>. Cancellation is wired through: the message's token both throws before showing
/// and registers a callback to hide the active dialog.
/// <para>
/// Lifetime: this is typically a process/window-lifetime service. <see cref="Attach"/> recreates (and
/// disposes the previous) orchestrator, and <see cref="Dispose"/> tears it down; both are idempotent via
/// <see cref="_disposed"/>. The dialog button handlers are attached to the per-call dialog instance, so
/// they die with that dialog and need no explicit removal.
/// </para>
/// </remarks>
public sealed class DialogService : IDisposable
{
    private readonly ILogger<DialogService> _logger;
    private readonly IFileManagerMessenger _messenger;
    private ContentDialog? _activeDialog;
    private UiDispatcherQueue? _dispatcherQueue;
    private DialogMessageOrchestrator? _orchestrator;
    private XamlRoot? _xamlRoot;
    private bool _disposed;

    /// <exception cref="ArgumentNullException">Thrown when <paramref name="messenger"/> is null.</exception>
    public DialogService(ILogger<DialogService> logger, IFileManagerMessenger messenger)
    {
        ArgumentNullException.ThrowIfNull(messenger);
        _logger = logger;
        _messenger = messenger;
    }

    /// <summary>Binds the service to a window's <see cref="XamlRoot"/> and UI dispatcher and starts (or
    /// restarts) the message orchestrator. Must be called from the UI thread once the window's content
    /// is loaded. Re-attaching disposes the previous orchestrator first.</summary>
    /// <exception cref="ArgumentNullException">Thrown when either argument is null.</exception>
    public void Attach(XamlRoot xamlRoot, UiDispatcherQueue dispatcherQueue)
    {
        ArgumentNullException.ThrowIfNull(xamlRoot);
        ArgumentNullException.ThrowIfNull(dispatcherQueue);

        _xamlRoot = xamlRoot;
        _dispatcherQueue = dispatcherQueue;
        _orchestrator?.Dispose();
        _orchestrator = new DialogMessageOrchestrator(
            dispatcherQueue,
            ShowDialogOnUiThreadAsync,
            _logger,
            _messenger);
    }

    /// <summary>Disposes the orchestrator (and thus its messenger subscription). Idempotent.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _orchestrator?.Dispose();
        _orchestrator = null;
    }

    /// <summary>Builds and shows the dialog for one message on the UI thread and maps the result.
    /// Returns <see cref="DialogResult.Unavailable"/> if not attached / off the UI thread, and honours
    /// the message's cancellation token (throwing before show, hiding the dialog if cancelled while open).</summary>
    private async Task<DialogResult> ShowDialogOnUiThreadAsync(ShowDialogMessage message)
    {
        if (_xamlRoot is null || _dispatcherQueue is null || !_dispatcherQueue.HasThreadAccess)
        {
            return DialogResult.Unavailable;
        }

        try
        {
            message.CancellationToken.ThrowIfCancellationRequested();

            var dialog = CreateDialog(message);
            DialogButtonRole? invokedButton = null;

            // Handlers attached to this per-call dialog instance; they are collected with the dialog.
            dialog.PrimaryButtonClick += (_, args) =>
                OnDialogButtonClick(message, DialogButtonRole.Primary, args, role => invokedButton = role);
            dialog.SecondaryButtonClick += (_, args) =>
                OnDialogButtonClick(message, DialogButtonRole.Secondary, args, role => invokedButton = role);
            dialog.CloseButtonClick += (_, args) =>
                OnDialogButtonClick(message, DialogButtonRole.Close, args, role => invokedButton = role);

            _activeDialog = dialog;

            // Hide the open dialog if the request is cancelled; the registration is disposed when the
            // method returns so it does not outlive this dialog.
            using var cancellationRegistration = message
                .CancellationToken
                .Register(static state => ((DialogService)state!).HideActiveDialog(), this);

            _ = await dialog.ShowAsync();

            return invokedButton.HasValue
                ? DialogResult.FromButton(invokedButton.Value)
                : DialogResult.Dismissed;
        }
        finally
        {
            _activeDialog = null;
        }
    }

    /// <summary>Creates a <see cref="ContentDialog"/> from a message: sets the XamlRoot/title/content,
    /// resolves the default button, and adds the configured buttons by role.</summary>
    private ContentDialog CreateDialog(ShowDialogMessage message)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = _xamlRoot,
            Title = message.Title,
            Content = CreateContent(message),
            DefaultButton = GetDefaultButton(message.Buttons),
        };

        foreach (var button in message.Buttons)
        {
            switch (button.Role)
            {
                case DialogButtonRole.Primary:
                    dialog.PrimaryButtonText = button.Text;
                    break;
                case DialogButtonRole.Secondary:
                    dialog.SecondaryButtonText = button.Text;
                    break;
                case DialogButtonRole.Close:
                    dialog.CloseButtonText = button.Text;
                    break;
            }
        }

        return dialog;
    }

    /// <summary>Builds the dialog body: if the message names a <see cref="DataTemplate"/> resource key,
    /// loads it and binds it to the message's view model; otherwise hosts the view model directly in a
    /// <see cref="ContentControl"/>.</summary>
    private static object CreateContent(ShowDialogMessage message)
    {
        var template = ResolveContentTemplate(message.ContentTemplateKey);
        if (template is null)
        {
            return new ContentControl
            {
                Content = message.ViewModel,
                DataContext = message.ViewModel,
            };
        }

        var content = template.LoadContent();
        if (content is FrameworkElement element)
        {
            element.DataContext = message.ViewModel;
            if (Microsoft.UI.Xaml.Markup.XamlBindingHelper.GetDataTemplateComponent(element) is { } component)
            {
                component.ProcessBindings(message.ViewModel, 0, 0, out _);
            }
        }

        return content;
    }

    /// <summary>Looks up a <see cref="DataTemplate"/> from application resources by key; null when the
    /// key is blank or does not resolve to a template.</summary>
    private static DataTemplate? ResolveContentTemplate(string? templateKey)
    {
        if (string.IsNullOrWhiteSpace(templateKey))
        {
            return null;
        }

        return Microsoft.UI.Xaml.Application.Current.Resources.TryGetValue(templateKey, out var resource)
            && resource is DataTemplate template
                ? template
                : null;
    }

    /// <summary>Handles a dialog button click. For dialog view models, it takes a deferral and awaits the
    /// VM's async confirmation (which may veto the close); otherwise it records the button and lets the
    /// dialog close. <c>async void</c> because it is a UI event handler — note the top-level try/catch
    /// (AGENTS.md §6). Cancellation lets the dialog close; other exceptions are logged and keep it open.</summary>
    // VSTHRD100: async void is intentional and correct here — this is a UI event handler with a top-level
    // try/catch (see the doc comment / AGENTS.md §6); it cannot return Task.
#pragma warning disable VSTHRD100
    private async void OnDialogButtonClick(
        ShowDialogMessage message,
        DialogButtonRole role,
        ContentDialogButtonClickEventArgs args,
        Action<DialogButtonRole> setInvokedButton)
    {
#pragma warning restore VSTHRD100
        if (message.ViewModel is not IDialogViewModel dialogViewModel)
        {
            setInvokedButton(role);
            return;
        }

        // Deferral keeps the dialog open while the VM decides asynchronously whether the click closes it.
        var deferral = args.GetDeferral();
        try
        {
            var result = await dialogViewModel.OnDialogButtonAsync(role, message.CancellationToken).ConfigureAwait(true);
            args.Cancel = !result.ShouldClose;
            if (result.ShouldClose)
            {
                setInvokedButton(role);
            }
        }
        catch (OperationCanceledException)
        {
            args.Cancel = false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Dialog button {Role} failed for dialog {DialogId}", role, message.DialogId);
            args.Cancel = true;
        }
        finally
        {
            deferral.Complete();
        }
    }

    /// <summary>Hides the currently shown dialog, marshalling onto the UI thread (called from the
    /// cancellation callback, which may run on an arbitrary thread).</summary>
    private void HideActiveDialog()
    {
        _ = _dispatcherQueue?.TryEnqueue(() => _activeDialog?.Hide());
    }

    /// <summary>Picks the dialog's default (highlighted) button: an explicitly-marked default if present,
    /// otherwise Primary → Close → Secondary by preference, else none.</summary>
    private static ContentDialogButton GetDefaultButton(IReadOnlyList<DialogButtonConfiguration> buttons)
    {
        var configured = buttons.FirstOrDefault(static button => button.IsDefault);
        if (configured is not null)
        {
            return ToContentDialogButton(configured.Role);
        }

        if (buttons.Any(static button => button.Role == DialogButtonRole.Primary))
        {
            return ContentDialogButton.Primary;
        }

        if (buttons.Any(static button => button.Role == DialogButtonRole.Close))
        {
            return ContentDialogButton.Close;
        }

        if (buttons.Any(static button => button.Role == DialogButtonRole.Secondary))
        {
            return ContentDialogButton.Secondary;
        }

        return ContentDialogButton.None;
    }

    /// <summary>Maps an app <see cref="DialogButtonRole"/> to the WinUI <see cref="ContentDialogButton"/>.</summary>
    private static ContentDialogButton ToContentDialogButton(DialogButtonRole role) =>
        role switch
        {
            DialogButtonRole.Primary => ContentDialogButton.Primary,
            DialogButtonRole.Secondary => ContentDialogButton.Secondary,
            DialogButtonRole.Close => ContentDialogButton.Close,
            _ => ContentDialogButton.None,
        };
}
