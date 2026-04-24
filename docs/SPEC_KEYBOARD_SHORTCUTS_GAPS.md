# Spec: Keyboard Shortcut Gap Audit

Scope: a gap analysis of currently-implemented keyboard shortcuts versus what a Windows developer user expects from a dual-pane file manager. Produces (a) a prioritized list of missing shortcuts to add, (b) a smaller list of shortcuts that exist but behave subtly wrong, and (c) deferred items that depend on other specs.

This spec is **complementary** to, not a replacement for, `winui-file-manager-keyboard-shortcuts-spec.md` (the existing source of truth). Where this spec updates an existing entry there, the update is called out explicitly.

## 1. Audit method

Baseline shortcut set = `winui-file-manager-keyboard-shortcuts-spec.md` §17 (20 entries).

Evaluated against the following expectation sources:

- **Windows Explorer** — what every Windows user already knows.
- **Total Commander** — the reference dual-pane UX (already an explicit inspiration per `AGENT_BRIEF.md`).
- **VS Code / JetBrains** — developer-tool expectations for text-input and navigation affordances.
- The human-owner's flagged gap: `F2` does not edit a name.

For each candidate shortcut: is it implemented today? If yes, does the behavior match expectation? If not, what is the impact, and where does the work land?

## 2. Audit table

Legend: **OK** = implemented and correct · **WRONG** = implemented but behaves unexpectedly · **MISS** = not implemented · **DEFERRED** = intentionally not in v1, references its owning spec.

| Shortcut | Expected action | Status | Notes |
|---|---|---|---|
| `Tab` / `Shift+Tab` | Switch active pane | OK | `MainShellView.OnPreviewKeyDown` |
| `Backspace` | Navigate to parent | OK | `FileEntryTableView.BodyTable_KeyDown` plus the header-table handoff in `SPEC_FILE_ENTRY_TABLE_VIEW.md` |
| `Ctrl+PageUp` | Navigate to parent | OK | Same handler |
| `Alt+Up` | Navigate to parent | **MISS** | Windows Explorer convention. Add — see §4.4. |
| `Alt+Left` | Back in navigation history | **MISS** | Per-pane back/forward stack. Add — see §4.6. |
| `Alt+Right` | Forward in navigation history | **MISS** | Same stack; forward half. Add — see §4.6. |
| `Enter` | Open / navigate-into current item | OK | `BodyTable_PreviewKeyDown` / `HeaderTable_PreviewKeyDown` in `FileEntryTableView` |
| `Ctrl+L` | Focus path box | OK | `MainShellView.OnPreviewKeyDown` |
| `F2` | Rename in-place | **MISS** | Delivered by `SPEC_UI_LAYOUT_AND_RESIZING.md` §6. Add `F2` alongside `Shift+F6`. |
| `Shift+F6` | Rename in-place | WRONG | Currently opens a modal dialog. Delivered by `SPEC_UI_LAYOUT_AND_RESIZING.md` §6. |
| `F3` | View current file (read-only preview) | DEFERRED | Total Commander convention. Out of v1; revisit with a preview pane feature. |
| `F4` | Edit current file in external editor | DEFERRED | Originally proposed in an earlier revision of this spec; de-scoped per human-owner direction (2026-04-21). Add when an external-editor config surface exists. |
| `F5` | Copy selection to other pane | OK | CommandBar `KeyboardAccelerator` |
| `F6` | Move selection to other pane | OK | CommandBar `KeyboardAccelerator` |
| `F7` | Create folder | OK | CommandBar |
| `Ctrl+Shift+N` | Create folder | OK | CommandBar |
| `F8` | Delete selection | OK | CommandBar |
| `Delete` | Delete selection | OK | CommandBar |
| `Shift+Delete` | Permanent delete (no recycle bin) | **MISS** / no-op | The app never uses the recycle bin; all deletes are permanent. Either wire `Shift+Delete` to the same command, or document that `Delete` IS permanent and add a dialog warning that mentions both keys. See §4.2. |
| `Alt+Enter` | Show shell Properties | **MISS** | Inspector has a "Properties" button but no key binding. Add — subject to `SPEC_LONG_PATHS.md` capability gating. |
| `Ctrl+R` | Refresh active pane | OK | CommandBar |
| `F5` (refresh) | Refresh | conflict | F5 = Copy in Total Commander. Keep F5 = Copy; `Ctrl+R` remains the refresh binding. No change. |
| `Ctrl+I` | Toggle inspector | OK | |
| `Ctrl+A` | Select all in active pane | OK | |
| `Ctrl+Shift+A` | Clear selection | OK | |
| `Ctrl+D` | Open favourites | OK | Favourites popup is now a **deferred** feature (see `SPEC_FEATURE_LOW_HANGING_FRUIT.md`); shortcut remains wired but the UX won't receive polish in v1. |
| `Ctrl+Shift+C` | Copy full path of selection | OK | |
| `Ctrl+C` | Copy (file-ops) to internal clipboard | DEFERRED | Requires clipboard-file-ops design. Not in v1. |
| `Ctrl+X` | Cut (file-ops) to internal clipboard | DEFERRED | Same. |
| `Ctrl+V` | Paste from clipboard | DEFERRED | Same. |
| `Ctrl+Insert` / `Shift+Insert` | Legacy copy/paste | DEFERRED | Not v1. |
| `Insert` | Toggle selection + advance | OK | |
| `Space` | Toggle selection | OK | |
| `Ctrl+Space` | Toggle selection | OK | |
| Typing a letter | Incremental prefix search | OK | `HandleIncrementalSearch` |
| `Esc` | Clear selection, then clear search | OK | |
| `Home` / `End` | First / last row | OK | |
| `PageUp` / `PageDown` | Page up / down | OK | |
| `Ctrl+Home` / `Ctrl+End` | First / last row | WRONG | Currently the `Home` / `End` paths don't short-circuit when `Ctrl` is down; the TableView's own handler runs. Either swallow the Ctrl variants (treat same as plain Home/End) or explicitly unbind. See §4.4. |
| `Ctrl+F` | Open quick filter | DEFERRED | Feature F1 in `SPEC_FEATURE_LOW_HANGING_FRUIT.md`. |
| `Ctrl+Shift+P` | Command palette | DEFERRED | Feature F3 in the same spec. |
| `Ctrl+;` | Jump-to palette | DEFERRED | Feature F10. |
| `Ctrl+Shift+E` | Reveal in Explorer | DEFERRED | Feature F4. |
| `Ctrl+Shift+T` | Open Terminal here | DEFERRED | Feature F4. |
| `Ctrl+Shift+.` | Toggle hidden files | DEFERRED | Feature F15. |
| `Ctrl+T` / `Ctrl+W` / `Ctrl+Tab` | Folder tabs | DEFERRED | Feature F2 in features spec. |
| `Alt+F4` | Exit | OK | Windows default |
| `F1` | Help | OK (implicit) | Out of v1 scope — no help system yet. |

