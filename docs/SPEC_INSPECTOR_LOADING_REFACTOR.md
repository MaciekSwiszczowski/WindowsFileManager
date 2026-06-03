# Inspector Loading Architecture Refactor — Implementation Plan

> **For agentic workers:** Implement this plan **one work-stream at a time**, each via its own step file under `docs/steps/`. Steps use checkbox (`- [ ]`) syntax for tracking. Do not start a later work-stream until the earlier one it depends on has been built and verified.

**Goal:** Clarify the inspector's diagnostics-loading architecture by splitting the two implicit phases (begin-loading vs. apply-results) into distinct responsibilities, centralizing field mutation behind one facade, and removing the misnamed `Prepare`/`Load` loader surface — while keeping the existing low-allocation, singleton, clear-and-reuse design.

**Architecture:** The inspector is a process-lifetime singleton whose category/field view-model tree is created once and **reused** (fields are cleared, never re-instantiated, on each selection). This plan keeps that model and re-shapes the collaborators around it: a non-generic field VM root with polymorphic `Reset()`, a single field-state facade, a phase-1 selection coordinator, phase-2 per-category appliers, and selection-token gating so late responses can't paint stale data.

**Tech Stack:** .NET 10 (`net10.0-windows10.0.19041.0`), WinUI 3 / Windows App SDK, CommunityToolkit.Mvvm (`ObservableObject`, `IMessenger`), System.Reactive, Autofac DI. **Build/platform: `x64` only** (AnyCPU breaks the CsWin32 interop).

---

## Context & Hard Constraints (read before designing)

### Current flow (what exists today)

1. `InspectorInitializationViewModel` builds the category/field tree and exposes three selection streams (`NonSingle`, `Immediate`, throttled `Deferred`).
2. `InspectorViewModel` (singleton) subscribes to those streams:
   - `ShowImmediateSelection` → fills sync fields via `InspectorFieldValueUpdater.ShowImmediateSelection` (which calls a private `ClearValues()` that loops **all** fields and has a `if (field is InspectorThumbnailFieldViewModel)` special-case), then loops loaders calling `Prepare`.
   - `LoadDeferredSelection` → loops loaders calling `Load`, then sends **one** `InspectorDiagnosticsRequestMessage`.
3. Each `IInspectorDeferredFieldLoader` (7 concrete) subscribes to **its own** response message, applies it via the updater, and toggles its fields' loading state.

### The smells this plan fixes

- **`Prepare`/`Load` are misnomers.** Neither loads anything; both just call `CancelCurrentLoad` + `SetLoading(true)`. The only difference is `Load` flips `_hasPendingRequest`. The actual request is sent centrally by `InspectorViewModel`. See `InspectorDeferredFieldLoaderBase.Prepare`/`Load`.
- **The loader wears three hats:** (1) per-category loading-state bookkeeping, (2) "am I expecting a response" tracking (`_hasPendingRequest`), (3) response subscription + apply.
- **Cardinality asymmetry:** **one** request message fans out to **N** category handlers, each returning its **own** response — so phase 1 is singular, phase 2 is plural.
- **`_hasPendingRequest` is a coarse staleness guard.** It cannot tell *which selection* a response is for; with reused singleton fields a late response for an old selection can land on the new selection.
- **Central type-switch** in `InspectorFieldValueUpdater.ClearValues` (`if (field is InspectorThumbnailFieldViewModel)`).

### Decision: NO generic field base (confirmed with user)

A generic base (`InspectorFieldViewModelBase<T>`) is **not viable** and is **explicitly out of scope**:

- `InspectorCategoryViewModel.Fields` is a single homogeneous `ObservableCollection<InspectorFieldViewModelBase>`.
- `InspectorCategoryView.xaml` dispatches via a **non-generic** `x:DataType="fields:InspectorFieldViewModelBase"` + `SwitchPresenter` on `FieldType`. `x:Bind` requires a closed/concrete `x:DataType`; an open generic cannot be the collection element type or the template data type.

**Chosen approach instead:** keep the non-generic root, and for non-string values add **specialized concrete field view models** that consume the typed value directly (following the existing `InspectorToggleFieldViewModel` / `InspectorThumbnailFieldViewModel` pattern: a new `InspectorFieldType` value + a concrete VM + its own `DataTemplate`/view + a DI-registered factory). The View binds to a string `DisplayValue` projection, so producers stop calling `.ToString()` at the call site and each field formats itself once.

---

## Target Architecture (end state)

