# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.3.0] - 2026-07-15

### Added
- Per-model bar selection: choose which models (Fable, Opus, ...) appear as bars via dynamic checkboxes in the settings window (`selectedModels` in config; empty = all)
- Multi-monitor support: place the widget on any display's taskbar (`monitor` in config, selector in settings), including secondary taskbars (Shell_SecondaryTrayWnd) with overlay fallback; fullscreen auto-hide now checks the widget's own display

## [1.2.0] - 2026-07-15

### Added
- Data-source indicator dot on the widget: green = live API, amber = cached (API temporarily unavailable), gray = local estimate, red = error
- Settings window UX: live preview (changes apply to the widget instantly, Cancel reverts), header with logo and version, data-source status footer, start-with-Windows checkbox, fallback token-limit input
- Hover feedback on the widget (hand cursor + highlight) to make it discoverable as clickable

## [1.1.1] - 2026-07-15

### Fixed
- Single-bar value text shows the "remaining" prefix again (regression in 1.1.0)

## [1.1.0] - 2026-07-15

### Added
- Settings window (left-click the widget): toggle title / value text / reset time, choose which bars to show
- Multiple bars: 5-hour session, weekly (all models), weekly per-model

### Changed
- Left-click now opens settings (refresh moved to the context menu)

## [1.0.0] - 2026-07-15

### Added
- Taskbar overlay widget showing Claude Code 5-hour session remaining %
- Accurate data via the same OAuth usage endpoint as /usage with local JSONL estimation fallback
- Weekly usage in tooltip
- Auto-hide during fullscreen apps
- Explorer-restart resilience
- Startup registration
- ja/en UI
- Configurable position/offset/width/refresh interval
- Rate-limit resilience: HTTP 429 backoff (honors Retry-After) and cached last-good API data during transient failures
- Official Claude logo as the app and widget icon
