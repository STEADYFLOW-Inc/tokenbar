# Contributing to ClaudeTokenMeter

## How to Build

**Requirements:** Windows with .NET Framework 4.8 installed (no SDK, no NuGet, no extra tools needed).

```powershell
.\build.ps1
```

This invokes the in-box `csc.exe` compiler shipped with .NET Framework 4.8. No `dotnet` CLI, no Visual Studio, and no package restore step is required.

### C# 5.0 Syntax Constraint

The project intentionally targets **C# 5.0 syntax** (the language version supported by the .NET Framework 4.8 in-box compiler). This constraint **must be preserved**. Do not use:

- `nameof(...)` (C# 6+)
- String interpolation `$"..."` (C# 6+)
- Expression-bodied members `=> ...` (C# 6+)
- `null?.` / `??=` operators (C# 6+ / C# 8+)
- `async` streams, records, pattern matching beyond `is`/`as` (C# 8+/9+)

Use explicit string concatenation, `String.Format`, and traditional property/method bodies instead.

## Code Style

- Keep files small and focused (target 200–400 lines, hard max 800).
- Handle errors explicitly — never silently swallow exceptions.
- Use descriptive variable names; avoid abbreviations in public-facing identifiers.
- No `Console.Write` debug statements in committed code.

## UI Strings

All user-visible strings (both Japanese and English) live in `Strings.cs` as `ja`/`en` pairs. When adding new UI text, add both translations there rather than hardcoding literals in logic files.

## Pull Request Guidelines

- Use [Conventional Commits](https://www.conventionalcommits.org/) for commit messages:
  `feat:`, `fix:`, `refactor:`, `docs:`, `chore:`, `perf:`
- One logical change per PR.
- Ensure `build.ps1` succeeds with exit code 0 before opening a PR.
- Describe *why* the change is needed, not just what changed.