- **`InspectorFieldViewModelBase` (non-generic root):** identity (`Category`/`Key`/`Tooltip`), shared UI state (`IsVisible`, `IsLoading`), `FieldType`, `DisplayValue` (string, what the View binds), `IsUnavailable`, `SearchText`, and a **polymorphic `Reset()`**. Remains the collection element type and the XAML `x:DataType`.
- **Concrete field VMs:** `InspectorBasicFieldViewModel` (text), `InspectorToggleFieldViewModel`, `InspectorThumbnailFieldViewModel`, plus **new specialized typed fields** (e.g. an integer field) added per the Toggle/Thumbnail pattern.
- **`InspectorFieldValueUpdater` (field-state facade):** the single owner of field mutation. Surface: `Reset()`, `BeginLoading(keys)`, `EndLoading(keys)`, `ShowImmediateSelection(...)`, and the typed `Show*Diagnostics(...)` writers. No type-switch (delegates clearing to `field.Reset()`).
- **Phase-1 selection coordinator:** the single place that reacts to a settled single selection and performs: reset fields → mark deferred fields loading → send the one request. Extracted out of `InspectorViewModel`.
- **Phase-2 appliers:** the de-hatted former loaders — *subscribe to my response → write my fields → clear my own spinner.* No `Prepare`/`Load`, no `_hasPendingRequest`.
- **Selection-token gating:** responses carry the path/selection token they answer; appliers apply only when it matches the currently-shown selection.

---

## Work-streams (execution order)

| # | Work-stream | Depends on | Step file |
|---|-------------|------------|-----------|
| **1** | **Field-state facade + polymorphic `Reset()`** (FIRST) | — | `docs/steps/inspector-step-01-field-state-facade.md` |
| 2 | Specialized typed field VM (integer-consuming) | WS1 | `docs/steps/inspector-step-02-typed-field.md` (to be written) |
| 3 | Two-phase split: phase-1 coordinator + phase-2 appliers | WS1 | `docs/steps/inspector-step-03-two-phase-split.md` (to be written) |
| 4 | Stale-response gating via selection token | WS3 | `docs/steps/inspector-step-04-stale-gating.md` (to be written) |

> Only **Work-stream 1** has a fully-detailed step file in this iteration (per the current request). The later step files will be authored once WS1 is reviewed, so their details reflect the actual post-WS1 code.

### Work-stream 1 — Field-state facade + polymorphic `Reset()` (detailed step exists)

**Outcome:** `InspectorFieldValueUpdater` exposes a clear two-phase surface (`Reset` / `BeginLoading` / `EndLoading` / `Show*`); the thumbnail type-switch is gone; each field type owns its own reset logic via `Reset()`. Behavior-preserving (no UI change).

**Files:** `InspectorFieldViewModelBase.cs`, `InspectorThumbnailFieldViewModel.cs`, `InspectorFieldValueUpdater.cs`, `InspectorDeferredFieldLoaderBase.cs`.

**Acceptance:** solution builds on `x64`; inspector still clears, shows spinners, and populates fields exactly as before; no remaining `SetLoading(` / `ClearValues(` references.

### Work-stream 2 — Specialized typed field VM

**Outcome:** a concrete `InspectorFieldType` value + VM (e.g. integer) that stores the typed value and projects `DisplayValue`, with its own `DataTemplate`/view and DI factory; the facade gains a typed `Show*`/setter for it. Producers stop pre-stringifying for that field.

**Files (anticipated):** new `InspectorFieldType` enum member; new `Inspector<Type>FieldViewModel.cs`; new `Inspector<Type>FieldView.xaml(.cs)`; `InspectorCategoryView.xaml` (new `SwitchPresenter` case + template); `InspectorInitializationViewModel.cs` (factory + field declaration); `PresentationContainerBuilderExtensions.cs` (DI registration); `InspectorFieldValueUpdater.cs` (typed write path).

### Work-stream 3 — Two-phase split

**Outcome:** a phase-1 coordinator owns *reset + spinners + send request*; the loaders become phase-2 appliers (*subscribe → apply → `EndLoading`*). `Prepare`/`Load`/`_hasPendingRequest` removed. Scope is the diagnostics-loading slice only — do **not** refactor toolbar/search/visibility in this pass.

### Work-stream 4 — Stale-response gating

**Outcome:** response messages carry the answered path/selection token; appliers apply only for the current selection, replacing the coarse pending-flag guard.

---

## Build & Verification Conventions

- **Always build `x64`:**
  - Solution: `dotnet build WinUiFileManager.sln -c Debug -p:Platform=x64 /nologo /v:m -m:1 /nr:false`
- WinUI wraps real errors inside `XamlCompiler.exe exited with code 1` / `MSB3073`. If generated XAML looks stale after XAML/public-surface changes, clean `src\WinUiFileManager.Presentation\{bin,obj}` (and TestApp's) before rebuilding.
- No Presentation-layer unit-test project exists, and these types touch WinUI (`ImageSource`), so **verification is build + manual/TestApp**, not unit tests. Do not invent a test project unless a work-stream explicitly calls for one.
- `TreatWarningsAsErrors` is on; keep the build warning-clean.

---

## Self-Review Checklist (run before calling the plan done)

1. **Constraint honored:** no generic field base introduced anywhere.
2. **Behavior-preserving WS1:** `Reset()` reproduces the old `ClearValues()` exactly (value cleared, not loading, visible; thumbnail image nulled) — including the Toggle deriving `IsToggleOn=false` from the cleared value.
3. **Name consistency:** `Reset` / `BeginLoading` / `EndLoading` used identically across the facade and all callers.
4. **Scope:** WS3 touches only the diagnostics-loading slice of `InspectorViewModel`, not toolbar/search/visibility.