Total: **7 items to fix** (F2, Shift+F6 behavior, Alt+Up, Alt+Left, Alt+Right, Shift+Delete, Alt+Enter) and **2 minor corrections** (Ctrl+Home / Ctrl+End routing). Everything else is either already correct or tracked elsewhere.

## 3. Source-of-truth strategy

Before adding new shortcuts piecemeal, wire them through a **single registry** so the existing entries and the new entries share one definition. `MISSING_FEATURES_SPEC.md` §2 already asks for this; we deliver it as part of this spec.

New file: `src/WinUiFileManager.Presentation/Input/ShortcutRegistry.cs`

```csharp
internal sealed record ShortcutDescriptor(
    string CommandId,
    IReadOnlyList<ShortcutBinding> Bindings,
    string TooltipText);

internal sealed record ShortcutBinding(
    VirtualKey Key,
    VirtualKeyModifiers Modifiers,
    ShortcutContext Context);

internal enum ShortcutContext
{
    Global,          // anywhere except text input
    PaneList,        // only when a pane's file list has focus
    PathBox,         // only when the path textbox has focus
    Dialog,          // only inside a ContentDialog
    InlineRename,    // only while a row is in rename-edit mode
}
```

Populate once in `ShortcutRegistry.Default` (a static field). Command bar tooltip generators and the `OnPreviewKeyDown` router both read from the registry — no string duplication, no drift.

All shortcuts listed in §2 with status `OK` or marked "Add" in §4 are migrated into the registry in a single PR. Existing behavior is unchanged; the registry is a refactor, not a feature.

## 4. Implementation tasks for the missing items

### 4.1. `F2` for rename (fix F2 = MISS)

Delivered by `SPEC_UI_LAYOUT_AND_RESIZING.md` §6.1. Add `F2` to `FileEntryTableView.BodyTable_PreviewKeyDown`, alongside `Shift+F6`, both routing to `FilePaneViewModel.BeginRenameCurrent()`.

Also update `winui-file-manager-keyboard-shortcuts-spec.md` §12.9 and §17:

