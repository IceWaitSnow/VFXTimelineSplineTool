# VFX Timeline Spline Tool - Dev Notes

This note is for continuing development after a long Codex session. It records the current editor workflow and the main design choices.

## Current Scene Editing Workflow

- Point editing is handled in Scene View by `VFXSplinePointAPI`, `VFXSplinePointSceneOverlay`, and `VFXSplineSceneDrawer`.
- The Scene status hint appears in the upper-left corner and shows the current edit mode, path mode, selection state, and shortcuts.
- Core shortcut keys are configurable from:
  - `Tools > VFX Timeline Spline > Shortcut Settings`
- Default shortcuts:
  - `P`: toggle point edit mode
  - `A`: enter/exit append point mode
  - `M`: open point/curve context menu
  - `F`: frame selected point
  - `Delete` / `Backspace`: delete point
  - `Esc`: exit append mode first, then object mode

## Shortcut Design

- Right-click / Shift-right-click is no longer the primary workflow because Unity Scene View often reserves right mouse input for camera navigation.
- `A` opens a stable append mode:
  - Left click on empty Scene space appends a point.
  - `A` or `Esc` exits append mode.
  - `Alt + mouse` still works for view navigation.
- `M` opens a context menu:
  - Near a point: opens that point's menu.
  - Near a curve: opens curve insertion menu.
- `Shift + append shortcut` still performs a single add-at-mouse operation.

## Bezier Editing

- Bezier mode supports in/out tangents and handle modes:
  - Free
  - Aligned
  - Mirrored
  - Auto Smooth
- Bezier point context menu includes:
  - Auto Smooth
  - Auto Smooth All
  - Point type presets
  - Handle mode presets
  - Delete current point / delete selected points
- Bezier curve context menu supports inserting a Bezier point at the nearest curve position.
- The inline Bezier toolbar shows only Free / Align / Mirror. Auto Smooth is kept in the context menu.

## Catmull-Rom Editing

- Catmull-Rom now also supports `M` curve insertion:
  - Near curve: insert Catmull-Rom point at the nearest raw progress.
  - Near point: insert after current point, or delete.
- Runtime helper:
  - `VFXSimpleSpline.InsertCatmullRomPointAtRawProgress(float progress)`
- Catmull-Rom point menus intentionally stay simpler than Bezier menus.

## Multi-Selection

- `Ctrl` / `Cmd` click toggles point selection.
- `Ctrl` / `Cmd` drag on empty Scene space performs box selection.
- Multi-selected points share one center move handle.
- Context menu on a selected point can:
  - Align selected points by world X/Y/Z min/max.
  - Delete all selected points.
- Deletion is done in descending index order and keeps at least 2 points.

## Important Implementation Notes

- Avoid `GUILayout` inside Scene View overlays. Use fixed `Rect` with `GUI.Box`, `GUI.Label`, or `GUI.Button`.
- Avoid broad `HandleUtility.AddDefaultControl`; it can break handle dragging. It is currently used only while append mode is active.
- Shortcut settings are stored in `EditorPrefs` through `VFXSplinePointAPI`.
- The old Unity `[Shortcut]` attribute for `P` was removed so custom shortcuts do not conflict with a hard-coded `P`.
- Some older source comments or UI text may appear garbled in terminal output because of encoding display, but Unity may still show the original text correctly.

## Useful Files

- `Runtime/VFXSimpleSpline.cs`
  - Runtime spline data and path evaluation.
  - Bezier and Catmull-Rom point insert/delete helpers.
- `Editor/VFXSimpleSplineEditor.cs`
  - Inspector plus most Scene drawing and editing logic.
- `Editor/VFXSplinePointAPI.cs`
  - Edit mode state, shortcut settings, point operations.
- `Editor/VFXSplinePointSceneOverlay.cs`
  - Scene overlay hook and Tools menu entries.
- `Editor/VFXSplineShortcutSettingsWindow.cs`
  - Shortcut settings window.
- `Editor/VFXSplinePointListWindow.cs`
  - Point list/editor window.

## Possible Next Improvements

- Add a small "reset shortcuts" menu item directly under `Tools > VFX Timeline Spline`.
- Add duplicate point / duplicate selected points.
- Add resample path to N points.
- Add remove-near-duplicate-points cleanup.
- Add project-plane / flatten-to-plane tools for selected points.
- Add path direction reverse command to the Scene context menu.
- Review Undo behavior in Unity for:
  - continuous append mode
  - multi-delete
  - multi-move
  - curve insertion
  - shortcut changes
