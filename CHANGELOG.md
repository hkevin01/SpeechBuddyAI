# Changelog

All notable changes to this project are documented in this file.

## [Unreleased] - 2026-07-03

### Added
- Persisted calibration metadata per attempt in `ProgressEntry`:
  - `RawConfidenceScore`
  - `EmpiricalOutcomeMean`
  - `EmpiricalOutcomeSupport`
  - `CalibrationResidual`
  - `CalibrationMethod`
  - `CalibrationTableJson`
  - `VarianceUncertaintyComponent`
  - `SparsityUncertaintyComponent`
- Schema migration support for the above fields in `ProgressSchemaMigration`.
- Non-linear per-target calibration tables (binned isotonic mapping) in `ConfidenceCalculator`.
- Reliability-aware uncertainty decomposition (`variance-driven` and `sparsity-driven`) in confidence computation and report export summaries.
- Longitudinal calibration drift reporting by target in export output.
- Clinician-facing calibration tuning controls in Settings UI:
  - Initial/medial/final calibration support coefficients
  - Calibration table activation threshold (minimum sample count)
- Persisted settings APIs for calibration support coefficients and calibration table activation threshold.
- New regression tests for calibration behavior under synthetic shift scenarios:
  - sudden improvement
  - sudden deterioration
  - noisy oscillation

### Changed
- `AiSpeechService` now applies clinician-configurable calibration table activation thresholds instead of a fixed internal value.
- End-to-end scoring/persistence/report tests now assert calibration residual persistence and uncertainty/decomposition report sections.
- Assignment hard-freeze test assertion updated to assert preserved baseline target rather than a hardcoded symbol.

### Removed
- Removed local test-only SQLite stub layer and switched tests to the real `sqlite-net-pcl` package for higher-fidelity persistence behavior.

### Why this release matters
- Improves trust by making calibration behavior observable and auditable per attempt.
- Improves safety by separating uncertainty sources (variance vs sparsity) for better clinical interpretation.
- Improves adaptability by allowing clinicians to tune calibration sensitivity without code edits.
- Improves model governance with clearer longitudinal drift signals and stronger regression coverage.
