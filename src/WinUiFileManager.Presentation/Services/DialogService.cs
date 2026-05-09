using UiDispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue;

namespace WinUiFileManager.Presentation.Services;

public sealed class DialogService : IDisposable
{
    private readonly ILogger<DialogService> _logger;
    private readonly IMessenger _messenger;
    private ContentDialog? _activeDialog;
    private UiDispatcherQueue? _dispatcherQueue;
    private DialogMessageOrchestrator? _orchestrator;
    private XamlRoot? _xamlRoot;
    private bool _disposed;

    public DialogService(ILogger<DialogService> logger, IMessenger messenger)
    {
        ArgumentNullException.ThrowIfNull(messenger);
        _logger = logger;
        _messenger = messenger;
    }

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

            dialog.PrimaryButtonClick += (_, args) =>
                OnDialogButtonClick(message, DialogButtonRole.Primary, args, role => invokedButton = role);
            dialog.SecondaryButtonClick += (_, args) =>
                OnDialogButtonClick(message, DialogButtonRole.Secondary, args, role => invokedButton = role);
            dialog.CloseButtonClick += (_, args) =>
                OnDialogButtonClick(message, DialogButtonRole.Close, args, role => invokedButton = role);

            _activeDialog = dialog;

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

    private async void OnDialogButtonClick(
        ShowDialogMessage message,
        DialogButtonRole role,
        ContentDialogButtonClickEventArgs args,
        Action<DialogButtonRole> setInvokedButton)
    {
        if (message.ViewModel is not IDialogViewModel dialogViewModel)
        {
            setInvokedButton(role);
            return;
        }

        var deferral = args.GetDeferral();
        try
        {
            var result = await dialogViewModel.OnDialogButtonAsync(role, message.CancellationToken);
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

    private void HideActiveDialog()
    {
        _ = _dispatcherQueue?.TryEnqueue(() => _activeDialog?.Hide());
    }

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

    private static ContentDialogButton ToContentDialogButton(DialogButtonRole role) =>
        role switch
        {
            DialogButtonRole.Primary => ContentDialogButton.Primary,
            DialogButtonRole.Secondary => ContentDialogButton.Secondary,
            DialogButtonRole.Close => ContentDialogButton.Close,
            _ => ContentDialogButton.None,
        };
}
