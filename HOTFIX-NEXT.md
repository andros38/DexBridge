# DexBridge — UI regression tracker

## Resolved in v0.13.2

The previous v0.13.1 recoloring of generic WPF brushes did not override the actual
Windows ComboBox/Button visual templates. This caused native white dropdown surfaces
and illegible text in Dark/OLED themes, especially for disabled controls.

v0.13.2 resolves the underlying styling path by replacing the WPF ControlTemplates
for ComboBox, ComboBoxItem, Button, TextBox, CheckBox and GroupBox. Their chrome,
content, hover/focus and disabled states are connected to live theme resources.
Light, Dark and OLED Dark use separate disabled colors. No global SystemColors
resource overriding is needed.

## Manual acceptance tests required on Windows

1. Toggle Light -> Dark -> OLED Dark -> Light while DexBridge is idle.
2. Start a DeX or App/Game session, where many selectors/buttons are disabled.
   Check that the selected text is readable and disabled controls are gray.
3. Open every dropdown (Galaxy, Session mode, Desktop profile, Application,
   video codec, FPS, theme selector); ensure selection, keyboard arrows, and scrolling work.
4. Verify the Session Log and FPS Log keep their scrollbars, caret and selection.
5. Open Files and check controls and list background in each theme.
6. Check Running Apps action buttons while no task is selected versus selected.
7. Switch themes while a session runs and after Stop to check resource updates.

Runtime compilation and visual rendering were not possible in the authoring environment.

## v0.13.3 follow-up watchlist

- Check media orientation/watch-page behavior on an actual Android 16 device. Per-app profiles can influence layout but cannot force an app to expose a comments panel.
- Continue investigating the Android desktop taskbar behavior separately. The DeX desktop mode deliberately retains system decorations because removing them can disable native desktop controls. App/Game already uses a decoration-free virtual display.
- Continue Windows visual acceptance testing for Dark/OLED themed native controls.

## v0.13.4 follow-up watchlist

- New coupled Desktop/App profile mappings need Windows runtime verification; build and WPF visual tests not available here.
- Check new app profile defaults (YouTube Quality) versus existing saved package profiles (must preserve existing settings).
- Verify theme ComboBox labels and tooltip contrast with new short profile names.
- Follow P0/P1 recommendations in OPTIMIZATION-NEXT.md before attempting automatic thermal-driven changes.
