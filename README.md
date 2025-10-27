# Console Critic

![Shell Nanny Banner](assets/shell-nanny-banner.png)

An unobtrusive companion for your PowerShell console.

Observes commands and outcomes to offer suggest-only guidance while keeping you in control.
Integrates natively with PowerShell FeedbackProvider and optional ICommandPredictor for concise inline and predictive hints.
Keeps context private with configurable logging, and evolves toward values- and project-aware feedback.

## Vision and meta‑objectives
- Unobtrusive and transparent: suggest-only, low-noise, no dialogs.
- Safety first: never auto-execute; user remains in control.
- Privacy-first: local logs by default, configurable redaction/retention.
- Native UX: use IFeedbackProvider rendering and optional ICommandPredictor; no standalone GUI.
- Extensible: pave the way for values/principles and optional TTS “angel” channel.

## Ambitions
- Context-aware guidance aligned to user/project values and principles.
- Dual consciences: optional AI “devil” and parallel TTS “angel” that remain suggest-only.
- Rich but private memory: sessions, outcomes, notes; export/redact by design.
- Predictor-first UI for low-noise assistance; inline feedback only when it truly helps.
- Configurability: per-trigger gating, verbosity tiers, do-not-disturb.
- Ecosystem-friendly: works with PSReadLine and PowerShell subsystems; easy to extend.

## Objectives
- v0.1 Silent listener (DONE): log Success/Error/CommandNotFound with minimal context.
- v0.2 Inline feedback (Errors/CNF): concise FeedbackItem with up to 3 actions.
- v0.3 Predictor integration: surface actionable items as PSReadLine suggestions.
- v0.4 Config and privacy: trigger gating, JSONL logs, redaction, retention.
- v0.5 Optional TTS (parked): non-blocking prompts for critical warnings.

## Status
- Early prototype: minimal logger registered as `IFeedbackProvider`.
- Logs to `%LocalAppData%/ConsoleCritic/logs/critic-YYYYMMDD.log`.

## UI model
- Inline: FeedbackItem rendered by HostUtilities.RenderFeedback (Portrait by default).
- Predictor: suggestions via ICommandPredictor, accepted with Tab/RightArrow.
- No standalone GUI; optional parallel TTS (parked) for spoken warnings.

## Build
- Requires .NET 8 SDK and PowerShell 7.4+.
- `dotnet restore`
- `dotnet build`

## Enable features (PowerShell)
- `Enable-ExperimentalFeature -Name PSFeedbackProvider`
- (Optional for discovery) `Enable-ExperimentalFeature -Name PSSubsystemPluginModel`
- Restart PowerShell after enabling.

## Import
- `Import-Module ./src/ConsoleCritic.Provider/bin/Debug/net8.0/ConsoleCritic.Provider.dll`

## Verify
- `Get-PSSubsystem -Kind FeedbackProvider` → should list `ConsoleCritic` alongside `General Feedback`.

## Roadmap (short)
- JSONL logging with session and environment context.
- Configurable triggers and retention.
- Privacy-first redaction and opt-in telemetry.
- Optional predictor integration.

## Non-goals
- Automated command execution.
- Heavy, persistent UI elements or custom windows.

## Design notes
- See `scratchpad/console-critic-brainstorm.md` for parked ideas (memory, values/principles, devil/angel, TTS, UI decisions).

## License
TBD.