- §12.9 heading becomes "Rename in place (`F2`, `Shift+F6`)".
- §17 table "Rename" row becomes "`F2`, `Shift+F6`".

### 4.2. `Shift+Delete` for permanent delete (Shift+Delete = MISS)

**Context.** The app does not use the recycle bin; every `Delete`/`F8` is already permanent. The current confirmation dialog (`ShowDeleteConfirmationAsync`) says "Permanently delete N items". So there is no *new* behavior — there's a user-expectation gap: Explorer users assume `Delete` = recycle and `Shift+Delete` = permanent. If they hit `Delete` in our app expecting recycle, they're surprised.

**Fix.** Two parts:

1. Make the confirmation dialog's message stronger for `Delete` alone: `"Permanently delete N items? They will NOT go to the Recycle Bin."` (existing text already mentions permanent; tighten the phrasing).
2. Wire `Shift+Delete` to the same command (via a `KeyboardAccelerator` on the Delete button or in `OnPreviewKeyDown`), so users who expect the Explorer keybinding land in the same place.

No "skip confirmation on `Shift+Delete`" shortcut — always confirm, in keeping with the existing spec's posture.

### 4.3. `Alt+Enter` for Properties (Alt+Enter = MISS)

Add a `KeyboardAccelerator` on the Inspector's "Properties" button:

```xml
<Button Content="Properties" Click="OnPropertiesClick" IsEnabled="{x:Bind ViewModel.HasItem, Mode=OneWay}">
    <Button.KeyboardAccelerators>
        <KeyboardAccelerator Key="Enter" Modifiers="Menu" ScopeOwner="{x:Bind}" />
    </Button.KeyboardAccelerators>
</Button>
```

`Modifiers="Menu"` = Alt on Windows. Scope owner ensures it fires globally within the shell.

Subject to long-path capability gating per `SPEC_LONG_PATHS.md` §6.1 — when the selection is a long path, the accelerator is still routed but `CanExecute` is false; nothing visible happens, matching the button's disabled state.

### 4.4. `Alt+Up` for parent directory (Alt+Up = MISS)

Add to `FileEntryTableView.BodyTable_KeyDown` alongside the existing `Backspace` / `Ctrl+PageUp`:

```csharp
case VirtualKey.Up when IsModifierDown(VirtualKey.Menu):
    host.NavigateUpCommand.Execute(null);
    e.Handled = true;
    break;
```

Update `winui-file-manager-keyboard-shortcuts-spec.md` §17 "Parent directory" row to `"Backspace, Ctrl+PageUp, Alt+Up"`.

### 4.5. `Ctrl+Home` / `Ctrl+End` routing (WRONG → OK)

Current `PreviewKeyDown` matches `VirtualKey.Home when !ctrl` and `VirtualKey.End when !ctrl`. When Ctrl is held, the TableView's own keyboard logic runs. On `Extended` selection, this is fine — the TableView does the right thing. But the current arms are inconsistent with the rest of the keybindings, which all ignore Ctrl for Home/End.

**Decision.** Remove the `when !ctrl` guard on `Home` and `End`; make them behave identically regardless of Ctrl. Rationale: there is no Ctrl-prefixed behavior that conflicts, and Explorer users expect both to jump.

### 4.6. `Alt+Left` / `Alt+Right` for back/forward navigation history (both = MISS)

**Scope.** Per-pane navigation history. Each `FilePaneViewModel` owns an independent back/forward pair; switching panes does not share history. History is in-memory only — **not persisted across app restarts** (keeps the design small; revisit if requested).

**Model.** New file: `src/WinUiFileManager.Presentation/ViewModels/PaneNavigationHistory.cs`

```csharp
internal sealed class PaneNavigationHistory
{
    private const int MaxDepth = 50;

    private readonly LinkedList<NormalizedPath> _back = new();
    private readonly LinkedList<NormalizedPath> _forward = new();

    public bool CanGoBack => _back.Count > 0;
    public bool CanGoForward => _forward.Count > 0;

    /// <summary>
    /// Records a navigation that happened *not* via back/forward. Pushes the previous
    /// location onto the back stack and clears the forward stack.
    /// </summary>
    public void RecordNavigation(NormalizedPath previous)
    {
        _back.AddFirst(previous);
        while (_back.Count > MaxDepth)
        {
            _back.RemoveLast();
        }
        _forward.Clear();
    }

    public bool TryGoBack(NormalizedPath current, out NormalizedPath target)
    {
        if (_back.Count == 0) { target = default; return false; }
        target = _back.First!.Value;
        _back.RemoveFirst();
        _forward.AddFirst(current);
        return true;
    }

    public bool TryGoForward(NormalizedPath current, out NormalizedPath target)
    {
        if (_forward.Count == 0) { target = default; return false; }
        target = _forward.First!.Value;
        _forward.RemoveFirst();
        _back.AddFirst(current);
        return true;
    }

    public void Clear()
    {
        _back.Clear();
        _forward.Clear();
    }
}
```

