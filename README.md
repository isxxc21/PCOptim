<p align="center">
  <img src="docs/images/logo.png" alt="PCOpti logo" width="120" /></p>

<h1 align="center">PCOpti</h1>

<p align="center">
  <b>An honest, open-source PC optimiser for gamers.</b><br/>
  Pick exactly which tweaks you want. Every change is backed up and has its own undo button.
</p>

<p align="center">
  <a href="https://github.com/isxxc21/PCOptim/actions/workflows/build.yml"><img src="https://github.com/isxxc21/PCOptim/actions/workflows/build.yml/badge.svg" alt="Build" /></a>
  <a href="https://github.com/isxxc21/PCOptim/releases/latest"><img src="https://img.shields.io/github/v/release/isxxc21/PCOptim" alt="Latest release" /></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-MIT-blue" alt="MIT License" /></a>
</p>

---

## Why PCOpti?

Most "PC boosters" press one big button, change dozens of settings you can't see, and then show a made-up
"your PC is 87% faster!" score. PCOpti does the opposite:

- **No "optimise all" button.** Every tweak is its own card with its own Apply / Revert button. Nothing runs unless you click it.
- **Honest descriptions.** Each card says what it changes and how much it really helps, including "small" when that's the truth.
- **Everything is reversible.** The original value is saved before any change. Undo one tweak, or everything at once.
- **No telemetry, no ads, no bundled software.** The only network request is an optional update check, and it's off by default.

## Screenshots

<!-- Screenshots placeholder: add images to docs/images/ and reference them here. -->
| Home | Gaming Tweaks | Backup & Restore |
|------|---------------|------------------|
| ![Home](docs/images/screenshot-home.png) | ![Gaming Tweaks](docs/images/screenshot-gaming.png) | ![Backup](docs/images/screenshot-backup.png) |

## Features

| Page | What you can do |
|------|-----------------|
| **Home** | Live CPU, RAM, disk and GPU usage and a short guide. |
| **Gaming Tweaks** | High Performance and Ultimate Performance power plans, Game Mode, Xbox Game Bar shortcuts, Game DVR background recording, Hardware-Accelerated GPU Scheduling, mouse acceleration, visual effects and animations, transparency, background Store apps, notification pop-ups. Each one is separate. |
| **Startup Apps** | See startup programs from the registry Run keys, Startup folders and Task Scheduler, with publisher, path and an estimated impact. Switch each one on or off, just like Task Manager does. Nothing is deleted. |
| **Cleaner** | Temp files, Windows Update cache, shader caches (NVIDIA, AMD, DirectX), thumbnail cache, Recycle Bin. Shows the size first, and always asks before deleting. |
| **Network** | Flush DNS, reset Winsock, and switch DNS between Cloudflare, Google or Automatic. Your original DNS is restored exactly. No placebo "latency tweaks". |
| **Services** | A short, curated list of services that are safe to set to Manual (Print Spooler, Fax, Remote Registry...), each with an explanation. Critical services are never offered. |
| **Game Boost** | Close the background apps you pick (and reopen them later), and raise a game's CPU priority. |
| **Backup & Restore** | Automatic backup before every change, optional System Restore point, a full change log, and **Revert all PCOpti changes**. |
| **Settings** | Accent colour, a link to this repo, version, and an update check that only notifies you. |

## Download and install

### Installer (recommended)

