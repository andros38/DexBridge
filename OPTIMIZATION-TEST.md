# DexBridge v0.13.5 — Windows / Galaxy acceptance tests

## Baseline (do not over-interpret still-image FPS)

Current tested environment: Samsung SM-G990E / Android 16, scrcpy 5.0, wired ADB, DeX Desktop mode. Preserve working *Desktop Balanced* baseline: virtual display 1920×1080, 220 DPI, H.264, 16 Mbps, target 60 FPS. This is a cap, not a required number of distinct frames when nothing changes.

## Test 1 — Desktop + video (15 minutes)

1. Start Desktop Balanced, enable **Keep phone panel off during session**.
2. Open Samsung My Files and Samsung Video in separate DeX windows. Play a 24/30-FPS video.
3. Confirm Running Apps lists both packages; manual Refresh should work immediately; automatic updates should be spaced at approximately 8 seconds.
4. Hover over Stream FPS. It must show the recent-sample average, and desktop low/idle FPS must not be labeled as game lag. No automatic profile change.
5. Watch CPU/skin temperatures and Android framework thermal status. A thermal **Light** display needs no warning.
6. Wake the physical panel intentionally; verify Phone Screen Guard detects and re-disables display 0 within a few seconds. Leave phone awake when the control is intentionally changed to **phone screen on**.

## Test 2 — Gaming with thermal advisory

1. Select an installed game via App/Game mode; leave working profile settings unchanged.
2. Play a visually busy scene. Compare latest scrcpy stream FPS and 15-sample average with actual game performance (if the game exposes its own counter).
3. If Android framework thermal status stays **Severe** or higher for at least 20 seconds, confirm a non-repeating advisory in Session Log; no automatic stream restart, DPI change, or bitrate modification.
4. Repeat with thermal Normal/Light; warning should reset after recovery. Do not deliberately heat the phone solely to trigger this condition.

## Test 3 — Session stop and recovery

1. Stop via DexBridge while two apps are open; confirm the phone panel is restored, the temporary display is cleaned and handoff obeys the user's configured option.
2. Start another DeX session; FPS history and the 15-sample average should reset.
3. Disconnect USB only if safe to do so, then reconnect and ensure no phantom Running Apps entries remain. This tests existing recovery; v0.13.5 does not claim to fully fix unexpected scrcpy termination.
4. Run a prolonged 30-minute session. Check that Session Log stops growing indefinitely and the UI stays responsive.

## Test 4 — Build and compatibility

In the project directory on Windows with the .NET 10 SDK:

```powershell
dotnet build -c Release
dotnet run -c Release
powershell -ExecutionPolicy Bypass -File .\tools\Build-Portable.ps1
```

Check OLED Dark/Light/Dark disabled states, existing presets, Favorite/Recent libraries, Files transfer, UHID gamepad, and borderless overlay. Static checks are not substitutes for these device tests.