**Integration with `FilePaneViewModel`.**

```csharp
private readonly PaneNavigationHistory _history = new();
private bool _navigatingViaHistory;

public bool CanGoBack => _history.CanGoBack;
public bool CanGoForward => _history.CanGoForward;

[RelayCommand(CanExecute = nameof(CanGoBack))]
private async Task GoBackAsync()
{
    if (IsLoading || _currentNormalizedPath is null) return;
    if (!_history.TryGoBack(_currentNormalizedPath.Value, out var target)) return;
    _navigatingViaHistory = true;
    try { await LoadDirectoryAsync(target, restoreSelectionName: null, CancellationToken.None); }
    finally
    {
        _navigatingViaHistory = false;
        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(CanGoForward));
        GoBackCommand.NotifyCanExecuteChanged();
        GoForwardCommand.NotifyCanExecuteChanged();
    }
}

[RelayCommand(CanExecute = nameof(CanGoForward))]
private async Task GoForwardAsync() { /* symmetric to GoBackAsync */ }
```

**Hook `LoadDirectoryAsync` to record forward transitions.** At the top of `LoadDirectoryAsync`, before any cancellation or state mutation:

```csharp
if (!_navigatingViaHistory && _currentNormalizedPath is { } previous && previous != path)
{
    _history.RecordNavigation(previous);
    OnPropertyChanged(nameof(CanGoBack));
    OnPropertyChanged(nameof(CanGoForward));
    GoBackCommand.NotifyCanExecuteChanged();
    GoForwardCommand.NotifyCanExecuteChanged();
}
```

This captures every navigation regardless of source: path box, favourites, Enter-on-directory, Backspace/Ctrl+PageUp/Alt+Up, drive combo box selection. No source-specific hooks are needed.

**Keyboard routing.** In `MainShellView.OnPreviewKeyDown`:

```csharp
case VirtualKey.Left when IsModifierDown(VirtualKey.Menu):
    if (ViewModel?.ActivePane.GoBackCommand is { } back && back.CanExecute(null))
    {
        back.Execute(null);
    }
    e.Handled = true;
    break;

case VirtualKey.Right when IsModifierDown(VirtualKey.Menu):
    if (ViewModel?.ActivePane.GoForwardCommand is { } fwd && fwd.CanExecute(null))
    {
        fwd.Execute(null);
    }
    e.Handled = true;
    break;
```

**Mouse buttons (XButton1 / XButton2).** Out of scope for this spec; the same commands would back the mouse gestures when added.

**Edge cases.**
- Navigating to the *same* path (refresh, no-op) does not push a history entry — the `previous != path` guard covers this.
- A failed navigation (target directory no longer exists) does not push: the current `LoadDirectoryAsync` flow uses `ResolveExistingDirectoryOrAncestorAsync` to fall back to an ancestor, and our hook captures *whatever path actually gets loaded* as the new current. Acceptable for a v1 history.
- Rapid Alt+Left spamming during a load: guarded by `IsLoading` check at the top of the back/forward methods — clicks during a load are ignored.
- Selection restoration after back/forward: best-effort. The new `LoadDirectoryAsync` call has `restoreSelectionName = null`, so the first entry is selected. A future enhancement could record the selected item alongside each history entry; out of scope.

**Tooltip text.** When wiring surface UI (toolbar button, context menu) for back/forward, text should read `"Back (Alt+Left)"` / `"Forward (Alt+Right)"`. No keyboard accelerator on the buttons themselves — the key binding lives in the shell-level `OnPreviewKeyDown` via the registry.

### 4.7. Route new shortcuts through the registry

After §3 is in place, each added shortcut becomes a single `ShortcutDescriptor` entry plus its handler. No scattered `switch` branches.

## 5. Deferred items (explicit)

These are not gaps — they're intentionally out of v1, each owned by an existing spec.

### 5.1. Clipboard file-ops (`Ctrl+C` / `Ctrl+X` / `Ctrl+V`)

Requires a distinct clipboard-file-ops design: `DataPackage.SetStorageItems` for the copy, a "pending-cut" visual state for cut, conflict resolution on paste. Already partially represented by the existing `CollisionPolicy` infrastructure but needs an explicit spec. **Ticket:** write `SPEC_CLIPBOARD_FILE_OPS.md` when queued.

