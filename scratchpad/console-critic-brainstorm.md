Console Critic – Brainstorm

Purpose
- An unobtrusive “console critic” that sits alongside PowerShell and observes commands and outcomes to help the user recover from errors, adopt better practices, and reflect project-specific principles.

Core ideas
- Feedback hooks: Use PowerShell’s IFeedbackProvider to receive triggers on Success, Error, and CommandNotFound. Emit minimal/no UI by default; log silently.
- Own memory: Keep a structured store of sessions, commands, outputs, and feedback, distinct from PowerShell history/transcripts.
- Values & principles: Separate global meta-values (safety, reproducibility, performance, compliance) from per-project principles that can gate suggestions.
- Dual consciences: “Devil” (AI) proposes provocative alternatives; “Angel” (user via TTS) voices cautions. Both are opt-in and never auto-execute.
- UX posture: Transparent, low-noise, opt-in signals, quick accept/ignore.

---

UI options & decisions
- Canonical rendering: Return FeedbackItem from GetFeedback; let the shell render via HostUtilities.RenderFeedback for consistent UX.
- Formatting: Use FeedbackItem header/actions/footer and FeedbackDisplayLayout (Portrait default; Landscape only for short action lists). Style via $PSStyle.Formatting.FeedbackName/Text/Action.
- Minimal output policy: one-line header + up to 3 actions; no dialogs; no auto-scroll. Return null to suppress output when not useful.
- When to show: inline only on Error/CommandNotFound; Success tips via predictor (ICommandPredictor) rather than inline.
- Predictor integration: convert actionable items into predictive suggestions; accept via Tab/RightArrow.
- Angel TTS (optional): speak critical warnings only; user-controlled volume/mute; default off.
- Controls: toggles for per-trigger enablement, verbosity levels, and log-only mode; project overrides via principles.
- Alternative UI: use ICommandPredictor as a primary UI channel to surface suggestions while typing (ghosted), favoring predictor over inline when appropriate.
- GUI stance: no standalone GUI; rely on native PowerShell feedback/predictor surfaces and optional TTS.
- Non-goal: suggest-only (no automated command execution).

Parked ideas: Console Critic

- Critic memory (TBD): The critic maintains its own memory distinct from PowerShell session artifacts (history, transcripts). Purpose: long-lived, structured store of commands, outputs, context, feedback, and user dispositions. Possible elements:
	- Sessions, command+output pairs, error/success metadata
	- Derived heuristics, learned preferences, allow/deny lists
	- User notes and tags, references to docs/remediations
	- Privacy controls, retention policy, export APIs
- IT Pro's (or Developer) Command-line (or Coding)  meta-Values
    - TBD
- Current Command Activity (or Project/repo) "command principles" (or "coding Principles")
    - TBD
- Devil and Angel consciences:
	- Devil (AI model, TBD): Generates provocative suggestions, shortcuts, optimizations, or creative alternatives; clearly labeled; opt-in; never auto-executes.
	- Angel (user via parallel TTS): Real-time spoken guidance from the user profile/preferences; can read critical warnings, confirmations, and safe alternatives in parallel while you type/run.
- Parallel TTS interaction (parked): The TTS engine can infer concerns and interact with the user in parallel to the console (non-blocking). Capabilities:
	- Ask clarifying questions, propose safer alternatives, summarize risk.
	- Accept user responses via voice or quick keyboard prompts; render a minimal inline confirmation if needed.
	- Respect privacy: transcript opt-in, redaction, local-only storage.
	- Guardrails: rate limiting, barge-in detection, do-not-disturb, and strict suggest-only (no auto-exec).
- UX goal: unobtrusive, transparent, opt-in signals; minimal terminal clutter; quick accept/ignore.

Status: Parked until capture/analysis pipeline is defined.