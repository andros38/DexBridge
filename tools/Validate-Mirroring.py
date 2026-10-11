#!/usr/bin/env python3
"""Source-only mirror-mode regression checks; live Samsung/Windows testing still required."""
from pathlib import Path
import json
import re
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parents[1]
code = (root / 'MainWindow.xaml.cs').read_text(encoding='utf8')
xaml = (root / 'MainWindow.xaml').read_text(encoding='utf8')
settings = (root / 'Models/AppSettings.cs').read_text(encoding='utf8')
translation = json.loads((root / 'Languages/id-ID.json').read_text(encoding='utf8'))

ET.parse(root / 'MainWindow.xaml')
rootxml = ET.fromstring(xaml)
ns = {'w': 'http://schemas.microsoft.com/winfx/2006/xaml/presentation'}
mode = rootxml.find('.//w:ComboBox[@{http://schemas.microsoft.com/winfx/2006/xaml}Name="SessionModeCombo"]', ns)
assert mode is not None
assert [item.attrib['Tag'] for item in mode] == ['desktop', 'app', 'mirror']
assert 'public string SessionMode { get; set; } = "desktop"' in settings  # existing users' default unchanged
for name, default in [('MirrorFps','60'), ('MirrorBitrate','16'), ('MirrorVideoCodec','h264'), ('MirrorWindowMode','windowed')]:
    assert f'public string {name} {{ get; set; }} = "{default}";' in settings
assert 'public bool MirrorGamepadForwarding { get; set; } = false;' in settings

start = code.split('private async void Start_Click', 1)[1].split('private void SetSessionControls', 1)[0]
assert 'if (mirrorMode) args.Append("--display-id=0 ");' in start
assert 'else args.Append($"--new-display={resolution}/{dpi} ");' in start
assert 'if (appMode)' in start and 'args.Append("--no-vd-system-decorations ");' in start
assert 'args.Append($"--start-app={appPackage} ");' in start
assert 'if (!mirrorMode && HandoffDexAppsOnStopCheck.IsChecked == true)' in start
assert 'if (!mirrorMode) await _adb.CleanupOverlayDisplayAsync' in start
assert 'if (mirrorMode && _session is { } mirrorSession) await OnMirroringStartedAsync(mirrorSession)' in start
assert '--video-codec={codec}' in start and '--max-fps {fps}' in start and '--print-fps' in start
assert 'if (appMode || mirrorMode)' in start and '--window-borderless --fullscreen' in start

assert 'private int? ActiveDisplayId' in code and '(IsMirroring ? 0 : _session.DisplayId)' in code
assert 'var displayId = ActiveDisplayId!.Value;' in code
assert 'CloseAllTasksButton.IsEnabled = hasTasks && !IsMirroring;' in code
assert 'MoveTaskPhoneButton.IsEnabled = selected && !IsMirroring;' in code
assert 'CloseTaskButton.IsEnabled = selected && !IsMirroring;' in code
assert 'if (IsMirroring || _phoneScreenGuardBusy' in code
assert 'if (IsMirroring || _adb is null' in code
assert 'if (!mirrorMode) await RestorePhoneScreenAsync();' in code
assert 'if (!mirrorMode && _adb is not null && _activeDevice is not null)' in code
assert 'ApplyMirrorStreamSettingsToControls();' in code
assert 'SaveMirrorStreamSettingsFromControls();' in code
assert 'ResolutionCombo.IsEnabled = !mirrorMode && idle;' in code
assert 'DpiCombo.IsEnabled = !mirrorMode && idle;' in code
assert 'PhoneScreenButton.IsEnabled = false;' in code

new_keys = ['Screen mirroring', 'Start Mirroring',
            'Starting phone screen mirroring…', 'No active session',
            'Window mode', 'Phone screen: mirroring',
            '{0} app task(s) on phone screen',
            'Screen mirroring stopped. Phone apps remain open.']
assert all(key in translation for key in new_keys)
for english, translated in translation.items():
    placeholders = lambda s: sorted(re.findall(r'(?<!\{)\{(\d+)(?:\:[^}]*)?\}', s))
    assert placeholders(english) == placeholders(translated), english

assert '<Version>0.13.17</Version>' in (root/'DexBridge.csproj').read_text()
assert '$AppVersion' in (root/'tools/Build-Portable.ps1').read_text()
print('PASS: 3 session modes (desktop, app, mirror) with legacy default and independent mirror settings.')
print('PASS: mirror targets display 0; does not create a virtual display or auto-start an app.')
print('PASS: mirror bypasses virtual-display cleanup, phone screen guard, DPI updates and mass app removal.')
print('PASS: stream telemetry, codec, FPS, bitrate, window mode, language and UI availability wired.')
print('NOTE: Source-level checks only. A Windows .NET 10 WPF build and Galaxy USB smoke test remain required.')
