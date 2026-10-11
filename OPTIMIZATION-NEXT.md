# DexBridge — optimization backlog after v0.13.5

## Completed in v0.13.5 (pending Windows/Galaxy field validation)

- Device-monitor ADB polling: default 2 s -> effective 4 s. Slow cycles cannot queue up.
- Running Apps polling: 8 s, explicit refresh unaffected. Ignore stale query results after the session ends.
- Phone screen guard: separate 2-s timer, no speculative power-off command if physical display state is unknown.
- Stream FPS context: recent 15-sample average; event-driven DeX and media streams are not mistaken for poor game rendering.
- Sustained framework thermal warning after >=20 s Severe/Critical; no automatic profile or codec changes.
- Session-log memory bounded; recurring ADB errors rate-limited.

## Next priorities

P0. **Session resilience:** eliminate race cases when scrcpy exits during ADB queries or cleanup; restore panel/display 0 and preserve handoff behavior. Test cable disconnects, renderer errors and shutdown under load.

P1. **Frame pacing diagnostics:** separate stream FPS from true in-game FPS; add timestamped FPS statistics and sampling metadata without claiming to measure frame latency or dropped frames from FPS alone.

P1. **Thermal trend, not single threshold:** collect a small rolling history of framework status and CPU/skin temperatures; offer an opt-in lower profile when sustained throttling is observed. Never automatically change a live virtual display.

P1. **Samsung DeX taskbar:** investigate supported auto-hide behavior and user choice; do not remove system navigation from desktop sessions just to fill every pixel.

P2. **Diagnostics export:** add privacy-redacted JSON/CSV for timing, stream FPS, thermal history, and scrcpy exit codes. Persist bounded local reports only when user opts in.

P2. **Visual/UI polish:** accessibility/disabled controls at 125–200% Windows scaling, compact mode, keyboard navigation and OLED contrast regression tests.

P3. **Adaptive encoder fallback (opt in only):** H.264/H.265 hardware compatibility check before proposing next-session changes. Do not silently increase bitrate, claim game render improvements, or automatically change stream settings mid-session.
