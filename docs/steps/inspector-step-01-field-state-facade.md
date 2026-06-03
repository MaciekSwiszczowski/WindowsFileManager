# Inspector Refactor — Step 01: Field-State Facade + Polymorphic `Reset()`

> **Parent plan:** `docs/SPEC_INSPECTOR_LOADING_REFACTOR.md` (Work-stream 1).
> **For the executing agent:** This is a **behavior-preserving** refactor. The inspector must clear fields, show spinners, and populate values exactly as before. Do not change UI/XAML. Build target is **`x64`** (AnyCPU breaks the CsWin32 interop). `TreatWarningsAsErrors` is on — keep it warning-clean.

**Goal:** Give `InspectorFieldValueUpdater` a clear two-phase surface — `Reset()`, `BeginLoading(keys)`, `EndLoading(keys)` — and move each field's clear logic into a polymorphic `Reset()` so the central `if (field is InspectorThumbnailFieldViewModel)` type-switch disappears.

**Why:** Today `ClearValues()` is private and special-cases the thumbnail; loading is toggled via a single `SetLoading(keys, bool)`; and the loaders call `SetLoading(..., true/false)`. The phases (begin vs. end loading) and the per-type clear behavior are not expressed in the API.

---

## Files

- Modify: `src/WinUiFileManager.Presentation/ViewModels/Inspector/Fields/InspectorFieldViewModelBase.cs`
- Modify: `src/WinUiFileManager.Presentation/ViewModels/Inspector/Fields/InspectorThumbnailFieldViewModel.cs`
- Modify: `src/WinUiFileManager.Presentation/ViewModels/Inspector/Fields/InspectorFieldValueUpdater.cs`
- Modify: `src/WinUiFileManager.Presentation/ViewModels/Inspector/Fields/InspectorDeferredFieldLoaderBase.cs`

No XAML, DI, or message changes in this step.

---

## Step 1 — Add a polymorphic `Reset()` to the field base

**File:** `InspectorFieldViewModelBase.cs`

- [ ] Add a `public virtual void Reset()` that reproduces exactly what `ClearValues()` does today for a generic field (clear value, stop loading, make visible). Insert it right after the constructor (before `FieldType`), or anywhere in the class body:

```csharp
    /// <summary>
    /// Resets the field to its empty/visible/not-loading default before a new selection is shown.
    /// Overridden by field types that hold non-text state (e.g. a thumbnail image).
    /// </summary>
    public virtual void Reset()
    {
        Value = string.Empty;
        IsLoading = false;
        IsVisible = true;
    }
```

> Note: setting `Value = string.Empty` triggers `OnValueChanged`, which is what makes the Toggle field re-derive `IsToggleOn = false`. This matches today's `ClearValues()` behavior — do not change it.

## Step 2 — Override `Reset()` on the thumbnail field

**File:** `InspectorThumbnailFieldViewModel.cs`

- [ ] Add an override that also clears the image (this is the logic currently special-cased inside `ClearValues()`):

```csharp
    /// <inheritdoc/>
    public override void Reset()
    {
        base.Reset();
        ThumbnailSource = null;
    }
```

## Step 3 — Reshape the facade surface

**File:** `InspectorFieldValueUpdater.cs`

- [ ] **3a.** In `ShowImmediateSelection`, replace the call to `ClearValues()` with `Reset()`:

```csharp
    public void ShowImmediateSelection(SpecFileEntryViewModel selectedItem)
    {
        Reset();

        if (selectedItem.Model is not { } model)
        {
            return;
        }
        // ... unchanged body ...
```

- [ ] **3b.** Replace the single `SetLoading(IEnumerable<string> keys, bool isLoading)` method with two intent-revealing methods:

