# DexBridge Android Companion — experimental design

This folder is a design placeholder for the future DexBridge companion APK.

Planned responsibilities:

- Display-aware wallpaper rendering for the DexBridge virtual display
- Optional clipboard/file metadata bridge
- Session state reporting
- Future notification/device integration

The companion is **not built or installed by v0.9**. Android's wallpaper service can create one engine per display, but making a separate DeX wallpaper without unexpectedly replacing the user's phone/home wallpaper requires careful state preservation and Samsung-specific testing. The Windows-side improvements in v0.9 do not depend on this component.