### 5.2. `F3` viewer

Preview pane feature — needs decoding pipeline, binary/hex mode, large-file handling. Out of v1. Mentioned in `SPEC_FEATURE_LOW_HANGING_FRUIT.md` non-goals.

### 5.3. `F4` external-editor launch

De-scoped per human-owner direction (2026-04-21). Reopen when an external-editor configuration surface is introduced (setting, resolver, fallback chain). Until then, `F4` is unbound.

### 5.4. `Ctrl+F` quick filter, command palette, jump-to palette, tabs, hidden-file toggle, Reveal-in-Explorer, Open-Terminal

All owned by `SPEC_FEATURE_LOW_HANGING_FRUIT.md` features F1, F3, F10, F2, F15, F4 (reveal + terminal) respectively. Each feature ships with its own shortcut. Do not pre-wire the keys in the registry; add them when the features land.

## 6. Interactions with existing specs

- **`winui-file-manager-keyboard-shortcuts-spec.md`** is the canonical reference. This spec proposes the following edits to it, to be applied in the same PR that implements §4:

  - §12.9 title → "Rename in place (`F2`, `Shift+F6`)"; remove the "rename dialog input" wording, replace with "inline rename editor".
  - §17 table updates:
    - Rename row → `F2, Shift+F6`.
    - Parent directory row → `Backspace, Ctrl+PageUp, Alt+Up`.
    - Add new row "Properties": `Alt+Enter`.
    - Add new row "Back in history": `Alt+Left`.
    - Add new row "Forward in history": `Alt+Right`.
    - Add new row "Permanent delete (same as Delete)": `Shift+Delete`.

- **`SPEC_UI_LAYOUT_AND_RESIZING.md` §6** delivers the in-cell rename surface that `F2` and `Shift+F6` target.
- **`SPEC_LONG_PATHS.md`** — `Alt+Enter` (shell Properties) is gated by the long-path capability policy; no additional work here.
- **`MISSING_FEATURES_SPEC.md` §2** (centralized shortcut registry) is **delivered by §3 of this spec**.
- **`SPEC_FEATURE_LOW_HANGING_FRUIT.md`** — feature shortcuts remain owned by their feature tickets.
- **`SPEC_BUG_FIXES.md`** — no interaction.

## 7. Manual verification checklist

- [ ] `F2` enters in-cell rename on the current item.
- [ ] `Shift+F6` enters in-cell rename (same behavior).
- [ ] `Alt+Up` navigates to parent.
- [ ] `Alt+Left` goes back in the active pane's history when history is non-empty; is a no-op (key swallowed, no error) when history is empty.
- [ ] `Alt+Right` goes forward when a prior Alt+Left has populated the forward stack; no-op otherwise.
- [ ] Opening a new path via the path box, Enter, or Alt+Up clears the forward stack.
- [ ] Back/forward history is **per pane** — history in the left pane is unaffected by navigation in the right pane.
- [ ] Closing and reopening the app starts with empty history in both panes.
- [ ] `Alt+Enter` opens shell Properties (on a short path). On a long path, nothing happens (per long-paths capability gating).
- [ ] `Shift+Delete` triggers the same delete flow as `Delete` / `F8`.
- [ ] `Ctrl+Home` / `Ctrl+End` jumps to first / last row.
- [ ] `grep` confirms all shortcuts go through `ShortcutRegistry` (no hand-rolled `case VirtualKey.X` outside the registry-driven dispatch, **except** for text-box handlers where WinUI's KeyboardAccelerator machinery conflicts).
- [ ] Every CommandBar button tooltip lists every shortcut that routes to it, in priority order.
- [ ] Every existing shortcut from `winui-file-manager-keyboard-shortcuts-spec.md` §17 continues to work — regression smoke.

## 8. Acceptance

- 7 gaps from §2 are closed (F2, Shift+F6 corrected, Alt+Up, Alt+Left, Alt+Right, Alt+Enter, Shift+Delete).
- 2 minor corrections land (Ctrl+Home / Ctrl+End guards removed).
- `ShortcutRegistry` is the single source of truth for shortcut definitions and tooltip text.
- `winui-file-manager-keyboard-shortcuts-spec.md` is updated per §6 (edit list above).
- Each pane owns an independent in-memory navigation history bounded to 50 entries; history is not persisted across app restarts.
- All items in §7 pass manual verification.
- Deferred items from §5 are explicitly not implemented — no half-built Ctrl+V, no F4.
