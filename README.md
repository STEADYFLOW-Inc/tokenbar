# ClaudeTokenMeter

A tiny Windows 11 taskbar widget that shows your remaining Claude Code usage at a glance.

(日本語版は [README.ja.md](README.ja.md))

![screenshot](docs/screenshot.png)

---

## Features

- **Main display** — 5-hour session remaining % + progress bar + reset time, always visible on the taskbar.
- **Hover tooltip** — weekly usage (all models combined / per-model breakdown) with reset times.
- **Accurate data** — primary source is the same OAuth endpoint that Claude Code's `/usage` command uses (`GET https://api.anthropic.com/api/oauth/usage`), authenticated with the token already stored in `~/.claude/.credentials.json`.
- **Automatic fallback** — when the API is unavailable, parses `~/.claude/projects/**/*.jsonl` transcripts and reconstructs the current 5-hour block (ccusage-style estimation).
- **Auto-hides** during fullscreen apps so it never blocks a game or presentation.
- **Survives Explorer restarts** — the widget reattaches automatically.
- **Single instance** — a second copy exits immediately if one is already running.
- **Rate-limit aware** — backs off on HTTP 429 (honoring `Retry-After`) and keeps showing the last good API data instead of jumping to the rough local estimate.
- **Bilingual UI** — Japanese / English, follows the OS display language.
- **Zero dependencies** — a single ~50 KB `.exe`; uses .NET Framework 4.8 that is already built into Windows. No runtime to install.

---

## Requirements

- Windows 10 or 11
- .NET Framework 4.8 (ships with Windows 10 1903+ and all Windows 11 editions)
- Claude Code installed and signed in (so `~/.claude/.credentials.json` exists)

---

## Install

### Option A — Download the exe

Download `ClaudeTokenMeter.exe` from the [Releases](../../releases) page and run it. That's all.

### Option B — Build from source

No SDK required. The compiler (`csc.exe`) ships with Windows:

```powershell
powershell -File build.ps1
```

Or invoke the compiler directly:

```powershell
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /platform:x64 /optimize+ `
  /out:ClaudeTokenMeter.exe `
  /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Web.Extensions.dll `
  Program.cs Config.cs UsageReader.cs ApiUsageReader.cs AppContext.cs WidgetForm.cs Strings.cs AssemblyInfo.cs
```

---

## Usage

```powershell
.\ClaudeTokenMeter.exe          # Start the widget (single-instance guard included)
.\ClaudeTokenMeter.exe --dump   # Write a diagnostic dump.txt and exit
```

| Interaction | Action |
|-------------|--------|
| Left-click | Refresh data immediately |
| Right-click | Context menu: Refresh / Open config / Reload config / Run at startup / Exit |

---

## Configuration

`config.json` is created automatically next to the exe on first run. Edit it with any text editor; use **Reload config** from the right-click menu to apply changes without restarting.

| Key | Default | Description |
|-----|---------|-------------|
| `tokenLimit` | `200000` | Token cap used by local-estimate (fallback) mode for the 5-hour block |
| `includeCacheRead` | `false` | Whether to count `cache_read` tokens in the local estimate |
| `refreshSec` | `60` | Data refresh interval in seconds |
| `position` | `"right"` | Taskbar side — `"right"` places the widget near the clock; `"left"` near the Start button |
| `offsetX` | `0` | Additional horizontal offset in logical pixels (positive = move toward center) |
| `widgetWidth` | `240` | Widget width in logical pixels (valid range: 160–400) |
| `claudeDir` | `""` | Path to your `.claude` directory; empty = `%USERPROFILE%\.claude` |
| `embed` | _(reserved)_ | Reserved display-mode flag; do not change |

---

## How It Works

**Primary source — OAuth usage API**

ClaudeTokenMeter calls:

```
GET https://api.anthropic.com/api/oauth/usage
Authorization: Bearer <token from ~/.claude/.credentials.json>
```

This is the exact same endpoint Claude Code's `/usage` command queries. The response contains accurate session and weekly counters for all models.

**Fallback — local transcript estimation**

If the API call fails (offline, token expired, etc.), the widget scans `~/.claude/projects/**/*.jsonl`, identifies the current 5-hour block, and sums token usage from JSONL entries — the same approach used by tools like `ccusage`. Accuracy depends on `tokenLimit` matching your account's actual limit.

**Rendering**

Windows 11's XAML taskbar composites over classic `SetParent` child windows, making traditional taskbar embedding unreliable. ClaudeTokenMeter instead uses a **TOPMOST overlay** positioned precisely over the taskbar (the same technique used by ElevenClock). It monitors fullscreen state and hides automatically to avoid covering fullscreen applications.

### Source file overview

| File | Role |
|------|------|
| `Program.cs` | Entry point — DPI awareness, single-instance guard, `--dump` flag |
| `AppContext.cs` | Timer management, widget lifetime monitoring (Explorer restart recovery), startup registration |
| `WidgetForm.cs` | Rendering, TOPMOST overlay positioning on the taskbar, fullscreen auto-hide |
| `ApiUsageReader.cs` | OAuth usage API reader (primary data source) |
| `UsageReader.cs` | JSONL transcript parser (fallback data source) |
| `Config.cs` | Read/write `config.json` |
| `Strings.cs` | Localized UI strings (ja/en) |

---

## Privacy & Security

- Everything runs **locally on your machine**.
- The OAuth token is read from Claude Code's own credentials file (`~/.claude/.credentials.json`). It is used solely for the single HTTPS request to `api.anthropic.com` and is never logged, cached elsewhere, or transmitted to any other server.
- No telemetry. No third-party servers. No network traffic beyond the one API call per refresh cycle.

---

## Troubleshooting

| Symptom | Likely cause & fix |
|---------|--------------------|
| Widget is not visible | Taskbar auto-hide may be covering it — move the cursor to the taskbar edge. Also check that a fullscreen app is not active. |
| Widget shows an error message | Claude Code is not signed in, or `~/.claude/.credentials.json` is missing. Sign in to Claude Code and restart the widget. |
| Percentages look wrong | The API is unavailable and the widget is in fallback mode. Set `tokenLimit` in `config.json` to match your account's actual 5-hour token limit. |

---

## Disclaimer

This is an **unofficial community tool** and is not affiliated with, sponsored by, or endorsed by Anthropic. "Claude" is a trademark of Anthropic PBC. The usage endpoint used by this widget is undocumented, not part of any public API contract, and may change or be removed at any time without notice.

---

## License

MIT — see [LICENSE](LICENSE).
