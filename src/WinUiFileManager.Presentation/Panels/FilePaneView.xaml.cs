using System.Collections.Specialized;

using System.ComponentModel;

using Microsoft.UI.Xaml;

using Microsoft.UI.Xaml.Controls;

using Microsoft.UI.Xaml.Input;

using Microsoft.UI.Xaml.Media;

using Windows.System;

using WinUiFileManager.Domain.ValueObjects;

using WinUiFileManager.Presentation.ViewModels;



namespace WinUiFileManager.Presentation.Panels;



public sealed partial class FilePaneView : UserControl

{

    private FilePaneViewModel? _viewModel;

    private bool _syncingDriveSelection;



    public FilePaneView()

    {

        InitializeComponent();

        EntryTable.ActivationRequested += OnEntryGridActivationRequested;

    }



    public FilePaneViewModel? ViewModel

    {

        get => _viewModel;

        set => SetViewModel(value);

    }



    public event Action? PaneActivationRequested;



    public void FocusPathBox()

    {

        PathBox.Focus(FocusState.Programmatic);

        PathBox.SelectAll();

    }



    public void FocusFileList()

    {

        EntryTable.FocusGrid();

    }



    public void SetActive(bool isActive)

    {

        PaneBorder.BorderThickness = isActive

            ? new Thickness(2)

            : new Thickness(1);



        var key = isActive

            ? "SystemControlHighlightAccentBrush"

            : "SystemControlBackgroundBaseLowBrush";



        if (Resources.TryGetValue(key, out var res) && res is Brush b)

            PaneBorder.BorderBrush = b;

        else if (Microsoft.UI.Xaml.Application.Current.Resources.TryGetValue(key, out var appRes) && appRes is Brush ab)

            PaneBorder.BorderBrush = ab;

    }



    private void OnEntryGridActivationRequested()

    {

        PaneActivationRequested?.Invoke();

    }



    private void FilePaneView_Loaded(object sender, RoutedEventArgs e)

    {

        EntryTable.ApplyColumnResizeFromOptions();

    }



    private void SetViewModel(FilePaneViewModel? value)

    {

        if (_viewModel is not null)

        {

            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;

            if (_viewModel.Items is INotifyCollectionChanged oldCol)

                oldCol.CollectionChanged -= OnItemsCollectionChanged;

        }



        _viewModel = value;



        if (_viewModel is not null)

        {

            _viewModel.PropertyChanged += OnViewModelPropertyChanged;

            if (_viewModel.Items is INotifyCollectionChanged newCol)

                newCol.CollectionChanged += OnItemsCollectionChanged;



            PathBox.Text = _viewModel.CurrentPath ?? string.Empty;

            EntryTable.Attach(_viewModel);

            DriveComboBox.ItemsSource = _viewModel.AvailableDrives;

            DriveComboBox.DisplayMemberPath = nameof(VolumeInfo.DriveLetter);

            UpdateOverlay();

            UpdatePaneStatus();

        }

        else

        {

            EntryTable.Attach(null);

        }

    }



    private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)

    {

        DispatcherQueue.TryEnqueue(() =>

        {

            UpdateOverlay();

            UpdatePaneStatus();

        });

    }



    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)

    {

        DispatcherQueue.TryEnqueue(() =>

        {

            switch (e.PropertyName)

            {

                case nameof(FilePaneViewModel.CurrentPath):

                    PathBox.Text = _viewModel?.CurrentPath ?? string.Empty;

                    SyncDriveSelection();

                    break;

                case nameof(FilePaneViewModel.IsLoading):

                case nameof(FilePaneViewModel.ErrorMessage):

                    UpdateOverlay();

                    break;

                case nameof(FilePaneViewModel.SelectedCount):

                    UpdatePaneStatus();

                    break;

            }

        });

    }



    private void PathBox_KeyDown(object sender, KeyRoutedEventArgs e)

    {

        switch (e.Key)

        {

            case VirtualKey.Enter:

                _viewModel?.NavigateToCommand.Execute(PathBox.Text);

                EntryTable.FocusGrid();

                e.Handled = true;

                break;

            case VirtualKey.Escape:

                PathBox.Text = _viewModel?.CurrentPath ?? string.Empty;

                EntryTable.FocusGrid();

                e.Handled = true;

                break;

        }

    }



    private void PaneBorder_Tapped(object sender, TappedRoutedEventArgs e)

    {

        PaneActivationRequested?.Invoke();

    }



    private void DriveComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)

    {

        if (_syncingDriveSelection)

            return;



        if (DriveComboBox.SelectedItem is VolumeInfo drive && _viewModel is not null)

        {

            _viewModel.SelectedDrive = drive;

        }

    }



    private void SyncDriveSelection()

    {

        if (_viewModel is null) return;



        var currentPath = _viewModel.CurrentPath;

        if (string.IsNullOrEmpty(currentPath)) return;



        var matchingDrive = _viewModel.AvailableDrives

            .FirstOrDefault(d => currentPath.StartsWith(d.RootPath.DisplayPath, StringComparison.OrdinalIgnoreCase));



        if (matchingDrive is not null && !Equals(DriveComboBox.SelectedItem, matchingDrive))

        {

            _syncingDriveSelection = true;

            try

            {

                DriveComboBox.SelectedItem = matchingDrive;

            }

            finally

            {

                _syncingDriveSelection = false;

            }

        }

    }



    private void UpdateOverlay()

    {

        if (_viewModel is null)

            return;



        if (_viewModel.IsLoading)

        {

            OverlayPanel.Visibility = Visibility.Visible;

            LoadingRing.IsActive = true;

            OverlayText.Text = "Loading...";

        }

        else if (_viewModel.ErrorMessage is { Length: > 0 } error)

        {

            OverlayPanel.Visibility = Visibility.Visible;

            LoadingRing.IsActive = false;

            OverlayText.Text = error;

        }

        else if (_viewModel.Items.Count == 0)

        {

            OverlayPanel.Visibility = Visibility.Visible;

            LoadingRing.IsActive = false;

            OverlayText.Text = "Empty folder";

        }

        else

        {

            OverlayPanel.Visibility = Visibility.Collapsed;

            LoadingRing.IsActive = false;

        }

    }



    private void UpdatePaneStatus()

    {

        if (_viewModel is null)

            return;



        var total = _viewModel.Items.Count;

        var selected = _viewModel.SelectedCount;

        PaneStatusText.Text = selected > 0

            ? $"{total} items, {selected} selected"

            : $"{total} items";

    }

}

