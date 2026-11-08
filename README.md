# dev-cockpit

**A desktop sidekick for developers: capture what you did in one keystroke, get your stand-up written for you.**

[Leer en español](README.es.md)

![.NET 10](https://img.shields.io/badge/.NET-10-512BD4) ![Avalonia](https://img.shields.io/badge/UI-Avalonia-8B44AC)
![Tests](https://img.shields.io/badge/tests-142%20passing-2EA44F) ![License: MIT](https://img.shields.io/badge/license-MIT-blue)

<!-- Screenshot: docs/screenshot.png (Daily tab with a generated summary) -->

I built this because I kept forgetting what I did yesterday by the time the daily stand-up started.
It lives in the system tray, reads your git history across all your repos, and turns commits plus
quick notes into a stand-up, a weekly summary, or a list of achievements for your CV and performance
reviews. I use it every day.

## Features

- **Quick note from anywhere** with `Ctrl+Alt+N`: type, press Enter, done. Mark it as an **achievement**
  and attach a number ("from 4 s to 300 ms").
- **Daily stand-up** (Yesterday / Today / Blockers) built from your commits and notes since the last
  working day (on Monday it looks at Friday).
- **Weekly summary** grouped by repository.
- **Achievements** exportable to Markdown.
- **Optional AI rewriting**, in Spanish or English:
  - **Ollama** (local, default): nothing leaves your machine. Text **streams in live** as it is generated.
  - **Claude** or **ChatGPT**: before every request the app shows **the exact text** that will be sent
    and asks for confirmation.
  - Without AI you still get a clean template.
- **Light / dark / system theme**, and an option to skip preloading the local model (it takes ~5 GB of RAM).

## Design decisions

| Decision | Why |
|---|---|
| **Hexagonal architecture** (`Core` / `Infrastructure` / `App`) | The domain (working days, templates, report service, hotkey rules) has no I/O and is tested in isolation. Git, SQLite, LLM providers and the OS are adapters behind ports. |
| **The AI only rewrites a draft, never raw data** | The model receives the template already built from commits and notes, so the user confirms exactly what is sent and the model can only rephrase facts that are already there. |
| **Privacy first** | Local model by default; cloud providers require explicit, per-request confirmation; API keys live in the Windows Credential Manager, never in config files. |
| **Streaming with an inactivity timeout** | On a CPU-only machine a 7B model writes ~3 tokens/s. The timeout resets on every chunk, so a 3-minute cold load followed by a long answer never gets cut, but a hung server still does. |
| **Git through its CLI** | Same behaviour as in your terminal (including `--all` and author filtering), no libgit2 native dependency. |
| **AltGr-aware global hotkey** | On Spanish keyboards AltGr is sent as Left Ctrl + Right Alt; the hotkey matcher tells them apart so `Ctrl+Alt+Q` never swallows `@`. |

## Tech stack

.NET 10 · C# · Avalonia UI · CommunityToolkit.Mvvm · SQLite + Dapper · SharpHook (global hotkey) ·
Anthropic C# SDK · xUnit

| Project | Contents |
|---|---|
| `src/DevCockpit.Core` | Domain, ports and pure logic. No I/O. |
| `src/DevCockpit.Infrastructure` | Adapters: git CLI, SQLite, JSON settings, Ollama / Claude / ChatGPT, Credential Manager. |
| `src/DevCockpit.App` | Avalonia + MVVM: tray icon, windows, global hotkey. |
| `tests/*` | 142 xUnit tests. Git tests create real temporary repositories. |

## Getting started

Requirements: Windows 10/11 and the [.NET 10 SDK](https://dotnet.microsoft.com/download) to build.

```powershell
.\scripts\publish.ps1          # self-contained exe in dist\DevCockpit.App.exe (~114 MB)
```

Local AI (optional, recommended):

```powershell
ollama pull qwen2.5:7b-instruct
```

To start with Windows: `Win+R` → `shell:startup` → create a shortcut to `DevCockpit.App.exe` with
`--minimized` at the end of the target.

First run, in **Settings**: add the repositories to follow, check your git emails (pre-filled from
`git config --global user.email`), pick the AI provider and press **Save**.

## Development

```powershell
dotnet test                                   # 142 tests
dotnet run --project src/DevCockpit.App       # debug run
```

Design spec and implementation plan: `docs/superpowers/`.

## Roadmap

- [x] **Module 1 · Work log**: notes, daily, weekly, achievements, AI rewriting.
- [ ] **Module 2 · Environments**: running Docker Compose stacks and which project uses which port.
- [ ] **Module 3 · Queries**: visual PostgreSQL `EXPLAIN ANALYZE`.

## Known limitations

- Tested on Windows 10 only. Avalonia runs on macOS and Linux, but API keys are stored with the
  Windows Credential Manager for now.
- A local 7B model sometimes gets nuances wrong; the AI-free draft is always one checkbox away.

## License

[MIT](LICENSE)