1. Go to the [**latest release**](https://github.com/isxxc21/PCOptim/releases/latest) and download **`PCOpti-Setup-vX.Y.Z.exe`**.
2. Run it and click through the setup. It installs PCOpti to `Program Files` and adds a Start menu shortcut (and a desktop shortcut if you tick the box).
3. Launch PCOpti from the Start menu and accept the administrator prompt. PCOpti needs admin rights because most settings it changes are system-wide.

Nothing else is needed: .NET is built in. To uninstall, use **Settings > Apps > Installed apps > PCOpti**.
Uninstalling doesn't undo PCOpti's changes, so run **Backup & Restore > Revert all** first if you want them undone.
Your backups are kept, so you can also reinstall later and revert then.

### Portable (no install)

Download `PCOpti-vX.Y.Z-win-x64-portable.zip`, extract it anywhere and run `PCOpti.exe`.

**Requirements:** Windows 10 (version 2004 or newer) or Windows 11, 64-bit.

**Optional, verify the download:** every release includes `SHA256SUMS.txt`. In PowerShell:
```powershell
Get-FileHash .\PCOpti-Setup-v1.0.0.exe -Algorithm SHA256
```
The hash should match the one in `SHA256SUMS.txt`.

### "Windows protected your PC" (SmartScreen)

When you first run PCOpti, Windows may show a blue **SmartScreen** warning. This happens because:

- PCOpti is **not code-signed**. Code-signing certificates cost hundreds of dollars a year, which is a lot for a free hobby project.
- SmartScreen builds "reputation" from how many people have downloaded a file. A new release of any small app starts with no reputation.

It does **not** mean a virus was found. To run it anyway: click **More info**, then **Run anyway**.
If you'd rather not trust a prebuilt exe, you can [build it yourself](#build-from-source) from the source code in a few minutes.

## Safety and FAQ

<details open>
<summary><b>Is this safe?</b></summary>

PCOpti only uses documented Windows settings: registry values that the Settings app also changes, `powercfg`,
`sc.exe`, Task Scheduler and DNS settings. Before every change it saves the original value. Tweaks are rated
**Safe**, **Moderate** or **Advanced**, and Moderate or Advanced ones explain the risk and ask you to confirm first.
It never deletes startup entries or touches critical Windows services.

Changing system settings always carries *some* risk, though, so use it at your own risk (see the [disclaimer](#disclaimer)).
Creating a System Restore point when PCOpti offers is a good idea.
</details>

<details>
<summary><b>How do I undo changes?</b></summary>

- **One tweak:** click **Revert** on its card (or switch off its toggle). It goes back to the exact value it had before.
- **Everything:** open **Backup & Restore** and click **Revert all PCOpti changes**.
- **Last resort:** if Windows won't start properly, use the System Restore point (Start > "Create a restore point" > System Restore).

Backups are stored in `%LocalAppData%\PCOpti\backups.json`, so they survive restarts and updates.
Two things *can't* be undone: files deleted by the Cleaner, and a Winsock reset (it resets to Windows defaults).
The app tells you this before you run them.
</details>

<details>
<summary><b>How much faster will my games be?</b></summary>

Honestly, on a well-maintained modern PC, usually a little: a few percent, or smoother frame times. The biggest real
wins are usually turning off unneeded startup apps, closing background programs while you play (Game Boost),
turning off background recording if it was on, and keeping your GPU driver updated. Each card says what to expect.
</details>

<details>
<summary><b>Why does a tweak already show "Applied"?</b></summary>

PCOpti reads the real current state from Windows. Some settings, like Game Mode, are already on by default, and
you or another program may have changed others before. In that case there's nothing to do.
</details>

<details>
<summary><b>A tweak failed. What now?</b></summary>

The error appears on that tweak's card, and nothing else is affected. The most common causes are Group Policy
(work or school PCs), security software blocking the change, or PCOpti not running as administrator.
If you think it's a bug, please [open an issue](https://github.com/isxxc21/PCOptim/issues/new/choose).
</details>

<details>
<summary><b>Does PCOpti collect any data?</b></summary>

No. There is no telemetry or analytics, and there are no accounts. The only network request is the update check, which you
turn on in Settings (or run with a button). It asks the public GitHub API for the latest version number and never
downloads or installs anything by itself.
</details>

<details>
<summary><b>I use a standard (non-admin) account. Anything to know?</b></summary>

If you approve the admin prompt with a *different* administrator account, Windows runs PCOpti as that account,
so per-user tweaks (mouse, visual effects, Game Bar...) apply to the admin account, not yours. For per-user tweaks,
run PCOpti from an account that is itself an administrator.
</details>

## Build from source

Requirements: Windows 10/11 x64 and the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```powershell
git clone https://github.com/isxxc21/PCOptim.git
cd PCOptim
dotnet test
dotnet publish src/PCOpti/PCOpti.csproj -c Release -r win-x64 --self-contained true -o publish
.\publish\PCOpti.exe
```

## Contributing

Contributions are welcome. Please read [CONTRIBUTING.md](CONTRIBUTING.md) first. The short version: only tweaks with
a real, demonstrable effect, described honestly, fully reversible, and with tests.

## Disclaimer

PCOpti modifies Windows system settings, including registry values, services, power plans, startup items and
network settings. Every change is backed up and can be reverted from the app, but **you use this software at your own
risk**. It is provided "as is", without warranty of any kind. The authors are not responsible for any damage,
data loss or other problems. Always keep backups of important data.

PCOpti is not affiliated with Microsoft, NVIDIA, AMD, Cloudflare or Google.

## License

[MIT](LICENSE)
