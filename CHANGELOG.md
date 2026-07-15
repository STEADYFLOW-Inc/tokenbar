# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.5.2] - 2026-07-16

### Fixed
- Reset time now displays in every layout: right-aligned on the title row when the title is shown, and as a right-side column in no-title multi-bar layouts (it previously only appeared in the single-bar layout, and narrow widths could suppress it entirely)
- Source dot no longer overlaps the first row's percentage in no-title layouts (moved to the top-left corner there)

## [1.5.1] - 2026-07-16

### Fixed
- Three or more bars no longer require hiding the title: when bars don't fit vertically, the card automatically switches to a two-column layout (up to 4 bars with the title, 6 without)
- Bars beyond the displayable count are no longer dropped silently — a small "+N" marker shows how many are hidden (the tooltip lists all)

## [1.5.0] - 2026-07-15

### Added
- First-run quick setup wizard: choose the Claude directory (with live credential validation), monitor, taskbar position, and startup registration on first launch
- Live widget preview inside the settings window — every toggle is reflected instantly in a rendered mock of the actual card
- Visual monitor picker (Windows display-settings style): click a screen rectangle to move the widget there

### Changed
- Widget card rendering extracted into a shared CardRenderer used by both the taskbar widget and the settings preview

## [1.4.1] - 2026-07-15

### Changed
- Default refresh interval raised from 60s to 120s to stay well clear of the usage endpoint's shared rate limit (frequent polling could transiently 429 Claude Code's own /usage as well)

## [1.4.0] - 2026-07-15

### Changed
- The meter now always shows API data; the last good API result is persisted to disk and shown (labeled "as of HH:mm") during temporary API outages and across restarts
- The local JSONL estimate no longer drives the meter (diagnostics/tooltip only); token-limit setting removed from the settings window
- Faster recovery from rate limiting (429 backoff default 300s → 120s)

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
