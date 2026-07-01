# UI Snapshot Baselines

This folder supports screenshot-based visual regression checks for compact phone layouts.

## Expected Structure

- `baseline/compact-phone/*.png` - approved baseline snapshots.
- `current/compact-phone/*.png` - newly captured snapshots from the latest run.

## Naming Convention

Use stable file names per screen, for example:

- `home.png`
- `practice.png`
- `progress.png`
- `notes.png`
- `settings.png`

## Test Behavior

`UiSnapshotVisualRegressionTests` compares baseline and current images with a normalized RGB delta threshold.

- If baseline or current directories are missing, the test exits early (non-failing).
- If baseline images exist, each baseline file must have a matching current file.
- Any image-size mismatch or high pixel delta fails the test.

## Device-Lab Capture Runner

CI uses an Android emulator device-lab job with:

- `.github/scripts/capture_android_compact_snapshots.sh`

The runner:

- builds and installs the Android app,
- launches the app,
- navigates all 5 tabs,
- captures `*.png` screenshots into `current/compact-phone`,
- captures `uiautomator` hierarchy XML into `current/compact-phone/layout-bounds`.

## Overlap Guardrail (Pre-Diff)

`UiLayoutBoundsOverlapGuardrailsTests` performs a stricter pass before image diff:

- parses captured layout-bounds XML,
- checks enabled clickable/focusable elements,
- fails if two actionable elements overlap with meaningful intersection area,
- ignores parent-child containment overlap to reduce false positives.

## CI Usage

You can override default snapshot paths with environment variables:

- `UI_SNAPSHOT_BASELINE_DIR`
- `UI_SNAPSHOT_CURRENT_DIR`
- `UI_LAYOUT_BOUNDS_CURRENT_DIR`
