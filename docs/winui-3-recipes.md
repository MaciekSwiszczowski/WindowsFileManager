# WinUI 3 Recipes

A growing collection of WinUI 3 / Windows App SDK gotchas and the fixes that work for them. Each recipe documents:

- **Symptom** — what the user sees (or doesn't see).
- **Root cause** — why it happens, in framework terms, with enough detail to recognize the same shape in other places.
- **Fix** — the minimal change that resolves it.
- **Notes** — adjacent gotchas worth knowing once you're in the area.

When adding a new recipe, prefer linking to the file:line of an example in this repo so the fix has a concrete anchor.

---

## Recipe 1: `{x:Bind}` in a `ResourceDictionary` template binds nothing (but `{Binding}` works)

### Symptom

A `DataTemplate` lives inside a `ResourceDictionary` XAML file with `x:Class` (e.g. `DialogTemplates.xaml` → `partial class DialogTemplates`). The template uses `{x:Bind PropertyName, Mode=OneWay}` against an `x:DataType`-typed view-model. At runtime the dialog opens, the view-model is set, but **all the `{x:Bind}` cells render blank**. Replacing each `{x:Bind ...}` with `{Binding ...}` makes everything render correctly.

### Root cause

`{x:Bind}` is a **compile-time** binding. The XAML compiler generates a per-template `IDataTemplateComponent` connector class nested inside the partial class declared by `x:Class`. The framework only invokes that connector after `InitializeComponent()` has run on an instance of the partial class, because that's where the generated metadata is registered.

`{Binding}` survives because it's runtime-only — it walks `DataContext` via reflection, with no codegen involvement.

How a `ResourceDictionary` is merged determines whether the partial class gets instantiated:

```xml
<!-- ❌ Source-merge: parses XAML directly, never instantiates the partial class -->
<ResourceDictionary Source="ms-appx:///MyApp/Dialogs/DialogTemplates.xaml" />

<!-- ✅ Typed-instance merge: framework constructs DialogTemplates, runs InitializeComponent -->
<dialogs:DialogTemplates xmlns:dialogs="using:MyApp.Dialogs" />
```

With the source-merge form, `{Binding}` keeps working but `{x:Bind}` connectors are silently absent. Even if the dialog-presentation code does the right thing on the consumer side:

```csharp
var content = template.LoadContent();
if (content is FrameworkElement element)
{
    element.DataContext = viewModel;
    if (Microsoft.UI.Xaml.Markup.XamlBindingHelper.GetDataTemplateComponent(element) is { } component)
    {
        component.ProcessBindings(viewModel, 0, 0, out _);
    }
}
```

…the `is { } component` check returns `null`, the call is silently skipped, and `{x:Bind}` never fires.

### Fix

Switch the merge form in `App.xaml`:

```xml
<Application.Resources>
    <ResourceDictionary>
        <ResourceDictionary.MergedDictionaries>
            <XamlControlsResources xmlns="using:Microsoft.UI.Xaml.Controls" />
            <ResourceDictionary Source="ms-appx:///Microsoft.UI.Xaml/DensityStyles/Compact.xaml" />
            <dialogs:DialogTemplates xmlns:dialogs="using:WinUiFileManager.Presentation.Dialogs" />
        </ResourceDictionary.MergedDictionaries>
        ...
```

Or equivalently in code (e.g. in `App.OnLaunched`):

```csharp
Application.Current.Resources.MergedDictionaries.Add(new DialogTemplates());
```

Either form constructs the partial class, runs `InitializeComponent()`, and registers the generated connectors. After that, `XamlBindingHelper.GetDataTemplateComponent(element)` returns a non-null component for every `x:DataType`-typed template and `ProcessBindings(viewModel, 0, 0, out _)` actually does something.

The reverse rule applies too: a `ResourceDictionary` XAML file that **doesn't** use `{x:Bind}` (only `{Binding}`, or only static resources) doesn't need typed-instance merging. The `Source="..."` form works fine for those.

### Notes

- **`{x:Bind}` does not honor `UpdateSourceTrigger=PropertyChanged`.** That's a `{Binding}`-only attribute and the XAML compiler will reject it on `{x:Bind}`. For `TextBox.Text Mode=TwoWay`, `{x:Bind}` defaults to `LostFocus`-on-write. If you need write-through-on-keystroke (the common need for a rename / search box), one of these:
  - Keep `{Binding}` for that one property.
  - Handle `TextBox.TextChanged` and push the value into the view-model manually.
  - Wrap the editor in a small custom control with a dependency property that mirrors the text on every change.
- **`{x:Bind}` does not flow through `DataContext`.** Setting `element.DataContext = viewModel` is a no-op for `{x:Bind}`. The data item is whatever was passed to `ProcessBindings(item, ...)`. Setting both is fine — `{Binding}` consumers in the same template will still see the `DataContext`.
- **Setter conditions for `{x:Bind}` connectors to be wired:**
  1. The `DataTemplate` must have `x:DataType`.
  2. The owning XAML file must have `x:Class` (i.e. it's a typed resource dictionary, not anonymous markup).
  3. The owning class's `InitializeComponent()` must run — meaning the class must be instantiated, either by typed-instance merging (preferred) or by code-side construction.
  4. The consumer must call `ProcessBindings(item, 0, 0, out _)` after `LoadContent()` (the framework does this automatically when the template is consumed by an `ItemsControl` realization path; for manual loading via `LoadContent()`, you do it yourself).
- **Pragmatic call:** for tiny dialog templates with a few labels and a textbox, `{Binding}` is fine. The codegen-vs-reflection performance gap doesn't matter at that scale, and `{Binding}` is more forgiving (works without the typed-instance merge, supports `UpdateSourceTrigger`). Switch to `{x:Bind}` deliberately when you want compile-time type checking on the binding paths or when the template is in a hot path (e.g. a list row).
- **Repo example:** `DialogTemplates.xaml` (this repo, `src/WinUiFileManager.Presentation/Dialogs/`) uses `{Binding}`. The `DialogService.CreateContent` consumer (`src/WinUiFileManager.Presentation/Services/DialogService.cs`) is already written to work with both binding modes — it sets `DataContext` *and* attempts `ProcessBindings`. So switching the templates to `{x:Bind}` is a contained change, provided the typed-instance merge is in `App.xaml` first.

---

## How to add a new recipe

1. Append a `## Recipe N: <one-line symptom>` section.
2. Use the four headings: **Symptom**, **Root cause**, **Fix**, **Notes**.
3. Anchor with at least one file:line in the repo where the bug or fix lives.
4. Keep it copy-paste-ready: the future reader (you or me on another machine) just needs to skim and apply.
