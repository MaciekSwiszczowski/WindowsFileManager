using System.ComponentModel;
using System.Linq;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using WinUiFileManager.Domain.Enums;
using WinUiFileManager.Domain.ValueObjects;
using WinUiFileManager.Presentation.Panels;
using WinUiFileManager.Presentation.ViewModels;

namespace WinUiFileManager.Presentation.Views;

public sealed partial class MainShellView : UserControl
{
    private readonly InputCursor _horizontalResizeCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeWestEast);
    private bool _isResizingInspector;
    private double _inspectorResizeStartX;
    private double _inspectorResizeStartWidth;

    public MainShellView()
    {
        InitializeComponent();
        PreviewKeyDown += OnPreviewKeyDown;
    }

    public Action? ToggleThemeAction { get; set; }

    private MainShellViewModel? ViewModel => DataContext as MainShellViewModel;

    public void Initialize(MainShellViewModel viewModel)
    {
        DataContext = viewModel;
        LeftPaneView.ViewModel = viewModel.LeftPane;
        RightPaneView.ViewModel = viewModel.RightPane;
        InspectorView.ViewModel = viewModel.Inspector;
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        viewModel.FocusActivePaneRequested += OnFocusActivePaneRequested;

        viewModel.LeftPane.PropertyChanged += OnPanePropertyChanged;
        viewModel.RightPane.PropertyChanged += OnPanePropertyChanged;

        LeftPaneView.PaneActivationRequested += () => ActivatePane(PaneId.Left);
        RightPaneView.PaneActivationRequested += () => ActivatePane(PaneId.Right);

        UpdateStatusBar();
        UpdateActivePaneBorders();
        UpdateInspectorLayout();
    }

    private void OnPanePropertyChanged(object? sender, PropertyChangedEventArgs e) =>
        DispatcherQueue.TryEnqueue(UpdateStatusBar);

    private void ActivatePane(PaneId paneId)
    {
        if (ViewModel is null)
        {
            return;
        }

        var desired = paneId == PaneId.Left ? ViewModel.LeftPane : ViewModel.RightPane;
        if (ViewModel.ActivePane != desired)
        {
            ViewModel.ActivePane = desired;
            UpdateActivePaneBorders();
        }
    }

    private void OnCopyClick(object sender, RoutedEventArgs e) =>
        ViewModel?.CopyCommand.Execute(null);

    private void OnMoveClick(object sender, RoutedEventArgs e) =>
        ViewModel?.MoveCommand.Execute(null);

    private void OnRenameClick(object sender, RoutedEventArgs e) =>
        ViewModel?.RenameCommand.Execute(null);

    private void OnDeleteClick(object sender, RoutedEventArgs e) =>
        ViewModel?.DeleteCommand.Execute(null);

    private void OnCreateFolderClick(object sender, RoutedEventArgs e) =>
        ViewModel?.CreateFolderCommand.Execute(null);

    private void OnRefreshClick(object sender, RoutedEventArgs e) =>
        ViewModel?.RefreshActivePaneCommand.Execute(null);

    private void OnFavouritesFlyoutOpening(object sender, object e)
    {
        if (ViewModel is null || sender is not MenuFlyout flyout)
        {
            return;
        }

        while (flyout.Items.Count > 2)
        {
            flyout.Items.RemoveAt(flyout.Items.Count - 1);
        }

        foreach (var fav in ViewModel.Favourites)
        {
            var item = new MenuFlyoutItem
            {
                Text = $"{fav.DisplayName} — {fav.Path.DisplayPath}",
                Tag = fav.Id,
            };
            item.Click += OnFavouriteItemClick;
            flyout.Items.Add(item);
        }

        if (ViewModel.Favourites.Count == 0)
        {
            flyout.Items.Add(new MenuFlyoutItem
            {
                Text = "(no favourites)",
                IsEnabled = false,
            });
        }
    }

    private void OnAddFavouriteClick(object sender, RoutedEventArgs e) =>
        ViewModel?.AddFavouriteCommand.Execute(null);

    private void OnFavouriteItemClick(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem { Tag: FavouriteFolderId id })
        {
            ViewModel?.OpenFavouriteCommand.Execute(id);
        }
    }

    private void OnCopyPathClick(object sender, RoutedEventArgs e) =>
        ViewModel?.CopyFullPathCommand.Execute(null);

    private void OnPreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        var ctrl = IsModifierDown(VirtualKey.Control);
        var shift = IsModifierDown(VirtualKey.Shift);
        var inTextInputContext = IsTextInputFocused();

        switch (e.Key)
        {
            // Tab / Shift+Tab — switch active pane (spec §5 and §12.1)
            case VirtualKey.Tab when !ctrl:
                ViewModel.SwitchActivePaneCommand.Execute(null);
                UpdateActivePaneBorders();
                GetActivePaneView().FocusFileList();
                e.Handled = true;
                break;

            // Ctrl+L — focus path box (spec §5 and §7)
            case VirtualKey.L when ctrl:
                GetActivePaneView().FocusPathBox();
                e.Handled = true;
                break;

            // Ctrl+A — select all items in active pane (spec §6.2 and §12.16)
            case VirtualKey.A when ctrl && !shift && !inTextInputContext:
                GetActivePaneView().SelectAllEntries();
                e.Handled = true;
                break;

            // Ctrl+Shift+A — clear selection in active pane (spec §6.2 and §12.17)
            case VirtualKey.A when ctrl && shift && !inTextInputContext:
                GetActivePaneView().ClearSelection();
                e.Handled = true;
                break;

            // Ctrl+D — open favourites flyout (spec §5 and §12.14)
            case VirtualKey.D when ctrl:
                FavouritesFlyout.ShowAt(FavouritesAppBarButton);
                e.Handled = true;
                break;

            case VirtualKey.I when ctrl:
                ViewModel.ToggleInspectorCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            UpdateStatusBar();
            if (e.PropertyName == nameof(MainShellViewModel.ActivePane))
            {
                UpdateActivePaneBorders();
            }
            if (e.PropertyName is nameof(MainShellViewModel.IsInspectorVisible)
                or nameof(MainShellViewModel.InspectorWidth))
            {
                UpdateInspectorLayout();
            }
        });
    }

    private void UpdateStatusBar()
    {
        if (ViewModel is null)
        {
            return;
        }

        var active = ViewModel.ActivePane;
        var paneName = active.PaneId == PaneId.Left ? "Left" : "Right";
        ActivePaneText.Text = $"{paneName} active";
        PathText.Text = active.CurrentPath;

        var itemsLine = $"{active.ItemCount} items";
        if (!string.IsNullOrEmpty(active.IncrementalSearchText))
        {
            itemsLine += $" | Search: {active.IncrementalSearchText}";
        }
        ItemCountText.Text = itemsLine;

        var selectedLine = $"{active.SelectedCount} selected";
        if (active.SelectedCount > 0)
        {
            var bytes = active.Items
                .Where(static i => i.IsSelected && !i.IsParentEntry && i.SizeBytes >= 0)
                .Sum(static i => i.SizeBytes);
            if (bytes > 0)
            {
                selectedLine += $" ({FormatByteSize(bytes)})";
            }
        }

        SelectedText.Text = selectedLine;
    }

    private static string FormatByteSize(long bytes)
    {
        string[] suffixes = ["B", "KB", "MB", "GB", "TB"];
        var suffixIndex = 0;
        var size = (double)bytes;
        while (size >= 1024 && suffixIndex < suffixes.Length - 1)
        {
            size /= 1024;
            suffixIndex++;
        }

        return suffixIndex == 0
            ? $"{size:F0} {suffixes[suffixIndex]}"
            : $"{size:F2} {suffixes[suffixIndex]}";
    }

    private void UpdateActivePaneBorders()
    {
        if (ViewModel is null)
        {
            return;
        }

        var leftActive = ViewModel.ActivePane.PaneId == PaneId.Left;
        LeftPaneView.SetActive(leftActive);
        RightPaneView.SetActive(!leftActive);
    }

    private void OnFocusActivePaneRequested()
    {
        DispatcherQueue.TryEnqueue(() => GetActivePaneView().FocusFileList());
    }

    private FilePaneView GetActivePaneView() =>
        ViewModel?.ActivePane.PaneId == PaneId.Left ? LeftPaneView : RightPaneView;

    private void OnToggleThemeClick(object sender, RoutedEventArgs e)
    {
        ToggleThemeAction?.Invoke();
    }

    private void UpdateInspectorLayout()
    {
        if (ViewModel is null)
        {
            return;
        }

        var isVisible = ViewModel.IsInspectorVisible;
        InspectorColumn.Width = isVisible
            ? new GridLength(ViewModel.InspectorWidth, GridUnitType.Pixel)
            : new GridLength(0, GridUnitType.Pixel);
        InspectorSplitterColumn.Width = isVisible
            ? new GridLength(6, GridUnitType.Pixel)
            : new GridLength(0, GridUnitType.Pixel);
        InspectorView.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
        InspectorResizeHandle.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnInspectorResizePointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (ViewModel?.IsInspectorVisible != true || sender is not UIElement element)
        {
            return;
        }

        ProtectedCursor = _horizontalResizeCursor;
        _isResizingInspector = true;
        _inspectorResizeStartX = e.GetCurrentPoint(this).Position.X;
        _inspectorResizeStartWidth = ViewModel.InspectorWidth;
        element.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void OnInspectorResizePointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isResizingInspector || ViewModel is null)
        {
            return;
        }

        var currentX = e.GetCurrentPoint(this).Position.X;
        var delta = _inspectorResizeStartX - currentX;
        ViewModel.SetInspectorWidth(_inspectorResizeStartWidth + delta);
        UpdateInspectorLayout();
        e.Handled = true;
    }

    private void OnInspectorResizePointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_isResizingInspector || sender is not UIElement element)
        {
            return;
        }

        _isResizingInspector = false;
        element.ReleasePointerCapture(e.Pointer);
        ProtectedCursor = null;
        e.Handled = true;
    }

    private void OnInspectorResizePointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (ViewModel?.IsInspectorVisible == true)
        {
            ProtectedCursor = _horizontalResizeCursor;
        }
    }

    private void OnInspectorResizePointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (!_isResizingInspector)
        {
            ProtectedCursor = null;
        }
    }

    private static bool IsModifierDown(VirtualKey key) =>
        InputKeyboardSource.GetKeyStateForCurrentThread(key)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

    private bool IsTextInputFocused()
    {
        var focused = FocusManager.GetFocusedElement(XamlRoot);
        return focused is TextBox
            or PasswordBox
            or RichEditBox
            or AutoSuggestBox;
    }
}