```csharp
    /// <summary>Marks the fields addressed by <paramref name="keys"/> as loading (phase 1); unknown keys are ignored.</summary>
    public void BeginLoading(IEnumerable<string> keys) => SetLoading(keys, isLoading: true);

    /// <summary>Clears the loading flag on the fields addressed by <paramref name="keys"/> (phase 2); unknown keys are ignored.</summary>
    public void EndLoading(IEnumerable<string> keys) => SetLoading(keys, isLoading: false);

    private void SetLoading(IEnumerable<string> keys, bool isLoading)
    {
        foreach (var key in keys)
        {
            if (_fields.TryGetValue(key, out var field))
            {
                field.IsLoading = isLoading;
            }
        }
    }
```

> Keep `SetLoading` as a **private** helper (it is now an implementation detail of `BeginLoading`/`EndLoading`). Do not leave a public `SetLoading`.

- [ ] **3c.** Convert the private `ClearValues()` into a `public void Reset()` that delegates to each field's polymorphic `Reset()` (deleting the `if (field is InspectorThumbnailFieldViewModel)` block):

```csharp
    /// <summary>Resets every field to its empty/visible/not-loading default before a new selection is shown.</summary>
    public void Reset()
    {
        foreach (var field in _fields.Values)
        {
            field.Reset();
        }
    }
```

> The thumbnail-nulling that used to live here now lives in `InspectorThumbnailFieldViewModel.Reset()` (Step 2). Confirm no other code referenced the old private `ClearValues()` — only `ShowImmediateSelection` did.

## Step 4 — Update the loader base to the new surface

**File:** `InspectorDeferredFieldLoaderBase.cs`

- [ ] Replace the three `SetLoading(FieldKeys, isLoading: true)` / `isLoading: false` call sites with `BeginLoading` / `EndLoading`. Specifically:

In `Prepare`:
```csharp
        FieldValueUpdater.BeginLoading(FieldKeys);
```

In `Load`:
```csharp
        FieldValueUpdater.BeginLoading(FieldKeys);
```

In `ApplyResponseAsync`'s `finally`:
```csharp
            FieldValueUpdater.EndLoading(FieldKeys);
```

In `CancelCurrentLoad` (note this site uses the nullable field, not the throwing property — keep that):
```csharp
            _fieldValueUpdater.EndLoading(FieldKeys);
```

> Do **not** otherwise change the loaders in this step (the `Prepare`/`Load`/`_hasPendingRequest` removal is Work-stream 3).

---

## Step 5 — Verify the build

- [ ] Build the solution on x64 and confirm 0 errors / 0 warnings:

```bash
dotnet build WinUiFileManager.sln -c Debug -p:Platform=x64 /nologo /v:m -m:1 /nr:false
```

Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`.

- [ ] If you see `XamlCompiler.exe exited with code 1` or stale generated-code errors, clean and rebuild:

```powershell
Remove-Item -LiteralPath src\WinUiFileManager.Presentation\bin,src\WinUiFileManager.Presentation\obj -Recurse -Force -ErrorAction SilentlyContinue
dotnet build WinUiFileManager.sln -c Debug -p:Platform=x64 /nologo /v:m -m:1 /nr:false
```

## Step 6 — Confirm the refactor is complete (grep)

- [ ] There must be **no remaining** references to the old names anywhere in `src/`:
  - `SetLoading(` used by callers (only the private helper definition may remain).
  - `ClearValues(`.

If any caller still references `SetLoading(... true/false)`, convert it to `BeginLoading`/`EndLoading`.

---

## Done criteria

- Solution builds on x64, warning-clean.
- `InspectorFieldValueUpdater` public surface is: `Reset()`, `BeginLoading(keys)`, `EndLoading(keys)`, `ShowImmediateSelection(...)`, and the `Show*Diagnostics(...)` writers.
- No type-switch on field type remains in the facade.
- Inspector behavior is unchanged: selecting an item clears fields, shows spinners on deferred fields, then populates them; thumbnail clears between selections.

## Out of scope (later work-streams)

- Renaming the facade class (`InspectorFieldValueUpdater` → e.g. `InspectorFieldState`) — deferred to avoid churn before the two-phase split.
- Removing `Prepare`/`Load`/`_hasPendingRequest` — Work-stream 3.
- Specialized typed (integer) field — Work-stream 2.
- Selection-token staleness gating — Work-stream 4.
