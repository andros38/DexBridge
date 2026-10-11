# DexBridge v0.13.3 — validation

Static checks on source:
- All 4 XAML documents parse as XML.
- All named XAML control references are unique per file.
- XAML event-handler identifiers have corresponding C# method declarations.
- DynamicResource names resolve to App.xaml resource keys.
- ComboBox, Button, TextBox, CheckBox and GroupBox have explicit ControlTemplates.
- ControlTemplate setters target existing template element names.
- Original bundled scrcpy.exe, scrcpy-server and adb.exe are preserved.

**Not verified:** actual Windows WPF compiler / rendered control behavior.
The authoring environment does not have a .NET SDK and cannot render WPF.

Verify on Windows:
```powershell
dotnet clean -c Release
dotnet build -c Release
dotnet run -c Release
```
Test themes both before and during a connected session, and test dropdown selection.

## New profile acceptance checks (Windows)

1. On a clean app profile, YouTube selects Media portrait (1080×1920, 420 DPI).
2. On a clean app profile, Samsung Music selects Media landscape (1920×1080, 320 DPI).
3. Switch each profile to Custom and back; inspect resolution, DPI, codec, bitrate and FPS.
4. Close/reopen DexBridge; confirm manually chosen profiles are retained for each package.
5. Confirm old saved profiles override the new first-use defaults.
6. Start the App/Game session and verify the scrcpy command uses selected width×height/DPI and --no-vd-system-decorations.
7. Stop and confirm phone screen recovery and app handoff still behave as previously.

**Not verified here:** actual Windows WPF build, Android app layout, and runtime streaming because this environment lacks a Windows/.NET SDK and a connected Samsung phone.
