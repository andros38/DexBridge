# DexBridge v0.13.4 — Resolution and stream rules

| Profile | Available in | Virtual display | DPI | Codec | Bitrate | Target FPS |
|---|---|---:|---:|---|---:|---:|
| Desktop Balanced | Desktop | 1920×1080 | 220 | H.264 | 16 Mbps | 60 |
| Desktop Compact | Desktop | 1920×1080 | 180 | H.264 | 16 Mbps | 60 |
| Desktop Sharp | Desktop | 2560×1440 | 200 | H.265 | 24 Mbps | 60 |
| YouTube Quality | Desktop + App/Game | 1920×1080 | 240 | H.265 | 32 Mbps | 60 |
| YouTube Balanced | Desktop + App/Game | 1920×1080 | 240 | H.265 | 20 Mbps | 60 |
| Game Performance | App/Game | 1600×900 | 240 | H.264 | 16 Mbps | 60 |
| Game Balanced | App/Game | 1920×1080 | 240 | H.264 | 20 Mbps | 60 |
| Game Quality | App/Game | 1920×1080 | 240 | H.265 | 24 Mbps | 60 |
| Game Maximum | App/Game | 2560×1440 | 240 | H.265 | 32 Mbps | 60 |
| Media Portrait | App/Game | 1080×1920 | 420 | H.265 | 16 Mbps | 60 |
| Media Landscape | App/Game | 1920×1080 | 240 | H.265 | 20 Mbps | 60 |
| Custom | Desktop + App/Game | User-set | User-set | User-set | User-set | User-set |

## Operational rules

1. The virtual display size and DPI are separate from video encoding and from a game's internal render resolution.
2. Changing a quick profile changes all five stream parameters in one operation. Manually changing any parameter selects Custom. No changes are silently applied to an already-running session.
3. The user retains independent Desktop settings and per-package App/Game settings. Saved per-app profiles take precedence over new defaults.
4. New YouTube App/Game profiles choose YouTube Quality by default (1080p/240/H.265/32 Mbps). Existing profiles remain untouched. For comments, switching to Media Portrait may work better, but layout is controlled by YouTube.
5. Use Desktop for Samsung's DeX launcher/taskbar and App/Game for one application on a decoration-free virtual display.
6. 60 FPS is a scrcpy target/cap, not a guarantee of game FPS or even 60 captured frames when static.
7. If thermal status is Severe, compare 20 Mbps and 32 Mbps without automatically downgrading (codec/encoder use and game load may dominate heat).

## Test cases on the user's Galaxy

- Desktop YouTube Quality: 1920×1080/240 H.265 32 Mbps max 60 FPS; video page, title, comments, video fullscreen; 20–30 min thermal trend.
- Desktop YouTube Balanced: same resolution/DPI at H.265 20 Mbps; compare image quality, stutter, thermals.
- App/Game YouTube Quality first-use selection; verify correct arguments and no DeX taskbar; explicitly inspect video detail/comments (not guaranteed by DPI).
- Game Performance versus Game Quality: compare actual stream FPS, in-game graphic detail, latency and thermal state.
- Manual DPI/bitrate change selects Custom. Restart preserves selected profile, switching App/Game ↔ Desktop restores independent settings.
- Existing v0.13.3 installed JSON app profiles are preserved, not silently overwritten.
- New session after each profile selection. Stop performs the existing cleanup/handoff flow.
