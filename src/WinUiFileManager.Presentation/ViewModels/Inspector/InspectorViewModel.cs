using System.Reactive.Disposables;
using WinUiFileManager.Presentation.FileEntryTable;

namespace WinUiFileManager.Presentation.ViewModels.Inspector;

public sealed partial class InspectorViewModel : ObservableObject, IDisposable
{
    private readonly CompositeDisposable _subscriptions = [];
    private int _selectedItemCount;
    private bool _disposed;

    public InspectorCommandsViewModel Commands { get; } = new();

    [ObservableProperty]
    public partial FileInspectorSelectionMode SelectionMode { get; private set; }

    public string MultiSelectionStatusText => _selectedItemCount == 1
        ? "1 item selected"
        : $"{_selectedItemCount} items selected";

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    public List<InspectorCategoryViewModel> Categories { get; }

    public InspectorViewModel()
    {
        Categories = [];
    }

    public InspectorViewModel(InspectorInitializationViewModel initialization)
    {
        ArgumentNullException.ThrowIfNull(initialization);

        Categories = initialization.Categories;

        _subscriptions.Add(initialization
            .NonSingleSelectionObservable
            .Subscribe(ShowNonSingleSelection));
        _subscriptions.Add(initialization
            .ImmediateSelectionObservable
            .Subscribe(ShowImmediateSelection));
        _subscriptions.Add(initialization
            .DeferredSelectionObservable
            .Subscribe(LoadDeferredSelection));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _subscriptions.Dispose();
    }

    private void ShowNonSingleSelection(IReadOnlyList<SpecFileEntryViewModel> selectedItems)
    {
        SetSelectedItemCount(selectedItems.Count);
        SelectionMode = selectedItems.Count == 0
            ? FileInspectorSelectionMode.NoSelection
            : FileInspectorSelectionMode.MultiSelection;
    }

    private void ShowImmediateSelection(IReadOnlyList<SpecFileEntryViewModel> selectedItems)
    {
        SetSelectedItemCount(selectedItems.Count);
        SelectionMode = FileInspectorSelectionMode.SingleSelection;
    }

    private void LoadDeferredSelection(IReadOnlyList<SpecFileEntryViewModel> selectedItems)
    {
    }

    private void SetSelectedItemCount(int value)
    {
        if (_selectedItemCount == value)
        {
            return;
        }

        _selectedItemCount = value;
        OnPropertyChanged(nameof(MultiSelectionStatusText));
    }
}
