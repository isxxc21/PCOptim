# Contributing to PCOpti

Thanks for helping! PCOpti has one rule above all others: **honesty over hype**. Every change should be real,
reversible and described truthfully.

## Ground rules

- **Only real tweaks.** A new tweak needs evidence that it has an effect (a benchmark, Microsoft documentation, or a
  well-known reproducible result). No "RAM boosters", placebo registry values or "latency tweaks" without proof.
- **Describe it truthfully.** The description says what it changes, who it helps and how much, including
  "small" or "only on low-end PCs" when that's the truth.
- **Reversible and logged.** Every tweak must back up what it changes and restore it exactly. Base classes
  (`RegistryTweak`, `ServiceTweak`, `PowerPlanTweak`) do this for you.
- **Independent.** A tweak must never depend on, or write the same value as, another tweak. A unit test enforces this for registry tweaks.
- **Honest risk level.** `Safe` = harmless and easy to undo. `Moderate` = changes behaviour the user might rely on.
  `Advanced` = system-level. Moderate and Advanced tweaks must set `Warning`.
- **No telemetry, ads, bundled software, or network calls** (other than the existing optional update check).

## Adding a tweak

1. Create a class in `src/PCOpti/Tweaks/Gaming/` (or `SystemServices/`). For registry tweaks:

   ```csharp
   public sealed class MyTweak : RegistryTweak
   {
       public MyTweak(TweakContext c) : base(c) { }
       public override string Id => "gaming.my-tweak";          // never change after release
       public override string Name => "Short name";
       public override string Description => "What it does, who it helps, and how much.";
       public override TweakCategory Category => TweakCategory.Gaming;
       public override RiskLevel RiskLevel => RiskLevel.Safe;
       public override IReadOnlyList<RegistrySetting> Settings { get; } = new[]
       {
           new RegistrySetting(RegHive.CurrentUser, @"Software\...", "ValueName",
               Applied: RegValue.DWord(0), RevertTo: RegValue.DWord(1)),
       };
   }
   ```

2. Add one line to `src/PCOpti/Tweaks/TweakCatalog.cs`. The UI card is generated automatically.
3. Add or extend a test in `tests/PCOpti.Tests` if the tweak has custom logic. The catalog round-trip test
   already checks that every registry tweak applies, detects itself and reverts exactly.

## Development setup

- Windows 10/11 x64, the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0), and Visual Studio 2022, VS Code with C# Dev Kit, or Rider.
- `dotnet build` and `dotnet test` from the repo root.
- The app requires administrator rights, so run your IDE or terminal **as administrator** to debug it.
- UI smoke test: `PCOpti.exe --smoke-test` opens every page once (read-only), then exits with code 0, or 1 if a
  page threw an error (details go to `%LocalAppData%\PCOpti\smoke-test.log`). To run it without the admin prompt:
  `$env:__COMPAT_LAYER="RunAsInvoker"; .\PCOpti.exe --smoke-test`.
- Please test on a VM or a PC you can restore before trying new tweaks on your main machine.

## Pull requests

- Keep PRs focused. One tweak or fix per PR is ideal.
- Make sure `dotnet build` and `dotnet test` pass (CI runs both on `windows-latest`).
- Say in the PR description how you tested it on real Windows (which version, and what you checked before and after).
- Add a line to the `[Unreleased]` section of `CHANGELOG.md`.

## Code layout

```
src/PCOpti/
  Models/      plain data types (RegValue, TweakBackup, ChangeEntry, StartupEntry)
  Services/    system access (registry, commands, backups, startup, cleaner, network, ...)
  Tweaks/      ITweak, base classes, TweakCatalog, and the tweaks themselves
  ViewModels/  MVVM view models (CommunityToolkit.Mvvm)
  Views/       XAML pages and the dialog window
  Themes/      dark theme and shared templates
tests/PCOpti.Tests/  xUnit tests with a fake registry and fake command runner
```

## Reporting bugs

Use the issue templates. Include your Windows version (`winver`), the PCOpti version, and the relevant lines from
the change log (Backup & Restore page) or `%LocalAppData%\PCOpti\error.log`.
