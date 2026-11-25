# Changelog

All notable changes to PCOpti are documented here.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project uses [Semantic Versioning](https://semver.org/).

## [Unreleased]

## [1.0.0] - 2026-11-25

### Added
- Home page with live CPU, RAM, disk and GPU usage and a short guide. There is no "optimise everything" button.
- **Gaming Tweaks**, each applied and reverted individually: High Performance and Ultimate Performance power plans,
  Game Mode, Xbox Game Bar shortcuts, Game DVR background recording, Hardware-Accelerated GPU Scheduling,
  mouse acceleration, visual effects, transparency, background Store apps and notification pop-ups.
- **Startup Apps**: Run keys, Startup folders and Task Scheduler logon tasks, each with an enable/disable switch.
  It uses the same mechanism as Task Manager, so nothing is ever deleted.
- **Cleaner**: temp files, Windows Update cache, shader caches, thumbnail cache and Recycle Bin. Each shows its size first and asks for confirmation.
- **Network**: flush DNS, reset Winsock, and switch DNS between Cloudflare, Google and Automatic, with exact restore of the original settings.
- **Services**: a curated list of six services that are safe to set to Manual.
- **Game Boost**: close the background apps you pick (and reopen them later), and raise a game's CPU priority.
- **Backup & Restore**: an automatic backup before every change, a per-tweak Revert, "Revert all PCOpti changes",
  an optional System Restore point and a change log you can view.
- **Settings**: accent colour, and an update check that only notifies (off by default).
- Windows installer (`PCOpti-Setup-vX.Y.Z.exe`, built with Inno Setup): installs to Program Files, adds Start menu
  and optional desktop shortcuts, and uninstalls cleanly from Settings > Apps.
- Portable single-file `PCOpti.exe` (zip), self-contained, so no .NET install is needed.
- App icon.

[Unreleased]: https://github.com/isxxc21/PCOptim/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/isxxc21/PCOptim/releases/tag/v1.0.0
